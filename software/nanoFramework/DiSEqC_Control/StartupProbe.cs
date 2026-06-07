using System;
using System.Threading;
using System.Device.Gpio;
using System.Device.I2c;
using System.Diagnostics;
using nanoFramework.Runtime.Events;

namespace DiSEqC_Control
{
    public static class StartupProbe
    {
        private const int LedPin = 2;
        private const int LnbBusId = 1;
        private const int LnbAddress = 0x08;
        private const int LnbSentinelEmptyAddress = 0x7A;
        private const int FramBusId = 3;
        private const int FramAddress = 0x50;
        private static bool ProbeW5500OnStartup = true;
        private static bool ProbeFramOnStartup = true;

        private const byte StageManagedEntry = 0xE0;
        private const byte StageW5500 = 0xE1;
        private const byte StageLnbh26 = 0xE2;
        private const byte StageFram = 0xE3;
        private const byte StagePhaseAAggregate = 0xEF;
        private const byte StageBootProbeAggregate = 0xF0;

        public static void Main()
        {
            // Sentinel: very first managed instruction.
            WriteProbeMarker(StageManagedEntry, DiagnosticsStatusWord.ResultRunning, 0x01);

            // Keep a direct Runtime.Events type reference so metadata processing
            // includes the managed assembly required by System.Device.Gpio.
            _ = typeof(NativeEventDispatcher);

            WriteProbeMarker(StageManagedEntry, DiagnosticsStatusWord.ResultPass, 0x02);

            byte aggregateResult;
            byte probeBitmap = RunHardwarePresenceProbes(out aggregateResult);
            Debug.WriteLine("[probe] bitmap=0x" + probeBitmap.ToString("X2") + " (bit0=W5500, bit1=LNBH26, bit2=FRAM)");

            uint aggregateWord = DiagnosticsStatusWord.Compose(StageBootProbeAggregate, aggregateResult, probeBitmap);
            Cubley.Interop.DiagnosticsMailbox.NativeTryLatchBootProbe(aggregateWord);
            WriteProbeMarker(StagePhaseAAggregate, aggregateResult, probeBitmap);

            Program.MainApp(HardwareCapabilities.FromBitmap(probeBitmap));
        }

        private static byte RunHardwarePresenceProbes(out byte aggregateResult)
        {
            byte bitmap = 0;
            byte failureCount = 0;
            byte skippedCount = 0;

            if (ProbeW5500OnStartup)
            {
                WriteProbeMarker(StageW5500, DiagnosticsStatusWord.ResultRunning, 0x01);
                if (ProbeW5500())
                {
                    bitmap |= HardwareCapabilities.W5500Bit;
                    WriteProbeMarker(StageW5500, DiagnosticsStatusWord.ResultPass, HardwareCapabilities.W5500Bit);
                }
                else
                {
                    failureCount++;
                    WriteProbeMarker(StageW5500, DiagnosticsStatusWord.ResultFail, 0x00);
                }
            }
            else
            {
                // Known bring-up mode: skip W5500 probe so LNB/FRAM startup is not gated by SPI path.
                skippedCount++;
                WriteProbeMarker(StageW5500, DiagnosticsStatusWord.ResultWarn, 0x01);
                Debug.WriteLine("[probe] W5500 startup probe skipped by configuration.");
            }

            WriteProbeMarker(StageLnbh26, DiagnosticsStatusWord.ResultRunning, 0x01);
            if (ProbeLnbh26())
            {
                bitmap |= HardwareCapabilities.LnbBit;
                WriteProbeMarker(StageLnbh26, DiagnosticsStatusWord.ResultPass, HardwareCapabilities.LnbBit);
            }
            else
            {
                failureCount++;
                WriteProbeMarker(StageLnbh26, DiagnosticsStatusWord.ResultFail, 0x00);
            }

            if (ProbeFramOnStartup)
            {
                WriteProbeMarker(StageFram, DiagnosticsStatusWord.ResultRunning, 0x01);
                if (ProbeFram())
                {
                    bitmap |= HardwareCapabilities.FramBit;
                    WriteProbeMarker(StageFram, DiagnosticsStatusWord.ResultPass, HardwareCapabilities.FramBit);
                }
                else
                {
                    failureCount++;
                    WriteProbeMarker(StageFram, DiagnosticsStatusWord.ResultFail, 0x00);
                }
            }
            else
            {
                skippedCount++;
                WriteProbeMarker(StageFram, DiagnosticsStatusWord.ResultWarn, 0x01);
                Debug.WriteLine("[probe] FRAM startup probe skipped by configuration.");
            }

            aggregateResult = DiagnosticsStatusWord.ComputeAggregateResult(failureCount, skippedCount);
            return bitmap;
        }

