using System;

namespace CubleyControl
{
    public static partial class Program
    {
        // A positioner job is the only representation of "the dish is moving".
        // It replaces the previous busy-flag-plus-deadline pair so that both
        // transports, and any future client, can name and query a movement
        // rather than only observing that something is in progress.
        //
        // There is deliberately no "succeeded" state: a DiSEqC 1.2 positioner
        // reports no arrival indication, so the device only ever knows what it
        // transmitted and how much time has elapsed. JobStateCompleted records
        // a client's assertion that motion finished, not a device observation.
        private const int DiseqcJobHistoryDepth = 4;
        private const string JobStateRunning = "running";
        private const string JobStateHalted = "halted";
        private const string JobStateTimeout = "timeout";
        private const string JobStateTimeoutHaltFailed = "timeout_halt_failed";
        private const string JobStateCompleted = "completed";

        private static readonly object _diseqcJobLock = new object();
        private static readonly int[] _diseqcJobIds = new int[DiseqcJobHistoryDepth];
        private static readonly string[] _diseqcJobOps = new string[DiseqcJobHistoryDepth];
        private static readonly string[] _diseqcJobStates = new string[DiseqcJobHistoryDepth];
        private static readonly string[] _diseqcJobVerifications = new string[DiseqcJobHistoryDepth];
        private static readonly string[] _diseqcJobDetails = new string[DiseqcJobHistoryDepth];
        private static readonly long[] _diseqcJobDeadlines = new long[DiseqcJobHistoryDepth];
        private static readonly int[] _diseqcJobTimeouts = new int[DiseqcJobHistoryDepth];
        private static int _diseqcJobRingNext;
        private static int _diseqcNextJobId;
        private static int _diseqcActiveJobId;
        private static int _diseqcLastTerminalJobId;

        // Per-command scratch used by the JSON response layer to report which
        // job a command started, or which job refused it. Both are written
        // only while _commandLock is held, like the other _active* command
        // fields, and are reset at the start of every JSON command.
        private static int _lastStartedDiseqcJobId;
        private static int _blockingDiseqcJobId;

        private static void ResetDiseqcJobCommandScratch()
        {
            _lastStartedDiseqcJobId = 0;
            _blockingDiseqcJobId = 0;
        }

        private static int BeginDiseqcJob(string operation, int durationMs)
        {
            lock (_diseqcJobLock)
            {
                _diseqcNextJobId++;
                if (_diseqcNextJobId <= 0)
                {
                    _diseqcNextJobId = 1;
                }

                int slot = _diseqcJobRingNext;
                int timeoutMs = _diseqcMotionTimeoutMs;
                _diseqcJobIds[slot] = _diseqcNextJobId;
                _diseqcJobOps[slot] = operation;
                _diseqcJobStates[slot] = JobStateRunning;
                _diseqcJobVerifications[slot] = "pending";
                _diseqcJobDetails[slot] = string.Empty;
                _diseqcJobTimeouts[slot] = timeoutMs;
                _diseqcJobDeadlines[slot] = Environment.TickCount64 +
                    (durationMs < timeoutMs ? durationMs : timeoutMs);
                _diseqcJobRingNext = (slot + 1) % DiseqcJobHistoryDepth;
                _diseqcActiveJobId = _diseqcNextJobId;
                return _diseqcActiveJobId;
            }
        }

        /// <summary>
        /// Ends <paramref name="jobId"/> only if it is still the active job.
        /// The identity check is what stops a late release or a stale watchdog
        /// from terminating a newer movement.
        /// </summary>
        private static bool TryEndDiseqcJob(int jobId, string state, string detail)
        {
            return TryEndDiseqcJob(jobId, state, "none", detail);
        }

        private static bool TryEndDiseqcJob(int jobId, string state, string verification, string detail)
        {
            bool ended;
            lock (_diseqcJobLock)
            {
                if (jobId == 0 || _diseqcActiveJobId != jobId)
                {
                    return false;
                }

                int slot = FindDiseqcJobSlotLocked(jobId);
                if (slot < 0)
                {
                    // Evicted from the ring while running, which can only
                    // happen if history depth is exhausted; drop the active
                    // marker so the positioner does not stay wedged busy.
                    _diseqcActiveJobId = 0;
                    return false;
                }

                _diseqcJobStates[slot] = state;
                _diseqcJobVerifications[slot] = verification;
                _diseqcJobDetails[slot] = detail == null ? string.Empty : detail;
                _diseqcJobDeadlines[slot] = 0;
                _diseqcActiveJobId = 0;
                _diseqcLastTerminalJobId = jobId;
                ended = true;
            }

            if (ended)
            {
                RestoreDiseqcMotionVoltageAfterJob();
            }

            return ended;
        }

        /// <summary>Ends whichever job is active. Returns its id, or 0 if none was.</summary>
        private static int EndActiveDiseqcJob(string state, string detail)
        {
            int activeJobId;
            lock (_diseqcJobLock)
            {
                activeJobId = _diseqcActiveJobId;
            }

            if (activeJobId == 0)
            {
                return 0;
            }

            return TryEndDiseqcJob(activeJobId, state, detail) ? activeJobId : 0;
        }

        private static int GetActiveDiseqcJobId()
        {
            lock (_diseqcJobLock)
            {
                return _diseqcActiveJobId;
            }
        }

        /// <summary>Returns the active job id if its deadline has passed, else 0.</summary>
        private static int GetExpiredDiseqcJobId()
        {
            lock (_diseqcJobLock)
            {
                if (_diseqcActiveJobId == 0)
                {
                    return 0;
                }

                int slot = FindDiseqcJobSlotLocked(_diseqcActiveJobId);
                if (slot < 0 || Environment.TickCount64 < _diseqcJobDeadlines[slot])
                {
                    return 0;
                }

                return _diseqcActiveJobId;
            }
        }

