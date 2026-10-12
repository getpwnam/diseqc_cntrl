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

        private static string AcceptApiCommand(string payload)
        {
            JsonObject command;
            string commandId;
            string operation;
            string validationFailure;
            if (!TryParseApiCommand(payload, out command, out commandId, out operation, out validationFailure))
            {
                return "{" + validationFailure + "}";
            }

            string cachedResponse;
            bool cachedConflict;
            if (TryGetCachedApiCommand(commandId, payload, out cachedResponse, out cachedConflict))
            {
                if (cachedConflict)
                {
                    return "{" + BuildApiFailureBody(
                        commandId,
                        "id_conflict",
                        "id reused within the deduplication window with a different payload",
                        0) + "}";
                }

                return "{" + cachedResponse + ",\"replayed\":true}";
            }

            int result = _apiCommandQueue.TryEnqueue(
                commandId,
                payload,
                operation == "positioner.halt");
            if (result == ApiCommandQueueResult.Conflict)
            {
                return "{" + BuildApiFailureBody(
                    commandId,
                    "id_conflict",
                    "id reused by a queued command with a different payload",
                    0) + "}";
            }

            if (result == ApiCommandQueueResult.Full)
            {
                _restDiagnostics.RecordQueueRejected();
                return "{" + BuildApiFailureBody(
                    commandId,
                    "queue_full",
                    "command queue is full",
                    0) + "}";
            }

            if (result == ApiCommandQueueResult.Accepted)
            {
                _restDiagnostics.RecordQueueAccepted(_apiCommandQueue.Count);
            }

            return "{" + BuildApiResponseBody(
                commandId,
                true,
                "accepted",
                result == ApiCommandQueueResult.Duplicate ? "command is already queued" : "command queued",
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
                string payload;
                if (!_apiCommandQueue.TryDequeue(out commandId, out payload))
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
                        payload,
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
