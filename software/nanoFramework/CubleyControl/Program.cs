using System;
using System.Diagnostics;
using System.Device.Gpio;
using System.Threading;
using Cubley.Interop;

namespace CubleyControl
{
    public static partial class Program
    {
        private const int HeartbeatIntervalMs = 10_000;
        private const int MainLoopSleepMs = 1000;
        private const int LedPulseMs = 100;
        private const int UsbConsoleReadTimeoutMs = 50;
        private const int UsbConsoleIdleSleepMs = 100;
        private const int UsbConsoleStatusIntervalMs = 1000;
        private const int UsbConsoleHealthLogIntervalLoops = 50;
        private const int UsbConsoleLineMaxLength = 64;
        private const int UsbWriteLogEveryNEvents = 20;
        private const int LnbChannelA = 0;

        // Candidate encodings for PB0 across providers/schemes.
        private static readonly int[] LedPinCandidates = { 16, 0 };

        private static GpioController _gpio;
        private static int _ledPin = -1;
        private static bool _ledReady;
        private static string _consoleLine = string.Empty;
        private static int _usbWriteFailureCount;
        private static int _usbWritePartialCount;
        private static int _usbWriteExceptionCount;
        private static int _cdcPreEnabledCount;
        private static int _cdcPostEnabledCount;
        private static int _requestId;
        private static int _responseTick;
        private static string _activeCommand = string.Empty;
        private static bool _watchEnabled;
        private static int _watchElapsedMs;

        public static void Main()
        {
            _ledReady = TryInitializeStatusLed();
            InitializeLnbSafeDefaults();

            var heartbeatThread = new Thread(HeartbeatLoop);
            heartbeatThread.Start();

            var usbConsoleThread = new Thread(UsbConsoleLoop);
            usbConsoleThread.Start();
            while (true)
            {
                Thread.Sleep(MainLoopSleepMs);
            }
        }

        private static void HeartbeatLoop()
        {
            while (true)
            {
                Debug.WriteLine("alive");
                Debug.WriteLine(
                    "[CDC-MON] pre=" + _cdcPreEnabledCount.ToString() +
                    " post=" + _cdcPostEnabledCount.ToString() +
                    " fail=" + _usbWriteFailureCount.ToString() +
                    " partial=" + _usbWritePartialCount.ToString() +
                    " ex=" + _usbWriteExceptionCount.ToString());

                if (_ledReady)
                {
                    try
                    {
                        _gpio.Write(_ledPin, PinValue.High);
                        Thread.Sleep(LedPulseMs);
                        _gpio.Write(_ledPin, PinValue.Low);
                    }
                    catch
                    {
                        _ledReady = false;
                    }
                }

                Thread.Sleep(HeartbeatIntervalMs);
            }
        }

        private static void UsbConsoleLoop()
        {
            Debug.WriteLine("[CDC] thread started");
            try
            {
                UsbConsoleLoopBody();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[CDC] FATAL thread exception: " + ex.Message);
            }
        }

        private static void UsbConsoleLoopBody()
        {
            bool wasEnabled = false;
            int healthLoop = 0;

            while (true)
            {
                healthLoop++;
                _cdcPreEnabledCount++;
                int enabled = UsbCdcConsole.NativeIsEnabled();
                _cdcPostEnabledCount++;

                if ((healthLoop % UsbConsoleHealthLogIntervalLoops) == 0)
                {
                    Debug.WriteLine(
                        "[CDC] health enabled=" + enabled.ToString() +
                        " fail=" + _usbWriteFailureCount.ToString() +
                        " partial=" + _usbWritePartialCount.ToString() +
                        " ex=" + _usbWriteExceptionCount.ToString());
                }

                if (enabled == 0)
                {
                    wasEnabled = false;
                    _watchElapsedMs = 0;
                    _consoleLine = string.Empty;
                    Thread.Sleep(UsbConsoleIdleSleepMs);
                    continue;
                }

                if (!wasEnabled)
                {
                    wasEnabled = true;
                    _watchElapsedMs = 0;
                    _consoleLine = string.Empty;
                    int rc = SafeUsbWrite("\r\nCubley USB CDC console ready. Type 'help'.\r\n> ");
                    Debug.WriteLine("[CDC] connected, banner rc=" + rc.ToString());
                }

                int value = UsbCdcConsole.NativeReadByte(UsbConsoleReadTimeoutMs);
                if (value < 0)
                {
                    _watchElapsedMs += UsbConsoleReadTimeoutMs + UsbConsoleIdleSleepMs;
                    if (_watchEnabled && _watchElapsedMs >= UsbConsoleStatusIntervalMs)
                    {
                        _watchElapsedMs = 0;
                        EmitStatusBar(enabled);
                    }

                    Thread.Sleep(UsbConsoleIdleSleepMs);
                    continue;
                }

                _watchElapsedMs = 0;

                char c = (char)value;

                if (c == '\r' || c == '\n')
                {
                    SafeUsbWrite("\r\n");
                    HandleConsoleCommand(_consoleLine);
                    _consoleLine = string.Empty;
                    SafeUsbWrite("> ");
                    continue;
                }

                if (c == '\b' || c == 127)
                {
                    if (_consoleLine.Length > 0)
                    {
                        _consoleLine = _consoleLine.Substring(0, _consoleLine.Length - 1);
                        SafeUsbWrite("\b \b");
                    }
                    continue;
                }

                if (_consoleLine.Length >= UsbConsoleLineMaxLength)
                {
                    continue;
                }

                if (c >= ' ' && c <= '~')
                {
                    _consoleLine += c.ToString();
                    SafeUsbWrite(c.ToString());
                }
            }
        }

        private static bool TrySetLed(PinValue value)
        {
            if (!_ledReady)
            {
                return false;
            }

            try
            {
                _gpio.Write(_ledPin, value);
                return true;
            }
            catch
            {
                _ledReady = false;
                return false;
            }
        }

        private static int SafeUsbWrite(string text)
        {
            if (text == null || text.Length == 0)
            {
                return 0;
            }

            try
            {
                int expected = text.Length;
                int written = UsbCdcConsole.NativeWrite(text);

                if (written == expected)
                {
                    return written;
                }

                if (written == 0)
                {
                    return written;
                }

                if (written < 0)
                {
                    _usbWriteFailureCount++;
                }
                else
                {
                    _usbWritePartialCount++;
                }

                int eventCount = _usbWriteFailureCount + _usbWritePartialCount + _usbWriteExceptionCount;
                if (eventCount == 1 || (eventCount % UsbWriteLogEveryNEvents) == 0)
                {
                    Debug.WriteLine(
                        "[CDC] write issue rc=" + written.ToString() +
                        " len=" + expected.ToString() +
                        " fail=" + _usbWriteFailureCount.ToString() +
                        " partial=" + _usbWritePartialCount.ToString() +
                        " ex=" + _usbWriteExceptionCount.ToString());
                }

                return written;
            }
            catch (Exception ex)
            {
                _usbWriteExceptionCount++;

                if (_usbWriteExceptionCount == 1 || (_usbWriteExceptionCount % UsbWriteLogEveryNEvents) == 0)
                {
                    Debug.WriteLine("[CDC] write exception: " + ex.Message);
                }

                return -1;
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
