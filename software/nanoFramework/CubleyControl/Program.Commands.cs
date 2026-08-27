using System;
using System.Device.Gpio;
using System.Threading;
using Cubley.Interop;

namespace CubleyControl
{
    public static partial class Program
    {
        private static void HandleConsoleCommand(string command)
        {
            string normalized = NormalizeCommandInput(command);
            string lower = normalized.ToLower();
            int reqId = NextRequestId();

            if (lower.Length == 0)
            {
                return;
            }

            if (lower[0] == '!')
            {
                return;
            }

            _activeCommand = RedactCommandForLog(normalized);
            WriteStructuredDebug(
                "COMMAND",
                "schema=1 sub=command comp=dispatch operation=receive stat=ok" +
                " transport=" + (_activeCommandTransport == CommandTransport.Mqtt ? "mqtt" : "cdc") +
                " command=" + SanitizeToken(_activeCommand));

            string[] tokens = SplitTokens(lower);
            string[] valueTokens = SplitTokens(normalized);
            _activeCommandIsSetter = IsSetterCommand(tokens);
            if (_activeCommandTransport == CommandTransport.Mqtt && !IsMqttOperationalCommand(tokens))
            {
                WriteCommandResult(reqId, false, "unsupported", "command unavailable on mqtt", "transport=mqtt");
                return;
            }

            if (_activeCommandTransport == CommandTransport.Usb && _usbConfigurationMode)
            {
                HandleConfigurationModeCommand(tokens, valueTokens, reqId);
                return;
            }

            if (HandleOperatorCommand(tokens, reqId))
            {
                return;
            }

            string head = tokens[0];

            if (IsConfigureCommand(tokens))
            {
                BeginUsbConfigurationSession(reqId);
                return;
            }

            if (head == "help" || head == "h" || head == "?")
            {
                HandleOperationalHelp(tokens);
                return;
            }

            if (head == "status" || head == "st")
            {
                EmitStatusSnapshot(reqId);
                return;
            }

            if (head == "watch" || head == "w")
            {
                if (tokens.Length == 1 || tokens[1] == "on" || tokens[1] == "1")
                {
                    _watchEnabled = true;
                    WriteCommandResult(reqId, true, "ok", "watch on", "watch=1");
                    return;
                }

                if (tokens[1] == "off" || tokens[1] == "0")
                {
                    _watchEnabled = false;
                    WriteCommandResult(reqId, true, "ok", "watch off", "watch=0");
                    return;
                }

                WriteCommandResult(reqId, false, "validation_error", "watch expects on|off", "arg=" + tokens[1]);
                return;
            }

            if (head == "capabilities" || head == "caps")
            {
                EmitCapabilities(reqId);
                return;
            }

            if (head == "version" || head == "ver")
            {
                EmitVersion(reqId);
                return;
            }

            if (head == "lnb" || head == "l")
            {
                HandleLnbCommand(tokens, reqId);
                return;
            }

            if (head == "dns")
            {
                HandleDnsCommand(tokens, reqId);
                return;
            }

            if (lower == "led on")
            {
                if (TrySetLed(PinValue.High))
                {
                    WriteCommandResult(reqId, true, "ok", "led on", "led=high");
                }
                else
                {
                    WriteCommandResult(reqId, false, "hw_fault", "LED unavailable", "led=unavailable");
                }
                return;
            }

            if (lower == "led off")
            {
                if (TrySetLed(PinValue.Low))
                {
                    WriteCommandResult(reqId, true, "ok", "led off", "led=low");
                }
                else
                {
                    WriteCommandResult(reqId, false, "hw_fault", "LED unavailable", "led=unavailable");
                }
                return;
            }

            if (lower == "pulse")
            {
                if (TrySetLed(PinValue.High))
                {
                    Thread.Sleep(LedPulseMs);
                    TrySetLed(PinValue.Low);
                    WriteCommandResult(reqId, true, "ok", "pulse complete", "led=pulse");
                }
                else
                {
                    WriteCommandResult(reqId, false, "hw_fault", "LED unavailable", "led=unavailable");
                }
                return;
            }

            WriteCommandResult(reqId, false, "unsupported", "unknown command", "cmd=" + head);
        }

