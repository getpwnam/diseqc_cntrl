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
        private const int UsbConsoleLineMaxLength = 192;
        private const int UsbConsoleHistoryDepth = 4;
        private const int ConsoleIdleTimeoutMs = 10 * 60 * 1000;
        private const int ConsoleIdleWarningMs = 60 * 1000;
        private const int MqttCommandMaxLength = 64;
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
        private static bool _lnbFaultCheckPending;
        private static readonly object _lnbFaultTransitionLock = new object();
        // Shared command execution across transports (serial + MQTT). All
        // commands funnel through ExecuteCommand -> HandleConsoleCommand ->
        // WriteCommandResult, which writes through whichever OutputSink is
        // active for the calling transport. The lock serializes command
        // execution across transports since Program's command-handling state
        // (LNB init status, DiSEqC TX-busy flag, etc.) is static and not
        // designed for concurrent access from two transport threads at once.
        public delegate void OutputSink(string line);
        private enum CommandTransport
        {
            Usb,
            Mqtt
        }

        private enum ConsoleTransport
        {
            None,
            Usb
        }

        private static readonly object _commandLock = new object();
        private static readonly object _consoleLeaseLock = new object();
        private static OutputSink _activeOutputSink;
        private static CommandTransport _activeCommandTransport;
        private static ConsoleTransport _consoleLeaseOwner;
        private static int _consoleLeaseSessionId;
        private static int _nextConsoleLeaseSessionId;
        private static long _consoleLeaseLastActivityMs;

        private static void WriteStructuredDebug(string subsystem, string payload)
        {
            Debug.WriteLine("[" + subsystem + "] " + payload);
        }

        private static void ExecuteCommand(string command, OutputSink outputSink, CommandTransport transport)
        {
            lock (_commandLock)
            {
                BeginLnbIoOperation();
                _activeOutputSink = outputSink;
                _activeCommandTransport = transport;
                try
                {
                    lock (_lnbIoLock)
                    {
                        HandleConsoleCommand(command);
                    }
                }
                finally
                {
                    _activeOutputSink = null;
                    EndLnbIoOperation();
                }
            }
        }

        private static bool TryAcquireConsoleLease(ConsoleTransport transport, out int sessionId)
        {
            lock (_consoleLeaseLock)
            {
                if (_consoleLeaseOwner != ConsoleTransport.None)
                {
                    sessionId = 0;
                    return false;
                }

                _nextConsoleLeaseSessionId++;
                if (_nextConsoleLeaseSessionId <= 0)
                {
                    _nextConsoleLeaseSessionId = 1;
                }

                _consoleLeaseOwner = transport;
                _consoleLeaseSessionId = _nextConsoleLeaseSessionId;
                _consoleLeaseLastActivityMs = Environment.TickCount64;
                sessionId = _consoleLeaseSessionId;
                return true;
            }
        }

        private static bool TryGetConsoleLeaseIdleMs(ConsoleTransport transport, int sessionId, out long idleMs)
        {
            lock (_consoleLeaseLock)
            {
                if (_consoleLeaseOwner != transport || _consoleLeaseSessionId != sessionId)
                {
                    idleMs = 0;
                    return false;
                }

                idleMs = Environment.TickCount64 - _consoleLeaseLastActivityMs;
                return true;
            }
        }

        private static bool TouchConsoleLease(ConsoleTransport transport, int sessionId, out bool expired)
        {
            lock (_consoleLeaseLock)
            {
                expired = false;
                if (_consoleLeaseOwner != transport || _consoleLeaseSessionId != sessionId)
                {
                    return false;
                }

                long nowMs = Environment.TickCount64;
                if (nowMs - _consoleLeaseLastActivityMs >= ConsoleIdleTimeoutMs)
                {
                    _consoleLeaseOwner = ConsoleTransport.None;
                    _consoleLeaseSessionId = 0;
                    _consoleLeaseLastActivityMs = 0;
                    expired = true;
                    return false;
                }

                _consoleLeaseLastActivityMs = nowMs;
                return true;
            }
        }

        private static bool ReleaseConsoleLease(ConsoleTransport transport, int sessionId)
        {
            lock (_consoleLeaseLock)
            {
                if (_consoleLeaseOwner != transport || _consoleLeaseSessionId != sessionId)
                {
                    return false;
                }

                _consoleLeaseOwner = ConsoleTransport.None;
                _consoleLeaseSessionId = 0;
                _consoleLeaseLastActivityMs = 0;
                return true;
            }
        }

        private static bool IsConsoleLeaseActive(ConsoleTransport transport)
        {
            lock (_consoleLeaseLock)
            {
                return _consoleLeaseOwner == transport;
            }
        }

        private static string _consoleLine = string.Empty;
        private static readonly string[] _consoleHistory = new string[UsbConsoleHistoryDepth];
        private static int _consoleHistoryCount;
        private static int _consoleHistoryIndex;
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

        private static void HeartbeatLoop()
        {
            while (true)
            {
                if (_ledReady)
                {
                    try
                    {
                        _gpio.Write(_ledPin, PinValue.High);
                        Thread.Sleep(LedPulseMs);
                        _gpio.Write(_ledPin, PinValue.Low);
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            _gpio.Write(_ledPin, PinValue.Low);
                        }
                        catch
                        {
                        }

                        WriteStructuredDebug(
                            "MAIN",
                            "schema=1 sub=main comp=heartbeat operation=led stat=error" +
                            " code=led_disabled detail=" + SanitizeToken(ex.Message));
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
            WriteStructuredDebug(
                "LNB",
                "schema=1 sub=lnb comp=fault operation=worker_start stat=ok" +
                " pin=" + _lnbFaultPin.ToString() +
                " level=debug");

            while (true)
            {
                bool processFault;
                lock (_lnbFaultTransitionLock)
                {
                    processFault = !_lnbFaultInterruptEnabled || _lnbFaultCheckPending || _lnbFaultAsserted;
                    _lnbFaultCheckPending = false;
                }

                if (processFault)
                {
                    TryProcessLnbFaultPin(_lnbFaultInterruptEnabled ? "worker" : "poll");
                }

                Thread.Sleep(LnbFaultPollIntervalMs);
            }
        }

        private static void EmitBootResetCauseLog()
        {
            uint diagWord = DiagMailbox.NativeGet();

            if ((diagWord & ResetCauseMarkerMask) != ResetCauseMarkerValue)
            {
                WriteStructuredDebug(
                    "MAIN",
                    "schema=1 sub=main comp=boot operation=reset_cause stat=unavailable" +
                    " diag=0x" + diagWord.ToString("X8"));
                return;
            }

            int flags = (int)((diagWord >> 16) & 0xFFu);
            int csrLow = (int)(diagWord & 0xFFFFu);

            WriteStructuredDebug(
                "MAIN",
                "schema=1 sub=main comp=boot operation=reset_cause stat=ok" +
                " flags=" + ResetFlagsToText(flags).ToLower() +
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
            WriteStructuredDebug(
                "CDC",
                "schema=1 sub=cdc comp=worker operation=start stat=ok");
            try
            {
                UsbConsoleLoopBody();
            }
            catch (Exception ex)
            {
                WriteStructuredDebug(
                    "CDC",
                    "schema=1 sub=cdc comp=worker operation=run stat=error" +
                    " code=worker_exception detail=" + SanitizeToken(ex.Message));
            }
        }

        private static void UsbConsoleLoopBody()
        {
            bool wasEnabled = false;
            bool idleWarningSent = false;
            bool suppressNextLineFeed = false;
            int escapeSequenceState = 0;
            int sessionId = 0;
            int bannerWriteOffset = 0;
            string banner = "\r\nCubley Rotation Control v" + ToAsciiUsbText(BuildInfo.Version) +
                "\r\nConsole inactive. Press Enter to activate.\r\n";

            while (true)
            {
                _cdcPreEnabledCount++;
                int enabled = UsbCdcConsole.NativeIsEnabled();
                _cdcPostEnabledCount++;

                if (enabled == 0)
                {
                    if (wasEnabled)
                    {
                        ReleaseConsoleLease(ConsoleTransport.Usb, sessionId);
                        ResetUsbConfigurationSession();
                    }

                    wasEnabled = false;
                    idleWarningSent = false;
                    suppressNextLineFeed = false;
                    sessionId = 0;
                    bannerWriteOffset = 0;
                    _watchElapsedMs = 0;
                    _consoleLine = string.Empty;
                    ClearConsoleHistory();
                    Thread.Sleep(UsbConsoleIdleSleepMs);
                    continue;
                }

                if (!wasEnabled)
                {
                    // Do NOT consume the enable transition until the banner has
                    // actually been written. On a fresh USB connection the output
                    // queue may not be draining yet, so a single write can return 0
                    // and the banner/prompt would be lost forever. Retry each loop
                    // iteration from the first unwritten byte until it succeeds.
                    string remainingBanner = banner.Substring(bannerWriteOffset);
                    int rc = SafeUsbWrite(remainingBanner);
                    if (rc > 0)
                    {
                        // The banner is ASCII-only, so the native byte count maps
                        // directly to managed string character positions.
                        int accepted = rc > remainingBanner.Length ? remainingBanner.Length : rc;
                        bannerWriteOffset += accepted;
                    }

                    bool bannerWritten = bannerWriteOffset >= banner.Length;
                    uint diag = DiagMailbox.NativeGet();
                    WriteStructuredDebug(
                        "CDC",
                        "schema=1 sub=cdc comp=connection operation=banner" +
                        " stat=" + (bannerWritten ? "ok" : "busy") +
                        " rc=" + rc.ToString() +
                        " off=" + bannerWriteOffset.ToString() +
                        " diag=0x" + diag.ToString("X8"));

                    if (!bannerWritten)
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
                    escapeSequenceState = 0;
                    if (sessionId != 0)
                    {
                        long idleMs;
                        if (!TryGetConsoleLeaseIdleMs(ConsoleTransport.Usb, sessionId, out idleMs))
                        {
                            ResetUsbConfigurationSession();
                            sessionId = 0;
                            idleWarningSent = false;
                            _consoleLine = string.Empty;
                            SafeUsbWrite("\r\nConsole inactive. Press Enter to activate.\r\n");
                        }
                        else if (idleMs >= ConsoleIdleTimeoutMs)
                        {
                            ReleaseConsoleLease(ConsoleTransport.Usb, sessionId);
                            ResetUsbConfigurationSession();
                            sessionId = 0;
                            idleWarningSent = false;
                            _consoleLine = string.Empty;
                            SafeUsbWrite("\r\nConsole session timed out. Press Enter to activate.\r\n");
                            WriteStructuredDebug(
                                "CDC",
                                "schema=1 sub=cdc comp=session operation=release stat=ok reason=timeout");
                        }
                        else if (!idleWarningSent && idleMs >= ConsoleIdleTimeoutMs - ConsoleIdleWarningMs)
                        {
                            idleWarningSent = true;
                            SafeUsbWrite("\r\nConsole session will time out in 1 minute.\r\n" + GetUsbPrompt() + _consoleLine);
                        }
                    }

                    _watchElapsedMs += UsbConsoleReadTimeoutMs + UsbConsoleIdleSleepMs;
                    if (sessionId != 0 && _watchEnabled && _watchElapsedMs >= UsbConsoleStatusIntervalMs)
                    {
                        _watchElapsedMs = 0;
                        EmitStatusBar(enabled);
                    }

                    Thread.Sleep(UsbConsoleIdleSleepMs);
                    continue;
                }

                _watchElapsedMs = 0;

                char c = (char)value;

                if (suppressNextLineFeed && c == '\n')
                {
                    suppressNextLineFeed = false;
                    continue;
                }

                suppressNextLineFeed = false;

                if (sessionId == 0)
                {
                    if (c == '\r' || c == '\n')
                    {
                        if (TryAcquireConsoleLease(ConsoleTransport.Usb, out sessionId))
                        {
                            idleWarningSent = false;
                            suppressNextLineFeed = c == '\r';
                            ClearConsoleHistory();
                            SafeUsbWrite("\r\nConsole active. Type 'quit' to release.\r\n" + GetUsbPrompt());
                            WriteStructuredDebug(
                                "CDC",
                                "schema=1 sub=cdc comp=session operation=acquire stat=ok transport=cdc");
                        }
                        else
                        {
                            SafeUsbWrite("\r\nConsole is currently in use. Press Enter to retry.\r\n");
                        }
                    }

                    continue;
                }

                bool leaseExpired;
                if (!TouchConsoleLease(ConsoleTransport.Usb, sessionId, out leaseExpired))
                {
                    ResetUsbConfigurationSession();
                    sessionId = 0;
                    idleWarningSent = false;
                    _consoleLine = string.Empty;
                    SafeUsbWrite(
                        leaseExpired
                            ? "\r\nConsole session timed out. Press Enter to activate.\r\n"
                            : "\r\nConsole inactive. Press Enter to activate.\r\n");
                    if (leaseExpired)
                    {
                        WriteStructuredDebug(
                            "CDC",
                            "schema=1 sub=cdc comp=session operation=release stat=ok reason=timeout");
                    }
                    continue;
                }

                idleWarningSent = false;

                if (escapeSequenceState != 0 || c == '\x1b')
                {
                    if (c == '\x1b')
                    {
                        escapeSequenceState = 1;
                    }
                    else if (escapeSequenceState == 1 && c == '[')
                    {
                        escapeSequenceState = 2;
                    }
                    else
                    {
                        if (escapeSequenceState == 2 && c == 'A')
                        {
                            RecallPreviousConsoleCommand();
                        }
                        else if (escapeSequenceState == 2 && c == 'B')
                        {
                            RecallNextConsoleCommand();
                        }

                        escapeSequenceState = 0;
                    }

                    continue;
                }

                if (c == '\x04')
                {
                    if (_usbConfigurationMode && _consoleLine.Length == 0)
                    {
                        SafeUsbWrite("\r\n");
                        ExecuteCommand("exit", WriteSerialLine, CommandTransport.Usb);
                        SafeUsbWrite(GetUsbPrompt());
                    }
                    else if (!_usbConfigurationMode && _consoleLine.Length == 0)
                    {
                        ReleaseConsoleLease(ConsoleTransport.Usb, sessionId);
                        ResetUsbConfigurationSession();
                        sessionId = 0;
                        SafeUsbWrite("\r\nConsole released. Press Enter to activate.\r\n");
                        WriteStructuredDebug(
                            "CDC",
                            "schema=1 sub=cdc comp=session operation=release stat=ok reason=ctrl_d");
                    }
                    continue;
                }

                if (c == '\r' || c == '\n')
                {
                    suppressNextLineFeed = c == '\r';
                    SafeUsbWrite("\r\n");

                    string normalized = NormalizeCommandInput(_consoleLine).ToLower();
                    if (normalized == "quit" || normalized == "logout")
                    {
                        _consoleLine = string.Empty;
                        if (_usbConfigurationMode && (_networkConfigurationDirty || _mqttConfigurationDirty))
                        {
                            SafeUsbWrite("Warning: uncommitted changes. Use 'commit' to apply or 'discard' to abandon them.\r\n" + GetUsbPrompt());
                            continue;
                        }

                        ReleaseConsoleLease(ConsoleTransport.Usb, sessionId);
                        ResetUsbConfigurationSession();
                        sessionId = 0;
                        SafeUsbWrite("Console released. Press Enter to activate.\r\n");
                        WriteStructuredDebug(
                            "CDC",
                            "schema=1 sub=cdc comp=session operation=release stat=ok reason=command");
                        continue;
                    }

                    StoreConsoleHistory(_consoleLine);
                    ExecuteCommand(_consoleLine, WriteSerialLine, CommandTransport.Usb);
                    _consoleLine = string.Empty;
                    _consoleHistoryIndex = _consoleHistoryCount;
                    SafeUsbWrite(GetUsbPrompt());
                    continue;
                }

                if (c == '\b' || c == 127)
                {
                    if (_consoleLine.Length > 0)
                    {
                        _consoleHistoryIndex = _consoleHistoryCount;
                        bool sensitive = IsSensitiveConsoleInput(_consoleLine);
                        _consoleLine = _consoleLine.Substring(0, _consoleLine.Length - 1);
                        if (!sensitive)
                        {
                            SafeUsbWrite("\b \b");
                        }
                    }
                    continue;
                }

                if (_consoleLine.Length >= UsbConsoleLineMaxLength)
                {
                    continue;
                }

                if (c >= ' ' && c <= '~')
                {
                    _consoleHistoryIndex = _consoleHistoryCount;
                    _consoleLine += c.ToString();
                    if (!IsSensitiveConsoleInput(_consoleLine))
                    {
                        SafeUsbWrite(c.ToString());
                    }
                }
            }
        }

        private static void StoreConsoleHistory(string line)
        {
            string command = NormalizeCommandInput(line);
            if (command.Length == 0 || command[0] == '!' || IsSensitiveConsoleInput(command))
            {
                return;
            }

            if (_consoleHistoryCount > 0 && _consoleHistory[_consoleHistoryCount - 1] == command)
            {
                _consoleHistoryIndex = _consoleHistoryCount;
                return;
            }

            if (_consoleHistoryCount == UsbConsoleHistoryDepth)
            {
                for (int i = 1; i < UsbConsoleHistoryDepth; i++)
                {
                    _consoleHistory[i - 1] = _consoleHistory[i];
                }

                _consoleHistoryCount--;
            }

            _consoleHistory[_consoleHistoryCount++] = command;
            _consoleHistoryIndex = _consoleHistoryCount;
        }

        private static void RecallPreviousConsoleCommand()
        {
            if (_consoleHistoryIndex == 0)
            {
                return;
            }

            _consoleHistoryIndex--;
            ReplaceConsoleLine(_consoleHistory[_consoleHistoryIndex]);
        }

        private static void RecallNextConsoleCommand()
        {
            if (_consoleHistoryIndex >= _consoleHistoryCount)
            {
                return;
            }

            _consoleHistoryIndex++;
            ReplaceConsoleLine(
                _consoleHistoryIndex < _consoleHistoryCount
                    ? _consoleHistory[_consoleHistoryIndex]
                    : string.Empty);
        }

        private static void ReplaceConsoleLine(string line)
        {
            int previousLength = _consoleLine.Length;
            _consoleLine = line;
            SafeUsbWrite("\r" + GetUsbPrompt() + _consoleLine);
            if (previousLength > _consoleLine.Length)
            {
                SafeUsbWrite(new string(' ', previousLength - _consoleLine.Length) +
                    new string('\b', previousLength - _consoleLine.Length));
            }
        }

        private static void ClearConsoleHistory()
        {
            for (int i = 0; i < _consoleHistoryCount; i++)
            {
                _consoleHistory[i] = null;
            }

            _consoleHistoryCount = 0;
            _consoleHistoryIndex = 0;
        }

        private static bool IsSensitiveConsoleInput(string line)
        {
            string normalized = NormalizeCommandInput(line).ToLower();
            return normalized.IndexOf("mqtt password ") == 0 ||
                normalized.IndexOf("mqtt pass ") == 0 ||
                normalized.IndexOf("mq password ") == 0 ||
                normalized.IndexOf("mq pass ") == 0;
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

        private static string ToAsciiUsbText(string text)
        {
            if (text == null || text.Length == 0)
            {
                return string.Empty;
            }

            char[] chars = text.ToCharArray();
            bool changed = false;
            for (int i = 0; i < chars.Length; i++)
            {
                if (chars[i] > 0x7F)
                {
                    chars[i] = '?';
                    changed = true;
                }
            }

            return changed ? new string(chars) : text;
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
                    WriteStructuredDebug(
                        "CDC",
                        "schema=1 sub=cdc comp=output operation=write stat=error" +
                        " code=" + (written < 0 ? "write_failed" : "partial_write") +
                        " rc=" + written.ToString() +
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
                    WriteStructuredDebug(
                        "CDC",
                        "schema=1 sub=cdc comp=output operation=write stat=error" +
                        " code=write_exception detail=" + SanitizeToken(ex.Message) +
                        " exceptions=" + _usbWriteExceptionCount.ToString());
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
                    WriteStructuredDebug(
                        "LNB",
                        "schema=1 sub=lnb comp=fault operation=monitor_init stat=unavailable" +
                        " code=pin_open_failed");
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

                WriteStructuredDebug(
                    "LNB",
                    "schema=1 sub=lnb comp=fault operation=monitor_init stat=ok" +
                    " pin=" + _lnbFaultPin.ToString() +
                    " mode=" + (_lnbFaultInterruptEnabled ? "interrupt" : "poll"));

                // Capture startup asserted state if fault is already active.
                TryProcessLnbFaultPin("startup");
            }
            catch
            {
                _lnbFaultReady = false;
                _lnbFaultInterruptEnabled = false;
                WriteStructuredDebug(
                    "LNB",
                    "schema=1 sub=lnb comp=fault operation=monitor_init stat=error" +
                    " code=exception");
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

            lock (_lnbFaultTransitionLock)
            {
                _lnbFaultCheckPending = true;
            }
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
            bool changed;
            int sequence;
            lock (_lnbFaultTransitionLock)
            {
                changed = asserted != _lnbFaultAsserted;
                if (!changed)
                {
                    return;
                }

                _lnbFaultAsserted = asserted;
                _lnbFaultSequence++;
                sequence = _lnbFaultSequence;
            }

            if (asserted)
            {
                BeginLnbIoOperation();
                try
                {
                    lock (_lnbIoLock)
                    {
                        EmitLnbFaultSnapshot(source, sequence);
                    }
                }
                finally
                {
                    EndLnbIoOperation();
                }
            }

            PublishMqttLnbFaultTransition(asserted, source);
        }
    }
}
