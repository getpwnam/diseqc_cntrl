using System;
using System.Device.Gpio;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using DiseqC.Manager;
using DiseqC.Native;
using nanoFramework.M2Mqtt;
using nanoFramework.M2Mqtt.Messages;

namespace DiseqC
{
    public class Program
    {
        // Configuration - TODO: Move to ConfigurationManager
        private const string MQTT_BROKER = "192.168.1.50";
        private const int MQTT_PORT = 1883;
        private const string MQTT_CLIENT_ID = "diseqc_controller";
        private const string MQTT_USERNAME = "";  // Optional
        private const string MQTT_PASSWORD = "";  // Optional

        // MQTT Topics
        private const string TOPIC_PREFIX = "diseqc";
        private const string TOPIC_AVAILABILITY = TOPIC_PREFIX + "/availability";

        // Global instances
        private static MqttClient _mqttClient;
        private static RotorManager _rotor;
        private static bool _isConnected = false;

        public static void Main()
        {
            Debug.WriteLine("==============================================");
            Debug.WriteLine("DiSEqC Controller Starting...");
            Debug.WriteLine("STM32F407VGT6 + W5500 + nanoFramework");
            Debug.WriteLine("==============================================");

            // Initialize hardware
            InitializeNetwork();

            // Initialize rotor manager (native driver)
            Debug.WriteLine("Initializing DiSEqC native driver...");
            _rotor = new RotorManager();

            // Connect to MQTT
            ConnectToMqtt();

            // Main loop
            Debug.WriteLine("Entering main loop...");
            MainLoop();
        }

        #region Network Initialization

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

            // Enable DHCP
            Debug.WriteLine("Requesting DHCP address...");
            netIf.EnableDhcp();

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
                netIf.EnableStaticIPv4("192.168.1.100", "255.255.255.0", "192.168.1.1");
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

        #region MQTT Connection

        private static void ConnectToMqtt()
        {
            Debug.WriteLine("\n--- MQTT Initialization ---");
            Debug.WriteLine($"Broker: {MQTT_BROKER}:{MQTT_PORT}");

            try
            {
                // Create MQTT client
                _mqttClient = new MqttClient(MQTT_BROKER, MQTT_PORT, false, null, null, MqttSslProtocols.None);

                // Set event handlers
                _mqttClient.MqttMsgPublishReceived += OnMqttMessageReceived;
                _mqttClient.ConnectionClosed += OnMqttConnectionClosed;

                // Connect with LWT (Last Will and Testament)
                byte willQos = MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE;
                bool willRetain = true;

                byte connectResult;
                if (string.IsNullOrEmpty(MQTT_USERNAME))
                {
                    connectResult = _mqttClient.Connect(
                        MQTT_CLIENT_ID,
                        TOPIC_AVAILABILITY,         // Will topic
                        Encoding.UTF8.GetBytes("offline"), // Will message
                        willQos,
                        willRetain
                    );
                }
                else
                {
                    connectResult = _mqttClient.Connect(
                        MQTT_CLIENT_ID,
                        MQTT_USERNAME,
                        MQTT_PASSWORD,
                        TOPIC_AVAILABILITY,
                        willQos,
                        willRetain,
                        Encoding.UTF8.GetBytes("offline")
                    );
                }

                if (connectResult == 0)
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
                TOPIC_PREFIX + "/command/goto/angle",
                TOPIC_PREFIX + "/command/goto/satellite",
                TOPIC_PREFIX + "/command/halt",

                // Manual control
                TOPIC_PREFIX + "/command/manual/step_east",
                TOPIC_PREFIX + "/command/manual/step_west",
                TOPIC_PREFIX + "/command/manual/drive_east",
                TOPIC_PREFIX + "/command/manual/drive_west",

                // LNB control
                TOPIC_PREFIX + "/command/lnb/voltage",
                TOPIC_PREFIX + "/command/lnb/polarization",
                TOPIC_PREFIX + "/command/lnb/tone",
                TOPIC_PREFIX + "/command/lnb/band",

                // Configuration
                TOPIC_PREFIX + "/command/config/save",
                TOPIC_PREFIX + "/command/config/reset",
                TOPIC_PREFIX + "/command/calibrate/reference"
            };

            byte[] qosLevels = new byte[topics.Length];
            for (int i = 0; i < topics.Length; i++)
            {
                qosLevels[i] = MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE;
            }

            _mqttClient.Subscribe(topics, qosLevels);

            Debug.WriteLine($"✓ Subscribed to {topics.Length} topics");
        }

        private static void PublishAvailability(bool online)
        {
            string payload = online ? "online" : "offline";
            _mqttClient.Publish(
                TOPIC_AVAILABILITY,
                Encoding.UTF8.GetBytes(payload),
                MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE,
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

            Debug.WriteLine("✓ Initial status published");
        }

        #endregion

        #region MQTT Message Handlers

        private static void OnMqttMessageReceived(object sender, MqttMsgPublishEventArgs e)
        {
            string topic = e.Topic;
            string payload = Encoding.UTF8.GetString(e.Message);

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
                else if (topic.Contains("/command/config/save"))
                {
                    HandleConfigSave();
                }
                else if (topic.Contains("/command/config/reset"))
                {
                    HandleConfigReset();
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
            // TODO: Implement configuration save
            PublishStatus("config/saved", "true");
        }

        private static void HandleConfigReset()
        {
            Debug.WriteLine("[CMD] Resetting to factory defaults...");
            // TODO: Implement configuration reset
            PublishStatus("config/reset", "true");
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

            string topic = $"{TOPIC_PREFIX}/status/{subtopic}";

            try
            {
                _mqttClient.Publish(
                    topic,
                    Encoding.UTF8.GetBytes(value),
                    MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE,
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
    }
}