using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading;
using Cubley.Diseqc;
using nanoFramework.Hardware.Stm32;
using nanoFramework.M2Mqtt;
using nanoFramework.M2Mqtt.Exceptions;
using nanoFramework.M2Mqtt.Messages;

namespace CubleyControl
{
    public static partial class Program
    {
        private const int MqttSubscriptionFailure = 0x80;
        private const int MqttCommandIdMaxLength = 5;
        private const int MqttCommandEnvelopeMaxLength = MqttCommandMaxLength + MqttCommandIdMaxLength + 1;
        private const int MqttDuplicateCacheSize = 8;
        private const int MqttCachedResponseLimit = 32;
        private static MqttClient _mqttClient;
        private static string _mqttCommandTopic = string.Empty;
        private static string _mqttResponseTopic = string.Empty;
        private static string _mqttEventTopic = string.Empty;
        private static string _mqttStateTopic = string.Empty;
        private static string _mqttRuntimeState = "disabled";
        private static string _mqttLastError = string.Empty;
        private static int _mqttReconnectAttempts;
        private static readonly ushort[] _mqttCachedCommandIds = new ushort[MqttDuplicateCacheSize];
        private static readonly bool[] _mqttCachedCommandValid = new bool[MqttDuplicateCacheSize];
        private static readonly string[] _mqttCachedCommands = new string[MqttDuplicateCacheSize];
        private static readonly int[] _mqttCachedResponseCounts = new int[MqttDuplicateCacheSize];
        private static readonly string[] _mqttCachedResponses = new string[MqttDuplicateCacheSize * MqttCachedResponseLimit];
        private static readonly string[] _mqttActiveResponses = new string[MqttCachedResponseLimit];
        private static readonly object _mqttCommandTransactionLock = new object();
        private static readonly object _mqttEventLock = new object();
        private static int _mqttDuplicateCacheNext;
        private static int _mqttEventSequence;
        private static ushort _mqttActiveCommandId;
        private static int _mqttActiveResponseCount;

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
            string topicRoot = BuildMqttTopicRoot(configuration);
            string availabilityTopic = topicRoot + "/availability";
            _mqttCommandTopic = topicRoot + "/command";
            _mqttResponseTopic = topicRoot + "/response";
            _mqttEventTopic = topicRoot + "/event";
            _mqttStateTopic = topicRoot + "/state";
            _mqttReconnectAttempts++;
            _mqttRuntimeState = "connecting";

            _mqttClient = new MqttClient(configuration.Broker, configuration.Port, false, null, null, MqttSslProtocols.None);
            _mqttClient.ProtocolVersion = MqttProtocolVersion.Version_3_1_1;
            _mqttClient.MqttMsgPublishReceived += OnMqttMessageReceived;
            _mqttClient.MqttMsgSubscribed += OnMqttSubscribed;
            _mqttClient.ConnectionClosed += OnMqttConnectionClosed;