        private static void HandleOperationalHelp(string[] tokens)
        {
            if (tokens.Length == 1)
            {
                WriteHumanHeading("Available commands");
                WriteHelpCommand("show [topic]", "Display device and service state");
                WriteHelpCommand("lnb <a|b> <action> [value]", "Control LNB channel state");
                WriteHelpCommand("diseqc <action> ...", "Control switches and positioners");
                WriteHelpCommand("dns lookup <hostname>", "Resolve a host name");
                WriteHelpCommand("configure", "Enter configuration mode");
                WriteHelpCommand("watch [on|off]", "Control periodic status output");
                WriteHelpCommand("led <on|off> | pulse", "Control the status LED");
                WriteHelpCommand("quit", "Release the console (alias: logout)");
                WriteHelpCommand("help [command]", "Show command help (alias: ?)");
                _activeOutputSink("\r\nUse 'help <command>' for more information.\r\n");
                return;
            }

            if (tokens.Length != 2)
            {
                _activeOutputSink("Usage: help [command]\r\n");
                return;
            }

            string topic = tokens[1];
            if (topic == "lnb" || topic == "l")
            {
                WriteHumanHeading("LNB commands");
                WriteHelpCommand("show lnb [a|b] [detail]", "Display LNB state or registers");
                WriteHelpCommand("lnb <a|b> enable", "Enable LNB output");
                WriteHelpCommand("lnb <a|b> disable", "Disable LNB output");
                WriteHelpCommand("lnb <a|b> polarization <value>", "Set vertical or horizontal polarization");
                WriteHelpCommand("lnb <a|b> band <value>", "Set low or high band");
                WriteHelpCommand("lnb <a|b> iset <value>", "Set current range");
                WriteHelpCommand("lnb <a|b> isw <value>", "Set switch current limit");
                return;
            }

            if (topic == "show")
            {
                WriteHumanHeading("Show commands");
                WriteHelpCommand("show", "Display a device summary");
                WriteHelpCommand("show lnb [a|b] [detail]", "Display LNB state");
                WriteHelpCommand("show diseqc", "Display DiSEqC state");
                WriteHelpCommand("show network", "Display live network state");
                WriteHelpCommand("show mqtt", "Display live MQTT state");
                WriteHelpCommand("show running-config [domain]", "Display active configuration");
                WriteHelpCommand("show startup-config [domain]", "Display persisted configuration");
                WriteHelpCommand("show status|capabilities|version", "Display system information");
                return;
            }

            if (topic == "diseqc")
            {
                WriteHumanHeading("DiSEqC commands");
                WriteHelpCommand("diseqc goto <0..255>", "Move to a stored position");
                WriteHelpCommand("diseqc step <east|west> <1..128>", "Move a fixed number of steps");
                WriteHelpCommand("diseqc drive <east|west>", "Start continuous movement");
                WriteHelpCommand("diseqc stop", "Stop movement");
                WriteHelpCommand("diseqc preset <value>", "Select or inspect routing preset");
                WriteHelpCommand("diseqc tx <hex bytes>", "Transmit a raw frame");
                WriteHelpCommand("diseqc tone <on|off|status>", "Control or inspect the carrier tone");
                WriteHelpCommand("diseqc listen <on|off>", "Control external modulation input");
                return;
            }

            if (topic == "network" || topic == "net")
            {
                WriteHumanHeading("Network commands");
                WriteHelpCommand("show network", "Display live interface state");
                WriteHelpCommand("show running-config network", "Display active network configuration");
                WriteHelpCommand("configure", "Change network configuration");
                return;
            }

            if (topic == "mqtt")
            {
                WriteHumanHeading("MQTT commands");
                WriteHelpCommand("show mqtt", "Display live MQTT service state");
                WriteHelpCommand("show running-config mqtt", "Display active MQTT configuration");
                WriteHelpCommand("configure", "Change MQTT configuration");
                return;
            }

            if (topic == "dns")
            {
                WriteHumanHeading("DNS commands");
                WriteHelpCommand("dns lookup <hostname>", "Resolve a host name");
                return;
            }

            if (topic == "configure" || topic == "config" || topic == "conf")
            {
                WriteHumanHeading("Configuration mode");
                WriteHelpCommand("configure", "Enter USB configuration mode");
                WriteHelpCommand("config | conf | conf t", "Accepted aliases");
                return;
            }

            if (topic == "watch" || topic == "w")
            {
                WriteHumanHeading("Watch command");
                WriteHelpCommand("watch [on|off]", "Control periodic status output");
                return;
            }

            if (topic == "led")
            {
                WriteHumanHeading("LED commands");
                WriteHelpCommand("led <on|off>", "Set the status LED");
                WriteHelpCommand("pulse", "Pulse the status LED for 100 ms");
                return;
            }

            if (topic == "quit" || topic == "logout")
            {
                WriteHumanHeading("Console session");
                WriteHelpCommand("quit | logout", "Release the console when configuration is clean");
                return;
            }

            _activeOutputSink("No help available for '" + topic + "'.\r\n");
        }

