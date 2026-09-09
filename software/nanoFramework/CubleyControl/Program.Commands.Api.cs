using System;
using Cubley.Diseqc;

namespace CubleyControl
{
    public static partial class Program
    {
        private const int DeviceContractVersion = 2;
        private const int ApiCommandIdMaxLength = 32;
        private const int ApiOperationMaxLength = 40;
        private const int ApiCommandEnvelopeMaxLength = 256;
        private const int ApiDuplicateCacheSize = 8;
        private const int ApiDedupTtlMs = 120_000;
        private const int ApiBridgedLineLimit = 16;

        private static readonly string[] _apiCachedCommandIds = new string[ApiDuplicateCacheSize];
        private static readonly string[] _apiCachedPayloads = new string[ApiDuplicateCacheSize];
        private static readonly string[] _apiCachedResponseBodies = new string[ApiDuplicateCacheSize];
        private static readonly long[] _apiCachedAtMs = new long[ApiDuplicateCacheSize];
        private static int _apiDuplicateCacheNext;

        private static string _activeApiCommandKey = "?";
        private static string _apiBridgedOutput = string.Empty;
        private static int _apiBridgedLineCount;
        private static bool _apiResultOk;
        private static bool _apiResultRecorded;
        private static string _apiResultCode = string.Empty;
        private static string _apiResultMessage = string.Empty;

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
                return "{" + BuildApiFailureBody("?", "validation_error", parseError, 0) + "}";
            }

            int version = DeviceContractVersion;
            if (command.Has("v") &&
                (!command.TryGetInt("v", out version) || version != DeviceContractVersion))
            {
                return "{" + BuildApiFailureBody(
                    "?",
                    "validation_error",
                    "unsupported contract version; this device speaks v" + DeviceContractVersion.ToString(),
                    0) + "}";
            }

            string commandId;
            if (!command.TryGetString("id", out commandId) || !IsValidApiCommandId(commandId))
            {
                return "{" + BuildApiFailureBody(
                    "?",
                    "validation_error",
                    "id must be 1 to " + ApiCommandIdMaxLength.ToString() + " characters of A-Za-z0-9._:-",
                    0) + "}";
            }

            string operation;
            if (!command.TryGetString("op", out operation) || !IsValidApiOperationName(operation))
            {
                return "{" + BuildApiFailureBody(
                    commandId,
                    "validation_error",
                    "op must be 1 to " + ApiOperationMaxLength.ToString() + " characters of a-z0-9._",
                    0) + "}";
            }

            int cachedIndex = FindCachedApiCommand(commandId);
            if (cachedIndex >= 0)
            {
                if (_apiCachedPayloads[cachedIndex] != payload)
                {
                    WriteStructuredDebug(
                        "COMMAND",
                        "schema=1 sub=command comp=deduplicate operation=reject stat=error" +
                        " transport=rest code=id_conflict id=" + SanitizeToken(commandId));
                    return "{" + BuildApiFailureBody(
                        commandId,
                        "id_conflict",
                        "id reused within the deduplication window with a different payload",
                        0) + "}";
                }

                WriteStructuredDebug(
                    "COMMAND",
                    "schema=1 sub=command comp=deduplicate operation=replay stat=ok" +
                    " transport=rest id=" + SanitizeToken(commandId));
                return "{" + _apiCachedResponseBodies[cachedIndex] + ",\"replayed\":true}";
            }

            _activeApiCommandKey = commandId;
            WriteStructuredDebug(
                "COMMAND",
                "schema=1 sub=command comp=dispatch operation=start stat=ok" +
                " transport=rest id=" + SanitizeToken(commandId) +
                " op=" + SanitizeToken(operation));

            string responseBody = ExecuteApiOperation(commandId, operation, command);
            CacheApiCommandResponse(commandId, payload, responseBody);

