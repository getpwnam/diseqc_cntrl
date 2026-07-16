using Cubley.Interop;
using System.Diagnostics;

namespace CubleyControl
{
    public static partial class Program
    {
        private static bool _lnbInitAttempted;
        private static int _lnbInitStatus = (int)LNBH26.Status.NotInitialized;

        private static void InitializeLnbSafeDefaults()
        {
            if (!EnsureLnbInitialized())
            {
                Debug.WriteLine("[CDC-LNB] boot safe defaults skipped; init rc=" + _lnbInitStatus.ToString());
                return;
            }

            int setEnableRc = LNBH26.NativeSetEnable(false);
            int setPolRc = LNBH26.NativeSetPolarizationForChannel(LnbChannelA, (int)LNBH26.Polarization.Vertical);
            int setBandRc = LNBH26.NativeSetBandForChannel(LnbChannelA, (int)LNBH26.Band.Low);

            Debug.WriteLine(
                "[CDC-LNB] boot safe profile rc enable=" + setEnableRc.ToString() +
                " pol=" + setPolRc.ToString() +
                " band=" + setBandRc.ToString());
        }

        private static void HandleShowCommand(string[] tokens, int reqId)
        {
            if (tokens.Length == 1)
            {
                int enabled = UsbCdcConsole.NativeIsEnabled();
                SafeUsbWrite("system serial=up cdc_enabled=" + enabled.ToString() + "\r\n");
                EmitLnbShowLine(LnbChannelA);
                SafeUsbWrite("diseqc state=unavailable note=placeholder\r\n");
                return;
            }

            if (tokens[1] == "lnb")
            {
                if (tokens.Length == 2)
                {
                    EmitLnbShowLine(LnbChannelA);
                    return;
                }

                if (tokens.Length == 3)
                {
                    int channel;
                    if (!TryParseLnbChannelToken(tokens[2], out channel))
                    {
                        WriteCommandResult(reqId, false, "validation_error", "show lnb channel invalid", "channel=" + tokens[2]);
                        return;
                    }

                    EmitLnbShowLine(channel);
                    return;
                }

                WriteCommandResult(reqId, false, "validation_error", "show lnb usage", "usage=show lnb [a]");
                return;
            }

            if (tokens[1] == "diseqc")
            {
                SafeUsbWrite("diseqc state=unavailable note=placeholder\r\n");
                return;
            }

            WriteCommandResult(reqId, false, "validation_error", "show target invalid", "target=" + tokens[1]);
        }

        private static void HandleGetCommand(string[] tokens, int reqId)
        {
            if (!EnsureLnbInitialized())
            {
                WriteCommandResult(reqId, false, "hw_fault", "get", "lnb_init_rc=" + _lnbInitStatus.ToString());
                return;
            }

            if (tokens.Length != 2)
            {
                WriteCommandResult(reqId, false, "validation_error", "get usage", "usage=get lnb.<channel>.<field>");
                return;
            }

            string[] parts = SplitByDot(tokens[1]);
            if (parts.Length != 3 || parts[0] != "lnb")
            {
                WriteCommandResult(reqId, false, "validation_error", "get key invalid", "key=" + tokens[1]);
                return;
            }

            int channel;
            if (!TryParseLnbChannelToken(parts[1], out channel))
            {
                WriteCommandResult(reqId, false, "validation_error", "get channel invalid", "channel=" + parts[1]);
                return;
            }

            string field = parts[2];
            if (field == "band")
            {
                int band = LNBH26.NativeGetBandForChannel(channel);
                WriteCommandResult(reqId, true, "ok", "get", "key=" + BuildLnbKey(channel, "band") + " value=" + BandToText(band));
                return;
            }

            if (field == "polarization" || field == "pol")
            {
                int pol = LNBH26.NativeGetPolarizationForChannel(channel);
                WriteCommandResult(reqId, true, "ok", "get", "key=" + BuildLnbKey(channel, "polarization") + " value=" + PolarizationToText(pol));
                return;
            }

            if (field == "status")
            {
                int s1;
                int s2;
                int rc = ReadLnbStatusPairSafe(out s1, out s2);
                if (rc == 0)
                {
                    WriteCommandResult(reqId, true, "ok", "get", "key=" + BuildLnbKey(channel, "status") + " status1=0x" + (s1 & 0xFF).ToString("X2") + " status2=0x" + (s2 & 0xFF).ToString("X2"));
                }
                else
                {
                    WriteCommandResult(reqId, false, "hw_fault", "get", "key=" + BuildLnbKey(channel, "status") + " rc=" + rc.ToString());
                }

                return;
            }

            if (field == "enabled" || field == "enable")
            {
                WriteCommandResult(reqId, false, "unsupported", "get", "key=" + BuildLnbKey(channel, "enabled") + " reason=no_native_getter");
                return;
            }

            WriteCommandResult(reqId, false, "validation_error", "get field invalid", "field=" + field);
        }