        private static void WriteHelpCommand(string syntax, string description)
        {
            const int DescriptionColumn = 40;
            string command = "  " + syntax;
            if (command.Length >= DescriptionColumn)
            {
                _activeOutputSink(command + "\r\n" + new string(' ', DescriptionColumn) + description + "\r\n");
                return;
            }

            _activeOutputSink(command.PadRight(DescriptionColumn, ' ') + description + "\r\n");
        }

        private static void EmitCapabilities(int reqId)
        {
            if (_activeCommandTransport == CommandTransport.Usb)
            {
                WriteHumanHeading("Capabilities");
                WriteHumanField("Serial commands", "Available");
                WriteHumanField("MQTT commands", "Available");
                WriteHumanField("USB configuration", "Available");
                WriteHumanField("MQTT configuration", "Unavailable");
                return;
            }

            WriteCommandResult(
                reqId,
                true,
                "ok",
                "capabilities",
                "root=cubley/v1/diseqc serial_format=hybrid transport.serial=1 transport.mqtt=1 config_usb=1 config_mqtt=0");
        }

        private static void EmitVersion(int reqId)
        {
            if (_activeCommandTransport == CommandTransport.Usb)
            {
                WriteHumanHeading("Version");
                WriteHumanField("Product", "Cubley Rotation Control");
                WriteHumanField("Version", BuildInfo.Version);
                WriteHumanField("Git commit", BuildInfo.GitCommit);
                WriteHumanField("Interface", "cubley/v1 serial");
                WriteHumanField("Shell", "main");
                return;
            }

            WriteCommandResult(reqId, true, "ok", "version", "version=" + BuildInfo.Version + " git=" + BuildInfo.GitCommit + " iface=cubley_v1_serial shell=main");
        }

        private static bool HandleOperatorCommand(string[] tokens, int reqId)
        {
            if (tokens.Length == 0)
            {
                return false;
            }

            string verb = tokens[0];
            if (verb == "show")
            {
                HandleShowCommand(tokens, reqId);
                return true;
            }

            if (verb == "diseqc")
            {
                HandleDiseqcCommand(tokens, reqId);
                return true;
            }

            return false;
        }

        private static bool IsSetterCommand(string[] tokens)
        {
            if (tokens.Length == 0)
            {
                return false;
            }

            string head = tokens[0];
            if (_usbConfigurationMode)
            {
                return head == "network" || head == "net" ||
                    head == "mqtt" || head == "mq" ||
                    head == "commit" || head == "apply" ||
                    head == "discard" || head == "abort" ||
                    head == "load" || head == "defaults" ||
                    head == "debug" || head == "exit" || head == "end";
            }

            if (head == "lnb" || head == "l" ||
                head == "watch" || head == "w" ||
                head == "led" || head == "pulse" ||
                IsConfigureCommand(tokens))
            {
                return true;
            }

            if (head != "diseqc")
            {
                return false;
            }

            return tokens.Length < 3 ||
                !((tokens[1] == "preset" || tokens[1] == "tone") && tokens[2] == "status");
        }

