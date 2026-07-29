using System;
using System.Diagnostics;
using System.Threading;
using nanoFramework.M2Mqtt;
using nanoFramework.M2Mqtt.Messages;

namespace CubleyControl
{
    public static partial class Program
    {
        // MQTT transport. Reuses the exact same command grammar as the serial
        // console: the payload of a message published to <prefix>/command is
        // handed to ExecuteCommand unchanged, so "diseqc drive east" or
        // "diseqc.rotor.step_east 5" behave identically whether typed over
        // USB-CDC or published over MQTT. Every OK/Fail line the command
        // produces is published (one message per line) to <prefix>/status
        // instead of being written to the serial port.
        //
        // Broker/port/topic-prefix are constants for now; there is no
        // persisted runtime config yet (system.config.* is declared in
        // Contracts/CommandIds.cs but not implemented). Networking itself is
        // not yet enabled in this firmware's native build (see defconfig),
        // so MqttEnabled defaults to false -- this transport is written and
        // ready but cannot be exercised until that native bring-up lands.
        private const bool MqttEnabled = false;
        private const string MqttBrokerHost = "192.168.1.50";
        private const int MqttBrokerPort = 1883;
        private const string MqttTopicPrefix = "diseqc";
        private const string MqttClientId = "cubley-diseqc-ctrl";
        private const ushort MqttKeepAliveSeconds = 60;
        private const int MqttReconnectDelayMs = 5000;
        private const int MqttConnectTimeoutMs = 8000;

        private static MqttClient _mqttClient;

        private static void MqttLoop()
        {
            if (!MqttEnabled)
            {
                Debug.WriteLine("[MQTT] transport disabled (MqttEnabled=false; native networking not yet enabled in this firmware build)");
                return;
            }

            while (true)
            {
                try
                {
                    RunMqttSession();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("[MQTT] session error: " + ex.Message);
                }

                Thread.Sleep(MqttReconnectDelayMs);
            }
        }

        private static void RunMqttSession()
        {
            string availabilityTopic = MqttTopicPrefix + "/availability";
            string commandTopic = MqttTopicPrefix + "/command";

            _mqttClient = new MqttClient(MqttBrokerHost, MqttBrokerPort, false, null, null, MqttSslProtocols.None);
            _mqttClient.MqttMsgPublishReceived += OnMqttMessageReceived;
            _mqttClient.ConnectionClosed += OnMqttConnectionClosed;

            MqttReasonCode result = _mqttClient.Connect(
                MqttClientId,
                string.Empty,
                string.Empty,
                true,
                MqttQoSLevel.AtLeastOnce,
                true,
                availabilityTopic,
                "offline",
                true,
                MqttKeepAliveSeconds);

            if (result != MqttReasonCode.Success)
            {
                Debug.WriteLine("[MQTT] connect failed: " + result);
                return;
            }

            Debug.WriteLine("[MQTT] connected to " + MqttBrokerHost + ":" + MqttBrokerPort);

            PublishLine(availabilityTopic, "online", true);
            _mqttClient.Subscribe(new string[] { commandTopic }, new MqttQoSLevel[] { MqttQoSLevel.AtLeastOnce });

            while (_mqttClient.IsConnected)
            {
                Thread.Sleep(1000);
            }
        }

        private static void OnMqttMessageReceived(object sender, MqttMsgPublishEventArgs e)
        {
            string payload = AsciiBytesToString(e.Message);
            Debug.WriteLine("[MQTT-CMD] topic=" + e.Topic + " payload=" + payload);

            ExecuteCommand(payload, MqttOutputSink);
        }

        private static void MqttOutputSink(string line)
        {
            PublishLine(MqttTopicPrefix + "/status", line.TrimEnd('\r', '\n'), false);
        }

        private static void OnMqttConnectionClosed(object sender, EventArgs e)
        {
            Debug.WriteLine("[MQTT] connection closed; will reconnect");
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

        // Minimal ASCII codec in lieu of System.Text.Encoding.UTF8: this
        // firmware build does not register native bindings for
        // nanoFramework.System.Text, so Encoding.UTF8.GetBytes would fail
        // with a missing-internal-call abort. MQTT command/status text here
        // is ASCII, so a plain 1-byte-per-char codec is sufficient.
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
