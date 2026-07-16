using System.Diagnostics;
using System.Device.Gpio;
using System.Threading;

namespace CubleyControl
{
    public static class Program
    {
        private const int HeartbeatIntervalMs = 10_000;
        private const int MainLoopSleepMs = 1000;
        private const int LedPulseMs = 100;

        // Candidate encodings for PB0 across providers/schemes.
        private static readonly int[] LedPinCandidates = { 16, 0 };

        private static GpioController _gpio;
        private static int _ledPin = -1;

        public static void Main()
        {
            var heartbeatThread = new Thread(HeartbeatLoop);
            heartbeatThread.Start();

            while (true)
            {
                Thread.Sleep(MainLoopSleepMs);
            }
        }

        private static void HeartbeatLoop()
        {
            bool ledReady = TryInitializeStatusLed();

            while (true)
            {
                Debug.WriteLine("alive");

                if (ledReady)
                {
                    try
                    {
                        _gpio.Write(_ledPin, PinValue.High);
                        Thread.Sleep(LedPulseMs);
                        _gpio.Write(_ledPin, PinValue.Low);
                    }
                    catch
                    {
                        ledReady = false;
                    }
                }

                Thread.Sleep(HeartbeatIntervalMs);
            }
        }

        private static bool TryInitializeStatusLed()
        {
            try
            {
                _gpio = new GpioController();

                for (int i = 0; i < LedPinCandidates.Length; i++)
                {
                    int pin = LedPinCandidates[i];

                    try
                    {
                        if (_gpio.IsPinOpen(pin))
                        {
                            _gpio.ClosePin(pin);
                        }

                        if (_gpio.IsPinModeSupported(pin, PinMode.Output))
                        {
                            _gpio.OpenPin(pin, PinMode.Output);
                        }
                        else if (_gpio.IsPinModeSupported(pin, PinMode.OutputOpenDrain))
                        {
                            _gpio.OpenPin(pin, PinMode.OutputOpenDrain);
                        }
                        else
                        {
                            continue;
                        }

                        _gpio.Write(pin, PinValue.Low);
                        _ledPin = pin;
                        return true;
                    }
                    catch
                    {
                        // Try next candidate pin.
                    }
                }
            }
            catch
            {
                // Ignore init exceptions; heartbeat logging can still run.
            }

            return false;
        }
    }
}
