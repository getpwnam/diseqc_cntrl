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
        // Wire contract version; see docs/software/DEVICE_API_V2.md.
        private const int DeviceContractVersion = 2;
        private const int MqttCommandIdMaxLength = 32;
        private const int MqttOpMaxLength = 40;
        private const int MqttCommandEnvelopeMaxLength = 256;
        private const int MqttDuplicateCacheSize = 8;
        // A time bound, not just a slot count: under a slot-only scheme
        // whether a reused id replayed or conflicted depended on how many
        // unrelated commands had happened since, which a client cannot see.
        private const int MqttDedupTtlMs = 120_000;
        private const int MqttBridgedLineLimit = 16;
        private const int MqttPublishQueueCapacity = 64;
        private const string MqttAvailabilityOnline = "online";
        private const string MqttAvailabilityOffline = "offline";
        private static MqttClient _mqttClient;
        private static string _mqttEventTopic = string.Empty;
        private static string _mqttStateTopic = string.Empty;
        private static string _mqttDiseqcEventTopic = string.Empty;
        private static string _mqttDiseqcStateTopic = string.Empty;
        private static string _mqttRuntimeState = "disabled";
        private static string _mqttLastError = string.Empty;
        private static int _mqttReconnectAttempts;
        private static readonly string[] _mqttCachedCommandIds = new string[MqttDuplicateCacheSize];
        private static readonly string[] _mqttCachedPayloads = new string[MqttDuplicateCacheSize];
        private static readonly string[] _mqttCachedResponseBodies = new string[MqttDuplicateCacheSize];
        private static readonly long[] _mqttCachedAtMs = new long[MqttDuplicateCacheSize];
        private static readonly object _mqttCommandTransactionLock = new object();
        private static readonly object _mqttEventLock = new object();
        private static readonly object _mqttPublishQueueLock = new object();
        private static readonly string[] _mqttPublishTopics = new string[MqttPublishQueueCapacity];
        private static readonly string[] _mqttPublishPayloads = new string[MqttPublishQueueCapacity];
        private static readonly MqttQoSLevel[] _mqttPublishQosLevels = new MqttQoSLevel[MqttPublishQueueCapacity];
        private static readonly bool[] _mqttPublishRetainFlags = new bool[MqttPublishQueueCapacity];
        private static int _mqttDuplicateCacheNext;
        private static int _mqttEventSequence;
        // Per-command scratch, written only under _commandLock.
        private static string _mqttActiveCommandKey = "?";
        private static string _mqttBridgedOutput = string.Empty;
        private static int _mqttBridgedLineCount;
        private static bool _mqttResultOk;
        private static bool _mqttResultRecorded;
        private static string _mqttResultCode = string.Empty;
        private static string _mqttResultMsg = string.Empty;
        private static int _mqttPublishQueueHead;
        private static int _mqttPublishQueueCount;
        private static int _mqttPublishDropCount;
        private static bool _mqttPublishAccepting;

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
            _mqttEventTopic = topicRoot + "/event/lnb";
            _mqttStateTopic = topicRoot + "/state/lnb";
            _mqttDiseqcEventTopic = topicRoot + "/event/diseqc";
            _mqttDiseqcStateTopic = topicRoot + "/state/diseqc";
            _mqttReconnectAttempts++;
            _mqttRuntimeState = "connecting";

            _mqttClient = new MqttClient(configuration.Broker, configuration.Port, false, null, null, MqttSslProtocols.None);
            _mqttClient.ProtocolVersion = MqttProtocolVersion.Version_3_1_1;
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
                    MqttAvailabilityOffline,
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
                SetMqttPublishAccepting(true);
                WriteStructuredDebug(
                    "MQTT",
                    "schema=1 sub=mqtt comp=connection operation=connect stat=ok" +
                    " broker=" + SanitizeToken(configuration.Broker) +
                    " port=" + configuration.Port.ToString());

                QueueMqttPublication(availabilityTopic, MqttAvailabilityOnline, MqttQoSLevel.AtLeastOnce, true);
                PublishMqttState();
                PublishMqttDiseqcState();
                if (!DrainMqttPublicationQueue(_mqttClient))
                {
                    return;
                }

                while (_mqttClient.IsConnected &&
                    revision == _mqttConfigurationRevision &&
                    _mqttRuntimeState != "error")
                {
                    if (!DrainMqttPublicationQueue(_mqttClient))
                    {
                        break;
                    }

                    Thread.Sleep(100);
                }

                if (_mqttClient.IsConnected)
                {
                    TryDisconnectMqttClient(_mqttClient);
                }
            }
            finally
            {
                SetMqttPublishAccepting(false);
                MqttClient client = _mqttClient;
                _mqttClient = null;
                _mqttEventTopic = string.Empty;
                _mqttStateTopic = string.Empty;
                _mqttDiseqcEventTopic = string.Empty;
                _mqttDiseqcStateTopic = string.Empty;
                if (client != null)
                {
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

        private static string ProcessApiCommand(string payload)
        {
            JsonObject command;
            string parseError;
            if (!Json.TryParseObject(payload, out command, out parseError))
            {
                WriteStructuredDebug(
                    "COMMAND",
                    "schema=1 sub=command comp=envelope operation=parse stat=error" +
                    " transport=rest code=invalid_envelope detail=" + SanitizeToken(parseError));
                return "{" + BuildMqttFailureBody("?", "validation_error", parseError, 0) + "}";
            }

            int version = DeviceContractVersion;
            if (command.Has("v") &&
                (!command.TryGetInt("v", out version) || version != DeviceContractVersion))
            {
                return "{" + BuildMqttFailureBody(
                    "?",
                    "validation_error",
                    "unsupported contract version; this device speaks v" + DeviceContractVersion.ToString(),
                    0) + "}";
            }

            string commandId;
            if (!command.TryGetString("id", out commandId) || !IsValidMqttCommandId(commandId))
            {
                return "{" + BuildMqttFailureBody(
                    "?",
                    "validation_error",
                    "id must be 1 to " + MqttCommandIdMaxLength.ToString() + " characters of A-Za-z0-9._:-",
                    0) + "}";
            }

            string op;
            if (!command.TryGetString("op", out op) || !IsValidMqttOpName(op))
            {
                return "{" + BuildMqttFailureBody(
                    commandId,
                    "validation_error",
                    "op must be 1 to " + MqttOpMaxLength.ToString() + " characters of a-z0-9._",
                    0) + "}";
            }

            int cachedIndex = FindCachedMqttCommand(commandId);
            if (cachedIndex >= 0)
            {
                if (_mqttCachedPayloads[cachedIndex] != payload)
                {
                    WriteStructuredDebug(
                        "COMMAND",
                        "schema=1 sub=command comp=deduplicate operation=reject stat=error" +
                        " transport=rest code=id_conflict id=" + SanitizeToken(commandId));
                    return "{" + BuildMqttFailureBody(
                        commandId,
                        "id_conflict",
                        "id reused within the deduplication window with a different payload",
                        0) + "}";
                }

                WriteStructuredDebug(
                    "COMMAND",
                    "schema=1 sub=command comp=deduplicate operation=replay stat=ok" +
                    " transport=rest id=" + SanitizeToken(commandId));
                return "{" + _mqttCachedResponseBodies[cachedIndex] + ",\"replayed\":true}";
            }

            _mqttActiveCommandKey = commandId;
            WriteStructuredDebug(
                "COMMAND",
                "schema=1 sub=command comp=dispatch operation=start stat=ok" +
                " transport=rest id=" + SanitizeToken(commandId) +
                " op=" + SanitizeToken(op));

            string responseBody = ExecuteMqttOperation(commandId, op, command);
            CacheMqttCommandResponse(commandId, payload, responseBody);

            WriteStructuredDebug(
                "COMMAND",
                "schema=1 sub=command comp=dispatch operation=complete stat=ok" +
                " transport=rest id=" + SanitizeToken(commandId));
            return "{" + responseBody + "}";
        }

        private static string ExecuteMqttOperation(string commandId, string op, JsonObject command)
        {
            if (op == "positioner.goto_angle")
            {
                return ExecuteMqttPositionerGotoAngle(commandId, command);
            }

            if (op == "positioner.goto" || op == "positioner.step" ||
                op == "positioner.drive" || op == "positioner.halt")
            {
                return ExecuteMqttPositionerMotion(commandId, op, command);
            }

            if (op == "positioner.release")
            {
                return ExecuteMqttPositionerQuery(commandId, op, command);
            }

            return ExecuteMqttBridgedOperation(commandId, op, command);
        }

        /// <summary>
        /// Motion operations are dispatched with typed parameters straight to
        /// the shared hardware path -- they never build a console command
        /// string. This is the parse/execute split described in
        /// docs/software/DEVICE_API_V2.md; the bridged operations below are
        /// the migration remainder.
        /// </summary>
        private static string ExecuteMqttPositionerMotion(string commandId, string op, JsonObject command)
        {
            int operation;
            int value = 0;
            string error;

            if (op == "positioner.goto")
            {
                if (!TryValidateMqttMembers(command, "position", null, out error))
                {
                    return BuildMqttFailureBody(commandId, "validation_error", error, 0);
                }

                int position;
                if (!command.TryGetInt("position", out position) ||
                    position < 0 || position > DiseqcMotorStoredPositionMax)
                {
                    return BuildMqttFailureBody(commandId, "validation_error", "position must be an integer 0 to 60", 0);
                }

                operation = PositionerOpGoto;
                value = position;
            }
            else if (op == "positioner.step")
            {
                if (!TryValidateMqttMembers(command, "direction", "count", out error))
                {
                    return BuildMqttFailureBody(commandId, "validation_error", error, 0);
                }

                string direction;
                if (!command.TryGetString("direction", out direction) ||
                    (direction != "east" && direction != "west"))
                {
                    return BuildMqttFailureBody(commandId, "validation_error", "direction must be \"east\" or \"west\"", 0);
                }

                int count;
                if (!command.TryGetInt("count", out count) || count < 1 || count > 128)
                {
                    return BuildMqttFailureBody(commandId, "validation_error", "count must be an integer 1 to 128", 0);
                }

                operation = direction == "east" ? PositionerOpStepEast : PositionerOpStepWest;
                value = count;
            }
            else if (op == "positioner.drive")
            {
                if (!TryValidateMqttMembers(command, "direction", null, out error))
                {
                    return BuildMqttFailureBody(commandId, "validation_error", error, 0);
                }

                string direction;
                if (!command.TryGetString("direction", out direction) ||
                    (direction != "east" && direction != "west"))
                {
                    return BuildMqttFailureBody(commandId, "validation_error", "direction must be \"east\" or \"west\"", 0);
                }

                operation = direction == "east" ? PositionerOpDriveEast : PositionerOpDriveWest;
            }
            else
            {
                if (!TryValidateMqttMembers(command, null, null, out error))
                {
                    return BuildMqttFailureBody(commandId, "validation_error", error, 0);
                }

                operation = PositionerOpHalt;
            }

            ResetMqttCommandOutcome();
            ExecutePositionerOperation(operation, value, MqttOutputSink);

            if (!_mqttResultOk)
            {
                return BuildMqttFailureBody(
                    commandId,
                    _mqttResultCode.Length == 0 ? "hw_fault" : _mqttResultCode,
                    _mqttResultMsg,
                    _blockingDiseqcJobId);
            }

            int jobId = _lastStartedDiseqcJobId;
            bool started = jobId != 0 && GetActiveDiseqcJobId() == jobId;
            return BuildMqttResponseBody(
                commandId,
                true,
                started ? "accepted" : "ok",
                null,
                started ? null : BuildDiseqcStateJson(),
                jobId,
                null);
        }

        private static string ExecuteMqttPositionerGotoAngle(string commandId, JsonObject command)
        {
            string error;
            if (!TryValidateMqttMembers(command, "direction", "angle", out error))
            {
                return BuildMqttFailureBody(commandId, "validation_error", error, 0);
            }

            string direction;
            if (!command.TryGetString("direction", out direction) ||
                (direction != "east" && direction != "west"))
            {
                return BuildMqttFailureBody(commandId, "validation_error", "direction must be \"east\" or \"west\"", 0);
            }

            string angle;
            if (!command.TryGetString("angle", out angle))
            {
                return BuildMqttFailureBody(commandId, "validation_error", "angle must be a decimal string", 0);
            }

            ResetMqttCommandOutcome();
            ExecutePositionerGotoAngle(
                direction == "east" ? DiseqcMotorDirection.East : DiseqcMotorDirection.West,
                angle,
                MqttOutputSink);

            if (!_mqttResultOk)
            {
                return BuildMqttFailureBody(
                    commandId,
                    _mqttResultCode.Length == 0 ? "hw_fault" : _mqttResultCode,
                    _mqttResultMsg,
                    _blockingDiseqcJobId);
            }

            int jobId = _lastStartedDiseqcJobId;
            bool started = jobId != 0 && GetActiveDiseqcJobId() == jobId;
            return BuildMqttResponseBody(
                commandId,
                true,
                started ? "accepted" : "ok",
                null,
                started ? null : BuildDiseqcStateJson(),
                jobId,
                null);
        }

        private static string ExecuteMqttPositionerQuery(string commandId, string op, JsonObject command)
        {
            string error;

            if (!TryValidateMqttMembers(command, "job", null, out error))
            {
                return BuildMqttFailureBody(commandId, "validation_error", error, 0);
            }

            int jobId;
            if (!command.TryGetInt("job", out jobId) || jobId <= 0)
            {
                return BuildMqttFailureBody(commandId, "validation_error", "job must be a positive integer", 0);
            }

            // positioner.release: the identity check inside TryEndDiseqcJob is
            // what stops a late release from ending a newer movement.
            if (!TryEndDiseqcJob(jobId, JobStateReleased, string.Empty))
            {
                return BuildMqttFailureBody(commandId, "validation_error", "job is not the running job", GetActiveDiseqcJobId());
            }

            lock (_diseqcMotionLock)
            {
                _diseqcPositionEstimate.CompletePending();
            }

            PublishMqttDiseqcJobTransition("end", jobId);
            return BuildMqttResponseBody(commandId, true, "ok", null, BuildDiseqcJobJson(jobId), jobId, null);
        }

        /// <summary>
        /// Migration path: these operations are still executed by the console
        /// tokenizer and return console text in "lines". Every parameter is
        /// charset-checked before it reaches the command string, so a value
        /// can never introduce an extra token.
        /// </summary>
        private static string ExecuteMqttBridgedOperation(string commandId, string op, JsonObject command)
        {
            string consoleCommand;
            string code;
            string error;
            if (!TryBuildBridgedConsoleCommand(op, command, out consoleCommand, out code, out error))
            {
                return BuildMqttFailureBody(commandId, code, error, 0);
            }

            ResetMqttCommandOutcome();
            ExecuteCommand(consoleCommand, MqttOutputSink, CommandTransport.Rest);

            if (!_mqttResultRecorded)
            {
                return BuildMqttResponseBody(commandId, true, "ok", null, null, 0, _mqttBridgedOutput);
            }

            return BuildMqttResponseBody(
                commandId,
                _mqttResultOk,
                _mqttResultOk ? "ok" : (_mqttResultCode.Length == 0 ? "hw_fault" : _mqttResultCode),
                _mqttResultOk ? null : _mqttResultMsg,
                null,
                0,
                _mqttBridgedOutput);
        }

        private static bool TryBuildBridgedConsoleCommand(
            string op,
            JsonObject command,
            out string consoleCommand,
            out string code,
            out string error)
        {
            consoleCommand = string.Empty;
            code = "validation_error";
            error = string.Empty;

            if (op == "lnb.enable" || op == "lnb.disable")
            {
                if (!TryValidateMqttMembers(command, "channel", null, out error))
                {
                    return false;
                }

                string channel;
                if (!TryReadLnbChannel(command, out channel, out error))
                {
                    return false;
                }

                consoleCommand = "lnb " + channel + (op == "lnb.enable" ? " enable" : " disable");
                return true;
            }

            if (op == "lnb.polarization" || op == "lnb.band")
            {
                if (!TryValidateMqttMembers(command, "channel", "value", out error))
                {
                    return false;
                }

                string channel;
                if (!TryReadLnbChannel(command, out channel, out error))
                {
                    return false;
                }

                string value;
                if (!command.TryGetString("value", out value) || !IsSafeConsoleToken(value))
                {
                    error = "value must be 1 to 16 characters of a-z0-9";
                    return false;
                }

                consoleCommand = "lnb " + channel +
                    (op == "lnb.polarization" ? " polarization " : " band ") + value;
                return true;
            }

            if (op == "diseqc.preset" || op == "diseqc.tone")
            {
                if (!TryValidateMqttMembers(command, "value", null, out error))
                {
                    return false;
                }

                string value;
                if (!command.TryGetString("value", out value) || !IsSafeConsoleToken(value))
                {
                    error = "value must be 1 to 16 characters of a-z0-9";
                    return false;
                }

                consoleCommand = (op == "diseqc.preset" ? "diseqc preset " : "diseqc tone ") + value;
                return true;
            }

            if (op == "diseqc.tx")
            {
                if (!TryValidateMqttMembers(command, "frame", null, out error))
                {
                    return false;
                }

                string frame;
                if (!command.TryGetString("frame", out frame) ||
                    frame.Length < 2 || frame.Length > 12 || (frame.Length % 2) != 0)
                {
                    error = "frame must be a hex string of 1 to 6 bytes";
                    return false;
                }

                string spaced = string.Empty;
                for (int index = 0; index < frame.Length; index += 2)
                {
                    int ignored;
                    string pair = frame.Substring(index, 2);
                    if (!TryParseByteHex(pair, out ignored))
                    {
                        error = "frame must be a hex string of 1 to 6 bytes";
                        return false;
                    }

                    spaced += (index == 0 ? string.Empty : " ") + pair;
                }

                consoleCommand = "diseqc tx " + spaced;
                return true;
            }

            code = "unsupported";
            error = "unknown operation";
            return false;
        }

        private static bool TryReadLnbChannel(JsonObject command, out string channel, out string error)
        {
            error = string.Empty;
            if (!command.TryGetString("channel", out channel) ||
                (channel != "a" && channel != "b"))
            {
                channel = string.Empty;
                error = "channel must be \"a\" or \"b\"";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Unknown members are rejected rather than ignored, so a typo fails
        /// loudly instead of silently doing something else to a motor.
        /// </summary>
        private static bool TryValidateMqttMembers(JsonObject command, string first, string second, out string error)
        {
            for (int index = 0; index < command.Count; index++)
            {
                string key = command.KeyAt(index);
                if (key == "v" || key == "id" || key == "op")
                {
                    continue;
                }

                if (first != null && key == first)
                {
                    continue;
                }

                if (second != null && key == second)
                {
                    continue;
                }

                error = "unknown member " + key;
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsValidMqttCommandId(string value)
        {
            if (value == null || value.Length == 0 || value.Length > MqttCommandIdMaxLength)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                bool allowed =
                    (current >= 'a' && current <= 'z') ||
                    (current >= 'A' && current <= 'Z') ||
                    (current >= '0' && current <= '9') ||
                    current == '.' || current == '_' || current == ':' || current == '-';
                if (!allowed)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsValidMqttOpName(string value)
        {
            if (value == null || value.Length == 0 || value.Length > MqttOpMaxLength)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                bool allowed =
                    (current >= 'a' && current <= 'z') ||
                    (current >= '0' && current <= '9') ||
                    current == '.' || current == '_';
                if (!allowed)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsSafeConsoleToken(string value)
        {
            if (value == null || value.Length == 0 || value.Length > 16)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char current = value[index];
                if (!((current >= 'a' && current <= 'z') || (current >= '0' && current <= '9')))
                {
                    return false;
                }
            }

            return true;
        }

        private static void ResetMqttCommandOutcome()
        {
            _mqttResultOk = false;
            _mqttResultRecorded = false;
            _mqttResultCode = string.Empty;
            _mqttResultMsg = string.Empty;
            _mqttBridgedOutput = string.Empty;
            _mqttBridgedLineCount = 0;
            ResetDiseqcJobCommandScratch();
        }

        /// <summary>
        /// Captures a command outcome instead of writing it to the transport,
        /// so the JSON layer can emit exactly one structured response per
        /// command rather than one message per console line.
        /// </summary>
        private static void RecordMqttCommandOutcome(bool ok, string code, string msg)
        {
            _mqttResultOk = ok;
            _mqttResultCode = code == null ? string.Empty : code;
            _mqttResultMsg = msg == null ? string.Empty : msg;
            _mqttResultRecorded = true;
        }

        private static void MqttOutputSink(string line)
        {
            if (line == null || _mqttBridgedLineCount >= MqttBridgedLineLimit)
            {
                return;
            }

            string trimmed = line.TrimEnd('\r', '\n');
            if (trimmed.Length == 0)
            {
                return;
            }

            _mqttBridgedOutput = _mqttBridgedLineCount == 0
                ? trimmed
                : _mqttBridgedOutput + "\n" + trimmed;
            _mqttBridgedLineCount++;
        }

        private static string BuildMqttFailureBody(string commandId, string code, string msg, int jobId)
        {
            return BuildMqttResponseBody(commandId, false, code, msg, null, jobId, null);
        }

        private static string BuildMqttResponseBody(
            string commandId,
            bool ok,
            string code,
            string msg,
            string dataRaw,
            int jobId,
            string lines)
        {
            JsonBuilder builder = new JsonBuilder()
                .AddInt("v", DeviceContractVersion)
                .AddString("id", commandId)
                .AddBool("ok", ok)
                .AddString("code", code)
                .AddLong("ts_ms", Environment.TickCount64);

            if (msg != null && msg.Length > 0)
            {
                builder.AddString("msg", msg);
            }

            if (jobId != 0)
            {
                builder.AddInt("job", jobId);
            }

            if (dataRaw != null)
            {
                builder.AddRaw("data", dataRaw);
            }

            if (lines != null && lines.Length > 0)
            {
                builder.AddString("lines", lines);
            }

            return builder.BuildBody();
        }

        private static int FindCachedMqttCommand(string commandId)
        {
            long nowMs = Environment.TickCount64;
            for (int index = 0; index < MqttDuplicateCacheSize; index++)
            {
                if (_mqttCachedCommandIds[index] == null || _mqttCachedCommandIds[index] != commandId)
                {
                    continue;
                }

                if (nowMs - _mqttCachedAtMs[index] > MqttDedupTtlMs)
                {
                    _mqttCachedCommandIds[index] = null;
                    _mqttCachedPayloads[index] = null;
                    _mqttCachedResponseBodies[index] = null;
                    return -1;
                }

                return index;
            }

            return -1;
        }

        private static void CacheMqttCommandResponse(string commandId, string payload, string responseBody)
        {
            int slot = _mqttDuplicateCacheNext;
            _mqttCachedCommandIds[slot] = commandId;
            _mqttCachedPayloads[slot] = payload;
            _mqttCachedResponseBodies[slot] = responseBody;
            _mqttCachedAtMs[slot] = Environment.TickCount64;
            _mqttDuplicateCacheNext = (slot + 1) % MqttDuplicateCacheSize;
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
            QueueMqttPublication(_mqttEventTopic, payload, MqttQoSLevel.AtLeastOnce, false);
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
            QueueMqttPublication(_mqttStateTopic, payload, MqttQoSLevel.AtLeastOnce, true);
        }

        private static void PublishMqttDiseqcJobTransition(string transition, int jobId)
        {
            string payload = BuildDiseqcJobEventJson(transition, jobId);
            WriteStructuredDebug("DISEQC", payload);
            QueueMqttPublication(_mqttDiseqcEventTopic, payload, MqttQoSLevel.AtLeastOnce, false);
            PublishMqttDiseqcState();
        }

        private static void PublishMqttDiseqcState()
        {
            string payload = BuildDiseqcStateJson();
            WriteStructuredDebug("DISEQC", payload);
            QueueMqttPublication(_mqttDiseqcStateTopic, payload, MqttQoSLevel.AtLeastOnce, true);
        }

        private static void OnMqttConnectionClosed(object sender, EventArgs e)
        {
            SetMqttPublishAccepting(false);
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

        private static void QueueMqttPublication(string topic, string payload, MqttQoSLevel qosLevel, bool retain)
        {
            bool dropped = false;
            lock (_mqttPublishQueueLock)
            {
                if (!_mqttPublishAccepting || string.IsNullOrEmpty(topic))
                {
                    return;
                }

                if (_mqttPublishQueueCount >= MqttPublishQueueCapacity)
                {
                    _mqttPublishDropCount++;
                    dropped = true;
                }
                else
                {
                    int queueIndex = (_mqttPublishQueueHead + _mqttPublishQueueCount) % MqttPublishQueueCapacity;
                    _mqttPublishTopics[queueIndex] = topic;
                    _mqttPublishPayloads[queueIndex] = payload;
                    _mqttPublishQosLevels[queueIndex] = qosLevel;
                    _mqttPublishRetainFlags[queueIndex] = retain;
                    _mqttPublishQueueCount++;
                }
            }

            if (dropped)
            {
                WriteStructuredDebug(
                    "MQTT",
                    "schema=1 sub=mqtt comp=publish operation=queue stat=error" +
                    " code=queue_full dropped=" + _mqttPublishDropCount.ToString());
            }
        }

        private static bool DrainMqttPublicationQueue(MqttClient client)
        {
            while (true)
            {
                string topic;
                string payload;
                MqttQoSLevel qosLevel;
                bool retain;
                if (!TryDequeueMqttPublication(out topic, out payload, out qosLevel, out retain))
                {
                    return true;
                }

                try
                {
                    client.Publish(
                        topic,
                        AsciiStringToBytes(payload),
                        null,
                        null,
                        qosLevel,
                        retain);
                }
                catch (Exception ex)
                {
                    _mqttLastError = "publish_exception";
                    _mqttRuntimeState = "error";
                    WriteStructuredDebug(
                        "MQTT",
                        "schema=1 sub=mqtt comp=publish operation=send stat=error" +
                        " code=publish_exception topic=" + topic +
                        " detail=" + SanitizeToken(ex.Message));
                    SetMqttPublishAccepting(false);
                    return false;
                }
            }
        }

        private static bool TryDequeueMqttPublication(
            out string topic,
            out string payload,
            out MqttQoSLevel qosLevel,
            out bool retain)
        {
            lock (_mqttPublishQueueLock)
            {
                if (_mqttPublishQueueCount == 0)
                {
                    topic = null;
                    payload = null;
                    qosLevel = MqttQoSLevel.AtMostOnce;
                    retain = false;
                    return false;
                }

                int queueIndex = _mqttPublishQueueHead;
                topic = _mqttPublishTopics[queueIndex];
                payload = _mqttPublishPayloads[queueIndex];
                qosLevel = _mqttPublishQosLevels[queueIndex];
                retain = _mqttPublishRetainFlags[queueIndex];
                _mqttPublishTopics[queueIndex] = null;
                _mqttPublishPayloads[queueIndex] = null;
                _mqttPublishRetainFlags[queueIndex] = false;
                _mqttPublishQueueHead = (_mqttPublishQueueHead + 1) % MqttPublishQueueCapacity;
                _mqttPublishQueueCount--;
                return true;
            }
        }

        private static void SetMqttPublishAccepting(bool accepting)
        {
            lock (_mqttPublishQueueLock)
            {
                _mqttPublishAccepting = accepting;
                if (accepting)
                {
                    return;
                }

                for (int index = 0; index < MqttPublishQueueCapacity; index++)
                {
                    _mqttPublishTopics[index] = null;
                    _mqttPublishPayloads[index] = null;
                    _mqttPublishRetainFlags[index] = false;
                }

                _mqttPublishQueueHead = 0;
                _mqttPublishQueueCount = 0;
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
    }
}