        private static void WriteProbeMarker(byte stage, byte result, byte detail)
        {
            Cubley.Interop.BringupStatus.NativeSet(DiagnosticsStatusWord.Compose(stage, result, detail));

            // Keep each marker visible long enough for SWD polling during bring-up.
            Thread.Sleep(75);
        }

        private static bool ProbeW5500()
        {
            try
            {
                uint version = Cubley.Interop.W5500Socket.NativeGetVersion();
                bool present = (version & 0xFFu) == 0x04u;

                Debug.WriteLine("[probe] W5500 version=0x" + version.ToString("X2") + " present=" + present);
                return present;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[probe] W5500 exception: " + ex.Message);
                return false;
            }
        }

        private static bool ProbeLnbh26()
        {
            byte lnbStatus = 0;
            byte sentinelValue = 0;

            try
            {
                if (!TryReadI2cRegisterByte(LnbBusId, LnbAddress, 0x01, out lnbStatus))
                {
                    Debug.WriteLine("[probe] LNBH26 I2C" + LnbBusId + " addr=0x" + LnbAddress.ToString("X2") + " present=false (no ACK/read failure)");
                    return false;
                }

                // LNBH26 status register uses only low 3 bits.
                if ((lnbStatus & 0xF8) != 0)
                {
                    Debug.WriteLine("[probe] LNBH26 status=0x" + lnbStatus.ToString("X2") + " invalid bit pattern; treating as absent");
                    return false;
                }

                // If an intentionally empty sentinel address also ACKs on the same bus,
                // the bus/probe path is likely unreliable for presence detection.
                if (TryReadI2cRegisterByte(LnbBusId, LnbSentinelEmptyAddress, 0x00, out sentinelValue))
                {
                    Debug.WriteLine("[probe] LNBH26 false-positive guard tripped: sentinel addr 0x" + LnbSentinelEmptyAddress.ToString("X2") + " also ACKed (value=0x" + sentinelValue.ToString("X2") + ")");
                    return false;
                }

                Debug.WriteLine("[probe] LNBH26 I2C" + LnbBusId + " addr=0x" + LnbAddress.ToString("X2") + " status=0x" + lnbStatus.ToString("X2") + " present=true");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[probe] LNBH26 exception: " + ex.Message);
                return false;
            }
        }

        private static bool TryReadI2cRegisterByte(int busId, int address, byte reg, out byte value)
        {
            I2cDevice device = null;
            value = 0;

            try
            {
                device = new I2cDevice(new I2cConnectionSettings(busId, address));
                byte[] writeBuffer = new byte[1] { reg };
                byte[] readBuffer = new byte[1];
                device.WriteRead(writeBuffer, readBuffer);
                value = readBuffer[0];
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (device != null)
                {
                    device.Dispose();
                }
            }
        }

        private static bool ProbeFram()
        {
            I2cDevice device = null;

            try
            {
                device = new I2cDevice(new I2cConnectionSettings(FramBusId, FramAddress));

                byte[] address = new byte[1] { 0x00 };
                byte[] data = new byte[1];
                device.WriteRead(address, data);

                Debug.WriteLine("[probe] FRAM I2C" + FramBusId + " addr=0x" + FramAddress.ToString("X2") + " present=true");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[probe] FRAM I2C" + FramBusId + " exception: " + ex.Message);
                return false;
            }
            finally
            {
                if (device != null)
                {
                    device.Dispose();
                }
            }
        }
    }
}
