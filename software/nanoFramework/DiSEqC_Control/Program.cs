using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using DiSEqC_Control.Manager;
using DiSEqC_Control.Native;
using nanoFramework.M2Mqtt;
using nanoFramework.M2Mqtt.Messages;

namespace DiSEqC_Control
{
    public class Program
    {
        // Global instances
        private static MqttClient _mqttClient;
        private static RotorManager _rotor;
        private static bool _isConnected = false;
        private static RuntimeConfiguration _runtimeConfig = RuntimeConfiguration.CreateDefaults();
        private static RuntimeConfiguration _savedConfig = RuntimeConfiguration.CreateDefaults();
        private static FramConfigurationStorage _configStorage;
        private static SerialPort _serialCommandPort;
        private const string SERIAL_COMMAND_PORT = "COM2";
        private const int SERIAL_COMMAND_BAUD = 115200;
        private const int FRAM_I2C_BUS = 3;
        private const int FRAM_DUMP_DEFAULT_BYTES = 64;
        private const int FRAM_DUMP_MAX_BYTES = 256;
        private const string FRAM_CLEAR_CONFIRMATION_TOKEN = "ERASE";

        private static string TopicPrefix => _runtimeConfig.MqttTopicPrefix;
        private static string TopicAvailability => TopicPrefix + "/availability";

        public static void Main()
        {
            Debug.WriteLine("==============================================");
            Debug.WriteLine("DiSEqC Controller Starting...");
            Debug.WriteLine("STM32F407VGT6 + W5500 + nanoFramework");
            Debug.WriteLine("==============================================");

            // Initialize runtime configuration and persistent storage
            InitializeConfiguration();

            // Initialize hardware
            InitializeNetwork();

            // Initialize rotor manager (native driver)
            Debug.WriteLine("Initializing DiSEqC native driver...");
            _rotor = new RotorManager();

            // Start serial command listener (MVP config channel)
            StartSerialCommandListener();

            // Connect to MQTT
            ConnectToMqtt();

            // Main loop
            Debug.WriteLine("Entering main loop...");
            MainLoop();
        }

        #region Network Initialization

        private static void InitializeConfiguration()
        {
            _runtimeConfig = RuntimeConfiguration.CreateDefaults();
            _savedConfig = _runtimeConfig.Clone();

            try
            {
                _configStorage = new FramConfigurationStorage(FRAM_I2C_BUS);

                if (_configStorage.TryLoad(out RuntimeConfiguration persisted, out string loadError))
                {
                    _runtimeConfig = persisted;
                    _savedConfig = persisted.Clone();
                    Debug.WriteLine("[CONFIG] Loaded persisted configuration from FRAM");
                }
                else
                {
                    Debug.WriteLine($"[CONFIG] No persisted config loaded: {loadError}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CONFIG] FRAM storage unavailable: {ex.Message}");
            }
        }

        private static void InitializeNetwork()
        {
            Debug.WriteLine("\n--- Network Initialization ---");

            // Get network interface (W5500)
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();

            if (interfaces.Length == 0)
            {
                Debug.WriteLine("ERROR: No network interfaces found!");
                Thread.Sleep(Timeout.Infinite);
            }

            NetworkInterface netIf = interfaces[0];

            Debug.WriteLine($"Network Interface: {netIf.NetworkInterfaceType}");
            Debug.WriteLine($"MAC Address: {GetMacAddress(netIf)}");

            if (_runtimeConfig.UseDhcp)
            {
                Debug.WriteLine("Requesting DHCP address...");
                netIf.EnableDhcp();
            }
            else
            {
                Debug.WriteLine("Applying configured static IP...");
                netIf.EnableStaticIPv4(_runtimeConfig.StaticIp, _runtimeConfig.StaticSubnetMask, _runtimeConfig.StaticGateway);
            }

            // Wait for valid IP
            int retries = 0;
            while (netIf.IPv4Address == "0.0.0.0" && retries < 30)
            {
                Debug.Write(".");
                Thread.Sleep(1000);
                retries++;
            }
            Debug.WriteLine("");

            if (netIf.IPv4Address == "0.0.0.0")
            {
                Debug.WriteLine("ERROR: Failed to get DHCP address!");
                Debug.WriteLine("Falling back to static IP...");

                // Fallback to static IP
                netIf.EnableStaticIPv4(_runtimeConfig.StaticIp, _runtimeConfig.StaticSubnetMask, _runtimeConfig.StaticGateway);
                Thread.Sleep(2000);
            }

            // Display network configuration
            Debug.WriteLine($"\n✓ Network Ready!");
            Debug.WriteLine($"  IP Address: {netIf.IPv4Address}");
            Debug.WriteLine($"  Subnet Mask: {netIf.IPv4SubnetMask}");
            Debug.WriteLine($"  Gateway: {netIf.IPv4GatewayAddress}");
            Debug.WriteLine($"  DNS: {netIf.IPv4DnsAddresses[0]}");
        }

        private static string GetMacAddress(NetworkInterface netIf)
        {
            byte[] mac = netIf.PhysicalAddress;
            return $"{mac[0]:X2}:{mac[1]:X2}:{mac[2]:X2}:{mac[3]:X2}:{mac[4]:X2}:{mac[5]:X2}";
        }

        #endregion

        #region Serial Command Interface

        private static void StartSerialCommandListener()
        {
            try
            {
                _serialCommandPort = new SerialPort(SERIAL_COMMAND_PORT, SERIAL_COMMAND_BAUD, Parity.None, 8, StopBits.One);
                _serialCommandPort.ReadTimeout = 500;
                _serialCommandPort.Open();

                Debug.WriteLine($"[SERIAL] Command interface active on {SERIAL_COMMAND_PORT} @ {SERIAL_COMMAND_BAUD}");
                Debug.WriteLine("[SERIAL] Commands: config get | config set key=value | config save | config reset | config reload | config fram-dump [bytes] | config fram-clear ERASE");

                new Thread(SerialCommandLoop).Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SERIAL] Command interface unavailable: {ex.Message}");
            }
        }

