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
            //   behavior is successful non-throwing execution.
            // - UsbCdcConsole (Tier-1): NativeIsEnabled/NativeReadByte(0)/NativeWrite(...)
            //   are invoked; expected read result >= -1 (timeout is -1) and write >= 0 when USB
            //   CDC is enabled.
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
                // Tier-1 smoke: LED and USB console interop should execute without exceptions.
                bool tier1Pass = true;
                BringupStatus.NativeSet(Compose(StageTier1, ResultEnter, detail));

                StatusLed.NativeInit();
                StatusLed.NativeSetLow();
                StatusLed.NativePulse(1, 50);
                detail |= 0x10;

                bool usbEnabled = UsbCdcConsole.NativeIsEnabled();
                int readByte = UsbCdcConsole.NativeReadByte(0);
                int writeRc = usbEnabled ? UsbCdcConsole.NativeWrite("CubleySmokeTier0 ready\r\n") : 0;

                // Read timeout (-1) is valid for immediate mode; only enforce minimal call success.
                if (readByte >= -1)
                {
                    detail |= 0x20;
                }
                else
                {
                    tier1Pass = false;
                }

                if (!usbEnabled || writeRc >= 0)
                {
                    detail |= 0x40;
                }
                else
                {
                    tier1Pass = false;
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
