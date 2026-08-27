using System;
using System.Diagnostics;
using System.Threading;
using Cubley.Interop;

namespace CubleyControl
{
    public static partial class Program
    {
        private const int LnbHealthIntervalMs = 10_000;
        private const int LnbHealthMaximumBackoffMs = 60_000;
        private const int LnbHealthStateRefreshMs = 60_000;
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
        private static int _lnbHealthPublishElapsedMs;

        private static void LnbHealthLoop()
        {
            int delayMs = LnbHealthIntervalMs;
            WriteStructuredDebug(
                "LNB",
                "schema=1 subsystem=lnb component=health operation=start status=ok" +
                " interval_ms=" + LnbHealthIntervalMs.ToString() +
                " level=debug");

            while (true)
            {
                Thread.Sleep(delayMs);
                _lnbHealthPublishElapsedMs += delayMs;

                if (!TryBeginLnbHealthOperation())
                {
                    WriteStructuredDebug(
                        "LNB",
                        "schema=1 subsystem=lnb component=health operation=check status=busy level=debug");
                    delayMs = LnbHealthIntervalMs;
                    continue;
                }

                try
                {
                    bool changed;
                    bool commsTransition;
                    bool previousCommsOk;
                    bool faultAssertion;
                    int faultSequence;
                    lock (_lnbIoLock)
                    {
                        previousCommsOk = _lnbHealthCommsOk;
                        bool hadResult = _lnbHealthHasResult;
                        changed = CheckLnbHealth(out faultAssertion, out faultSequence);
                        commsTransition = hadResult && previousCommsOk != _lnbHealthCommsOk;
                    }

                    if (faultAssertion)
                    {
                        PublishMqttLnbFaultTransition(true, "health", faultSequence);
                        _lnbHealthPublishElapsedMs = 0;
                    }

                    if (commsTransition)
                    {
                        PublishMqttLnbHealthEvent(
                            _lnbHealthCommsOk ? "restored" : "lost",
                            _lnbHealthCheckSequence,
                            _lnbHealthResult);
                    }

                    if (!faultAssertion && (changed || _lnbHealthPublishElapsedMs >= LnbHealthStateRefreshMs))
                    {
                        PublishMqttState();
                        _lnbHealthPublishElapsedMs = 0;
                    }

                    delayMs = CalculateLnbHealthDelay();
                }
                catch (Exception ex)
                {
                    WriteStructuredDebug(
                        "LNB",
                        "schema=1 subsystem=lnb component=health operation=check status=error" +
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

        private static bool CheckLnbHealth(out bool faultAssertion, out int faultSequence)
        {
            faultAssertion = false;
            faultSequence = 0;
            bool hadResult = _lnbHealthHasResult;
            bool previousCommsOk = _lnbHealthCommsOk;
            string previousState = _lnbHealthState;
            int previousResult = _lnbHealthResult;
            int previousS1 = _lnbHealthS1;
            int previousS2 = _lnbHealthS2;
            int previousD1 = _lnbHealthD1;
            int previousD2 = _lnbHealthD2;
            int previousD3 = _lnbHealthD3;
            int previousD4 = _lnbHealthD4;

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
                "schema=1 subsystem=lnb component=health operation=check" +
                " status=" + _lnbHealthState +
                " sequence=" + _lnbHealthCheckSequence.ToString() +
                " rc=" + result.ToString() +
                " failures=" + _lnbHealthConsecutiveFailures.ToString() +
                " s1=" + ToHexU8(s1) +
                " s2=" + ToHexU8(s2) +
                " level=debug");

            return !hadResult ||
                previousCommsOk != _lnbHealthCommsOk ||
                previousState != _lnbHealthState ||
                previousResult != _lnbHealthResult ||
                previousS1 != _lnbHealthS1 ||
                previousS2 != _lnbHealthS2 ||
                previousD1 != _lnbHealthD1 ||
                previousD2 != _lnbHealthD2 ||
                previousD3 != _lnbHealthD3 ||
                previousD4 != _lnbHealthD4;
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