            try
            {
                MqttReasonCode result = _mqttClient.Connect(
                    ResolveMqttClientId(configuration),
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

                PublishLine(availabilityTopic, "online", MqttQoSLevel.AtMostOnce, true);
                PublishMqttState();
                _mqttRuntimeState = "subscribing";
                ushort subscriptionMessageId = _mqttClient.Subscribe(
                    new string[] { _mqttCommandTopic },
                    new MqttQoSLevel[] { MqttQoSLevel.AtLeastOnce });
                Debug.WriteLine(
                    "[MQTT-CMD] subscribe requested topic=" + _mqttCommandTopic +
                    " qos=1 message_id=" + subscriptionMessageId.ToString());

                while (_mqttClient.IsConnected &&
                    revision == _mqttConfigurationRevision &&
                    _mqttRuntimeState != "error")
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
                _mqttResponseTopic = string.Empty;
                _mqttEventTopic = string.Empty;
                _mqttStateTopic = string.Empty;
                if (client != null)
                {
                    client.MqttMsgPublishReceived -= OnMqttMessageReceived;
                    client.MqttMsgSubscribed -= OnMqttSubscribed;
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

        private static void OnMqttSubscribed(object sender, MqttMsgSubscribedEventArgs e)
        {
            MqttQoSLevel[] grantedQosLevels = e.GrantedQoSLevels;
            if (grantedQosLevels == null ||
                grantedQosLevels.Length == 0 ||
                (int)grantedQosLevels[0] == MqttSubscriptionFailure)
            {
                _mqttLastError = "subscribe_rejected";
                _mqttRuntimeState = "error";
                Debug.WriteLine("[MQTT-CMD] subscribe rejected message_id=" + e.MessageId.ToString());
                return;
            }

            _mqttLastError = string.Empty;
            _mqttRuntimeState = "connected";
            Debug.WriteLine(
                "[MQTT-CMD] subscribed topic=" + _mqttCommandTopic +
                " qos=" + ((int)grantedQosLevels[0]).ToString() +
                " message_id=" + e.MessageId.ToString());
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
            int payloadLength = e.Message == null ? 0 : e.Message.Length;
            Debug.WriteLine(
                "[MQTT-CMD] received topic=" + e.Topic +
                " qos=" + ((int)e.QosLevel).ToString() +
                " retained=" + e.Retain.ToString() +
                " length=" + payloadLength.ToString());

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

            if (e.Message == null || e.Message.Length == 0 || e.Message.Length > MqttCommandEnvelopeMaxLength)
            {
                Debug.WriteLine("[MQTT-CMD] rejected invalid length");
                return;
            }

            string payload = AsciiBytesToString(e.Message);
            lock (_mqttCommandTransactionLock)
            {
                ProcessMqttCommand(payload);
            }
        }

        private static void ProcessMqttCommand(string payload)
        {
            ushort commandId;
            string command;
            if (!TryParseMqttCommandEnvelope(payload, out commandId, out command))
            {
                Debug.WriteLine("[MQTT-CMD] rejected invalid envelope");
                PublishMqttResponse("id=none Fail: invalid command envelope", false);
                return;
            }

            int cachedIndex = FindCachedMqttCommand(commandId);
            if (cachedIndex >= 0)
            {
                if (_mqttCachedCommands[cachedIndex] != command)
                {
                    Debug.WriteLine("[MQTT-CMD] rejected id conflict id=" + commandId.ToString());
                    PublishMqttResponse("id=" + commandId.ToString() + " Fail: command id conflict", false);
                    return;
                }

                Debug.WriteLine("[MQTT-CMD] duplicate id=" + commandId.ToString() + " replaying response");
                ReplayCachedMqttResponses(cachedIndex);
                return;
            }

            _mqttActiveCommandId = commandId;
            _mqttActiveResponseCount = 0;
            Debug.WriteLine(
                "[MQTT-CMD] dispatch id=" + commandId.ToString() +
                " payload=" + RedactCommandForLog(command));

            ExecuteCommand(command, MqttOutputSink, CommandTransport.Mqtt);
            CacheMqttCommandResponses(commandId, command);
            PublishMqttState();
            Debug.WriteLine("[MQTT-CMD] dispatch complete id=" + commandId.ToString());
        }

        private static void MqttOutputSink(string line)
        {
            string payload = "id=" + _mqttActiveCommandId.ToString() + " " + line.TrimEnd('\r', '\n');
            if (_mqttActiveResponseCount < MqttCachedResponseLimit)
            {
                _mqttActiveResponses[_mqttActiveResponseCount++] = payload;
            }

            PublishMqttResponse(payload, false);
        }

        private static bool TryParseMqttCommandEnvelope(string payload, out ushort commandId, out string command)
        {
            commandId = 0;
            command = string.Empty;

            int separator = payload.IndexOf(' ');
            if (separator < 1 || separator > MqttCommandIdMaxLength || separator == payload.Length - 1)
            {
                return false;
            }

            int parsedId;
            if (!int.TryParse(payload.Substring(0, separator), out parsedId) || parsedId < 0 || parsedId > ushort.MaxValue)
            {
                return false;
            }

            command = payload.Substring(separator + 1).Trim();
            if (command.Length == 0 || command.Length > MqttCommandMaxLength)
            {
                return false;
            }

            commandId = (ushort)parsedId;
            return true;
        }

        private static int FindCachedMqttCommand(ushort commandId)
        {
            for (int index = 0; index < MqttDuplicateCacheSize; index++)
            {
                if (_mqttCachedCommandValid[index] && _mqttCachedCommandIds[index] == commandId)
                {
                    return index;
                }
            }

            return -1;
        }

        private static void CacheMqttCommandResponses(ushort commandId, string command)
        {
            int cacheIndex = _mqttDuplicateCacheNext;
            int responseOffset = cacheIndex * MqttCachedResponseLimit;
            int responseCount = _mqttActiveResponseCount;

            for (int index = 0; index < MqttCachedResponseLimit; index++)
            {
                _mqttCachedResponses[responseOffset + index] = index < responseCount ? _mqttActiveResponses[index] : null;
                _mqttActiveResponses[index] = null;
            }

            _mqttCachedCommandIds[cacheIndex] = commandId;
            _mqttCachedCommands[cacheIndex] = command;
            _mqttCachedResponseCounts[cacheIndex] = responseCount;
            _mqttCachedCommandValid[cacheIndex] = true;
            _mqttDuplicateCacheNext = (cacheIndex + 1) % MqttDuplicateCacheSize;
        }

        private static void ReplayCachedMqttResponses(int cacheIndex)
        {
            int responseOffset = cacheIndex * MqttCachedResponseLimit;
            int responseCount = _mqttCachedResponseCounts[cacheIndex];
            for (int index = 0; index < responseCount; index++)
            {
                PublishMqttResponse(_mqttCachedResponses[responseOffset + index], true);
            }
        }

        private static void PublishMqttResponse(string payload, bool duplicate)
        {
            Debug.WriteLine(
                "[MQTT-CMD] response topic=" + _mqttResponseTopic +
                " duplicate=" + duplicate.ToString() +
                " payload=" + payload);
            PublishLine(_mqttResponseTopic, payload, MqttQoSLevel.AtLeastOnce, false);
        }

        private static void PublishMqttLnbFaultTransition(bool active, string source, int sequence)
        {
            string payload =
                "event_id=" + NextMqttEventId().ToString() +
                " type=lnb_fault" +
                " active=" + (active ? "1" : "0") +
                " fault_sequence=" + sequence.ToString() +
                " source=" + source;
            PublishMqttEvent(payload);
            PublishMqttState();
        }

        private static void PublishMqttLnbHealthEvent(string status, int sequence, int result)
        {
            string payload =
                "event_id=" + NextMqttEventId().ToString() +
                " type=lnb_comms" +
                " status=" + status +
                " health_sequence=" + sequence.ToString() +
                " rc=" + result.ToString();
            PublishMqttEvent(payload);
        }

        private static int NextMqttEventId()
        {
            lock (_mqttEventLock)
            {
                _mqttEventSequence++;
                if (_mqttEventSequence < 1)
                {
                    _mqttEventSequence = 1;
                }

                return _mqttEventSequence;
            }
        }

        private static void PublishMqttEvent(string payload)
        {
            if (_mqttClient == null || !_mqttClient.IsConnected || string.IsNullOrEmpty(_mqttEventTopic))
            {
                return;
            }

            Debug.WriteLine("[MQTT-EVENT] topic=" + _mqttEventTopic + " payload=" + payload);
            PublishLine(_mqttEventTopic, payload, MqttQoSLevel.AtLeastOnce, false);
        }

        private static void PublishMqttState()
        {
            if (_mqttClient == null || !_mqttClient.IsConnected || string.IsNullOrEmpty(_mqttStateTopic))
            {
                return;
            }

            string payload;
            lock (_lnbIoLock)
            {
                payload =
                    "lnb_health=" + _lnbHealthState +
                    " lnb_comms=" + (!_lnbHealthHasResult ? "unknown" : (_lnbHealthCommsOk ? "ok" : "error")) +
                    " health_sequence=" + _lnbHealthCheckSequence.ToString() +
                    " health_failures=" + _lnbHealthConsecutiveFailures.ToString() +
                    " health_rc=" + _lnbHealthResult.ToString() +
                    " s1=" + ToHexU8(_lnbHealthS1) +
                    " s2=" + ToHexU8(_lnbHealthS2) +
                    " d1=" + ToHexU8(_lnbHealthD1) +
                    " d2=" + ToHexU8(_lnbHealthD2) +
                    " d3=" + ToHexU8(_lnbHealthD3) +
                    " d4=" + ToHexU8(_lnbHealthD4) +
                    " lnb_fault=" + (_lnbFaultAsserted ? "1" : "0") +
                    " lnb_monitor=" + (_lnbFaultReady ? "ready" : "unavailable") +
                    " fault_sequence=" + _lnbFaultSequence.ToString() +
                    " lnb_init=" + LnbStatusToToken(_lnbInitStatus) +
                    " diseqc_preset=" + DiseqcV1Presets.ToText(_diseqcRoutePreset) +
                    " diseqc_tone=" + (_diseqcCarrier == null ? "off" : "on");

                if (_lnbInitStatus == (int)Cubley.Interop.LNBH26.Status.Ok)
                {
                    payload +=
                        " lnb_a_pol=" + PolarizationToText(Cubley.Interop.LNBH26.NativeGetPolarizationForChannel(LnbChannelA)) +
                        " lnb_a_band=" + BandToText(Cubley.Interop.LNBH26.NativeGetBandForChannel(LnbChannelA)) +
                        " lnb_b_pol=" + PolarizationToText(Cubley.Interop.LNBH26.NativeGetPolarizationForChannel(1)) +
                        " lnb_b_band=" + BandToText(Cubley.Interop.LNBH26.NativeGetBandForChannel(1));
                }
            }

            Debug.WriteLine("[MQTT-STATE] topic=" + _mqttStateTopic + " payload=" + payload);
            PublishLine(_mqttStateTopic, payload, MqttQoSLevel.AtLeastOnce, true);
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

        private static string ResolveMqttClientId(MqttConfiguration configuration)
        {
            if (!string.IsNullOrEmpty(configuration.ClientId))
            {
                return configuration.ClientId;
            }

            return ResolveHostname(configuration.Hostname);
        }

        private static string BuildMqttTopicRoot(MqttConfiguration configuration)
        {
            return configuration.TopicPrefix + "/" + ResolveHostname(configuration.Hostname);
        }

        private static string ResolveHostname(string configuredHostname)
        {
            if (!string.IsNullOrEmpty(configuredHostname))
            {
                return configuredHostname;
            }

            byte[] uniqueDeviceId = Utilities.UniqueDeviceId;
            if (uniqueDeviceId == null || uniqueDeviceId.Length == 0)
            {
                return "cubley";
            }

            uint hash = 2166136261;
            for (int index = 0; index < uniqueDeviceId.Length; index++)
            {
                hash ^= uniqueDeviceId[index];
                hash = unchecked(hash * 16777619);
            }

            return "cubley-" + ToLowerHex((byte)(hash >> 16)) +
                ToLowerHex((byte)(hash >> 8)) + ToLowerHex((byte)hash);
        }

        private static string ToLowerHex(byte value)
        {
            const string digits = "0123456789abcdef";
            return new string(new char[] { digits[(value >> 4) & 0x0F], digits[value & 0x0F] });
        }

        private static void PublishLine(string topic, string payload, MqttQoSLevel qosLevel, bool retain)
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
                    qosLevel,
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