        private static void SerialCommandLoop()
        {
            byte[] oneByte = new byte[1];
            StringBuilder buffer = new StringBuilder();

            while (true)
            {
                try
                {
                    int bytesRead = _serialCommandPort.Read(oneByte, 0, 1);
                    if (bytesRead == 0)
                    {
                        continue;
                    }

                    char c = (char)oneByte[0];

                    if (c == '\r')
                    {
                        continue;
                    }

                    if (c == '\n')
                    {
                        string command = buffer.ToString().Trim();
                        buffer.Clear();

                        if (!string.IsNullOrEmpty(command))
                        {
                            HandleSerialCommand(command);
                        }

                        continue;
                    }

                    buffer.Append(c);
                }
                catch
                {
                    Thread.Sleep(50);
                }
            }
        }

        private static void HandleSerialCommand(string command)
        {
            string normalized = command.ToLower();
            Debug.WriteLine($"[SERIAL] Command: {command}");

            if (normalized == "help" || normalized == "?")
            {
                Debug.WriteLine("[SERIAL] config get");
                Debug.WriteLine("[SERIAL] config set <key>=<value>");
                Debug.WriteLine("[SERIAL] config save");
                Debug.WriteLine("[SERIAL] config reset");
                Debug.WriteLine("[SERIAL] config reload");
                Debug.WriteLine("[SERIAL] config fram-dump [bytes]");
                Debug.WriteLine("[SERIAL] config fram-clear ERASE");
                return;
            }

            if (normalized == "config get")
            {
                HandleConfigGet(string.Empty);
                return;
            }

            if (normalized == "config save")
            {
                HandleConfigSave();
                return;
            }

            if (normalized == "config reset")
            {
                HandleConfigReset();
                return;
            }

            if (normalized == "config reload")
            {
                HandleConfigReload();
                return;
            }

            if (normalized.StartsWith("config fram-dump"))
            {
                string payload = string.Empty;
                if (command.Length > "config fram-dump".Length)
                {
                    payload = command.Substring("config fram-dump".Length).Trim();
                }

                HandleConfigFramDump(payload);
                return;
            }

            if (normalized.StartsWith("config fram-clear"))
            {
                string payload = string.Empty;
                if (command.Length > "config fram-clear".Length)
                {
                    payload = command.Substring("config fram-clear".Length).Trim();
                }

                HandleConfigFramClear(payload);
                return;
            }

            if (normalized.StartsWith("config set "))
            {
                string payload = command.Substring("config set ".Length).Trim();
                HandleConfigSet(payload);
                return;
            }

            Debug.WriteLine("[SERIAL] Unknown command. Type 'help'.");
        }