        private static void HandleSetCommand(string[] tokens, int reqId)
        {
            if (!EnsureLnbInitialized())
            {
                WriteCommandResult(reqId, false, "hw_fault", "set", "lnb_init_rc=" + _lnbInitStatus.ToString());
                return;
            }

            if (tokens.Length != 3)
            {
                WriteCommandResult(reqId, false, "validation_error", "set usage", "usage=set lnb.<channel>.<field> <value>");
                return;
            }

            string[] parts = SplitByDot(tokens[1]);
            if (parts.Length != 3 || parts[0] != "lnb")
            {
                WriteCommandResult(reqId, false, "validation_error", "set key invalid", "key=" + tokens[1]);
                return;
            }

            int channel;
            if (!TryParseLnbChannelToken(parts[1], out channel))
            {
                WriteCommandResult(reqId, false, "validation_error", "set channel invalid", "channel=" + parts[1]);
                return;
            }

            string field = parts[2];
            string value = tokens[2];
            if (field == "band")
            {
                int band;
                if (!TryParseBand(value, out band))
                {
                    WriteCommandResult(reqId, false, "validation_error", "set", "key=" + BuildLnbKey(channel, "band") + " value=" + value);
                    return;
                }

                int rc = LNBH26.NativeSetBandForChannel(channel, band);
                if (rc == 0)
                {
                    WriteCommandResult(reqId, true, "ok", "set", "key=" + BuildLnbKey(channel, "band") + " value=" + BandToText(band));
                }
                else
                {
                    WriteCommandResult(reqId, false, "hw_fault", "set", "key=" + BuildLnbKey(channel, "band") + " rc=" + rc.ToString());
                }

                return;
            }

            if (field == "polarization" || field == "pol")
            {
                int pol;
                if (!TryParsePolarization(value, out pol))
                {
                    WriteCommandResult(reqId, false, "validation_error", "set", "key=" + BuildLnbKey(channel, "polarization") + " value=" + value);
                    return;
                }

                int rc = LNBH26.NativeSetPolarizationForChannel(channel, pol);
                if (rc == 0)
                {
                    WriteCommandResult(reqId, true, "ok", "set", "key=" + BuildLnbKey(channel, "polarization") + " value=" + PolarizationToText(pol));
                }
                else
                {
                    WriteCommandResult(reqId, false, "hw_fault", "set", "key=" + BuildLnbKey(channel, "polarization") + " rc=" + rc.ToString());
                }

                return;
            }

            if (field == "enabled" || field == "enable")
            {
                bool enable;
                if (!TryParseOnOff(value, out enable))
                {
                    WriteCommandResult(reqId, false, "validation_error", "set", "key=" + BuildLnbKey(channel, "enabled") + " value=" + value);
                    return;
                }

                int rc = LNBH26.NativeSetEnable(enable);
                if (rc == 0)
                {
                    WriteCommandResult(reqId, true, "ok", "set", "key=" + BuildLnbKey(channel, "enabled") + " value=" + (enable ? "on" : "off"));
                }
                else
                {
                    WriteCommandResult(reqId, false, "hw_fault", "set", "key=" + BuildLnbKey(channel, "enabled") + " rc=" + rc.ToString());
                }

                return;
            }

            WriteCommandResult(reqId, false, "validation_error", "set field invalid", "field=" + field);
        }

