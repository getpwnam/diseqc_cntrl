using System;
using System.Threading;
using Cubley.Interop;

namespace CubleyControl
{
    public static partial class Program
    {
        private const int LnbHealthIntervalMs = 10_000;
        private const int LnbHealthMaximumBackoffMs = 60_000;
        private static readonly object _lnbIoLock = new object();
        private static readonly object _lnbIoReservationLock = new object();
        private static int _lnbIoReservations;
        private static string _lnbHealthState = "unknown";
        private static bool _lnbHealthCommsOk;
        private static bool _lnbHealthHasResult;
        private static int _lnbHealthCheckSequence;
        private static int _lnbHealthConsecutiveFailures;
        private static int _lnbHealthResult = (int)LNBH26.Status.NotInitialized;
        private static int _lnbHealthS1;
        private static int _lnbHealthS2;
        private static int _lnbHealthD1;
        private static int _lnbHealthD2;
        private static int _lnbHealthD3;
        private static int _lnbHealthD4;

        private static string BuildLnbStateJson()
        {
            lock (_lnbIoLock)
            {
                JsonBuilder builder = new JsonBuilder()
                    .AddInt("v", DeviceContractVersion)
                    .AddString("sub", "lnb")
                    .AddString("comp", "state")
                    .AddString("status", _lnbHealthState)
                    .AddString("communication", !_lnbHealthHasResult ? "unknown" : (_lnbHealthCommsOk ? "ok" : "error"))
                    .AddInt("health_failures", _lnbHealthConsecutiveFailures)
                    .AddInt("health_rc", _lnbHealthResult)
                    .AddString("s1", ToHexU8(_lnbHealthS1))
                    .AddString("s2", ToHexU8(_lnbHealthS2))
                    .AddString("d1", ToHexU8(_lnbHealthD1))
                    .AddString("d2", ToHexU8(_lnbHealthD2))
                    .AddString("d3", ToHexU8(_lnbHealthD3))
                    .AddString("d4", ToHexU8(_lnbHealthD4))
                    .AddBool("fault", _lnbFaultAsserted)
                    .AddString("monitor", _lnbFaultReady ? "ready" : "unavailable")
                    .AddString("initialization", LnbStatusToToken(_lnbInitStatus));

                if (_lnbInitStatus == (int)LNBH26.Status.Ok)
                {
                    builder
                        .AddString("a_polarization", PolarizationToText(LNBH26.NativeGetPolarizationForChannel(LnbChannelA)))
                        .AddString("a_band", BandToText(LNBH26.NativeGetBandForChannel(LnbChannelA)))
                        .AddString("b_polarization", PolarizationToText(LNBH26.NativeGetPolarizationForChannel(1)))
                        .AddString("b_band", BandToText(LNBH26.NativeGetBandForChannel(1)));
                }

                return builder.Build();
            }
        }

        private static void LnbHealthLoop()
        {
            int delayMs = LnbHealthIntervalMs;
            WriteStructuredDebug(
                "LNB",
                "schema=1 sub=lnb comp=health operation=start stat=ok" +
                " interval_ms=" + LnbHealthIntervalMs.ToString() +
                " level=debug");

            while (true)
            {
                Thread.Sleep(delayMs);

                if (!TryBeginLnbHealthOperation())
                {
                    WriteStructuredDebug(
                        "LNB",
                        "schema=1 sub=lnb comp=health operation=check stat=busy level=debug");
                    delayMs = LnbHealthIntervalMs;
                    continue;
                }

                try
                {
                    bool commsTransition;
                    bool previousCommsOk;
                    bool faultAssertion;
                    int faultSequence;
                    lock (_lnbIoLock)
                    {
                        previousCommsOk = _lnbHealthCommsOk;
                        bool hadResult = _lnbHealthHasResult;
                        CheckLnbHealth(out faultAssertion, out faultSequence);
                        commsTransition = hadResult && previousCommsOk != _lnbHealthCommsOk;
                    }

                    if (faultAssertion)
                    {
                        EmitLnbFaultTransition(true, "health", faultSequence);
                    }

                    if (commsTransition)
                    {
                        EmitLnbHealthEvent(
                            _lnbHealthCommsOk ? "restored" : "lost",
                            _lnbHealthCheckSequence,
                            _lnbHealthResult);
                    }

                    delayMs = CalculateLnbHealthDelay();
                }
                catch (Exception ex)
                {
                    WriteStructuredDebug(
                        "LNB",
                        "schema=1 sub=lnb comp=health operation=check stat=error" +
                        " code=worker_exception detail=" + SanitizeToken(ex.Message) +
                        " level=error");
                    delayMs = LnbHealthMaximumBackoffMs;
                }
                finally
                {
                    EndLnbIoOperation();
                }
            }
        }