        private static bool TryGetDiseqcJobSnapshot(
            int jobId,
            out string operation,
            out string state,
            out string verification,
            out int remainingMs,
            out int timeoutMs,
            out string detail)
        {
            operation = "idle";
            state = string.Empty;
            verification = "none";
            remainingMs = 0;
            timeoutMs = 0;
            detail = string.Empty;

            lock (_diseqcJobLock)
            {
                int slot = FindDiseqcJobSlotLocked(jobId);
                if (slot < 0)
                {
                    return false;
                }

                operation = _diseqcJobOps[slot];
                state = _diseqcJobStates[slot];
                verification = _diseqcJobVerifications[slot];
                timeoutMs = _diseqcJobTimeouts[slot];
                detail = _diseqcJobDetails[slot];
                remainingMs = state == JobStateRunning
                    ? ClampRemainingMs(_diseqcJobDeadlines[slot] - Environment.TickCount64)
                    : 0;
                return true;
            }
        }

        private static int FindDiseqcJobSlotLocked(int jobId)
        {
            if (jobId == 0)
            {
                return -1;
            }

            for (int slot = 0; slot < DiseqcJobHistoryDepth; slot++)
            {
                if (_diseqcJobIds[slot] == jobId)
                {
                    return slot;
                }
            }

            return -1;
        }

        private static int ClampRemainingMs(long remaining)
        {
            if (remaining <= 0)
            {
                return 0;
            }

            return remaining > int.MaxValue ? int.MaxValue : (int)remaining;
        }

        /// <summary>
        /// Compatibility view for the console renderer and the schema-1 debug
        /// log, both of which predate the job model and describe motion as a
        /// busy flag plus a completion source.
        /// </summary>
        private static void GetDiseqcMotionSnapshot(
            out bool busy,
            out int motionId,
            out string operation,
            out int remainingMs,
            out string completionSource)
        {
            int activeJobId = GetActiveDiseqcJobId();
            int reportedJobId;
            lock (_diseqcJobLock)
            {
                reportedJobId = activeJobId != 0 ? activeJobId : _diseqcLastTerminalJobId;
            }

            busy = activeJobId != 0;
            motionId = reportedJobId;

            string state;
            string verification;
            int timeoutMs;
            string detail;
            if (!TryGetDiseqcJobSnapshot(reportedJobId, out operation, out state, out verification, out remainingMs, out timeoutMs, out detail))
            {
                operation = "idle";
                remainingMs = 0;
                completionSource = "none";
                return;
            }

            if (busy)
            {
                completionSource = "pending";
                return;
            }

            operation = "idle";
            completionSource = state;
        }

        private static string BuildDiseqcJobJson(int jobId)
        {
            string operation;
            string state;
            string verification;
            int remainingMs;
            int timeoutMs;
            string detail;
            if (!TryGetDiseqcJobSnapshot(jobId, out operation, out state, out verification, out remainingMs, out timeoutMs, out detail))
            {
                return "null";
            }

            return new JsonBuilder()
                .AddInt("job", jobId)
                .AddString("op", operation)
                .AddString("state", state)
                .AddString("verification", verification)
                .AddInt("remaining_ms", remainingMs)
                .AddInt("timeout_ms", timeoutMs)
                .AddString("detail", detail)
                .Build();
        }

        private static string BuildDiseqcStateJson()
        {
            int activeJobId = GetActiveDiseqcJobId();
            int lastTerminalJobId;
            lock (_diseqcJobLock)
            {
                lastTerminalJobId = _diseqcLastTerminalJobId;
            }

            JsonBuilder builder = new JsonBuilder()
                .AddInt("v", DeviceContractVersion)
                .AddString("sub", "diseqc")
                .AddString("comp", "state")
                .AddBool("busy", activeJobId != 0)
                .AddInt("timeout_ms", _diseqcMotionTimeoutMs)
                .AddRaw("active", activeJobId == 0 ? "null" : BuildDiseqcJobJson(activeJobId))
                .AddRaw("last", lastTerminalJobId == 0 ? "null" : BuildDiseqcJobJson(lastTerminalJobId));

            lock (_diseqcMotionLock)
            {
                builder
                    .AddString("position_confidence", _diseqcPositionEstimate.Confidence)
                    .AddRaw("estimated_angle_deg", _diseqcPositionEstimate.HasEstimate
                        ? Json.Quote(FormatSignedDiseqcAngle(_diseqcPositionEstimate.EstimatedAngleMicrodegrees))
                        : "null")
                    .AddString("position_source", _diseqcPositionEstimate.Source)
                    .AddRaw("pending_target_deg", _diseqcPositionEstimate.HasPendingTarget
                        ? Json.Quote(FormatSignedDiseqcAngle(_diseqcPositionEstimate.PendingTargetMicrodegrees))
                        : "null");
            }

            return builder.Build();
        }

        private static string BuildDiseqcJobEventJson(string transition, int jobId)
        {
            string operation;
            string state;
            string verification;
            int remainingMs;
            int timeoutMs;
            string detail;
            TryGetDiseqcJobSnapshot(jobId, out operation, out state, out verification, out remainingMs, out timeoutMs, out detail);

            return new JsonBuilder()
                .AddInt("v", DeviceContractVersion)
                .AddString("sub", "diseqc")
                .AddString("comp", "job")
                .AddString("transition", transition)
                .AddInt("job", jobId)
                .AddString("op", operation)
                .AddString("state", state)
                .AddString("verification", verification)
                .AddInt("remaining_ms", remainingMs)
                .Build();
        }
    }
}
