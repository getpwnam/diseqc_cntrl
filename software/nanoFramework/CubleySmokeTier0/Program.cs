using System;
using System.Threading;
using Cubley.Interop;

namespace CubleySmokeTier0
{
    public static class Program
    {
        private const byte ResultEnter = 0;
        private const byte ResultPass = 1;
        private const byte ResultWarn = 2;
        private const byte ResultFail = 14;

        private const byte StageStart = 0xC0;
        private const byte StageTier0 = 0xC1;
        private const byte StageTier1 = 0xC2;
        private const byte StageFinal = 0xCF;
        private const byte StageTier1IterationBase = 0xD0;

        private const uint LatchWord = 0xD5F00111;
        private const uint LatchOverwriteAttemptWord = 0xD5F00E22;

        public static void Main()
        {
            // Smoke contract for firmware-first interop validation:
            // - BringupStatus (Tier-0): write 0xD5SSRRDD markers with NativeSet(), then
            //   verify NativeGet() returns the exact marker value for round-trip integrity.
            // - DiagnosticsMailbox (Tier-0): write LatchWord (0xD5F00111) via
            //   NativeTryLatchBootProbe(); expected first call=true and NativeGetBootProbe()==LatchWord.
            //   Then attempt overwrite with LatchOverwriteAttemptWord (0xD5F00E22);
            //   expected second call=false and NativeGetBootProbe() unchanged (still LatchWord).
            // - StatusLed (Tier-1): NativeInit/NativeSetLow/NativePulse are invoked; expected
            //   behavior is successful non-throwing execution under repeated mixed-order calls.
            // - UsbCdcConsole (Tier-1): NativeIsEnabled/NativeReadByte(0)/NativeWrite(...)
            //   are invoked repeatedly; expected read result >= -1 (timeout is -1) and
            //   write >= 0 when USB CDC is enabled.
            // - Tier-1 safety rule: Tier-1 calls must not clobber boot-probe sticky latch;
            //   NativeGetBootProbe() must remain equal to LatchWord before/after each Tier-1 iteration.
            byte detail = 0;
            bool pass = true;

            BringupStatus.NativeSet(Compose(StageStart, ResultEnter, 0x01));

            try
            {
                // Tier-0 smoke: set/get + boot-probe latch one-shot semantics.
                BringupStatus.NativeSet(Compose(StageTier0, ResultEnter, 0x01));

                uint marker = Compose(StageTier0, ResultPass, 0x10);
                BringupStatus.NativeSet(marker);
                uint markerRead = BringupStatus.NativeGet();
                if (markerRead == marker)
                {
                    detail |= 0x01;
                }
                else
                {
                    pass = false;
                }

                bool firstLatched = DiagnosticsMailbox.NativeTryLatchBootProbe(LatchWord);
                uint bootAfterFirst = DiagnosticsMailbox.NativeGetBootProbe();
                if (firstLatched)
                {
                    detail |= 0x02;
                }
                else
                {
                    pass = false;
                }

                bool secondLatched = DiagnosticsMailbox.NativeTryLatchBootProbe(LatchOverwriteAttemptWord);
                uint bootAfterSecond = DiagnosticsMailbox.NativeGetBootProbe();
                if (!secondLatched)
                {
                    detail |= 0x04;
                }
                else
                {
                    pass = false;
                }

                if (bootAfterFirst == LatchWord && bootAfterSecond == LatchWord)
                {
                    detail |= 0x08;
                }
                else
                {
                    pass = false;
                }

                BringupStatus.NativeSet(Compose(StageTier0, pass ? ResultPass : ResultFail, detail));
            }
            catch
            {
                pass = false;
                BringupStatus.NativeSet(Compose(StageTier0, ResultFail, detail));
            }

            try
            {
                // Tier-1 smoke: repeated mixed-order LED/USB calls with sticky-slot preservation checks.
                bool tier1Pass = true;
                BringupStatus.NativeSet(Compose(StageTier1, ResultEnter, detail));

                StatusLed.NativeInit();
                StatusLed.NativeSetLow();
                StatusLed.NativePulse(1, 50);
                detail |= 0x10;

                uint bootProbeBeforeTier1 = DiagnosticsMailbox.NativeGetBootProbe();
                if (bootProbeBeforeTier1 != LatchWord)
                {
                    tier1Pass = false;
                }

                bool usbEnabled = UsbCdcConsole.NativeIsEnabled();
                if (usbEnabled)
                {
                    detail |= 0x20;
                }

                for (int i = 0; i < 6; i++)
                {
                    uint bootProbeBeforeIter = DiagnosticsMailbox.NativeGetBootProbe();
                    if (bootProbeBeforeIter != LatchWord)
                    {
                        tier1Pass = false;
                    }

                    // Emit deterministic per-iteration breadcrumbs for SWD diagnosis.
                    BringupStatus.NativeSet(Compose((byte)(StageTier1IterationBase + i), ResultEnter, (byte)i));

                    if ((i % 2) == 0)
                    {
                        StatusLed.NativeSetHigh();
                        int readByte = UsbCdcConsole.NativeReadByte(0);
                        StatusLed.NativeSetLow();
                        StatusLed.NativePulse(1, 20 + (i * 5));

                        // Read timeout (-1) is valid for immediate mode; require bounded sentinel semantics.
                        if (readByte < -1)
                        {
                            tier1Pass = false;
                        }
                    }
                    else
                    {
                        int writeRc = usbEnabled ? UsbCdcConsole.NativeWrite("CubleySmokeTier0 iter " + i.ToString() + "\r\n") : 0;
                        StatusLed.NativePulse(2, 15 + (i * 5));
                        int readByte = UsbCdcConsole.NativeReadByte(0);

                        if (readByte < -1)
                        {
                            tier1Pass = false;
                        }

                        if (usbEnabled && writeRc < 0)
                        {
                            tier1Pass = false;
                        }
                    }

                    uint bootProbeAfterIter = DiagnosticsMailbox.NativeGetBootProbe();
                    if (bootProbeAfterIter != LatchWord)
                    {
                        tier1Pass = false;
                    }
                }

                if (tier1Pass)
                {
                    detail |= 0x40;
                }

                BringupStatus.NativeSet(Compose(StageTier1, tier1Pass ? ResultPass : ResultFail, detail));
                pass = pass && tier1Pass;
            }
            catch
            {
                pass = false;
                BringupStatus.NativeSet(Compose(StageTier1, ResultFail, detail));
            }

            BringupStatus.NativeSet(Compose(StageFinal, pass ? ResultPass : ResultFail, detail));

            while (true)
            {
                Thread.Sleep(1000);
            }
        }

        private static uint Compose(byte stage, byte result, byte detail)
        {
            return ((uint)0xD5 << 24) | ((uint)stage << 16) | ((uint)result << 8) | detail;
        }
    }
}