        private static void CheckLnbHealth(out bool faultAssertion, out int faultSequence)
        {
            faultAssertion = false;
            faultSequence = 0;

            _lnbHealthCheckSequence++;
            _lnbHealthHasResult = true;

            int s1;
            int s2;
            int result = ReadLnbStatusPairSafe(out s1, out s2);
            int d1 = 0;
            int d2 = 0;
            int d3 = 0;
            int d4 = 0;
            if (result == (int)LNBH26.Status.Ok)
            {
                result = ReadLnbDataRegistersSafe(out d1, out d2, out d3, out d4);
            }

            _lnbHealthResult = result;
            _lnbHealthS1 = s1;
            _lnbHealthS2 = s2;
            _lnbHealthD1 = d1;
            _lnbHealthD2 = d2;
            _lnbHealthD3 = d3;
            _lnbHealthD4 = d4;
            _lnbHealthCommsOk = result == (int)LNBH26.Status.Ok;

            if (_lnbHealthCommsOk)
            {
                _lnbHealthConsecutiveFailures = 0;
                _lnbHealthState = HasFaultStatus(s1) ? "fault" : "ok";
                if (_lnbHealthState == "fault")
                {
                    lock (_lnbFaultTransitionLock)
                    {
                        if (!_lnbFaultAsserted)
                        {
                            _lnbFaultAsserted = true;
                            _lnbFaultSequence++;
                            faultSequence = _lnbFaultSequence;
                            faultAssertion = true;
                        }
                    }
                }
            }
            else
            {
                _lnbHealthConsecutiveFailures++;
                _lnbHealthState = "unavailable";
            }

            WriteStructuredDebug(
                "LNB",
                "schema=1 sub=lnb comp=health operation=check" +
                " stat=" + _lnbHealthState +
                " seq=" + _lnbHealthCheckSequence.ToString() +
                " rc=" + result.ToString() +
                " failures=" + _lnbHealthConsecutiveFailures.ToString() +
                " s1=" + ToHexU8(s1) +
                " s2=" + ToHexU8(s2) +
                " level=debug");

        }

        private static void EmitLnbFaultTransition(bool active, string source, int sequence)
        {
            WriteStructuredDebug(
                "LNB",
                "schema=1 sub=lnb comp=fault operation=transition" +
                " stat=" + (active ? "active" : "clear") +
                " seq=" + sequence.ToString() +
                " source=" + SanitizeToken(source));
        }

        private static void EmitLnbHealthEvent(string status, int sequence, int result)
        {
            WriteStructuredDebug(
                "LNB",
                "schema=1 sub=lnb comp=health operation=comms" +
                " stat=" + status +
                " seq=" + sequence.ToString() +
                " rc=" + result.ToString());
        }

        private static int CalculateLnbHealthDelay()
        {
            int delayMs = LnbHealthIntervalMs;
            for (int failure = 1; failure < _lnbHealthConsecutiveFailures; failure++)
            {
                if (delayMs >= LnbHealthMaximumBackoffMs / 2)
                {
                    return LnbHealthMaximumBackoffMs;
                }

                delayMs *= 2;
            }

            return delayMs > LnbHealthMaximumBackoffMs ? LnbHealthMaximumBackoffMs : delayMs;
        }

        private static bool TryBeginLnbHealthOperation()
        {
            lock (_lnbIoReservationLock)
            {
                if (_lnbIoReservations != 0 || _diseqcTxBusy)
                {
                    return false;
                }

                _lnbIoReservations++;
                return true;
            }
        }

        private static void BeginLnbIoOperation()
        {
            lock (_lnbIoReservationLock)
            {
                _lnbIoReservations++;
            }
        }

        private static void EndLnbIoOperation()
        {
            lock (_lnbIoReservationLock)
            {
                _lnbIoReservations--;
            }
        }
    }
}