        private static void EmitStatusSnapshot(int reqId)
        {
            int enabled = UsbCdcConsole.NativeIsEnabled();
            bool active = enabled != 0 && IsConsoleLeaseActive(ConsoleTransport.Usb);
            if (_activeCommandTransport == CommandTransport.Usb)
            {
                WriteHumanHeading("System");
                WriteHumanField("USB console", enabled == 0 ? "Disconnected" : active ? "Active" : "Inactive");
                WriteHumanField("Status LED", _ledReady ? "Ready" : "Unavailable");
                WriteHumanField("Write failures", _usbWriteFailureCount.ToString());
                WriteHumanField("Partial writes", _usbWritePartialCount.ToString());
                WriteHumanField("Write exceptions", _usbWriteExceptionCount.ToString());
                return;
            }

            WriteCommandResult(
                reqId,
                true,
                "ok",
                "status",
                "enabled=" + enabled.ToString() +
                " active=" + (active ? "1" : "0") +
                " led=" + (_ledReady ? "ready" : "not_ready") +
                " fail=" + _usbWriteFailureCount.ToString() +
                " partial=" + _usbWritePartialCount.ToString() +
                " ex=" + _usbWriteExceptionCount.ToString());
        }

        private static void EmitStatusBar(int enabled)
        {
            SafeUsbWrite("Status: USB " + (enabled != 0 ? "connected" : "disconnected") +
                ", LED " + (_ledReady ? "ready" : "unavailable") + "\r\n");
        }

        private static void WriteHumanHeading(string heading)
        {
            _activeOutputSink(heading + "\r\n");
        }

        private static void WriteHumanField(string label, string value)
        {
            _activeOutputSink("  " + label + ": " + value + "\r\n");
        }

        private static void WriteCommandResult(int reqId, bool ok, string code, string msg, string data)
        {
            string safeMsg = SanitizeToken(msg);
            string payload = data == null ? string.Empty : data;
            string kvLine =
                "kv ok=" + (ok ? "1" : "0") +
                " code=" + code +
                " msg=" + safeMsg +
                " ts_ms=" + NextResponseTick().ToString() +
                " req_id=" + reqId.ToString() +
                (payload.Length > 0 ? " " + payload : string.Empty);

            WriteStructuredDebug(
                "COMMAND",
                "schema=1 sub=command comp=completion" +
                " operation=execute" +
                " stat=" + (ok ? "ok" : "error") +
                " code=" + code +
                " transport=" + (_activeCommandTransport == CommandTransport.Mqtt ? "mqtt" : "cdc") +
                (_activeCommandTransport == CommandTransport.Mqtt ? " id=" + _mqttActiveCommandId.ToString() : string.Empty) +
                " request_id=" + reqId.ToString() +
                " command=" + SanitizeToken(_activeCommand) +
                " detail=" + safeMsg +
                (payload.Length > 0 ? " " + payload : string.Empty));

            if (ok &&
                _activeCommandTransport == CommandTransport.Usb &&
                _activeCommandIsSetter &&
                !_usbDebugEnabled)
            {
                return;
            }

            if (ok && _activeCommandTransport == CommandTransport.Mqtt)
            {
                _activeOutputSink("OK\r\n");
                return;
            }

            if (ok)
            {
                _activeOutputSink("OK\r\n");
                if (payload.Length > 0)
                {
                    _activeOutputSink("kv " + payload + "\r\n");
                }
            }
            else
            {
                _activeOutputSink("Fail: " + HumanizeFailure(code, msg) + "\r\n");
            }
        }

        private static string HumanizeFailure(string code, string msg)
        {
            if (code == "validation_error")
            {
                return "invalid input";
            }

            if (code == "unsupported")
            {
                return "unsupported command";
            }

            if (code == "hw_fault")
            {
                return "hardware fault";
            }

            if (code == "timeout")
            {
                return "timeout";
            }

            if (code == "busy")
            {
                return "busy";
            }

            if (msg == null || msg.Length == 0)
            {
                return "operation failed";
            }

            return msg;
        }

        private static int NextRequestId()
        {
            _requestId++;
            if (_requestId <= 0)
            {
                _requestId = 1;
            }

            return _requestId;
        }

        private static int NextResponseTick()
        {
            _responseTick += UsbConsoleReadTimeoutMs;
            if (_responseTick <= 0)
            {
                _responseTick = UsbConsoleReadTimeoutMs;
            }

            return _responseTick;
        }

