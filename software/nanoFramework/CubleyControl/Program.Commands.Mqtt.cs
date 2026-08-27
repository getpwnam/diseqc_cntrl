using System;
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
                    WriteStructuredDebug(
                        "MQTT",
                        "schema=1 sub=mqtt comp=session operation=run stat=error code=" + _mqttLastError);
                }
                catch (Exception ex)
                {
                    _mqttLastError = SanitizeToken(ex.Message);
                    _mqttRuntimeState = "error";
                    WriteStructuredDebug(
                        "MQTT",
                        "schema=1 sub=mqtt comp=session operation=run stat=error" +
                        " code=session_exception detail=" + _mqttLastError);
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
            _mqttEventTopic = topicRoot + "/event/lnb";
            _mqttStateTopic = topicRoot + "/state/lnb";
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
                    WriteStructuredDebug(
                        "MQTT",
                        "schema=1 sub=mqtt comp=connection operation=connect stat=error" +
                        " code=" + SanitizeToken(_mqttLastError));
                    return;
                }

                _mqttReconnectAttempts = 0;
                _mqttLastError = string.Empty;
                _mqttRuntimeState = "connected";
                WriteStructuredDebug(
                    "MQTT",
                    "schema=1 sub=mqtt comp=connection operation=connect stat=ok" +
                    " broker=" + SanitizeToken(configuration.Broker) +
                    " port=" + configuration.Port.ToString());

                PublishLine(availabilityTopic, "online", MqttQoSLevel.AtMostOnce, true);
                PublishMqttState();
                _mqttRuntimeState = "subscribing";
                ushort subscriptionMessageId = _mqttClient.Subscribe(
                    new string[] { _mqttCommandTopic },
                    new MqttQoSLevel[] { MqttQoSLevel.AtLeastOnce });
                WriteStructuredDebug(
                    "MQTT",
                    "schema=1 sub=mqtt comp=subscribe operation=request stat=pending" +
                    " topic=" + _mqttCommandTopic +
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
                        WriteStructuredDebug(
                            "MQTT",
                            "schema=1 sub=mqtt comp=session operation=close stat=error" +
                            " code=close_exception detail=" + SanitizeToken(ex.Message));
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
                WriteStructuredDebug(
                    "MQTT",
                    "schema=1 sub=mqtt comp=subscribe operation=ack stat=error" +
                    " code=subscribe_rejected message_id=" + e.MessageId.ToString());
                return;
            }

            _mqttLastError = string.Empty;
            _mqttRuntimeState = "connected";
            WriteStructuredDebug(
                "MQTT",
                "schema=1 sub=mqtt comp=subscribe operation=ack stat=ok" +
                " topic=" + _mqttCommandTopic +
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
                WriteStructuredDebug(
                    "MQTT",
                    "schema=1 sub=mqtt comp=connection operation=disconnect stat=error" +
                    " code=disconnect_exception detail=" + SanitizeToken(ex.Message));
            }
        }

        private static void OnMqttMessageReceived(object sender, MqttMsgPublishEventArgs e)
        {
            int payloadLength = e.Message == null ? 0 : e.Message.Length;
            WriteStructuredDebug(
                "COMMAND",
                "schema=1 sub=command comp=receive operation=decode stat=ok transport=mqtt" +
                " topic=" + e.Topic +
                " qos=" + ((int)e.QosLevel).ToString() +
                " retained=" + (e.Retain ? "1" : "0") +
                " length=" + payloadLength.ToString());

            if (e.Topic != _mqttCommandTopic)
            {
                WriteStructuredDebug(
                    "COMMAND",
                    "schema=1 sub=command comp=receive operation=reject stat=error" +
                    " transport=mqtt code=unexpected_topic topic=" + e.Topic);
                return;
            }

            if (e.Retain)
            {
                WriteStructuredDebug(
                    "COMMAND",
                    "schema=1 sub=command comp=receive operation=reject stat=error" +
                    " transport=mqtt code=retained_command");
                return;
            }

            if (e.Message == null || e.Message.Length == 0 || e.Message.Length > MqttCommandEnvelopeMaxLength)
            {
                WriteStructuredDebug(
                    "COMMAND",
                    "schema=1 sub=command comp=receive operation=reject stat=error" +
                    " transport=mqtt code=invalid_length length=" + payloadLength.ToString());
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
                WriteStructuredDebug(
                    "COMMAND",
                    "schema=1 sub=command comp=envelope operation=parse stat=error" +
                    " transport=mqtt code=invalid_envelope");
                PublishMqttResponse("id=none Fail: invalid command envelope", false);
                return;
            }

            int cachedIndex = FindCachedMqttCommand(commandId);
            if (cachedIndex >= 0)
            {
                if (_mqttCachedCommands[cachedIndex] != command)
                {
                    WriteStructuredDebug(
                        "COMMAND",
                        "schema=1 sub=command comp=deduplicate operation=reject stat=error" +
                        " transport=mqtt code=id_conflict id=" + commandId.ToString());
                    PublishMqttResponse("id=" + commandId.ToString() + " Fail: command id conflict", false);
                    return;
                }

                WriteStructuredDebug(
                    "COMMAND",
                    "schema=1 sub=command comp=deduplicate operation=replay stat=ok" +
                    " transport=mqtt id=" + commandId.ToString());
                ReplayCachedMqttResponses(cachedIndex);
                return;
            }

            _mqttActiveCommandId = commandId;
            _mqttActiveResponseCount = 0;
            WriteStructuredDebug(
                "COMMAND",
                "schema=1 sub=command comp=dispatch operation=start stat=ok" +
                " transport=mqtt id=" + commandId.ToString() +
                " command=" + SanitizeToken(RedactCommandForLog(command)));

            ExecuteCommand(command, MqttOutputSink, CommandTransport.Mqtt);
            CacheMqttCommandResponses(commandId, command);
            PublishMqttState();
            WriteStructuredDebug(
                "COMMAND",
                "schema=1 sub=command comp=dispatch operation=complete stat=ok" +
                " transport=mqtt id=" + commandId.ToString());
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
            WriteStructuredDebug(
                "COMMAND",
                "schema=1 sub=command comp=response operation=publish stat=ok" +
                " transport=mqtt topic=" + _mqttResponseTopic +
                " duplicate=" + (duplicate ? "1" : "0") +
                " payload=" + SanitizeToken(payload));
            PublishLine(_mqttResponseTopic, payload, MqttQoSLevel.AtLeastOnce, false);
        }

        private static void PublishMqttLnbFaultTransition(bool active, string source)
        {
            string payload =
                "schema=1 sub=lnb comp=fault" +
                " stat=" + (active ? "active" : "clear") +
                " event_id=" + NextMqttEventId().ToString() +
                " source=" + source;
            PublishMqttLnbEvent(payload);
            PublishMqttState();
        }

        private static void PublishMqttLnbHealthEvent(string status, int sequence, int result)
        {
            string payload =
                "schema=1 sub=lnb comp=health" +
                " operation=comms" +
                " stat=" + status +
                " event_id=" + NextMqttEventId().ToString() +
                " seq=" + sequence.ToString() +
                " rc=" + result.ToString();
            PublishMqttLnbEvent(payload);
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

        private static void PublishMqttLnbEvent(string payload)
        {
            WriteStructuredDebug("LNB", payload);
            if (_mqttClient == null || !_mqttClient.IsConnected || string.IsNullOrEmpty(_mqttEventTopic))
            {
                return;
            }

            PublishLine(_mqttEventTopic, payload, MqttQoSLevel.AtLeastOnce, false);
        }

        private static void PublishMqttState()
        {
            string payload;
            lock (_lnbIoLock)
            {
                payload =
                    "schema=1 sub=lnb comp=state" +
                    " stat=" + _lnbHealthState +
                    " comm=" + (!_lnbHealthHasResult ? "unknown" : (_lnbHealthCommsOk ? "ok" : "error")) +
                    " health_failures=" + _lnbHealthConsecutiveFailures.ToString() +
                    " health_rc=" + _lnbHealthResult.ToString() +
                    " s1=" + ToHexU8(_lnbHealthS1) +
                    " s2=" + ToHexU8(_lnbHealthS2) +
                    " d1=" + ToHexU8(_lnbHealthD1) +
                    " d2=" + ToHexU8(_lnbHealthD2) +
                    " d3=" + ToHexU8(_lnbHealthD3) +
                    " d4=" + ToHexU8(_lnbHealthD4) +
                    " fault=" + (_lnbFaultAsserted ? "1" : "0") +
                    " monitor=" + (_lnbFaultReady ? "ready" : "unavailable") +
                    " init=" + LnbStatusToToken(_lnbInitStatus);

                if (_lnbInitStatus == (int)Cubley.Interop.LNBH26.Status.Ok)
                {
                    payload +=
                        " a_pol=" + PolarizationToText(Cubley.Interop.LNBH26.NativeGetPolarizationForChannel(LnbChannelA)) +
                        " a_band=" + BandToText(Cubley.Interop.LNBH26.NativeGetBandForChannel(LnbChannelA)) +
                        " b_pol=" + PolarizationToText(Cubley.Interop.LNBH26.NativeGetPolarizationForChannel(1)) +
                        " b_band=" + BandToText(Cubley.Interop.LNBH26.NativeGetBandForChannel(1));
                }
            }

            WriteStructuredDebug("LNB", payload);
            if (_mqttClient == null || !_mqttClient.IsConnected || string.IsNullOrEmpty(_mqttStateTopic))
            {
                return;
            }

            PublishLine(_mqttStateTopic, payload, MqttQoSLevel.AtLeastOnce, true);
        }

        private static void OnMqttConnectionClosed(object sender, EventArgs e)
        {
            _mqttRuntimeState = "disconnected";
            WriteStructuredDebug(
                "MQTT",
                "schema=1 sub=mqtt comp=connection operation=close stat=error" +
                " code=connection_closed reconnect=1");
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
                WriteStructuredDebug(
                    "MQTT",
                    "schema=1 sub=mqtt comp=publish operation=send stat=error" +
                    " code=publish_exception topic=" + topic +
                    " detail=" + SanitizeToken(ex.Message));
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
