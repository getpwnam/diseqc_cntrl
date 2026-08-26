using Cubley.Interop;
using Cubley.Lnbh26;
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
                _activeOutputSink("system serial=up cdc_enabled=" + enabled.ToString() + "\r\n");
                EmitLnbShowSummaryLine(LnbChannelA);
                _activeOutputSink("diseqc state=unavailable note=placeholder\r\n");
                return;
            }

            if (tokens[1] == "lnb")
            {
                int channel = LnbChannelA;
                bool detail = false;
                bool channelSet = false;

                for (int i = 2; i < tokens.Length; i++)
                {
                    if (tokens[i] == "detail")
                    {
                        detail = true;
                        continue;
                    }

                    if (!channelSet)
                    {
                        int parsedChannel;
                        if (!TryParseLnbChannelToken(tokens[i], out parsedChannel))
                        {
                            WriteCommandResult(reqId, false, "validation_error", "show lnb token invalid", "token=" + tokens[i]);
                            return;
                        }

                        channel = parsedChannel;
                        channelSet = true;
                        continue;
                    }

                    WriteCommandResult(reqId, false, "validation_error", "show lnb usage", "usage=show lnb [a|b] [detail]");
                    return;
                }

                if (detail)
                {
                    EmitLnbShowDetailJson(channel);
                }
                else
                {
                    EmitLnbShowSummaryLine(channel);
                }

                return;
            }

            if (tokens[1] == "diseqc")
            {
                _activeOutputSink("diseqc state=unavailable note=placeholder\r\n");
                return;
            }

            if (tokens[1] == "network" || tokens[1] == "net")
            {
                EmitNetworkStatus(reqId);
                return;
            }

            if (tokens[1] == "mqtt")
            {
                EmitMqttStatus(reqId);
                return;
            }

            WriteCommandResult(reqId, false, "validation_error", "show target invalid", "target=" + tokens[1]);
        }

        private static void HandleGetCommand(string[] tokens, int reqId)
        {
            if (tokens.Length >= 2 && (tokens[1] == "network" || tokens[1] == "net"))
            {
                HandleGetNetworkCommand(tokens, reqId);
                return;
            }

            if (tokens.Length >= 2 && tokens[1] == "mqtt")
            {
                HandleGetMqttCommand(tokens, reqId);
                return;
            }

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

            if (field == "iset")
            {
                int isetLow = LNBH26Tweaks.NativeGetIsetLowForChannel(channel);
                WriteCommandResult(reqId, true, "ok", "get", "key=" + BuildLnbKey(channel, "iset") + " value=" + IsetToText(isetLow));
                return;
            }

            if (field == "isw")
            {
                int iswLow = LNBH26Tweaks.NativeGetIswLowForChannel(channel);
                WriteCommandResult(reqId, true, "ok", "get", "key=" + BuildLnbKey(channel, "isw") + " value=" + IswToText(iswLow));
                return;
            }

            WriteCommandResult(reqId, false, "validation_error", "get field invalid", "field=" + field);
        }

        private static void HandleSetCommand(string[] tokens, string[] valueTokens, int reqId)
        {
            if (tokens.Length >= 2 && (tokens[1] == "network" || tokens[1] == "net"))
            {
                HandleSetNetworkCommand(tokens, reqId);
                return;
            }

            if (tokens.Length >= 2 && tokens[1] == "mqtt")
            {
                HandleSetMqttCommand(tokens, valueTokens, reqId);
                return;
            }

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

            if (field == "iset")
            {
                bool lowRange;
                if (!TryParseIset(value, out lowRange))
                {
                    WriteCommandResult(reqId, false, "validation_error", "set", "key=" + BuildLnbKey(channel, "iset") + " value=" + value);
                    return;
                }

                int rc = LNBH26Tweaks.NativeSetIsetLowForChannel(channel, lowRange);
                if (rc == 0)
                {
                    WriteCommandResult(reqId, true, "ok", "set", "key=" + BuildLnbKey(channel, "iset") + " value=" + IsetToText(lowRange ? 1 : 0));
                }
                else
                {
                    WriteCommandResult(reqId, false, "hw_fault", "set", "key=" + BuildLnbKey(channel, "iset") + " rc=" + rc.ToString());
                }

                return;
            }

            if (field == "isw")
            {
                bool lowLimit;
                if (!TryParseIsw(value, out lowLimit))
                {
                    WriteCommandResult(reqId, false, "validation_error", "set", "key=" + BuildLnbKey(channel, "isw") + " value=" + value);
                    return;
                }

                int rc = LNBH26Tweaks.NativeSetIswLowForChannel(channel, lowLimit);
                if (rc == 0)
                {
                    WriteCommandResult(reqId, true, "ok", "set", "key=" + BuildLnbKey(channel, "isw") + " value=" + IswToText(lowLimit ? 1 : 0));
                }
                else
                {
                    WriteCommandResult(reqId, false, "hw_fault", "set", "key=" + BuildLnbKey(channel, "isw") + " rc=" + rc.ToString());
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

        private static void EmitLnbShowSummaryLine(int channel)
        {
            if (!EnsureLnbInitialized())
            {
                _activeOutputSink("lnb." + LnbChannelToSchemaName(channel) + " state=init_failed rc=" + _lnbInitStatus.ToString() + "\r\n");
                return;
            }

            int pol = LNBH26.NativeGetPolarizationForChannel(channel);
            int band = LNBH26.NativeGetBandForChannel(channel);

            int s1;
            int s2;
            int rc = ReadLnbStatusPairSafe(out s1, out s2);
            if (rc != (int)LNBH26.Status.Ok)
            {
                _activeOutputSink("lnb." + LnbChannelToSchemaName(channel) + " status=read_failed rc=" + rc.ToString() + "\r\n");
                return;
            }

            int d1;
            int d2;
            int d3;
            int d4;
            rc = ReadLnbDataRegistersSafe(out d1, out d2, out d3, out d4);
            if (rc != (int)LNBH26.Status.Ok)
            {
                _activeOutputSink("lnb." + LnbChannelToSchemaName(channel) + " config=read_failed rc=" + rc.ToString() + "\r\n");
                return;
            }

            _activeOutputSink(
                "lnb." + LnbChannelToSchemaName(channel) +
                " pol=" + PolarizationToText(pol) +
                " band=" + BandToText(band) +
                " voltage=" + VoltageSelectForChannelToText(channel, d1) +
                " tone=" + (IsToneEnabledForChannel(channel, d2) ? "on" : "off") +
                " lpm=" + (IsLowPowerEnabledForChannel(channel, d2) ? "on" : "off") +
                " extm=" + (IsExtmEnabledForChannel(channel, d2) ? "on" : "off") +
                " status=" + (HasFaultStatus(s1) ? "fault" : "ok") +
                " s1=" + ToHexU8(s1) +
                " s2=" + ToHexU8(s2) +
                "\r\n");
        }

        private static void EmitLnbShowDetailJson(int channel)
        {
            if (!EnsureLnbInitialized())
            {
                _activeOutputSink("{\"schema\":\"cubley/v1/lnbh26\",\"channel\":\"" + LnbChannelToSchemaName(channel) + "\",\"error\":\"init_failed\",\"rc\":" + _lnbInitStatus.ToString() + "}\r\n");
                return;
            }

            int s1;
            int s2;
            int rc = ReadLnbStatusPairSafe(out s1, out s2);
            if (rc != (int)LNBH26.Status.Ok)
            {
                _activeOutputSink(
                    "{\"schema\":\"cubley/v1/lnbh26\",\"channel\":\"" + LnbChannelToSchemaName(channel) +
                    "\",\"error\":\"status_read_failed\",\"rc\":" + rc.ToString() + "}\r\n");
                return;
            }

            int d1;
            int d2;
            int d3;
            int d4;
            rc = ReadLnbDataRegistersSafe(out d1, out d2, out d3, out d4);
            if (rc != (int)LNBH26.Status.Ok)
            {
                _activeOutputSink(
                    "{\"schema\":\"cubley/v1/lnbh26\",\"channel\":\"" + LnbChannelToSchemaName(channel) +
                    "\",\"error\":\"data_read_failed\",\"rc\":" + rc.ToString() + "}\r\n");
                return;
            }

            _activeOutputSink(BuildLnbRegisterJson(channel, s1, s2, d1, d2, d3, d4) + "\r\n");
        }

        private static bool HasFaultStatus(int status1)
        {
            int faultMask =
                (int)LNBH26.Status1Flags.OlfA |
                (int)LNBH26.Status1Flags.OlfB |
                (int)LNBH26.Status1Flags.PdoA |
                (int)LNBH26.Status1Flags.PdoB |
                (int)LNBH26.Status1Flags.Otf |
                (int)LNBH26.Status1Flags.Png;

            return (status1 & faultMask) != 0;
        }

        private static bool IsToneEnabledForChannel(int channel, int data2)
        {
            return channel == LnbChannelA
                ? (data2 & (int)LNBH26.Data2Flags.TenA) != 0
                : (data2 & (int)LNBH26.Data2Flags.TenB) != 0;
        }

        private static bool IsLowPowerEnabledForChannel(int channel, int data2)
        {
            return channel == LnbChannelA
                ? (data2 & (int)LNBH26.Data2Flags.LpmA) != 0
                : (data2 & (int)LNBH26.Data2Flags.LpmB) != 0;
        }

        private static bool IsExtmEnabledForChannel(int channel, int data2)
        {
            return channel == LnbChannelA
                ? (data2 & (int)LNBH26.Data2Flags.ExtmA) != 0
                : (data2 & (int)LNBH26.Data2Flags.ExtmB) != 0;
        }

        private static string VoltageSelectForChannelToText(int channel, int data1)
        {
            int nibble = channel == LnbChannelA ? (data1 & 0x0F) : ((data1 >> 4) & 0x0F);
            if (nibble == 0x00)
            {
                return "disabled";
            }

            if (nibble == 0x01)
            {
                return "13V";
            }

            if (nibble == 0x08)
            {
                return "18V";
            }

            return "unknown(" + ToHexU8(nibble) + ")";
        }

        private static string ToHexU8(int value)
        {
            return "0x" + (value & 0xFF).ToString("X2");
        }

        private static void EmitLnbFaultSnapshot(string source, int sequence)
        {
            if (!EnsureLnbInitialized())
            {
                Debug.WriteLine(
                    "[LNB-FAULT] seq=" + sequence.ToString() +
                    " src=" + source +
                    " init_failed rc=" + _lnbInitStatus.ToString());
                return;
            }

            int s1;
            int s2;
            int statusRc = ReadLnbStatusPairSafe(out s1, out s2);
            if (statusRc != (int)LNBH26.Status.Ok)
            {
                int statusLastError = LNBH26.NativeGetLastError();
                int statusLastDetail = LNBH26.NativeGetLastErrorDetail();
                Debug.WriteLine(
                    "[LNB-FAULT] seq=" + sequence.ToString() +
                    " src=" + source +
                    " status_read_failed rc=" + statusRc.ToString() +
                    " last=" + statusLastError.ToString() +
                    " detail=" + statusLastDetail.ToString());
                return;
            }

            int d1;
            int d2;
            int d3;
            int d4;
            int dataRc = ReadLnbDataRegistersSafe(out d1, out d2, out d3, out d4);
            if (dataRc != (int)LNBH26.Status.Ok)
            {
                int dataLastError = LNBH26.NativeGetLastError();
                int dataLastDetail = LNBH26.NativeGetLastErrorDetail();
                Debug.WriteLine(
                    "[LNB-FAULT] seq=" + sequence.ToString() +
                    " src=" + source +
                    " data_read_failed rc=" + dataRc.ToString() +
                    " s1=" + ToHexU8(s1) +
                    " s2=" + ToHexU8(s2) +
                    " last=" + dataLastError.ToString() +
                    " detail=" + dataLastDetail.ToString());
                return;
            }

            int pol = LNBH26.NativeGetPolarizationForChannel(LnbChannelA);
            int band = LNBH26.NativeGetBandForChannel(LnbChannelA);
            int lastError = LNBH26.NativeGetLastError();
            int lastDetail = LNBH26.NativeGetLastErrorDetail();

            Debug.WriteLine(
                "[LNB-FAULT] seq=" + sequence.ToString() +
                " src=" + source +
                " ch=a" +
                " pol=" + PolarizationToText(pol) +
                " band=" + BandToText(band) +
                " voltage=" + VoltageSelectForChannelToText(LnbChannelA, d1) +
                " tone=" + (IsToneEnabledForChannel(LnbChannelA, d2) ? "on" : "off") +
                " lpm=" + (IsLowPowerEnabledForChannel(LnbChannelA, d2) ? "on" : "off") +
                " extm=" + (IsExtmEnabledForChannel(LnbChannelA, d2) ? "on" : "off") +
                " status=" + (HasFaultStatus(s1) ? "fault" : "ok") +
                " s1=" + ToHexU8(s1) +
                " s2=" + ToHexU8(s2) +
                " d1=" + ToHexU8(d1) +
                " d2=" + ToHexU8(d2) +
                " d3=" + ToHexU8(d3) +
                " d4=" + ToHexU8(d4) +
                " last=" + lastError.ToString() +
                " detail=" + lastDetail.ToString());
        }

        private static int ReadLnbDataRegistersSafe(out int d1, out int d2, out int d3, out int d4)
        {
            int rc;

            d1 = 0;
            d2 = 0;
            d3 = 0;
            d4 = 0;

            rc = ReadLnbRegisterSafe(LNBH26.Register.Data1, out d1);
            if (rc != (int)LNBH26.Status.Ok)
            {
                return rc;
            }

            rc = ReadLnbRegisterSafe(LNBH26.Register.Data2, out d2);
            if (rc != (int)LNBH26.Status.Ok)
            {
                return rc;
            }

            rc = ReadLnbRegisterSafe(LNBH26.Register.Data3, out d3);
            if (rc != (int)LNBH26.Status.Ok)
            {
                return rc;
            }

            rc = ReadLnbRegisterSafe(LNBH26.Register.Data4, out d4);
            if (rc != (int)LNBH26.Status.Ok)
            {
                return rc;
            }

            return (int)LNBH26.Status.Ok;
        }

        private static int ReadLnbRegisterSafe(LNBH26.Register register, out int value)
        {
            int rc = LNBH26Registers.NativeReadRegister((int)register, out value);

            if (rc == (int)LNBH26.Status.IoError || rc == (int)LNBH26.Status.NotInitialized)
            {
                _lnbInitAttempted = false;
                if (EnsureLnbInitialized())
                {
                    rc = LNBH26Registers.NativeReadRegister((int)register, out value);
                }
            }

            return rc;
        }

        private static string BuildLnbRegisterJson(int channel, int s1, int s2, int d1, int d2, int d3, int d4)
        {
            Lnbh26ParsedPayload parsed = LNBH26RegisterParser.Parse(LnbChannelToSchemaName(channel), s1, s2, d1, d2, d3, d4);
            return LNBH26JsonRenderer.Render(parsed);
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

        private static string IsetToText(int isetLow)
        {
            return isetLow != 0 ? "low" : "default";
        }

        private static string IswToText(int iswLow)
        {
            return iswLow != 0 ? "2.5a" : "4a";
        }

        private static bool TryParseIset(string value, out bool lowRange)
        {
            if (value == "default" || value == "normal" || value == "high" || value == "0")
            {
                lowRange = false;
                return true;
            }

            if (value == "low" || value == "reduced" || value == "1")
            {
                lowRange = true;
                return true;
            }

            lowRange = false;
            return false;
        }

        private static bool TryParseIsw(string value, out bool lowLimit)
        {
            if (value == "4a" || value == "4" || value == "default" || value == "high" || value == "0")
            {
                lowLimit = false;
                return true;
            }

            if (value == "2.5a" || value == "2p5a" || value == "2_5a" || value == "low" || value == "reduced" || value == "1")
            {
                lowLimit = true;
                return true;
            }

            lowLimit = false;
            return false;
        }

        private static string LnbChannelToName(int channel)
        {
            return channel == LnbChannelA ? "a" : channel.ToString();
        }

        private static string LnbChannelToSchemaName(int channel)
        {
            if (channel == LnbChannelA)
            {
                return "a";
            }

            if (channel == 1)
            {
                return "b";
            }

            return channel.ToString();
        }

        private static bool TryParseLnbChannelToken(string token, out int channel)
        {
            if (token == "a" || token == "0")
            {
                channel = LnbChannelA;
                return true;
            }

            if (token == "b" || token == "1")
            {
                channel = 1;
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
