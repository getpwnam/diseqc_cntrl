using System;
using System.Diagnostics;
using System.Device.Gpio;
using System.Threading;
using Cubley.Interop;

namespace CubleyControl
{
    public static class Program
    {
        private const int HeartbeatIntervalMs = 10_000;
        private const int MainLoopSleepMs = 1000;
        private const int LedPulseMs = 100;
        private const int UsbConsoleReadTimeoutMs = 50;
        private const int UsbConsoleIdleSleepMs = 100;
        private const int UsbConsoleBannerIntervalMs = 3000;
        private const int UsbConsoleHealthLogIntervalLoops = 50;
        private const int UsbConsoleLineMaxLength = 64;
        private const int UsbWriteLogEveryNEvents = 20;

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
        private static int _cdcPostSetCount;
        private static int _cdcPostEnabledCount;

        public static void Main()
        {
            _ledReady = TryInitializeStatusLed();

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
                uint mailbox = DiagMailbox.NativeGet();
                Debug.WriteLine(
                    "[CDC-MON] pre=" + _cdcPreEnabledCount.ToString() +
                    " postSet=" + _cdcPostSetCount.ToString() +
                    " post=" + _cdcPostEnabledCount.ToString() +
                    " mb=0x" + mailbox.ToString("X8") +
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
            int bannerElapsedMs = 0;
            int healthLoop = 0;

            while (true)
            {
                healthLoop++;
                _cdcPreEnabledCount++;

                // Managed-side breadcrumbs around NativeIsEnabled dispatch.
                // 0xA1000001 -> about to call NativeIsEnabled
                // 0xA1000002 -> NativeIsEnabled returned
                DiagMailbox.NativeSet(0xA1000001u);
                _cdcPostSetCount++;
                int enabled = UsbCdcConsole.NativeIsEnabled();
                DiagMailbox.NativeSet(0xA1000002u);
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
                    bannerElapsedMs = 0;
                    _consoleLine = string.Empty;
                    Thread.Sleep(UsbConsoleIdleSleepMs);
                    continue;
                }

                if (!wasEnabled)
                {
                    wasEnabled = true;
                    bannerElapsedMs = 0;
                    _consoleLine = string.Empty;
                    int rc = SafeUsbWrite("\r\nCubley USB CDC console ready. Type 'help'.\r\n> ");
                    Debug.WriteLine("[CDC] connected, banner rc=" + rc.ToString());
                }

                int value = UsbCdcConsole.NativeReadByte(UsbConsoleReadTimeoutMs);
                if (value < 0)
                {
                    bannerElapsedMs += UsbConsoleReadTimeoutMs + UsbConsoleIdleSleepMs;
                    if (bannerElapsedMs >= UsbConsoleBannerIntervalMs)
                    {
                        bannerElapsedMs = 0;
                        int rc = SafeUsbWrite("\r\nCubley USB CDC console ready. Type 'help'.\r\n> ");
                        if (rc < 0)
                        {
                            Debug.WriteLine("[CDC] periodic banner failed rc=" + rc.ToString());
                        }
                    }

                    Thread.Sleep(UsbConsoleIdleSleepMs);
                    continue;
                }

                bannerElapsedMs = 0;

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

        private static void HandleConsoleCommand(string command)
        {
            string trimmed = command == null ? string.Empty : command.Trim();
            string lower = trimmed.ToLower();

            if (lower.Length == 0)
            {
                return;
            }

            if (lower == "help")
            {
                SafeUsbWrite("Commands: help, status, led on, led off, pulse\r\n");
                return;
            }

            if (lower == "status")
            {
                SafeUsbWrite("LED: ");
                SafeUsbWrite(_ledReady ? "ready" : "not-ready");
                SafeUsbWrite("\r\n");
                return;
            }

            if (lower == "led on")
            {
                if (TrySetLed(PinValue.High))
                {
                    SafeUsbWrite("LED set high\r\n");
                }
                else
                {
                    SafeUsbWrite("LED unavailable\r\n");
                }
                return;
            }

            if (lower == "led off")
            {
                if (TrySetLed(PinValue.Low))
                {
                    SafeUsbWrite("LED set low\r\n");
                }
                else
                {
                    SafeUsbWrite("LED unavailable\r\n");
                }
                return;
            }

            if (lower == "pulse")
            {
                if (TrySetLed(PinValue.High))
                {
                    Thread.Sleep(LedPulseMs);
                    TrySetLed(PinValue.Low);
                    SafeUsbWrite("Pulse complete\r\n");
                }
                else
                {
                    SafeUsbWrite("LED unavailable\r\n");
                }
                return;
            }

            SafeUsbWrite("Unknown command. Type 'help'.\r\n");
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
                // Keep app alive if USB console writes fail transiently.
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