        private static void HandleLnbCommand(string[] tokens, int reqId)
        {
            if (!EnsureLnbInitialized())
            {
                WriteCommandResult(reqId, false, "hw_fault", "lnb init failed", "lnb_init_rc=" + _lnbInitStatus.ToString());
                return;
            }

            if (tokens.Length < 3)
            {
                WriteCommandResult(reqId, false, "validation_error", "lnb command too short", "usage=lnb get|set ...");
                return;
            }

            string action = tokens[1];
            string field = tokens[2];

            if (action == "get" || action == "g")
            {
                if (field == "pol" || field == "p" || field == "polarization")
                {
                    int pol = LNBH26.NativeGetPolarizationForChannel(LnbChannelA);
                    WriteCommandResult(reqId, true, "ok", "lnb polarization", "value=" + PolarizationToText(pol));
                    return;
                }

                if (field == "band" || field == "b")
                {
                    int band = LNBH26.NativeGetBandForChannel(LnbChannelA);
                    WriteCommandResult(reqId, true, "ok", "lnb band", "value=" + BandToText(band));
                    return;
                }

                if (field == "status" || field == "s")
                {
                    int s1;
                    int s2;
                    int rc = ReadLnbStatusPairSafe(out s1, out s2);
                    if (rc == 0)
                    {
                        WriteCommandResult(reqId, true, "ok", "lnb status", "s1=0x" + (s1 & 0xFF).ToString("X2") + " s2=0x" + (s2 & 0xFF).ToString("X2"));
                    }
                    else
                    {
                        WriteCommandResult(reqId, false, "hw_fault", "lnb status read failed", "rc=" + rc.ToString());
                    }
                    return;
                }

                WriteCommandResult(reqId, false, "validation_error", "unknown lnb get field", "field=" + field);
                return;
            }

            if (action == "set" || action == "s")
            {
                if (tokens.Length < 4)
                {
                    WriteCommandResult(reqId, false, "validation_error", "lnb set missing value", "field=" + field);
                    return;
                }

                string value = tokens[3];

                if (field == "enable" || field == "e")
                {
                    bool enable;
                    if (!TryParseOnOff(value, out enable))
                    {
                        WriteCommandResult(reqId, false, "validation_error", "enable expects on|off", "value=" + value);
                        return;
                    }

                    int rc = LNBH26.NativeSetEnable(enable);
                    if (rc == 0)
                    {
                        WriteCommandResult(reqId, true, "ok", "lnb enable set", "value=" + (enable ? "on" : "off"));
                    }
                    else
                    {
                        WriteCommandResult(reqId, false, "hw_fault", "lnb enable failed", "rc=" + rc.ToString());
                    }
                    return;
                }

                if (field == "pol" || field == "p" || field == "polarization")
                {
                    int pol;
                    if (!TryParsePolarization(value, out pol))
                    {
                        WriteCommandResult(reqId, false, "validation_error", "pol expects vertical|horizontal", "value=" + value);
                        return;
                    }

                    int rc = LNBH26.NativeSetPolarizationForChannel(LnbChannelA, pol);
                    if (rc == 0)
                    {
                        WriteCommandResult(reqId, true, "ok", "lnb polarization set", "value=" + PolarizationToText(pol));
                    }
                    else
                    {
                        WriteCommandResult(reqId, false, "hw_fault", "lnb polarization failed", "rc=" + rc.ToString());
                    }
                    return;
                }

                if (field == "band" || field == "b")
                {
                    int band;
                    if (!TryParseBand(value, out band))
                    {
                        WriteCommandResult(reqId, false, "validation_error", "band expects low|high", "value=" + value);
                        return;
                    }

                    int rc = LNBH26.NativeSetBandForChannel(LnbChannelA, band);
                    if (rc == 0)
                    {
                        WriteCommandResult(reqId, true, "ok", "lnb band set", "value=" + BandToText(band));
                    }
                    else
                    {
                        WriteCommandResult(reqId, false, "hw_fault", "lnb band failed", "rc=" + rc.ToString());
                    }
                    return;
                }

                WriteCommandResult(reqId, false, "validation_error", "unknown lnb set field", "field=" + field);
                return;
            }

            WriteCommandResult(reqId, false, "validation_error", "unknown lnb action", "action=" + action);
        }

