using System;
using System.Diagnostics;
using System.Device.Gpio;
using System.Threading;
using DiSEqC_Control.Mqtt;
using DiSEqC_Control.Native;

namespace DiSEqC_Control
{
    /// <summary>
    /// MQTT-first slim entry point. Rotor and LNB control are stubbed until their native
    /// InternalCall bindings are registered as g_CLR_AssemblyNative_DiSEqC_Control. Currently
    /// only Cubley.Interop (W5500 + BringupStatus) is registered with the firmware.
    /// </summary>
    public class Program : IMqttCommandSink, IMqttConfigSink
    {
        private static MqttClient _mqttClient;
        private static bool _isConnected;
        private static RuntimeConfiguration _runtimeConfig = RuntimeConfiguration.CreateDefaults();
        private static RuntimeConfiguration _savedConfig = RuntimeConfiguration.CreateDefaults();
        private static Program _instance;

        private const int STATUS_LED_PIN = 2;
        private const int STATUS_LED_BLINK_MS = 500;

        private static string TopicPrefix { get { return _runtimeConfig.MqttTopicPrefix; } }
        private static string TopicAvailability { get { return TopicPrefix + "/availability"; } }

        private static void Beacon(byte stage, byte detail)
        {
            try
            {
                uint word = ((uint)0xD5 << 24) | ((uint)stage << 16) | detail;
                Cubley.Interop.BringupStatus.NativeSet(word);
            }
            catch
            {
            }
        }

        public static void Main()
        {
            Beacon(0xA0, 0x01);
            Debug.WriteLine("==============================================");
            Debug.WriteLine("DiSEqC Controller (MQTT-first build)");
            Debug.WriteLine("STM32F407VGT6 + W5500 + nanoFramework");
            Debug.WriteLine("==============================================");

            Beacon(0xA1, 0x01);
            _runtimeConfig = RuntimeConfiguration.CreateDefaults();
            _savedConfig = _runtimeConfig.Clone();
            _instance = new Program();

            Beacon(0xA2, 0x01);
            StartStatusLedHeartbeat();

            Beacon(0xA3, 0x01);
            InitializeNetwork();

            Beacon(0xA6, 0x01);
            ConnectToMqtt();

            Beacon(0xA7, 0x01);
            Debug.WriteLine("Entering main loop...");
            MainLoop();
        }

        private static void StartStatusLedHeartbeat()
        {
            try
            {
                var gpio = new GpioController();
                var led = gpio.OpenPin(STATUS_LED_PIN, PinMode.Output);
                new Thread(() =>
                {
                    bool on = false;
                    while (true)
                    {
                        on = !on;
                        led.Write(on ? PinValue.High : PinValue.Low);
                        Thread.Sleep(STATUS_LED_BLINK_MS);
                    }
                }).Start();
                Debug.WriteLine("[LED] Heartbeat enabled on pin " + STATUS_LED_PIN);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[LED] Heartbeat unavailable: " + ex.Message);
            }
        }

        private static void InitializeNetwork()
        {
            Debug.WriteLine("\n--- Network Initialization ---");
            Debug.WriteLine("  Static IP : " + _runtimeConfig.StaticIp);
            Debug.WriteLine("  Mask      : " + _runtimeConfig.StaticSubnetMask);
            Debug.WriteLine("  Gateway   : " + _runtimeConfig.StaticGateway);
            Debug.WriteLine("  MAC       : " + _runtimeConfig.NetworkMac);

            var configureStatus = W5500Socket.ConfigureNetwork(
                _runtimeConfig.StaticIp,
                _runtimeConfig.StaticSubnetMask,
                _runtimeConfig.StaticGateway,
                _runtimeConfig.NetworkMac);

            Debug.WriteLine("[W5500] ConfigureNetwork => " + configureStatus);

            if (configureStatus != W5500Socket.Status.Ok)
            {
                Debug.WriteLine("ERROR: W5500 ConfigureNetwork failed; halting.");
                Thread.Sleep(Timeout.Infinite);
            }

            const int PhyWaitIterations = 30;
            const int PhyWaitIntervalMs = 100;
            uint phy = 0;
            for (int i = 0; i < PhyWaitIterations; i++)
            {
                phy = W5500Socket.GetVersionPhyStatus();
                if ((phy & 0x01) != 0)
                {
                    break;
                }
                Thread.Sleep(PhyWaitIntervalMs);
            }
            Debug.WriteLine("[W5500] PHY status raw=0x" + phy.ToString("X4") + " link=" + (((phy & 0x01) != 0) ? "UP" : "DOWN"));
            Debug.WriteLine("Network Ready (static, W5500 native path)");
        }

