using System;
using System.Diagnostics;
using System.Device.Gpio;
using System.Device.I2c;
using System.Threading;

namespace FramBringup
{
    public static class Program
    {
        private const int StatusLedPin = 2;
        private const int FramI2cBus = 3;
        private const int FramBaseAddress = 0x50;
        private const int FramBlockCount = 8;
        private const int FramSizeBytes = 2048;
        private const int FramTestAddress = 2046;

        private const int FailCodeRawRead = 1;
        private const int FailCodeWriteProbe = 2;
        private const int FailCodeVerifyRead = 3;
        private const int FailCodeValueMismatch = 4;
        private const int FailCodeRestoreBaseline = 5;
        private const int FailCodeWriteIgnored = 6;

        private static GpioController _gpio;
        private static I2cDevice _framDevice;
        private static byte _framOffset;

        public static void Main()
        {
            if (!InitializeLed())
            {
                Debug.WriteLine("[LED] Failed to initialize status LED");
            }

            Debug.WriteLine("[FRAM] Bring-up test starting");

            // Startup signature so we can confirm managed code execution before I2C operations.
            Blink(3, 100, 100);

            int failCode = 0;

            try
            {
                if (!InitializeFramDevice(out string deviceError))
                {
                    Debug.WriteLine("[FRAM] Device init failed: " + deviceError);
                    failCode = FailCodeRawRead;
                }

                if (failCode != 0)
                {
                    throw new InvalidOperationException("FRAM device init failed");
                }

                StageMarker(1); // About to perform baseline read.
                if (!TryReadByte(out byte baseline, out string rawError))
                {
                    Debug.WriteLine("[FRAM] Raw read failed: " + rawError);
                    failCode = FailCodeRawRead;
                }
                else
                {
                    StageMarker(2); // Baseline read succeeded.
                    byte probe = (byte)(baseline ^ 0x5A);
                    Debug.WriteLine("[FRAM] Baseline byte @" + FramTestAddress + " = 0x" + baseline.ToString("X2"));

                    StageMarker(3); // About to write probe.
                    if (!TryWriteByte(probe, out string writeError))
                    {
                        Debug.WriteLine("[FRAM] Write probe failed: " + writeError);
                        failCode = FailCodeWriteProbe;
                    }

                    if (failCode == 0)
                    {
                        StageMarker(4); // Probe write succeeded.
                        if (!TryReadByte(out byte verify, out string verifyError))
                        {
                            Debug.WriteLine("[FRAM] Verify read failed: " + verifyError);
                            failCode = FailCodeVerifyRead;
                        }

                        if (failCode == 0)
                        {
                            StageMarker(5); // Verify read succeeded.

                            if (verify != probe)
                            {
                                if (verify == baseline)
                                {
                                    Debug.WriteLine("[FRAM] Write appears ignored (value unchanged at 0x" + verify.ToString("X2") + ")");
                                    failCode = FailCodeWriteIgnored;
                                }
                                else
                                {
                                    Debug.WriteLine("[FRAM] Value mismatch. Expected 0x" + probe.ToString("X2") + " got 0x" + verify.ToString("X2"));
                                    failCode = FailCodeValueMismatch;
                                }
                            }
                        }

                        if (failCode == 0)
                        {
                            StageMarker(6); // Verify value matched.

                            if (!TryWriteByte(baseline, out string restoreError))
                            {
                                Debug.WriteLine("[FRAM] Restore baseline failed: " + restoreError);
                                failCode = FailCodeRestoreBaseline;
                            }
                        }

                        if (failCode == 0)
                        {
                            StageMarker(7); // Baseline restore succeeded.
                            Debug.WriteLine("[FRAM] Baseline restored, test PASS");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[FRAM] Exception: " + ex.Message);
                failCode = FailCodeRawRead;
            }

            if (failCode == 0)
            {
                Debug.WriteLine("[LED] PASS pattern");
                RunPassLoop();
            }
            else
            {
                Debug.WriteLine("[LED] FAIL code: " + failCode);
                RunFailLoop(failCode);
            }
        }

        private static bool InitializeFramDevice(out string error)
        {
            error = null;

            try
            {
                GetDeviceAndOffset(FramTestAddress, out int deviceAddress, out _framOffset);
                _framDevice = new I2cDevice(new I2cConnectionSettings(FramI2cBus, deviceAddress, I2cBusSpeed.StandardMode));
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryWriteByte(byte value, out string error)
        {
            error = null;

            try
            {
                _framDevice.Write(new byte[] { _framOffset, value });
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryReadByte(out byte value, out string error)
        {
            value = 0;
            error = null;

            try
            {
                var writeBuffer = new byte[] { _framOffset };
                var readBuffer = new byte[1];

                _framDevice.WriteRead(writeBuffer, readBuffer);
                value = readBuffer[0];
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static void GetDeviceAndOffset(int address, out int deviceAddress, out byte offset)
        {
            int block = (address >> 8) & 0x07;
            if (block < 0 || block >= FramBlockCount)
            {
                throw new ArgumentOutOfRangeException(nameof(address));
            }

            deviceAddress = FramBaseAddress + block;
            offset = (byte)(address & 0xFF);
        }

        private static bool InitializeLed()
        {
            try
            {
                _gpio = new GpioController();
                _gpio.OpenPin(StatusLedPin, PinMode.Output);
                _gpio.SetPinMode(StatusLedPin, PinMode.Output);
                _gpio.Write(StatusLedPin, PinValue.Low);
                Debug.WriteLine("[LED] Initialized on PA2");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[LED] Initialization failed: " + ex.Message);
                return false;
            }
        }

        private static void RunPassLoop()
        {
            while (true)
            {
                // PASS latch: one long + two short, then heartbeat cadence.
                Pulse(800, 250);
                Pulse(180, 180);
                Pulse(180, 600);

                Pulse(500, 500);
                Pulse(500, 1000);
            }
        }

        private static void RunFailLoop(int code)
        {
            if (code < 1)
            {
                code = 1;
            }

            while (true)
            {
                // Clear separator before each fail code burst.
                Pulse(900, 900);

                for (int i = 0; i < code; i++)
                {
                    Pulse(280, 280);
                }

                Thread.Sleep(3500);
            }
        }

        private static void Blink(int pulses, int onMs, int offMs)
        {
            for (int i = 0; i < pulses; i++)
            {
                Pulse(onMs, offMs);
            }
        }

        private static void StageMarker(int stage)
        {
            if (stage < 1)
            {
                stage = 1;
            }

            // Distinct short pulse count before each critical step.
            Blink(stage, 140, 220);
            Thread.Sleep(900);
        }

        private static void Pulse(int onMs, int offMs)
        {
            if (_gpio == null)
            {
                Thread.Sleep(onMs + offMs);
                return;
            }

            _gpio.Write(StatusLedPin, PinValue.High);
            Thread.Sleep(onMs);
            _gpio.Write(StatusLedPin, PinValue.Low);
            Thread.Sleep(offMs);
        }

    }
}
