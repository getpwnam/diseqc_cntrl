using System;
using System.Diagnostics;
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

            _activeCommand = normalized;
            Debug.WriteLine("[CDC-CMD] cmd=" + _activeCommand);

            string[] tokens = SplitTokens(lower);
            if (HandleOperatorCommand(tokens, reqId))
            {
                return;
            }

            if (HandleCanonicalCommand(lower, reqId))
            {
                return;
            }

            string head = tokens[0];

            if (head == "help" || head == "h")
            {
                if (tokens.Length == 1)
                {
                    WriteCommandResult(reqId, true, "ok", "help", "commands=show,get,set,dns,diseqc,help,status,watch,capabilities,version,led,lnb aliases=h,st,w,caps,ver,l");
                }
                else
                {
                    string topic = tokens[1];
                    if (topic == "lnb" || topic == "l")
                    {
                        WriteCommandResult(reqId, true, "ok", "help lnb", "usage=lnb get <pol|band|status>, l g <p|b|s>, set lnb.a.<band|polarization|enabled|iset|isw> <value>, get lnb.a.<band|polarization|status|enabled|iset|isw>, l s <e|p|b> <value>");
                    }
                    else if (topic == "network" || topic == "net")
                    {
                        WriteCommandResult(reqId, true, "ok", "help network", "usage=get network; set network <mode dhcp|static|address IP|mask MASK|gateway IP|dns auto|dns static DNS1 [DNS2]|save|apply|discard|defaults|reboot>");
                    }
                    else if (topic == "dns")
                    {
                        WriteCommandResult(reqId, true, "ok", "help dns", "usage=dns lookup <hostname>");
                    }
                    else if (topic == "show" || topic == "get" || topic == "set" || topic == "diseqc")
                    {
                        WriteCommandResult(reqId, true, "ok", "help cli", "show=show network|net|lnb [a|b] [detail]|diseqc get=get lnb.a.<band|polarization|status|enabled|iset|isw> set=set lnb.a.<band|polarization|enabled|iset|isw> <value> diseqc=diseqc tone on [freq_hz] [duty_pct]|tone off|tone status|listen on|off|preset <off|direct|aa|ab|ba|bb|status>");
                    }
                    else
                    {
                        WriteCommandResult(reqId, false, "validation_error", "unknown help topic", "topic=" + topic);
                    }
                }
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
                WriteCommandResult(
                    reqId,
                    true,
                    "ok",
                    "capabilities",
                    "root=cubley/v1/diseqc serial_format=hybrid cmd_count=7");
                return;
            }

            if (head == "version" || head == "ver")
            {
                WriteCommandResult(reqId, true, "ok", "version", "iface=cubley_v1_serial shell=main");
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

            if (verb == "get")
            {
                HandleGetCommand(tokens, reqId);
                return true;
            }

            if (verb == "set")
            {
                HandleSetCommand(tokens, reqId);
                return true;
            }

            if (verb == "diseqc")
            {
                HandleDiseqcCommand(tokens, reqId);
                return true;
            }

            return false;
        }

        private static void EmitStatusSnapshot(int reqId)
        {
            int enabled = UsbCdcConsole.NativeIsEnabled();
            WriteCommandResult(
                reqId,
                true,
                "ok",
                "status",
                "enabled=" + enabled.ToString() +
                " led=" + (_ledReady ? "ready" : "not_ready") +
                " fail=" + _usbWriteFailureCount.ToString() +
                " partial=" + _usbWritePartialCount.ToString() +
                " ex=" + _usbWriteExceptionCount.ToString());
        }

        private static void EmitStatusBar(int enabled)
        {
            SafeUsbWrite(
                "status enabled=" + enabled.ToString() +
                " led=" + (_ledReady ? "ready" : "off") +
                "\r\n");
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

            Debug.WriteLine("[CDC-CMD] cmd=" + _activeCommand + " " + kvLine);

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