        private static void ConnectToMqtt()
        {
            Debug.WriteLine("\n--- MQTT Initialization ---");
            Debug.WriteLine("Broker: " + _runtimeConfig.MqttBroker + ":" + _runtimeConfig.MqttPort);
            Debug.WriteLine("Transport: w5500-native (in-tree MQTT 3.1.1 client)");

            try
            {
                _mqttClient = CreateMqttClient();
                _mqttClient.MessageReceived += OnMqttMessageReceived;
                _mqttClient.ConnectionClosed += OnMqttConnectionClosed;

                bool hasCredentials = !string.IsNullOrEmpty(_runtimeConfig.MqttUsername);
                byte connectResult = MqttPacket.ConnAckServerUnavailable;

                const int ConnectRetryCount = 12;
                const int ConnectRetryDelayMs = 1000;
                const int ConnAckTimeoutMs = 5000;
                for (int attempt = 1; attempt <= ConnectRetryCount; attempt++)
                {
                    Beacon(0xB0, (byte)attempt);
                    try
                    {
                        Debug.WriteLine("[MQTT] Connect attempt " + attempt + "/" + ConnectRetryCount);
                        byte[] willPayloadBytes = AsciiCodec.GetBytes("offline");
                        string willTopicStr = TopicAvailability;
                        string user = hasCredentials ? _runtimeConfig.MqttUsername : null;
                        string pass = hasCredentials ? _runtimeConfig.MqttPassword : null;
                        connectResult = _mqttClient.Connect(
                            _runtimeConfig.MqttClientId,
                            60,
                            true,
                            user,
                            pass,
                            willTopicStr,
                            willPayloadBytes,
                            1,
                            true,
                            ConnAckTimeoutMs);
                    }
                    catch (Exception cex)
                    {
                        Debug.WriteLine("[MQTT] Connect attempt " + attempt + " threw: " + cex.Message);
                        connectResult = MqttPacket.ConnAckServerUnavailable;
                    }

                    Beacon(0xB1, connectResult);
                    if (connectResult == MqttPacket.ConnAckAccepted)
                    {
                        break;
                    }

                    if (attempt < ConnectRetryCount)
                    {
                        Thread.Sleep(ConnectRetryDelayMs);
                    }
                }

                if (connectResult == MqttPacket.ConnAckAccepted)
                {
                    Beacon(0xB2, 0x01);
                    Debug.WriteLine("Connected to MQTT broker!");
                    _isConnected = true;

                    PublishAvailability(true);
                    SubscribeToTopics();
                    PublishInitialStatus();
                    Beacon(0xB3, 0x01);
                }
                else
                {
                    Debug.WriteLine("ERROR: MQTT connection failed! CONNACK code: 0x" + connectResult.ToString("X2"));
                    _isConnected = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ERROR: MQTT exception: " + ex.Message);
                _isConnected = false;
            }
        }

        private static MqttClient CreateMqttClient()
        {
            var channel = new W5500MqttNetworkChannelCore(
                _runtimeConfig.MqttBroker,
                _runtimeConfig.MqttPort,
                5000,
                30000,
                new W5500SocketApi());
            return new MqttClient(channel);
        }

        private static void SubscribeToTopics()
        {
            Debug.WriteLine("\n--- Subscribing to MQTT Topics ---");
            string[] topics = new[]
            {
                TopicPrefix + "/command/goto/angle",
                TopicPrefix + "/command/goto/satellite",
                TopicPrefix + "/command/halt",
                TopicPrefix + "/command/manual/step_east",
                TopicPrefix + "/command/manual/step_west",
                TopicPrefix + "/command/manual/drive_east",
                TopicPrefix + "/command/manual/drive_west",
                TopicPrefix + "/command/lnb/voltage",
                TopicPrefix + "/command/lnb/polarization",
                TopicPrefix + "/command/lnb/tone",
                TopicPrefix + "/command/lnb/band",
                TopicPrefix + "/command/config/get",
                TopicPrefix + "/command/config/set",
                TopicPrefix + "/command/config/save",
                TopicPrefix + "/command/config/reset",
                TopicPrefix + "/command/config/reload",
                TopicPrefix + "/command/config/fram_clear",
                TopicPrefix + "/command/calibrate/reference"
            };
            for (int i = 0; i < topics.Length; i++)
            {
                _mqttClient.Subscribe(topics[i], 1);
            }
            Debug.WriteLine("Subscribed to " + topics.Length + " topics");
        }

        private static void PublishAvailability(bool online)
        {
            string payload = online ? "online" : "offline";
            _mqttClient.Publish(TopicAvailability, AsciiCodec.GetBytes(payload), 1, true);
            Debug.WriteLine("Published availability: " + payload);
        }

        private static void PublishInitialStatus()
        {
            Debug.WriteLine("\n--- Publishing Initial Status ---");
            PublishStatusInternal("state", "idle");
            PublishStatusInternal("position/angle", "0.0");
            PublishStatusInternal("position/satellite", "unknown");
            PublishStatusInternal("busy", "false");
            PublishStatusInternal("lnb/voltage", "unknown");
            PublishStatusInternal("lnb/tone", "unknown");
            PublishStatusInternal("lnb/polarization", "unknown");
            PublishStatusInternal("lnb/band", "unknown");
            PublishEffectiveConfigInternal();
            Debug.WriteLine("Initial status published");
        }

        private static void OnMqttMessageReceived(string topic, byte[] message)
        {
            string payload = AsciiCodec.GetString(message, 0, message.Length);
            Debug.WriteLine("\n[MQTT] Topic: " + topic);
            Debug.WriteLine("[MQTT] Payload: " + payload);

            try
            {
                if (MqttConfigCommandProcessor.TryHandle(topic, payload, _runtimeConfig, _instance))
                {
                    return;
                }
                if (!MqttCommandRouter.TryHandle(topic, payload, _instance))
                {
                    Debug.WriteLine("[MQTT] Unknown topic: " + topic);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[MQTT] Error handling message: " + ex.Message);
                PublishErrorInternal("Command error: " + ex.Message);
            }
        }

        private static void OnMqttConnectionClosed()
        {
            Debug.WriteLine("\n[MQTT] Connection lost! Will reconnect in main loop.");
            _isConnected = false;
        }

        private static void PublishStatusInternal(string subtopic, string value)
        {
            if (!_isConnected) return;
            string topic = TopicPrefix + "/status/" + subtopic;
            try
            {
                _mqttClient.Publish(topic, AsciiCodec.GetBytes(value), 0, true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[MQTT] Publish error: " + ex.Message);
            }
        }

        private static void PublishErrorInternal(string errorMessage)
        {
            Debug.WriteLine("[ERROR] " + errorMessage);
            if (_isConnected)
            {
                PublishStatusInternal("error", errorMessage);
            }
        }

        private static void PublishEffectiveConfigInternal()
        {
            if (!_isConnected) return;
            try
            {
                _mqttClient.Publish(TopicPrefix + "/status/config", AsciiCodec.GetBytes(_runtimeConfig.ToKeyValueLines()), 0, true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[MQTT] Config publish error: " + ex.Message);
            }
        }

        private static void MainLoop()
        {
            int loopCounter = 0;
            while (true)
            {
                if (!_isConnected || (_mqttClient != null && !_mqttClient.IsConnected))
                {
                    Debug.WriteLine("[MQTT] Disconnected. Reconnecting...");
                    Thread.Sleep(5000);
                    ConnectToMqtt();
                }

                if (loopCounter % 30 == 0 && _isConnected)
                {
                    PublishStatusInternal("uptime_s", loopCounter.ToString());
                }

                if (loopCounter % 10 == 0)
                {
                    Debug.WriteLine("[HEARTBEAT] Uptime: " + loopCounter + "s");
                }

                loopCounter++;
                Thread.Sleep(1000);
            }
        }

        // ------------------ IMqttCommandSink (rotor/LNB stubs) ------------------
        public void HandleGotoAngle(string payload) { PublishErrorInternal("rotor not yet bound"); }
        public void HandleGotoSatellite(string payload) { PublishErrorInternal("rotor not yet bound"); }
        public void HandleHalt() { PublishErrorInternal("rotor not yet bound"); }
        public void HandleStepEast(string payload) { PublishErrorInternal("rotor not yet bound"); }
        public void HandleStepWest(string payload) { PublishErrorInternal("rotor not yet bound"); }
        public void HandleDriveEast() { PublishErrorInternal("rotor not yet bound"); }
        public void HandleDriveWest() { PublishErrorInternal("rotor not yet bound"); }
        public void HandleLnbVoltage(string payload) { PublishErrorInternal("LNB not yet bound"); }
        public void HandleLnbPolarization(string payload) { PublishErrorInternal("LNB not yet bound"); }
        public void HandleLnbTone(string payload) { PublishErrorInternal("LNB not yet bound"); }
        public void HandleLnbBand(string payload) { PublishErrorInternal("LNB not yet bound"); }
        public void HandleCalibrateReference() { PublishErrorInternal("calibration not yet bound"); }

        // ------------------ IMqttConfigSink ------------------
        public void PublishStatus(string subtopic, string value) { PublishStatusInternal(subtopic, value); }
        public void PublishError(string message) { PublishErrorInternal(message); }
        public void PublishEffectiveConfig() { PublishEffectiveConfigInternal(); }
        public void HandleConfigSave() { PublishErrorInternal("config/save: FRAM not yet enabled in this firmware build"); }
        public void HandleConfigReset()
        {
            _runtimeConfig = RuntimeConfiguration.CreateDefaults();
            _savedConfig = _runtimeConfig.Clone();
            PublishStatusInternal("config/reset", "ok");
            PublishEffectiveConfigInternal();
        }
        public void HandleConfigReload() { PublishEffectiveConfigInternal(); }
        public void HandleConfigFramClear(string token) { PublishErrorInternal("config/fram_clear: FRAM not yet enabled in this firmware build"); }
    }
}
