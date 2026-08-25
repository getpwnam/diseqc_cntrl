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
        private const int LnbFaultPollIntervalMs = 25;
        private const int LnbChannelA = 0;
        private const uint ResetCauseMarkerMask = 0xFF000000;
        private const uint ResetCauseMarkerValue = 0xCB000000;
        private const int ResetFlagBor = 1 << 0;
        private const int ResetFlagPin = 1 << 1;
        private const int ResetFlagPor = 1 << 2;
        private const int ResetFlagSft = 1 << 3;
        private const int ResetFlagIwdg = 1 << 4;
        private const int ResetFlagWwdg = 1 << 5;
        private const int ResetFlagLpwr = 1 << 6;

        // Candidate encodings for PB0 across providers/schemes.
        private static readonly int[] LedPinCandidates = { 16, 0 };
        // Candidate encodings for PC8 across providers/schemes.
        private static readonly int[] LnbFaultPinCandidates = { 40, 8 };

        private static GpioController _gpio;
        private static int _ledPin = -1;
        private static bool _ledReady;
        private static int _lnbFaultPin = -1;
        private static bool _lnbFaultReady;
        private static bool _lnbFaultInterruptEnabled;
        private static bool _lnbFaultAsserted;
        private static int _lnbFaultSequence;
        // Shared command execution across transports (serial + MQTT). All
        // commands funnel through ExecuteCommand -> HandleConsoleCommand ->
        // WriteCommandResult, which writes through whichever OutputSink is
        // active for the calling transport. The lock serializes command
        // execution across transports since Program's command-handling state
        // (LNB init status, DiSEqC TX-busy flag, etc.) is static and not
        // designed for concurrent access from two transport threads at once.
        public delegate void OutputSink(string line);
        private static readonly object _commandLock = new object();
        private static OutputSink _activeOutputSink;

        private static void ExecuteCommand(string command, OutputSink outputSink)
        {
            lock (_commandLock)
            {
                _activeOutputSink = outputSink;
                try
                {
                    HandleConsoleCommand(command);
                }
                finally
                {
                    _activeOutputSink = null;
                }
            }
        }

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
            EmitBootResetCauseLog();
            InitializeNetworkConfiguration();
            _ledReady = TryInitializeStatusLed();
            InitializeLnbSafeDefaults();
            InitializeLnbFaultMonitor();

            var heartbeatThread = new Thread(HeartbeatLoop);
            heartbeatThread.Start();

            if (_lnbFaultReady && !_lnbFaultInterruptEnabled)
            {
                var lnbFaultPollThread = new Thread(LnbFaultPollLoop);
                lnbFaultPollThread.Start();
            }

            var usbConsoleThread = new Thread(UsbConsoleLoop);
            usbConsoleThread.Start();

            var mqttThread = new Thread(MqttLoop);
            mqttThread.Start();

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

                if (_lnbFaultReady)
                {
                    TryProcessLnbFaultPin("heartbeat");
                }

                Thread.Sleep(HeartbeatIntervalMs);
            }
        }

        private static void LnbFaultPollLoop()
        {
            Debug.WriteLine("[LNB-FAULT] poll loop started pin=" + _lnbFaultPin.ToString());

            while (true)
            {
                TryProcessLnbFaultPin("poll");
                Thread.Sleep(LnbFaultPollIntervalMs);
            }
        }

        private static void EmitBootResetCauseLog()
        {
            uint diagWord = DiagMailbox.NativeGet();

            if ((diagWord & ResetCauseMarkerMask) != ResetCauseMarkerValue)
            {
                Debug.WriteLine("[BOOT] reset cause unavailable diag=0x" + diagWord.ToString("X8"));
                return;
            }

            int flags = (int)((diagWord >> 16) & 0xFFu);
            int csrLow = (int)(diagWord & 0xFFFFu);

            Debug.WriteLine(
                "[BOOT] reset flags=" + ResetFlagsToText(flags) +
                " csr_low=0x" + csrLow.ToString("X4"));
        }

        private static string ResetFlagsToText(int flags)
        {
            string text = string.Empty;

            AppendResetFlagIfSet(ref text, flags, ResetFlagBor, "BOR");
            AppendResetFlagIfSet(ref text, flags, ResetFlagPin, "PIN");
            AppendResetFlagIfSet(ref text, flags, ResetFlagPor, "POR");
            AppendResetFlagIfSet(ref text, flags, ResetFlagSft, "SFT");
            AppendResetFlagIfSet(ref text, flags, ResetFlagIwdg, "IWDG");
            AppendResetFlagIfSet(ref text, flags, ResetFlagWwdg, "WWDG");
            AppendResetFlagIfSet(ref text, flags, ResetFlagLpwr, "LPWR");

            if (text.Length == 0)
            {
                return "none";
            }

            return text;
        }

        private static void AppendResetFlagIfSet(ref string text, int flags, int mask, string label)
        {
            if ((flags & mask) == 0)
            {
                return;
            }

            if (text.Length > 0)
            {
                text += ",";
            }

            text += label;
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
                    // Do NOT consume the enable transition until the banner has
                    // actually been written. On a fresh USB connection the output
                    // queue may not be draining yet, so a single write can return 0
                    // and the banner/prompt would be lost forever. Retry each loop
                    // iteration until the write succeeds.
                    string banner = "\r\nCubley USB CDC console ready. Type 'help'.\r\n> ";
                    int rc = SafeUsbWrite(banner);
                    uint diag = DiagMailbox.NativeGet();
                    Debug.WriteLine("[CDC] connected, banner rc=" + rc.ToString() +
                        " diag=0x" + diag.ToString("X8"));

                    if (rc < banner.Length)
                    {
                        // Banner not fully written yet; keep wasEnabled false so we
                        // retry on the next iteration once the queue can drain.
                        Thread.Sleep(UsbConsoleIdleSleepMs);
                        continue;
                    }

                    wasEnabled = true;
                    _watchElapsedMs = 0;
                    _consoleLine = string.Empty;
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
                    ExecuteCommand(_consoleLine, WriteSerialLine);
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

        private static void WriteSerialLine(string line)
        {
            SafeUsbWrite(line);
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
                if (_gpio == null)
                {
                    _gpio = new GpioController();
                }

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

        private static void InitializeLnbFaultMonitor()
        {
            try
            {
                if (_gpio == null)
                {
                    _gpio = new GpioController();
                }

                for (int i = 0; i < LnbFaultPinCandidates.Length; i++)
                {
                    int pin = LnbFaultPinCandidates[i];

                    try
                    {
                        if (_gpio.IsPinOpen(pin))
                        {
                            _gpio.ClosePin(pin);
                        }

                        if (_gpio.IsPinModeSupported(pin, PinMode.InputPullUp))
                        {
                            _gpio.OpenPin(pin, PinMode.InputPullUp);
                        }
                        else if (_gpio.IsPinModeSupported(pin, PinMode.Input))
                        {
                            _gpio.OpenPin(pin, PinMode.Input);
                        }
                        else
                        {
                            continue;
                        }

                        _lnbFaultPin = pin;
                        _lnbFaultReady = true;
                        break;
                    }
                    catch
                    {
                        // Try next candidate pin.
                    }
                }

                if (!_lnbFaultReady)
                {
                    Debug.WriteLine("[LNB-FAULT] monitor disabled; unable to open PC8 candidate pins");
                    return;
                }

                try
                {
                    _gpio.RegisterCallbackForPinValueChangedEvent(_lnbFaultPin, PinEventTypes.Falling, LnbFaultPinChanged);
                    _lnbFaultInterruptEnabled = true;
                }
                catch
                {
                    _lnbFaultInterruptEnabled = false;
                }

                Debug.WriteLine(
                    "[LNB-FAULT] monitor ready pin=" + _lnbFaultPin.ToString() +
                    " mode=" + (_lnbFaultInterruptEnabled ? "interrupt" : "poll"));

                // Capture startup asserted state if fault is already active.
                TryProcessLnbFaultPin("startup");
            }
            catch
            {
                _lnbFaultReady = false;
                _lnbFaultInterruptEnabled = false;
                Debug.WriteLine("[LNB-FAULT] monitor initialization failed");
            }
        }

        private static void LnbFaultPinChanged(object sender, PinValueChangedEventArgs args)
        {
            if (!_lnbFaultReady)
            {
                return;
            }

            if (args.ChangeType != PinEventTypes.Falling)
            {
                return;
            }

            TryProcessLnbFaultPin("irq");
        }

        private static void TryProcessLnbFaultPin(string source)
        {
            if (!_lnbFaultReady)
            {
                return;
            }

            PinValue value;
            try
            {
                value = _gpio.Read(_lnbFaultPin);
            }
            catch
            {
                return;
            }

            bool asserted = value == PinValue.Low;
            if (asserted)
            {
                if (!_lnbFaultAsserted)
                {
                    _lnbFaultAsserted = true;
                    _lnbFaultSequence++;
                    EmitLnbFaultSnapshot(source, _lnbFaultSequence);
                }

                return;
            }

            _lnbFaultAsserted = false;
        }
    }
}