        #endregion

        #region MQTT Connection

        private static void ConnectToMqtt()
        {
            Debug.WriteLine("\n--- MQTT Initialization ---");
            Debug.WriteLine($"Broker: {_runtimeConfig.MqttBroker}:{_runtimeConfig.MqttPort}");

            try
            {
                // Create MQTT client
                _mqttClient = new MqttClient(_runtimeConfig.MqttBroker, _runtimeConfig.MqttPort, false, null, null, MqttSslProtocols.None);

                // Set event handlers
                _mqttClient.MqttMsgPublishReceived += OnMqttMessageReceived;
                _mqttClient.ConnectionClosed += OnMqttConnectionClosed;

                // Connect with LWT (Last Will and Testament)
                MqttQoSLevel willQos = MqttQoSLevel.AtLeastOnce;
                bool willRetain = true;

                MqttReasonCode connectResult;
                bool hasCredentials = !string.IsNullOrEmpty(_runtimeConfig.MqttUsername);
                connectResult = _mqttClient.Connect(
                    _runtimeConfig.MqttClientId,
                    hasCredentials ? _runtimeConfig.MqttUsername : null,
                    hasCredentials ? _runtimeConfig.MqttPassword : null,
                    willRetain,
                    willQos,
                    true,
                    TopicAvailability,
                    "offline",
                    true,
                    60
                );

                if (connectResult == MqttReasonCode.Success)
                {
                    Debug.WriteLine("✓ Connected to MQTT broker!");
                    _isConnected = true;

                    // Publish availability (online)
                    PublishAvailability(true);

                    // Subscribe to command topics
                    SubscribeToTopics();

                    // Publish initial status
                    PublishInitialStatus();
                }
                else
                {
                    Debug.WriteLine($"ERROR: MQTT connection failed! Code: {connectResult}");
                    _isConnected = false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR: MQTT exception: {ex.Message}");
                _isConnected = false;
            }
        }

        private static void SubscribeToTopics()
        {
            Debug.WriteLine("\n--- Subscribing to MQTT Topics ---");

            string[] topics = new[]
            {
                // Position control
                TopicPrefix + "/command/goto/angle",
                TopicPrefix + "/command/goto/satellite",
                TopicPrefix + "/command/halt",

                // Manual control
                TopicPrefix + "/command/manual/step_east",
                TopicPrefix + "/command/manual/step_west",
                TopicPrefix + "/command/manual/drive_east",
                TopicPrefix + "/command/manual/drive_west",

                // LNB control
                TopicPrefix + "/command/lnb/voltage",
                TopicPrefix + "/command/lnb/polarization",
                TopicPrefix + "/command/lnb/tone",
                TopicPrefix + "/command/lnb/band",

                // Configuration
                TopicPrefix + "/command/config/get",
                TopicPrefix + "/command/config/set",
                TopicPrefix + "/command/config/save",
                TopicPrefix + "/command/config/reset",
                TopicPrefix + "/command/config/reload",
                TopicPrefix + "/command/config/fram_clear",
                TopicPrefix + "/command/calibrate/reference"
            };

            MqttQoSLevel[] qosLevels = new MqttQoSLevel[topics.Length];
            for (int i = 0; i < topics.Length; i++)
            {
                qosLevels[i] = MqttQoSLevel.AtLeastOnce;
            }

            _mqttClient.Subscribe(topics, qosLevels);

            Debug.WriteLine($"✓ Subscribed to {topics.Length} topics");
        }

        private static void PublishAvailability(bool online)
        {
            string payload = online ? "online" : "offline";
            _mqttClient.Publish(
                TopicAvailability,
                Encoding.UTF8.GetBytes(payload),
                null,
                null,
                MqttQoSLevel.AtLeastOnce,
                true  // Retained
            );
            Debug.WriteLine($"Published availability: {payload}");
        }

        private static void PublishInitialStatus()
        {
            Debug.WriteLine("\n--- Publishing Initial Status ---");

            // State
            PublishStatus("state", "idle");

            // Position
            PublishStatus("position/angle", "0.0");
            PublishStatus("position/satellite", "unknown");

            // Busy flag
            PublishStatus("busy", "false");

            // LNB status
            var voltage = LNB.GetVoltage();
            var tone = LNB.GetTone();
            var polarization = LNB.GetPolarization();
            var band = LNB.GetBand();

            PublishStatus("lnb/voltage", voltage == LNB.Voltage.V13 ? "13" : "18");
            PublishStatus("lnb/tone", tone ? "on" : "off");
            PublishStatus("lnb/polarization", polarization == LNB.Polarization.Vertical ? "vertical" : "horizontal");
            PublishStatus("lnb/band", band == LNB.Band.Low ? "low" : "high");

            PublishEffectiveConfig();

            Debug.WriteLine("✓ Initial status published");
        }

        #endregion

        #region MQTT Message Handlers

        private static void OnMqttMessageReceived(object sender, MqttMsgPublishEventArgs e)
        {
            string topic = e.Topic;
            string payload = Encoding.UTF8.GetString(e.Message, 0, e.Message.Length);

            Debug.WriteLine($"\n[MQTT] Topic: {topic}");
            Debug.WriteLine($"[MQTT] Payload: {payload}");

            try
            {
                // Route to appropriate handler
                if (topic.Contains("/command/goto/angle"))
                {
                    HandleGotoAngle(payload);
                }
                else if (topic.Contains("/command/goto/satellite"))
                {
                    HandleGotoSatellite(payload);
                }
                else if (topic.Contains("/command/halt"))
                {
                    HandleHalt();
                }
                else if (topic.Contains("/command/manual/step_east"))
                {
                    HandleStepEast(payload);
                }
                else if (topic.Contains("/command/manual/step_west"))
                {
                    HandleStepWest(payload);
                }
                else if (topic.Contains("/command/manual/drive_east"))
                {
                    HandleDriveEast();
                }
                else if (topic.Contains("/command/manual/drive_west"))
                {
                    HandleDriveWest();
                }
                else if (topic.Contains("/command/lnb/voltage"))
                {
                    HandleLnbVoltage(payload);
                }
                else if (topic.Contains("/command/lnb/polarization"))
                {
                    HandleLnbPolarization(payload);
                }
                else if (topic.Contains("/command/lnb/tone"))
                {
                    HandleLnbTone(payload);
                }
                else if (topic.Contains("/command/lnb/band"))
                {
                    HandleLnbBand(payload);
                }
                else if (topic.Contains("/command/config/get"))
                {
                    HandleConfigGet(payload);
                }
                else if (topic.Contains("/command/config/set"))
                {
                    HandleConfigSet(payload);
                }
                else if (topic.Contains("/command/config/save"))
                {
                    HandleConfigSave();
                }
                else if (topic.Contains("/command/config/reset"))
                {
                    HandleConfigReset();
                }
                else if (topic.Contains("/command/config/reload"))
                {
                    HandleConfigReload();
                }
                else if (topic.Contains("/command/config/fram_clear"))
                {
                    HandleConfigFramClear(payload);
                }
                else if (topic.Contains("/command/calibrate/reference"))
                {
                    HandleCalibrateReference();
                }
                else
                {
                    Debug.WriteLine($"[MQTT] Unknown topic: {topic}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MQTT] Error handling message: {ex.Message}");
                PublishError($"Command error: {ex.Message}");
            }
        }

        private static void OnMqttConnectionClosed(object sender, EventArgs e)
        {
            Debug.WriteLine("\n[MQTT] Connection lost! Attempting to reconnect...");
            _isConnected = false;

            // Will attempt reconnection in main loop
        }

        #endregion

        #region Command Handlers

        private static void HandleGotoAngle(string payload)
        {
            if (!float.TryParse(payload, out float angle))
            {
                PublishError("Invalid angle format");
                return;
            }

            if (_rotor.IsBusy())
            {
                PublishError("Rotor is busy");
                return;
            }

            Debug.WriteLine($"[CMD] Moving to angle: {angle}°");
            PublishStatus("state", "moving");
            PublishStatus("busy", "true");

            _rotor.GotoAngle(angle);

            // Wait for completion in background
            new Thread(() =>
            {
                while (_rotor.IsBusy())
                {
                    Thread.Sleep(100);
                }

                PublishStatus("state", "idle");
                PublishStatus("position/angle", angle.ToString("F1"));
                PublishStatus("position/satellite", "unknown");  // TODO: Match to satellite
                PublishStatus("busy", "false");

                Debug.WriteLine($"[CMD] Movement complete: {angle}°");
            }).Start();
        }

        private static void HandleGotoSatellite(string payload)
        {
            // TODO: Look up satellite in database
            // For now, hardcoded examples
            float angle = 0;

            switch (payload.ToLower())
            {
                case "astra_19.2e":
                    angle = 19.2f;
                    break;
                case "hotbird_13e":
                    angle = 13.0f;
                    break;
                case "astra_28.2e":
                    angle = 28.2f;
                    break;
                default:
                    PublishError($"Unknown satellite: {payload}");
                    return;
            }

            Debug.WriteLine($"[CMD] Moving to satellite: {payload} ({angle}°)");
            PublishStatus("state", "moving");
            PublishStatus("busy", "true");

            _rotor.GotoAngle(angle);

            // Wait for completion
            new Thread(() =>
            {
                while (_rotor.IsBusy())
                {
                    Thread.Sleep(100);
                }

                PublishStatus("state", "idle");
                PublishStatus("position/angle", angle.ToString("F1"));
                PublishStatus("position/satellite", payload);
                PublishStatus("busy", "false");

                Debug.WriteLine($"[CMD] At satellite: {payload}");
            }).Start();
        }

        private static void HandleHalt()
        {
            Debug.WriteLine("[CMD] Halting rotor");
            _rotor.Halt();

            PublishStatus("state", "idle");
            PublishStatus("busy", "false");
        }

        private static void HandleStepEast(string payload)
        {
            byte steps = 1;
            if (!string.IsNullOrEmpty(payload))
            {
                if (!byte.TryParse(payload, out steps))
                {
                    PublishError("Invalid step count");
                    return;
                }
            }

            if (_rotor.IsBusy())
            {
                PublishError("Rotor is busy");
                return;
            }

            Debug.WriteLine($"[CMD] Step East: {steps} steps");
            PublishStatus("state", "stepping_east");
            PublishStatus("busy", "true");

            _rotor.StepEast(steps);

            // Wait and update status
            new Thread(() =>
            {
                Thread.Sleep(2000);  // Wait for step to complete
                PublishStatus("state", "idle");
                PublishStatus("busy", "false");
            }).Start();
        }

        private static void HandleStepWest(string payload)
        {
            byte steps = 1;
            if (!string.IsNullOrEmpty(payload))
            {
                if (!byte.TryParse(payload, out steps))
                {
                    PublishError("Invalid step count");
                    return;
                }
            }

            if (_rotor.IsBusy())
            {
                PublishError("Rotor is busy");
                return;
            }

            Debug.WriteLine($"[CMD] Step West: {steps} steps");
            PublishStatus("state", "stepping_west");
            PublishStatus("busy", "true");

            _rotor.StepWest(steps);

            new Thread(() =>
            {
                Thread.Sleep(2000);
                PublishStatus("state", "idle");
                PublishStatus("busy", "false");
            }).Start();
        }

        private static void HandleDriveEast()
        {
            if (_rotor.IsBusy())
            {
                PublishError("Rotor is busy");
                return;
            }

            Debug.WriteLine("[CMD] Drive East (continuous)");
            PublishStatus("state", "driving_east");
            PublishStatus("busy", "true");

            _rotor.DriveEast();
        }

        private static void HandleDriveWest()
        {
            if (_rotor.IsBusy())
            {
                PublishError("Rotor is busy");
                return;
            }

            Debug.WriteLine("[CMD] Drive West (continuous)");
            PublishStatus("state", "driving_west");
            PublishStatus("busy", "true");

            _rotor.DriveWest();
        }

        private static void HandleLnbVoltage(string payload)
        {
            Debug.WriteLine($"[CMD] Set LNB voltage: {payload}V");

            LNB.Voltage voltage;
            if (payload == "13")
            {
                voltage = LNB.Voltage.V13;
            }
            else if (payload == "18")
            {
                voltage = LNB.Voltage.V18;
            }
            else
            {
                PublishError($"Invalid voltage: {payload}. Use 13 or 18");
                return;
            }

            var status = LNB.SetVoltage(voltage);
            if (status == LNB.Status.Ok)
            {
                PublishStatus("lnb/voltage", payload);
                PublishStatus("lnb/polarization", voltage == LNB.Voltage.V13 ? "vertical" : "horizontal");
            }
            else
            {
                PublishError($"Failed to set voltage: {status}");
            }
        }

        private static void HandleLnbPolarization(string payload)
        {
            Debug.WriteLine($"[CMD] Set LNB polarization: {payload}");

            LNB.Polarization polarization;
            if (payload.ToLower() == "vertical" || payload == "v")
            {
                polarization = LNB.Polarization.Vertical;
            }
            else if (payload.ToLower() == "horizontal" || payload == "h")
            {
                polarization = LNB.Polarization.Horizontal;
            }
            else
            {
                PublishError($"Invalid polarization: {payload}. Use vertical or horizontal");
                return;
            }

            var status = LNB.SetPolarization(polarization);
            if (status == LNB.Status.Ok)
            {
                PublishStatus("lnb/polarization", polarization == LNB.Polarization.Vertical ? "vertical" : "horizontal");
                PublishStatus("lnb/voltage", polarization == LNB.Polarization.Vertical ? "13" : "18");
            }
            else
            {
                PublishError($"Failed to set polarization: {status}");
            }
        }

        private static void HandleLnbTone(string payload)
        {
            Debug.WriteLine($"[CMD] Set LNB tone: {payload}");

            bool enable;
            if (payload.ToLower() == "on" || payload == "1" || payload.ToLower() == "true")
            {
                enable = true;
            }
            else if (payload.ToLower() == "off" || payload == "0" || payload.ToLower() == "false")
            {
                enable = false;
            }
            else
            {
                PublishError($"Invalid tone value: {payload}. Use on or off");
                return;
            }

            var status = LNB.SetTone(enable);
            if (status == LNB.Status.Ok)
            {
                PublishStatus("lnb/tone", enable ? "on" : "off");
                PublishStatus("lnb/band", enable ? "high" : "low");
            }
            else
            {
                PublishError($"Failed to set tone: {status}");
            }
        }

        private static void HandleLnbBand(string payload)
        {
            Debug.WriteLine($"[CMD] Set LNB band: {payload}");

            LNB.Band band;
            if (payload.ToLower() == "low" || payload == "l")
            {
                band = LNB.Band.Low;
            }
            else if (payload.ToLower() == "high" || payload == "h")
            {
                band = LNB.Band.High;
            }
            else
            {
                PublishError($"Invalid band: {payload}. Use low or high");
                return;
            }

            var status = LNB.SetBand(band);
            if (status == LNB.Status.Ok)
            {
                PublishStatus("lnb/band", band == LNB.Band.Low ? "low" : "high");
                PublishStatus("lnb/tone", band == LNB.Band.High ? "on" : "off");
            }
            else
            {
                PublishError($"Failed to set band: {status}");
            }
        }

        private static void HandleConfigSave()
        {
            Debug.WriteLine("[CMD] Saving configuration...");
            _savedConfig = _runtimeConfig.Clone();

            bool persisted = false;
            if (_configStorage != null)
            {
                if (_configStorage.TrySave(_savedConfig, out string persistenceError))
                {
                    persisted = true;
                }
                else
                {
                    Debug.WriteLine($"[CONFIG] Persist save failed: {persistenceError}");
                }
            }

            PublishStatus("config/saved", "true");
            PublishStatus("config/save_result", "ok");
            PublishStatus("config/persisted", persisted ? "true" : "false");
        }

        private static void HandleConfigReset()
        {
            Debug.WriteLine("[CMD] Resetting to factory defaults...");
            _runtimeConfig = RuntimeConfiguration.CreateDefaults();
            PublishStatus("config/reset", "true");
            PublishEffectiveConfig();
        }

        private static void HandleConfigReload()
        {
            Debug.WriteLine("[CMD] Reloading last saved configuration...");

            bool loadedFromFram = false;
            if (_configStorage != null && _configStorage.TryLoad(out RuntimeConfiguration persisted, out string loadError))
            {
                _runtimeConfig = persisted;
                _savedConfig = persisted.Clone();
                loadedFromFram = true;
            }
            else
            {
                _runtimeConfig = _savedConfig.Clone();

                if (_configStorage != null)
                {
                    Debug.WriteLine($"[CONFIG] FRAM reload fallback to RAM snapshot");
                }
            }

            PublishStatus("config/reloaded", "true");
            PublishStatus("config/reload_source", loadedFromFram ? "fram" : "ram");
            PublishEffectiveConfig();
        }

        private static void HandleConfigGet(string payload)
        {
            Debug.WriteLine("[CMD] Returning effective configuration");
            PublishEffectiveConfig();
        }

        private static void HandleConfigSet(string payload)
        {
            if (string.IsNullOrEmpty(payload))
            {
                PublishError("Config set payload must be key=value");
                return;
            }

            int separator = payload.IndexOf('=');
            if (separator <= 0 || separator >= payload.Length - 1)
            {
                PublishError("Config set payload must be key=value");
                return;
            }

            string key = payload.Substring(0, separator).Trim();
            string value = payload.Substring(separator + 1).Trim();

            if (!_runtimeConfig.TrySetValue(key, value, out string error))
            {
                PublishError(error);
                return;
            }

            Debug.WriteLine($"[CMD] Config updated: {key}={value}");
            PublishStatus("config/updated", key);
            PublishEffectiveConfig();
        }

        private static void HandleConfigFramDump(string payload)
        {
            if (_configStorage == null)
            {
                Debug.WriteLine("[CONFIG] FRAM storage unavailable");
                return;
            }

            int bytesToRead = FRAM_DUMP_DEFAULT_BYTES;
            if (!string.IsNullOrEmpty(payload))
            {
                if (!int.TryParse(payload, out bytesToRead))
                {
                    Debug.WriteLine("[CONFIG] fram-dump expects optional integer byte count");
                    return;
                }

                if (bytesToRead <= 0)
                {
                    Debug.WriteLine("[CONFIG] fram-dump byte count must be > 0");
                    return;
                }

                if (bytesToRead > FRAM_DUMP_MAX_BYTES)
                {
                    bytesToRead = FRAM_DUMP_MAX_BYTES;
                }
            }

            if (!_configStorage.TryReadRaw(0, bytesToRead, out byte[] data, out string error))
            {
                Debug.WriteLine($"[CONFIG] FRAM dump failed: {error}");
                return;
            }

            Debug.WriteLine($"[CONFIG] FRAM dump (0..{bytesToRead - 1})");

            if (data.Length >= 9)
            {
                bool magicValid = data[0] == (byte)'D' && data[1] == (byte)'C' && data[2] == (byte)'F' && data[3] == (byte)'G';
                int version = data[4];
                int payloadLength = data[5] | (data[6] << 8);
                int checksum = data[7] | (data[8] << 8);

                Debug.WriteLine($"[CONFIG] Header magic={(magicValid ? "DCFG" : "invalid")}, version={version}, length={payloadLength}, checksum=0x{checksum:X4}");
            }

            for (int offset = 0; offset < data.Length; offset += 16)
            {
                int chunk = data.Length - offset;
                if (chunk > 16)
                {
                    chunk = 16;
                }

                StringBuilder line = new StringBuilder();
                line.Append("[FRAM] ");
                line.Append(offset.ToString("X4"));
                line.Append(": ");

                for (int i = 0; i < chunk; i++)
                {
                    if (i > 0)
                    {
                        line.Append(' ');
                    }

                    line.Append(data[offset + i].ToString("X2"));
                }

                Debug.WriteLine(line.ToString());
            }
        }

        private static void HandleConfigFramClear(string payload)
        {
            if (_configStorage == null)
            {
                Debug.WriteLine("[CONFIG] FRAM storage unavailable");
                return;
            }

            if (payload != FRAM_CLEAR_CONFIRMATION_TOKEN)
            {
                Debug.WriteLine($"[CONFIG] Refusing FRAM clear. Use: config fram-clear {FRAM_CLEAR_CONFIRMATION_TOKEN}");
                return;
            }

            if (!_configStorage.TryClear(out string error))
            {
                Debug.WriteLine($"[CONFIG] FRAM clear failed: {error}");
                return;
            }

            _runtimeConfig = RuntimeConfiguration.CreateDefaults();
            _savedConfig = _runtimeConfig.Clone();

            Debug.WriteLine("[CONFIG] FRAM cleared; runtime config reset to defaults");
            PublishStatus("config/fram_cleared", "true");
            PublishEffectiveConfig();
        }

        private static void PublishEffectiveConfig()
        {
            PublishStatus("config/effective/network/use_dhcp", _runtimeConfig.UseDhcp ? "true" : "false");
            PublishStatus("config/effective/network/static_ip", _runtimeConfig.StaticIp);
            PublishStatus("config/effective/network/static_subnet", _runtimeConfig.StaticSubnetMask);
            PublishStatus("config/effective/network/static_gateway", _runtimeConfig.StaticGateway);

            PublishStatus("config/effective/mqtt/broker", _runtimeConfig.MqttBroker);
            PublishStatus("config/effective/mqtt/port", _runtimeConfig.MqttPort.ToString());
            PublishStatus("config/effective/mqtt/client_id", _runtimeConfig.MqttClientId);
            PublishStatus("config/effective/mqtt/topic_prefix", _runtimeConfig.MqttTopicPrefix);

            PublishStatus("config/effective/system/device_name", _runtimeConfig.DeviceName);
            PublishStatus("config/effective/system/location", _runtimeConfig.DeviceLocation);
        }

        private static void HandleCalibrateReference()
        {
            Debug.WriteLine("[CMD] Setting current position as reference (0°)");
            // TODO: Implement calibration
            PublishStatus("calibration/reference", "set");
        }

        #endregion

        #region MQTT Publishing Helpers

        private static void PublishStatus(string subtopic, string value)
        {
            if (!_isConnected) return;

            string topic = $"{TopicPrefix}/status/{subtopic}";

            try
            {
                _mqttClient.Publish(
                    topic,
                    Encoding.UTF8.GetBytes(value),
                    null,
                    null,
                    MqttQoSLevel.AtMostOnce,
                    true  // Retained
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MQTT] Publish error: {ex.Message}");
            }
        }

        private static void PublishError(string errorMessage)
        {
            Debug.WriteLine($"[ERROR] {errorMessage}");

            if (_isConnected)
            {
                PublishStatus("error", errorMessage);
            }
        }

        #endregion

        #region Main Loop

        private static void MainLoop()
        {
            int loopCounter = 0;

            while (true)
            {
                // Check MQTT connection
                if (!_isConnected || !_mqttClient.IsConnected)
                {
                    Debug.WriteLine("[MQTT] Disconnected. Reconnecting...");
                    Thread.Sleep(5000);
                    ConnectToMqtt();
                }

                // Periodic status update (every 30 seconds)
                if (loopCounter % 30 == 0)
                {
                    PublishPeriodicStatus();
                }

                // Heartbeat
                if (loopCounter % 10 == 0)
                {
                    Debug.WriteLine($"[HEARTBEAT] Uptime: {loopCounter} seconds");
                }

                loopCounter++;
                Thread.Sleep(1000);
            }
        }

        private static void PublishPeriodicStatus()
        {
            if (!_isConnected) return;

            // Current state
            bool busy = _rotor.IsBusy();
            PublishStatus("busy", busy ? "true" : "false");

            if (!busy)
            {
                PublishStatus("state", "idle");
            }

            // Current angle (if tracked)
            float angle = _rotor.CurrentAngle;
            PublishStatus("position/angle", angle.ToString("F1"));

            // Uptime (optional)
            // PublishStatus("health/uptime", DateTime.UtcNow.Ticks.ToString());
        }

        #endregion
    }
}