        private static void EmitLnbShowLine(int channel)
        {
            if (!EnsureLnbInitialized())
            {
                SafeUsbWrite("lnb.a state=init_failed rc=" + _lnbInitStatus.ToString() + "\r\n");
                return;
            }

            string channelName = LnbChannelToName(channel);
            int pol = LNBH26.NativeGetPolarizationForChannel(channel);
            int band = LNBH26.NativeGetBandForChannel(channel);
            int s1;
            int s2;
            int rc = ReadLnbStatusPairSafe(out s1, out s2);
            if (rc == 0)
            {
                SafeUsbWrite(
                    "lnb." + channelName +
                    " pol=" + PolarizationToText(pol) +
                    " band=" + BandToText(band) +
                    " status1=0x" + (s1 & 0xFF).ToString("X2") +
                    " status2=0x" + (s2 & 0xFF).ToString("X2") +
                    "\r\n");
            }
            else
            {
                SafeUsbWrite(
                    "lnb." + channelName +
                    " pol=" + PolarizationToText(pol) +
                    " band=" + BandToText(band) +
                    " status=read_failed rc=" + rc.ToString() +
                    "\r\n");
            }
        }

        private static bool EnsureLnbInitialized()
        {
            if (_lnbInitAttempted && _lnbInitStatus == (int)LNBH26.Status.Ok)
            {
                return true;
            }

            _lnbInitAttempted = true;
            _lnbInitStatus = LNBH26.NativeInit();
            int lastError = LNBH26.NativeGetLastError();
            int lastDetail = LNBH26.NativeGetLastErrorDetail();
            Debug.WriteLine(
                "[CDC-LNB] init rc=" + _lnbInitStatus.ToString() +
                " last=" + lastError.ToString() +
                " detail=" + lastDetail.ToString());

            return _lnbInitStatus == (int)LNBH26.Status.Ok;
        }

        private static int ReadLnbStatusPairSafe(out int s1, out int s2)
        {
            s1 = 0;
            s2 = 0;

            int rc = LNBH26.NativeReadStatusPair(out s1, out s2);
            if (rc != (int)LNBH26.Status.Ok)
            {
                int lastError = LNBH26.NativeGetLastError();
                int lastDetail = LNBH26.NativeGetLastErrorDetail();
                Debug.WriteLine(
                    "[CDC-LNB] status_pair rc=" + rc.ToString() +
                    " s1=0x" + (s1 & 0xFF).ToString("X2") +
                    " s2=0x" + (s2 & 0xFF).ToString("X2") +
                    " last=" + lastError.ToString() +
                    " detail=" + lastDetail.ToString());
            }

            if (rc == (int)LNBH26.Status.IoError || rc == (int)LNBH26.Status.NotInitialized)
            {
                _lnbInitAttempted = false;
                if (EnsureLnbInitialized())
                {
                    rc = LNBH26.NativeReadStatusPair(out s1, out s2);
                    if (rc != (int)LNBH26.Status.Ok)
                    {
                        int retryLastError = LNBH26.NativeGetLastError();
                        int retryLastDetail = LNBH26.NativeGetLastErrorDetail();
                        Debug.WriteLine(
                            "[CDC-LNB] status_pair retry rc=" + rc.ToString() +
                            " s1=0x" + (s1 & 0xFF).ToString("X2") +
                            " s2=0x" + (s2 & 0xFF).ToString("X2") +
                            " last=" + retryLastError.ToString() +
                            " detail=" + retryLastDetail.ToString());
                    }
                }
            }

            return rc;
        }

        private static string BuildLnbKey(int channel, string field)
        {
            return "lnb." + LnbChannelToName(channel) + "." + field;
        }

        private static string LnbChannelToName(int channel)
        {
            return channel == LnbChannelA ? "a" : channel.ToString();
        }

        private static bool TryParseLnbChannelToken(string token, out int channel)
        {
            if (token == "a" || token == "0")
            {
                channel = LnbChannelA;
                return true;
            }

            channel = -1;
            return false;
        }

        private static string[] SplitByDot(string text)
        {
            if (text == null || text.Length == 0)
            {
                return new string[0];
            }

            int count = 1;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '.')
                {
                    count++;
                }
            }

            string[] parts = new string[count];
            int partIndex = 0;
            int start = 0;
            for (int i = 0; i <= text.Length; i++)
            {
                if (i == text.Length || text[i] == '.')
                {
                    parts[partIndex++] = text.Substring(start, i - start);
                    start = i + 1;
                }
            }

            return parts;
        }
    }
}