            WriteStructuredDebug(
                "COMMAND",
                "schema=1 sub=command comp=dispatch operation=complete stat=ok" +
                " transport=rest id=" + SanitizeToken(commandId));
            return "{" + responseBody + "}";
        }

        private static string ExecuteApiOperation(string commandId, string operation, JsonObject command)
        {
            if (operation == "positioner.goto_angle")
            {
                return ExecuteApiPositionerGotoAngle(commandId, command);
            }

            if (operation == "positioner.goto" || operation == "positioner.step" ||
                operation == "positioner.drive" || operation == "positioner.halt")
            {
                return ExecuteApiPositionerMotion(commandId, operation, command);
            }

            if (operation == "positioner.complete")
            {
                return ExecuteApiPositionerComplete(commandId, command);
            }

            return ExecuteApiBridgedOperation(commandId, operation, command);
        }

        private static string ExecuteApiPositionerMotion(string commandId, string operationName, JsonObject command)
        {
            int operation;
            int value = 0;
            string error;

            if (operationName == "positioner.goto")
            {
                if (!TryValidateApiMembers(command, "position", null, out error))
                {
                    return BuildApiFailureBody(commandId, "validation_error", error, 0);
                }

                int position;
                if (!command.TryGetInt("position", out position) ||
                    position < 0 || position > DiseqcMotorStoredPositionMax)
                {
                    return BuildApiFailureBody(commandId, "validation_error", "position must be an integer 0 to 60", 0);
                }

                operation = PositionerOpGoto;
                value = position;
            }
            else if (operationName == "positioner.step")
            {
                if (!TryValidateApiMembers(command, "direction", "count", out error))
                {
                    return BuildApiFailureBody(commandId, "validation_error", error, 0);
                }

                string direction;
                if (!command.TryGetString("direction", out direction) ||
                    (direction != "east" && direction != "west"))
                {
                    return BuildApiFailureBody(commandId, "validation_error", "direction must be \"east\" or \"west\"", 0);
                }

                int count;
                if (!command.TryGetInt("count", out count) || count < 1 || count > 128)
                {
                    return BuildApiFailureBody(commandId, "validation_error", "count must be an integer 1 to 128", 0);
                }

                operation = direction == "east" ? PositionerOpStepEast : PositionerOpStepWest;
                value = count;
            }
            else if (operationName == "positioner.drive")
            {
                if (!TryValidateApiMembers(command, "direction", null, out error))
                {
                    return BuildApiFailureBody(commandId, "validation_error", error, 0);
                }

                string direction;
                if (!command.TryGetString("direction", out direction) ||
                    (direction != "east" && direction != "west"))
                {
                    return BuildApiFailureBody(commandId, "validation_error", "direction must be \"east\" or \"west\"", 0);
                }

                operation = direction == "east" ? PositionerOpDriveEast : PositionerOpDriveWest;
            }
            else
            {
                if (!TryValidateApiMembers(command, null, null, out error))
                {
                    return BuildApiFailureBody(commandId, "validation_error", error, 0);
                }

                operation = PositionerOpHalt;
            }

            ResetApiCommandOutcome();
            ExecutePositionerOperation(operation, value, ApiOutputSink);

            if (!_apiResultOk)
            {
                return BuildApiFailureBody(
                    commandId,
                    _apiResultCode.Length == 0 ? "hw_fault" : _apiResultCode,
                    _apiResultMessage,
                    _blockingDiseqcJobId);
            }

            int jobId = _lastStartedDiseqcJobId;
            bool started = jobId != 0 && GetActiveDiseqcJobId() == jobId;
            return BuildApiResponseBody(
                commandId,
                true,
                started ? "accepted" : "ok",
                null,
                started ? null : BuildDiseqcStateJson(),
                jobId,
                null);
        }

        private static string ExecuteApiPositionerGotoAngle(string commandId, JsonObject command)
        {
            string error;
            if (!TryValidateApiMembers(command, "direction", "angle", out error))
            {
                return BuildApiFailureBody(commandId, "validation_error", error, 0);
            }

            string direction;
            if (!command.TryGetString("direction", out direction) ||
                (direction != "east" && direction != "west"))
            {
                return BuildApiFailureBody(commandId, "validation_error", "direction must be \"east\" or \"west\"", 0);
            }

            string angle;
            if (!command.TryGetString("angle", out angle))
            {
                return BuildApiFailureBody(commandId, "validation_error", "angle must be a decimal string", 0);
            }

            ResetApiCommandOutcome();
            ExecutePositionerGotoAngle(
                direction == "east" ? DiseqcMotorDirection.East : DiseqcMotorDirection.West,
                angle,
                ApiOutputSink);

            if (!_apiResultOk)
            {
                return BuildApiFailureBody(
                    commandId,
                    _apiResultCode.Length == 0 ? "hw_fault" : _apiResultCode,
                    _apiResultMessage,
                    _blockingDiseqcJobId);
            }

            int jobId = _lastStartedDiseqcJobId;
            bool started = jobId != 0 && GetActiveDiseqcJobId() == jobId;
            return BuildApiResponseBody(
                commandId,
                true,
                started ? "accepted" : "ok",
                null,
                started ? null : BuildDiseqcStateJson(),
                jobId,
                null);
        }

        private static string ExecuteApiPositionerComplete(string commandId, JsonObject command)
        {
            string error;
            if (!TryValidateApiMembers(command, "job", "verification", out error))
            {
                return BuildApiFailureBody(commandId, "validation_error", error, 0);
            }

            int jobId;
            if (!command.TryGetInt("job", out jobId) || jobId <= 0)
            {
                return BuildApiFailureBody(commandId, "validation_error", "job must be a positive integer", 0);
            }

            string verification;
            if (!command.TryGetString("verification", out verification) ||
                (verification != "estimated" && verification != "rf_verified" &&
                verification != "verification_failed"))
            {
                return BuildApiFailureBody(
                    commandId,
                    "validation_error",
                    "verification must be estimated, rf_verified, or verification_failed",
                    0);
            }

            if (BuildDiseqcJobJson(jobId) == "null")
            {
                return BuildApiFailureBody(commandId, "not_found", "job is unknown or has been evicted", 0);
            }

            if (GetActiveDiseqcJobId() != jobId)
            {
                return BuildApiFailureBody(commandId, "validation_error", "job is not the running job", GetActiveDiseqcJobId());
            }

            lock (_diseqcMotionLock)
            {
                if (verification == "rf_verified" && !_diseqcPositionEstimate.HasPendingTarget)
                {
                    return BuildApiFailureBody(
                        commandId,
                        "validation_error",
                        "job has no angular target to verify",
                        jobId);
                }
            }

            if (!TryEndDiseqcJob(jobId, JobStateCompleted, verification, string.Empty))
            {
                return BuildApiFailureBody(commandId, "validation_error", "job is not the running job", GetActiveDiseqcJobId());
            }

            lock (_diseqcMotionLock)
            {
                if (verification == "rf_verified")
                {
                    _diseqcPositionEstimate.CompletePendingAsRfVerified();
                }
                else if (verification == "verification_failed")
                {
                    _diseqcPositionEstimate.FailVerification();
                }
                else
                {
                    _diseqcPositionEstimate.CompletePending();
                }
            }

            WriteStructuredDebug("DISEQC", BuildDiseqcJobEventJson("end", jobId));
            return BuildApiResponseBody(commandId, true, "ok", null, BuildDiseqcJobJson(jobId), jobId, null);
        }

        private static string ExecuteApiBridgedOperation(string commandId, string operation, JsonObject command)
        {
            string consoleCommand;
            string code;
            string error;
            if (!TryBuildBridgedConsoleCommand(operation, command, out consoleCommand, out code, out error))
            {
                return BuildApiFailureBody(commandId, code, error, 0);
            }

            ResetApiCommandOutcome();
            ExecuteCommand(consoleCommand, ApiOutputSink, CommandTransport.Rest);

            if (!_apiResultRecorded)
            {
                return BuildApiResponseBody(commandId, true, "ok", null, null, 0, _apiBridgedOutput);
            }

            return BuildApiResponseBody(
                commandId,
                _apiResultOk,
                _apiResultOk ? "ok" : (_apiResultCode.Length == 0 ? "hw_fault" : _apiResultCode),
                _apiResultOk ? null : _apiResultMessage,
                null,
                0,
                _apiBridgedOutput);
        }

        private static bool TryBuildBridgedConsoleCommand(
            string operation,
            JsonObject command,
            out string consoleCommand,
            out string code,
            out string error)
        {
            consoleCommand = string.Empty;
            code = "validation_error";
            error = string.Empty;

            if (operation == "lnb.enable" || operation == "lnb.disable")
            {
                if (!TryValidateApiMembers(command, "channel", null, out error))
                {
                    return false;
                }

                string channel;
                if (!TryReadLnbChannel(command, out channel, out error))
                {
                    return false;
                }

                consoleCommand = "lnb " + channel + (operation == "lnb.enable" ? " enable" : " disable");
                return true;
            }

            if (operation == "lnb.polarization" || operation == "lnb.band")
            {
                if (!TryValidateApiMembers(command, "channel", "value", out error))
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
                    (operation == "lnb.polarization" ? " polarization " : " band ") + value;
                return true;
            }

            if (operation == "diseqc.preset" || operation == "diseqc.tone")
            {
                if (!TryValidateApiMembers(command, "value", null, out error))
                {
                    return false;
                }

                string value;
                if (!command.TryGetString("value", out value) || !IsSafeConsoleToken(value))
                {
                    error = "value must be 1 to 16 characters of a-z0-9";
                    return false;
                }

                consoleCommand = (operation == "diseqc.preset" ? "diseqc preset " : "diseqc tone ") + value;
                return true;
            }

            if (operation == "diseqc.tx")
            {
                if (!TryValidateApiMembers(command, "frame", null, out error))
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

        private static bool TryValidateApiMembers(JsonObject command, string first, string second, out string error)
        {
            for (int index = 0; index < command.Count; index++)
            {
                string key = command.KeyAt(index);
                if (key == "v" || key == "id" || key == "op" ||
                    (first != null && key == first) || (second != null && key == second))
                {
                    continue;
                }

                error = "unknown member " + key;
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool IsValidApiCommandId(string value)
        {
            if (value == null || value.Length == 0 || value.Length > ApiCommandIdMaxLength)
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

        private static bool IsValidApiOperationName(string value)
        {
            if (value == null || value.Length == 0 || value.Length > ApiOperationMaxLength)
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

        private static void ResetApiCommandOutcome()
        {
            _apiResultOk = false;
            _apiResultRecorded = false;
            _apiResultCode = string.Empty;
            _apiResultMessage = string.Empty;
            _apiBridgedOutput = string.Empty;
            _apiBridgedLineCount = 0;
            ResetDiseqcJobCommandScratch();
        }

        private static void RecordApiCommandOutcome(bool ok, string code, string message)
        {
            _apiResultOk = ok;
            _apiResultCode = code == null ? string.Empty : code;
            _apiResultMessage = message == null ? string.Empty : message;
            _apiResultRecorded = true;
        }

        private static void ApiOutputSink(string line)
        {
            if (line == null || _apiBridgedLineCount >= ApiBridgedLineLimit)
            {
                return;
            }

            string trimmed = line.TrimEnd('\r', '\n');
            if (trimmed.Length == 0)
            {
                return;
            }

            _apiBridgedOutput = _apiBridgedLineCount == 0
                ? trimmed
                : _apiBridgedOutput + "\n" + trimmed;
            _apiBridgedLineCount++;
        }

        private static string BuildApiFailureBody(string commandId, string code, string message, int jobId)
        {
            return BuildApiResponseBody(commandId, false, code, message, null, jobId, null);
        }

        private static string BuildApiResponseBody(
            string commandId,
            bool ok,
            string code,
            string message,
            string dataRaw,
            int jobId,
            string lines)
        {
            JsonBuilder builder = new JsonBuilder()
                .AddInt("v", DeviceContractVersion)
                .AddString("boot_id", RestBootId)
                .AddString("id", commandId)
                .AddBool("ok", ok)
                .AddString("code", code)
                .AddLong("ts_ms", Environment.TickCount64);

            if (message != null && message.Length > 0)
            {
                builder.AddString("msg", message);
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

        private static int FindCachedApiCommand(string commandId)
        {
            long nowMs = Environment.TickCount64;
            for (int index = 0; index < ApiDuplicateCacheSize; index++)
            {
                if (_apiCachedCommandIds[index] == null || _apiCachedCommandIds[index] != commandId)
                {
                    continue;
                }

                if (nowMs - _apiCachedAtMs[index] > ApiDedupTtlMs)
                {
                    _apiCachedCommandIds[index] = null;
                    _apiCachedPayloads[index] = null;
                    _apiCachedResponseBodies[index] = null;
                    return -1;
                }

                return index;
            }

            return -1;
        }

        private static void CacheApiCommandResponse(string commandId, string payload, string responseBody)
        {
            int slot = _apiDuplicateCacheNext;
            _apiCachedCommandIds[slot] = commandId;
            _apiCachedPayloads[slot] = payload;
            _apiCachedResponseBodies[slot] = responseBody;
            _apiCachedAtMs[slot] = Environment.TickCount64;
            _apiDuplicateCacheNext = (slot + 1) % ApiDuplicateCacheSize;
        }
    }
}