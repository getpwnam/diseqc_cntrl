using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading;
using nanoFramework.M2Mqtt;
using nanoFramework.M2Mqtt.Exceptions;
using nanoFramework.M2Mqtt.Messages;

namespace CubleyControl
{
    public static partial class Program
    {
        private static MqttClient _mqttClient;
        private static string _mqttCommandTopic = string.Empty;
        private static string _mqttStatusTopic = string.Empty;
        private static string _mqttRuntimeState = "disabled";
        private static string _mqttLastError = string.Empty;
        private static int _mqttReconnectAttempts;

        private static void MqttLoop()
        {
            while (true)
            {
                MqttConfiguration configuration;
                int revision;
                lock (_mqttConfigurationLock)
                {
                    configuration = _mqttConfiguration.Clone();
                    revision = _mqttConfigurationRevision;
                }

                if (!configuration.Enabled)
                {
                    _mqttRuntimeState = "disabled";
                    Thread.Sleep(1000);
                    continue;
                }

                if (!HasUsableIpv4Address())
                {
                    _mqttRuntimeState = "waiting_network";
                    Thread.Sleep(configuration.ReconnectSeconds * 1000);
                    continue;
                }

                try
                {
                    RunMqttSession(configuration, revision);
                }
                catch (MqttCommunicationException)
                {
                    _mqttLastError = "communication_error";
                    _mqttRuntimeState = "error";
                    Debug.WriteLine("[MQTT] session error: " + _mqttLastError);
                }
                catch (Exception ex)
                {
                    _mqttLastError = SanitizeToken(ex.Message);
                    _mqttRuntimeState = "error";
                    Debug.WriteLine("[MQTT] session error: " + _mqttLastError);
                }

                Thread.Sleep(configuration.ReconnectSeconds * 1000);
            }
        }

        private static void RunMqttSession(MqttConfiguration configuration, int revision)
        {
            string availabilityTopic = configuration.TopicPrefix + "/availability";
            _mqttCommandTopic = configuration.TopicPrefix + "/command";
            _mqttStatusTopic = configuration.TopicPrefix + "/status";
            _mqttReconnectAttempts++;
            _mqttRuntimeState = "connecting";

            _mqttClient = new MqttClient(configuration.Broker, configuration.Port, false, null, null, MqttSslProtocols.None);
            _mqttClient.ProtocolVersion = MqttProtocolVersion.Version_3_1_1;
            _mqttClient.MqttMsgPublishReceived += OnMqttMessageReceived;
            _mqttClient.ConnectionClosed += OnMqttConnectionClosed;

            try
            {
                MqttReasonCode result = _mqttClient.Connect(
                    ResolveMqttClientId(configuration.ClientId),
                    string.IsNullOrEmpty(configuration.Username) ? null : configuration.Username,
                    string.IsNullOrEmpty(configuration.Password) ? null : configuration.Password,
                    true,
                    MqttQoSLevel.AtLeastOnce,
                    true,
                    availabilityTopic,
                    "offline",
                    true,
                    (ushort)configuration.KeepAliveSeconds);

                if (result != MqttReasonCode.Success)
                {
                    _mqttLastError = "connect_" + result.ToString();
                    _mqttRuntimeState = "error";
                    Debug.WriteLine("[MQTT] connect failed: " + result);
                    return;
                }

                _mqttReconnectAttempts = 0;
                _mqttLastError = string.Empty;
                _mqttRuntimeState = "connected";
                Debug.WriteLine("[MQTT] connected to " + configuration.Broker + ":" + configuration.Port.ToString());

                PublishLine(availabilityTopic, "online", true);
                _mqttClient.Subscribe(new string[] { _mqttCommandTopic }, new MqttQoSLevel[] { MqttQoSLevel.AtLeastOnce });

                while (_mqttClient.IsConnected && revision == _mqttConfigurationRevision)
                {
                    Thread.Sleep(500);
                }

                if (_mqttClient.IsConnected)
                {
                    TryDisconnectMqttClient(_mqttClient);
                }
            }
            finally
            {
                MqttClient client = _mqttClient;
                _mqttClient = null;
                _mqttCommandTopic = string.Empty;
                _mqttStatusTopic = string.Empty;
                if (client != null)
                {
                    client.MqttMsgPublishReceived -= OnMqttMessageReceived;
                    client.ConnectionClosed -= OnMqttConnectionClosed;
                    TryDisconnectMqttClient(client);
                    try
                    {
                        client.Close();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[MQTT] close ignored: " + ex.Message);
                    }
                }
            }
        }

