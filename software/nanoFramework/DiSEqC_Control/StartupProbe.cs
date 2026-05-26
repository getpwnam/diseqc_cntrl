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
        private const int FramBusId = 3;
        private const int FramAddress = 0x50;

        private const uint ProbeStageBase = 0xD5E10000u;
        private const uint ProbeStageW5500 = 0x0100u;
        private const uint ProbeStageLnb = 0x0200u;
        private const uint ProbeStageFram = 0x0300u;

        public static void Main()
        {
            // Sentinel: very first managed instruction. If mailbox shows this,
            // Main() is reachable and the CLR started the app successfully.
            Cubley.Interop.BringupStatus.NativeSet(0xD5AA0001u);

            // Keep a direct Runtime.Events type reference so metadata processing
            // includes the managed assembly required by System.Device.Gpio.
            _ = typeof(NativeEventDispatcher);

            Cubley.Interop.BringupStatus.NativeSet(0xD5AA0002u);

            byte probeBitmap = RunHardwarePresenceProbes();
            Debug.WriteLine("[probe] bitmap=0x" + probeBitmap.ToString("X2") + " (bit0=W5500, bit1=LNBH26, bit2=FRAM)");

            Cubley.Interop.DiagnosticsMailbox.NativeTryLatchBootProbe(0xD5E20000u | probeBitmap);

            Program.MainApp(HardwareCapabilities.FromBitmap(probeBitmap));
        }

        private static byte RunHardwarePresenceProbes()
        {
            byte bitmap = 0;

            WriteProbeStage(ProbeStageW5500, 0x01);
            if (ProbeW5500())
            {
                bitmap |= HardwareCapabilities.W5500Bit;
                WriteProbeStage(ProbeStageW5500, 0x11);
            }
            else
            {
                WriteProbeStage(ProbeStageW5500, 0x10);
            }

            WriteProbeStage(ProbeStageLnb, 0x01);
            if (ProbeLnbh26())
            {
                bitmap |= HardwareCapabilities.LnbBit;
                WriteProbeStage(ProbeStageLnb, 0x11);
            }
            else
            {
                WriteProbeStage(ProbeStageLnb, 0x10);
            }

            WriteProbeStage(ProbeStageFram, 0x01);
            if (ProbeFram())
            {
                bitmap |= HardwareCapabilities.FramBit;
                WriteProbeStage(ProbeStageFram, 0x11);
            }
            else
            {
                WriteProbeStage(ProbeStageFram, 0x10);
            }

            WriteProbeStage(0x0400u, bitmap);
            return bitmap;
        }

        private static void WriteProbeStage(uint stageId, uint value)
        {
            Cubley.Interop.BringupStatus.NativeSet(ProbeStageBase | stageId | (value & 0xFFu));

            // Keep each marker visible long enough for SWD polling during bring-up.
            Thread.Sleep(75);
        }

        private static bool ProbeW5500()
        {
            try
            {
                int socketHandle;
                int openStatus = Cubley.Interop.W5500Socket.NativeOpen(out socketHandle);
                if (openStatus != (int)Cubley.Interop.W5500Socket.Status.Ok)
                {
                    Debug.WriteLine("[probe] W5500 open_status=" + openStatus + " present=false");
                    return false;
                }

                uint version = Cubley.Interop.W5500Socket.NativeGetVersion();
                bool present = (version & 0xFFu) == 0x04u;

                if (socketHandle >= 0)
                {
                    Cubley.Interop.W5500Socket.NativeClose(socketHandle);
                }

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
            I2cDevice device = null;

            try
            {
                device = new I2cDevice(new I2cConnectionSettings(LnbBusId, LnbAddress));

                // Probe by reading status register (0x01); ACK on address+register is sufficient for presence.
                byte[] reg = new byte[1] { 0x01 };
                byte[] status = new byte[1];
                device.WriteRead(reg, status);

                Debug.WriteLine("[probe] LNBH26 I2C" + LnbBusId + " addr=0x" + LnbAddress.ToString("X2") + " present=true");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[probe] LNBH26 exception: " + ex.Message);
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