        private static string[] SplitTokens(string text)
        {
            if (text == null)
            {
                return new string[0];
            }

            text = text.Trim();
            if (text.Length == 0)
            {
                return new string[0];
            }

            int count = 1;
            bool previousWasSpace = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == ' ')
                {
                    if (!previousWasSpace)
                    {
                        count++;
                    }
                    previousWasSpace = true;
                }
                else
                {
                    previousWasSpace = false;
                }
            }

            string[] tokens = new string[count];
            int tokenIndex = 0;
            int start = -1;
            for (int i = 0; i <= text.Length; i++)
            {
                bool boundary = i == text.Length || text[i] == ' ';
                if (!boundary && start < 0)
                {
                    start = i;
                }

                if (boundary && start >= 0)
                {
                    tokens[tokenIndex++] = text.Substring(start, i - start);
                    start = -1;
                }
            }

            if (tokenIndex == tokens.Length)
            {
                return tokens;
            }

            string[] compact = new string[tokenIndex];
            for (int i = 0; i < tokenIndex; i++)
            {
                compact[i] = tokens[i];
            }

            return compact;
        }

        private static string SanitizeToken(string text)
        {
            if (text == null || text.Length == 0)
            {
                return string.Empty;
            }

            char[] chars = text.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (chars[i] == ' ')
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }

        private static string RedactCommandForLog(string command)
        {
            if (string.IsNullOrEmpty(command))
            {
                return string.Empty;
            }

            string[] tokens = SplitTokens(NormalizeCommandInput(command).ToLower());
            int domainIndex = tokens.Length > 0 && tokens[0] == "set" ? 1 : 0;
            int fieldIndex = domainIndex + 1;
            if (tokens.Length > fieldIndex &&
                (tokens[domainIndex] == "mqtt" || tokens[domainIndex] == "mq") &&
                (tokens[fieldIndex] == "password" || tokens[fieldIndex] == "pass"))
            {
                return "mqtt password <redacted>";
            }

            return command;
        }

        private static string NormalizeCommandInput(string text)
        {
            if (text == null || text.Length == 0)
            {
                return string.Empty;
            }

            char[] chars = new char[text.Length];
            int outIndex = 0;
            bool previousWasSpace = true;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                bool isSpace = c == ' ' || c == '\t' || c == '\r' || c == '\n';

                // Allow only printable 7-bit ASCII bytes in command text.
                bool isPrintableAscii = c >= ' ' && c <= '~';

                if (!isPrintableAscii)
                {
                    if (isSpace && !previousWasSpace)
                    {
                        chars[outIndex++] = ' ';
                        previousWasSpace = true;
                    }

                    continue;
                }

                if (c == ' ')
                {
                    if (!previousWasSpace)
                    {
                        chars[outIndex++] = ' ';
                        previousWasSpace = true;
                    }

                    continue;
                }

                chars[outIndex++] = c;
                previousWasSpace = false;
            }

            while (outIndex > 0 && chars[outIndex - 1] == ' ')
            {
                outIndex--;
            }

            return outIndex == 0 ? string.Empty : new string(chars, 0, outIndex);
        }

        private static bool TryParseOnOff(string value, out bool result)
        {
            if (value == "on" || value == "1" || value == "true")
            {
                result = true;
                return true;
            }

            if (value == "off" || value == "0" || value == "false")
            {
                result = false;
                return true;
            }

            result = false;
            return false;
        }

        private static bool TryParsePolarization(string value, out int pol)
        {
            if (value == "vertical" || value == "v")
            {
                pol = (int)LNBH26.Polarization.Vertical;
                return true;
            }

            if (value == "horizontal" || value == "h")
            {
                pol = (int)LNBH26.Polarization.Horizontal;
                return true;
            }

            pol = 0;
            return false;
        }

        private static bool TryParseBand(string value, out int band)
        {
            if (value == "low" || value == "l")
            {
                band = (int)LNBH26.Band.Low;
                return true;
            }

            if (value == "high" || value == "h")
            {
                band = (int)LNBH26.Band.High;
                return true;
            }

            band = 0;
            return false;
        }

        private static string PolarizationToText(int pol)
        {
            return pol == (int)LNBH26.Polarization.Horizontal ? "horizontal" : "vertical";
        }

        private static string BandToText(int band)
        {
            return band == (int)LNBH26.Band.High ? "high" : "low";
        }
    }
}