        private static void TryDisconnectMqttClient(MqttClient client)
        {
            if (client == null || !client.IsConnected)
            {
                return;
            }

            try
            {
                client.Disconnect();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[MQTT] disconnect ignored: " + ex.Message);
            }
        }

        private static void OnMqttMessageReceived(object sender, MqttMsgPublishEventArgs e)
        {
            if (e.Topic != _mqttCommandTopic)
            {
                Debug.WriteLine("[MQTT-CMD] rejected topic=" + e.Topic);
                return;
            }

            if (e.Retain)
            {
                Debug.WriteLine("[MQTT-CMD] rejected retained message");
                return;
            }

            if (e.Message == null || e.Message.Length == 0 || e.Message.Length > UsbConsoleLineMaxLength)
            {
                Debug.WriteLine("[MQTT-CMD] rejected invalid length");
                return;
            }

            string payload = AsciiBytesToString(e.Message);
            Debug.WriteLine("[MQTT-CMD] topic=" + e.Topic + " payload=" + RedactCommandForLog(payload));

            ExecuteCommand(payload, MqttOutputSink);
        }

        private static void MqttOutputSink(string line)
        {
            PublishLine(_mqttStatusTopic, line.TrimEnd('\r', '\n'), false);
        }

        private static void OnMqttConnectionClosed(object sender, EventArgs e)
        {
            _mqttRuntimeState = "disconnected";
            Debug.WriteLine("[MQTT] connection closed; will reconnect");
        }

        private static bool HasUsableIpv4Address()
        {
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                return false;
            }

            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
            return interfaces != null &&
                interfaces.Length > 0 &&
                !string.IsNullOrEmpty(interfaces[0].IPv4Address) &&
                interfaces[0].IPv4Address != "0.0.0.0";
        }

        private static string ResolveMqttClientId(string configuredClientId)
        {
            if (!string.IsNullOrEmpty(configuredClientId))
            {
                return configuredClientId;
            }

            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
            if (interfaces == null || interfaces.Length == 0 || interfaces[0].PhysicalAddress == null || interfaces[0].PhysicalAddress.Length < 3)
            {
                return "cubley";
            }

            byte[] address = interfaces[0].PhysicalAddress;
            return "cubley-" + ToHex(address[address.Length - 3]) + ToHex(address[address.Length - 2]) + ToHex(address[address.Length - 1]);
        }

        private static string ToHex(byte value)
        {
            const string digits = "0123456789ABCDEF";
            return new string(new char[] { digits[(value >> 4) & 0x0F], digits[value & 0x0F] });
        }

        private static void PublishLine(string topic, string payload, bool retain)
        {
            if (_mqttClient == null || !_mqttClient.IsConnected)
            {
                return;
            }

            try
            {
                _mqttClient.Publish(
                    topic,
                    AsciiStringToBytes(payload),
                    null,
                    null,
                    MqttQoSLevel.AtMostOnce,
                    retain);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[MQTT] publish error: " + ex.Message);
            }
        }

        // Command and status payloads are intentionally limited to ASCII.
        private static byte[] AsciiStringToBytes(string text)
        {
            if (text == null)
            {
                return new byte[0];
            }

            byte[] result = new byte[text.Length];
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                result[i] = (c <= 0x7F) ? (byte)c : (byte)'?';
            }

            return result;
        }

        private static string AsciiBytesToString(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return string.Empty;
            }

            char[] chars = new char[bytes.Length];
            for (int i = 0; i < bytes.Length; i++)
            {
                byte b = bytes[i];
                chars[i] = (b <= 0x7F) ? (char)b : '?';
            }

            return new string(chars);
        }
    }
}
