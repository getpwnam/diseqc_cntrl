using System;
using System.Threading;

namespace CubleyControl
{
    public static partial class Program
    {
        private const int ApiCommandQueueCapacity = 4;
        private const int ApiCommandWorkerIdleMs = 10;
        private static readonly ApiCommandQueue _apiCommandQueue =
            new ApiCommandQueue(ApiCommandQueueCapacity);

        private static string AcceptApiCommand(
            string payload,
            out int statusCode,
            out int retryAfterSeconds)
        {
            statusCode = 202;
            retryAfterSeconds = 0;
            JsonObject command;
            string commandId;
            string operation;
            string validationFailure;
            if (!TryParseApiCommand(payload, out command, out commandId, out operation, out validationFailure))
            {
                statusCode = 400;
                return "{" + validationFailure + "}";
            }

            string identity = ApiCommandCanonicalizer.Build(command, DeviceContractVersion);
            string cachedResponse;
            bool cachedConflict;
            if (TryGetCachedApiCommand(commandId, identity, out cachedResponse, out cachedConflict))
            {
                if (cachedConflict)
                {
                    statusCode = 409;
                    return "{" + BuildApiFailureBody(
                        commandId,
                        "id_conflict",
                        "id reused within the deduplication window with a different command",
                        0) + "}";
                }

                statusCode = 200;
                return "{" + cachedResponse + ",\"replayed\":true}";
            }

            if (operation == "positioner.halt")
            {
                string error;
                if (!TryValidateApiMembers(command, null, null, out error))
                {
                    statusCode = 400;
                    return "{" + BuildApiFailureBody(commandId, "validation_error", error, 0) + "}";
                }
            }

            int queueDepth;
            int result = _apiCommandQueue.TryEnqueue(
                commandId,
                identity,
                payload,
                operation == "positioner.halt",
                out queueDepth);
            if (result == ApiCommandQueueResult.Conflict)
            {
                statusCode = 409;
                return "{" + BuildApiFailureBody(
                    commandId,
                    "id_conflict",
                    "id reused by a queued or active command with a different command",
                    0) + "}";
            }

            if (result == ApiCommandQueueResult.Full)
            {
                _restDiagnostics.RecordQueueRejected();
                statusCode = 503;
                retryAfterSeconds = 1;
                return "{" + BuildApiFailureBody(
                    commandId,
                    "queue_full",
                    "command queue is full",
                    0) + "}";
            }

            if (result == ApiCommandQueueResult.Accepted)
            {
                _restDiagnostics.RecordQueueAccepted(queueDepth);
            }

            return "{" + BuildApiResponseBody(
                commandId,
                true,
                "queued",
                result == ApiCommandQueueResult.Duplicate ? "command is already queued or active" : "command queued",
                null,
                0,
                null) +
                (result == ApiCommandQueueResult.Duplicate ? ",\"replayed\":true" : string.Empty) +
                "}";
        }

        private static void ApiCommandWorkerLoop()
        {
            while (true)
            {
                string commandId;
                string identity;
                string payload;
                if (!_apiCommandQueue.TryDequeue(out commandId, out identity, out payload))
                {
                    Thread.Sleep(ApiCommandWorkerIdleMs);
                    continue;
                }

                try
                {
                    ProcessApiCommand(payload);
                }
                catch (Exception ex)
                {
                    CacheApiCommandResponse(
                        commandId,
                        identity,
                        BuildApiFailureBody(commandId, "internal_error", "command execution failed", 0));
                    WriteStructuredDebug(
                        "COMMAND",
                        "schema=1 sub=command comp=worker operation=execute stat=error id=" +
                        SanitizeToken(commandId) + " detail=" + SanitizeToken(ex.Message));
                }
                finally
                {
                    _apiCommandQueue.Complete(commandId);
                }
            }
        }
    }
}
