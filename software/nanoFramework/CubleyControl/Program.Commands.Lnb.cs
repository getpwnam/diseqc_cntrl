using Cubley.Interop;
using Cubley.Lnbh26;

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
                return;
            }

            int setEnableRc = LNBH26.NativeSetEnable(false);
            int setPolRc = LNBH26.NativeSetPolarizationForChannel(LnbChannelA, (int)LNBH26.Polarization.Vertical);
            int setBandRc = LNBH26.NativeSetBandForChannel(LnbChannelA, (int)LNBH26.Band.Low);

            WriteStructuredDebug(
                "LNB",
                "schema=1 sub=lnb comp=control operation=safe_defaults" +
                " stat=" + (setEnableRc == 0 && setPolRc == 0 && setBandRc == 0 ? "ok" : "error") +
                " enable_rc=" + setEnableRc.ToString() +
                " pol_rc=" + setPolRc.ToString() +
                " band_rc=" + setBandRc.ToString());
        }

        private static void HandleShowCommand(string[] tokens, int reqId)
        {
            if (tokens.Length == 1)
            {
                int enabled = UsbCdcConsole.NativeIsEnabled();
                if (_activeCommandTransport == CommandTransport.Usb)
                {
                    WriteHumanHeading("System");
                    WriteHumanField("Serial", "Up");
                    WriteHumanField(
                        "USB console",
                        enabled == 0 ? "Disconnected" : IsConsoleLeaseActive(ConsoleTransport.Usb) ? "Active" : "Inactive");
                }
                else
                {
                    _activeOutputSink(
                        "system serial=up cdc_enabled=" + enabled.ToString() +
                        " console_active=" + (IsConsoleLeaseActive(ConsoleTransport.Usb) ? "1" : "0") + "\r\n");
                }
                EmitLnbShowSummaryLine(LnbChannelA);
                EmitLnbShowSummaryLine(1);
                EmitDiseqcShowSummaryLine();
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
                    if (channelSet)
                    {
                        EmitLnbShowDetailJson(channel);
                    }
                    else
                    {
                        EmitLnbShowDetailJson(LnbChannelA);
                        EmitLnbShowDetailJson(1);
                    }
                }
                else
                {
                    if (channelSet)
                    {
                        EmitLnbShowSummaryLine(channel);
                    }
                    else
                    {
                        EmitLnbShowSummaryLine(LnbChannelA);
                        EmitLnbShowSummaryLine(1);
                    }
                }

                return;
            }

            if (tokens[1] == "diseqc")
            {
                EmitDiseqcShowSummaryLine();
                return;
            }

            if (tokens.Length == 2 && tokens[1] == "status")
            {
                EmitStatusSnapshot(reqId);
                return;
            }

            if (tokens.Length == 2 && (tokens[1] == "capabilities" || tokens[1] == "caps"))
            {
                EmitCapabilities(reqId);
                return;
            }

            if (tokens.Length == 2 && (tokens[1] == "version" || tokens[1] == "ver"))
            {
                EmitVersion(reqId);
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

            if (tokens[1] == "running-config" || tokens[1] == "run")
            {
                HandleShowConfigurationCommand(tokens, false, reqId);
                return;
            }

            if (tokens[1] == "startup-config" || tokens[1] == "start")
            {
                HandleShowConfigurationCommand(tokens, true, reqId);
                return;
            }

            WriteCommandResult(reqId, false, "validation_error", "show target invalid", "target=" + tokens[1]);
        }

        private static void HandleLnbCommand(string[] tokens, int reqId)
        {
            if (!EnsureLnbInitialized())
            {
                WriteCommandResult(reqId, false, "hw_fault", "lnb init failed", BuildLnbInitDiagnosticData());
                return;
            }

            if (tokens.Length != 4)
            {
                WriteCommandResult(reqId, false, "validation_error", "lnb usage", "usage=lnb <a|b> <enable|polarization|band|iset|isw> <value>");
                return;
            }

            int channel;
            if (!TryParseLnbChannelToken(tokens[1], out channel))
            {
                WriteCommandResult(reqId, false, "validation_error", "lnb channel invalid", "channel=" + tokens[1]);
                return;
            }

            string field = tokens[2];
            string value = tokens[3];

            if (field == "enable" || field == "enabled" || field == "e")
            {
                bool enable;
                if (!TryParseOnOff(value, out enable))
                {
                    WriteCommandResult(reqId, false, "validation_error", "lnb enable invalid", "value=" + value);
                    return;
                }

                int rc = LNBH26.NativeSetEnable(enable);
                if (rc == (int)LNBH26.Status.Ok)
                {
                    WriteCommandResult(reqId, true, "ok", "lnb enable", "channel=" + LnbChannelToSchemaName(channel) + " value=" + (enable ? "on" : "off") + " scope=global");
                }
                else
                {
                    WriteCommandResult(reqId, false, "hw_fault", "lnb enable failed", "channel=" + LnbChannelToSchemaName(channel) + " rc=" + rc.ToString());
                }

                return;
            }

            if (field == "polarization" || field == "pol" || field == "p")
            {
                int polarization;
                if (!TryParsePolarization(value, out polarization))
                {
                    WriteCommandResult(reqId, false, "validation_error", "lnb polarization invalid", "value=" + value);
                    return;
                }

                int rc = LNBH26.NativeSetPolarizationForChannel(channel, polarization);
                if (rc == (int)LNBH26.Status.Ok)
                {
                    WriteCommandResult(reqId, true, "ok", "lnb polarization", "channel=" + LnbChannelToSchemaName(channel) + " value=" + PolarizationToText(polarization));
                }
                else
                {
                    WriteCommandResult(reqId, false, "hw_fault", "lnb polarization failed", "channel=" + LnbChannelToSchemaName(channel) + " rc=" + rc.ToString());
                }

                return;
            }

            if (field == "band" || field == "b")
            {
                int band;
                if (!TryParseBand(value, out band))
                {
                    WriteCommandResult(reqId, false, "validation_error", "lnb band invalid", "value=" + value);
                    return;
                }

                int rc = LNBH26.NativeSetBandForChannel(channel, band);
                if (rc == (int)LNBH26.Status.Ok)
                {
                    WriteCommandResult(reqId, true, "ok", "lnb band", "channel=" + LnbChannelToSchemaName(channel) + " value=" + BandToText(band));
                }
                else
                {
                    WriteCommandResult(reqId, false, "hw_fault", "lnb band failed", "channel=" + LnbChannelToSchemaName(channel) + " rc=" + rc.ToString());
                }

                return;
            }

            if (field == "iset")
            {
                bool lowRange;
                if (!TryParseIset(value, out lowRange))
                {
                    WriteCommandResult(reqId, false, "validation_error", "lnb iset invalid", "value=" + value);
                    return;
                }

                int rc = LNBH26Tweaks.NativeSetIsetLowForChannel(channel, lowRange);
                if (rc == (int)LNBH26.Status.Ok)
                {
                    WriteCommandResult(reqId, true, "ok", "lnb iset", "channel=" + LnbChannelToSchemaName(channel) + " value=" + IsetToText(lowRange ? 1 : 0));
                }
                else
                {
                    WriteCommandResult(reqId, false, "hw_fault", "lnb iset failed", "channel=" + LnbChannelToSchemaName(channel) + " rc=" + rc.ToString());
                }

                return;
            }

            if (field == "isw")
            {
                bool lowLimit;
                if (!TryParseIsw(value, out lowLimit))
                {
                    WriteCommandResult(reqId, false, "validation_error", "lnb isw invalid", "value=" + value);
                    return;
                }

                int rc = LNBH26Tweaks.NativeSetIswLowForChannel(channel, lowLimit);
                if (rc == (int)LNBH26.Status.Ok)
                {
                    WriteCommandResult(reqId, true, "ok", "lnb isw", "channel=" + LnbChannelToSchemaName(channel) + " value=" + IswToText(lowLimit ? 1 : 0));
                }
                else
                {
                    WriteCommandResult(reqId, false, "hw_fault", "lnb isw failed", "channel=" + LnbChannelToSchemaName(channel) + " rc=" + rc.ToString());
                }

                return;
            }

            WriteCommandResult(reqId, false, "validation_error", "lnb field invalid", "field=" + field);
        }

        private static void EmitLnbShowSummaryLine(int channel)
        {
            if (!EnsureLnbInitialized())
            {
                if (_activeCommandTransport == CommandTransport.Usb)
                {
                    WriteHumanHeading("LNB " + LnbChannelToSchemaName(channel).ToUpper());
                    WriteHumanField("State", BuildLnbInitFailureText());
                }
                else
                {
                    _activeOutputSink("lnb." + LnbChannelToSchemaName(channel) + " state=init_failed " + BuildLnbInitDiagnosticData() + "\r\n");
                }
                return;
            }

            int pol = LNBH26.NativeGetPolarizationForChannel(channel);
            int band = LNBH26.NativeGetBandForChannel(channel);
            int isetLow = LNBH26Tweaks.NativeGetIsetLowForChannel(channel);
            int iswLow = LNBH26Tweaks.NativeGetIswLowForChannel(channel);

            int s1;
            int s2;
            int rc = ReadLnbStatusPairSafe(out s1, out s2);
            if (rc != (int)LNBH26.Status.Ok)
            {
                if (_activeCommandTransport == CommandTransport.Usb)
                {
                    WriteHumanHeading("LNB " + LnbChannelToSchemaName(channel).ToUpper());
                    WriteHumanField("Status", "Read failed (code " + rc.ToString() + ")");
                }
                else
                {
                    _activeOutputSink("lnb." + LnbChannelToSchemaName(channel) + " status=read_failed rc=" + rc.ToString() + "\r\n");
                }
                return;
            }

            int d1;
            int d2;
            int d3;
            int d4;
            rc = ReadLnbDataRegistersSafe(out d1, out d2, out d3, out d4);
            if (rc != (int)LNBH26.Status.Ok)
            {
                if (_activeCommandTransport == CommandTransport.Usb)
                {
                    WriteHumanHeading("LNB " + LnbChannelToSchemaName(channel).ToUpper());
                    WriteHumanField("Configuration", "Read failed (code " + rc.ToString() + ")");
                }
                else
                {
                    _activeOutputSink("lnb." + LnbChannelToSchemaName(channel) + " config=read_failed rc=" + rc.ToString() + "\r\n");
                }
                return;
            }

            if (_activeCommandTransport == CommandTransport.Usb)
            {
                WriteHumanHeading("LNB " + LnbChannelToSchemaName(channel).ToUpper());
                WriteHumanField("Polarization", PolarizationToText(pol));
                WriteHumanField("Band", BandToText(band));
                WriteHumanField("Current range", IsetToText(isetLow));
                WriteHumanField("Current limit", IswToText(iswLow));
                WriteHumanField("Voltage", VoltageSelectForChannelToText(channel, d1));
                WriteHumanField("Tone", IsToneEnabledForChannel(channel, d2) ? "On" : "Off");
                WriteHumanField("Low-power mode", IsLowPowerEnabledForChannel(channel, d2) ? "On" : "Off");
                WriteHumanField("External modulation", IsExtmEnabledForChannel(channel, d2) ? "On" : "Off");
                WriteHumanField("Status", HasFaultStatus(s1) ? "Fault" : "OK");
                WriteHumanField("Status registers", "S1 " + ToHexU8(s1) + ", S2 " + ToHexU8(s2));
                return;
            }

            _activeOutputSink(
                "lnb." + LnbChannelToSchemaName(channel) +
                " pol=" + PolarizationToText(pol) +
                " band=" + BandToText(band) +
                " iset=" + IsetToText(isetLow) +
                " isw=" + IswToText(iswLow) +
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
                if (_activeCommandTransport == CommandTransport.Usb)
                {
                    WriteHumanHeading("LNB " + LnbChannelToSchemaName(channel).ToUpper() + " details");
                    WriteHumanField("State", BuildLnbInitFailureText());
                }
                else
                {
                    _activeOutputSink("{\"schema\":\"cubley/v1/lnbh26\",\"channel\":\"" + LnbChannelToSchemaName(channel) + "\",\"error\":\"init_failed\",\"rc\":" + _lnbInitStatus.ToString() + "}\r\n");
                }
                return;
            }

            int s1;
            int s2;
            int rc = ReadLnbStatusPairSafe(out s1, out s2);
            if (rc != (int)LNBH26.Status.Ok)
            {
                if (_activeCommandTransport == CommandTransport.Usb)
                {
                    WriteHumanHeading("LNB " + LnbChannelToSchemaName(channel).ToUpper() + " details");
                    WriteHumanField("Status", "Read failed (code " + rc.ToString() + ")");
                }
                else
                {
                    _activeOutputSink(
                        "{\"schema\":\"cubley/v1/lnbh26\",\"channel\":\"" + LnbChannelToSchemaName(channel) +
                        "\",\"error\":\"status_read_failed\",\"rc\":" + rc.ToString() + "}\r\n");
                }
                return;
            }

            int d1;
            int d2;
            int d3;
            int d4;
            rc = ReadLnbDataRegistersSafe(out d1, out d2, out d3, out d4);
            if (rc != (int)LNBH26.Status.Ok)
            {
                if (_activeCommandTransport == CommandTransport.Usb)
                {
                    WriteHumanHeading("LNB " + LnbChannelToSchemaName(channel).ToUpper() + " details");
                    WriteHumanField("Configuration", "Read failed (code " + rc.ToString() + ")");
                }
                else
                {
                    _activeOutputSink(
                        "{\"schema\":\"cubley/v1/lnbh26\",\"channel\":\"" + LnbChannelToSchemaName(channel) +
                        "\",\"error\":\"data_read_failed\",\"rc\":" + rc.ToString() + "}\r\n");
                }
                return;
            }

            if (_activeCommandTransport == CommandTransport.Usb)
            {
                WriteHumanHeading("LNB " + LnbChannelToSchemaName(channel).ToUpper() + " details");
                WriteHumanField("Status 1", ToHexU8(s1));
                WriteHumanField("Status 2", ToHexU8(s2));
                WriteHumanField("Data 1", ToHexU8(d1));
                WriteHumanField("Data 2", ToHexU8(d2));
                WriteHumanField("Data 3", ToHexU8(d3));
                WriteHumanField("Data 4", ToHexU8(d4));
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

        private static string BuildLnbInitDiagnosticData()
        {
            int lastError = LNBH26.NativeGetLastError();
            int lastDetail = LNBH26.NativeGetLastErrorDetail();
            string data =
                "lnb_init_rc=" + _lnbInitStatus.ToString() +
                " lnb_init_status=" + LnbStatusToToken(_lnbInitStatus);

            if (lastError == (int)LNBH26.Status.IoError)
            {
                data +=
                    " i2c_error=" + LnbI2cDetailToToken(lastDetail) +
                    " i2c_detail=" + ToHexU8(lastDetail);
            }

            return data;
        }

        private static string BuildLnbInitFailureText()
        {
            int lastError = LNBH26.NativeGetLastError();
            int lastDetail = LNBH26.NativeGetLastErrorDetail();

            if (lastError == (int)LNBH26.Status.IoError)
            {
                return "Initialization failed: I2C " + LnbI2cDetailToToken(lastDetail) + " (" + ToHexU8(lastDetail) + ")";
            }

            return "Initialization failed: " + LnbStatusToToken(_lnbInitStatus) + " (code " + _lnbInitStatus.ToString() + ")";
        }

        private static string LnbStatusToToken(int status)
        {
            if (status == (int)LNBH26.Status.Ok)
            {
                return "ok";
            }

            if (status == (int)LNBH26.Status.InvalidParam)
            {
                return "invalid_param";
            }

            if (status == (int)LNBH26.Status.NotInitialized)
            {
                return "not_initialized";
            }

            if (status == (int)LNBH26.Status.IoError)
            {
                return "io_error";
            }

            return "unknown";
        }

        private static string LnbI2cDetailToToken(int detail)
        {
            if (detail == -1)
            {
                return "timeout";
            }

            if (detail == -2)
            {
                return "peripheral_error_unspecified";
            }

            if (detail == 0)
            {
                return "unspecified";
            }

            string text = string.Empty;
            AppendLnbI2cFlag(ref text, detail, 0x01, "bus_error");
            AppendLnbI2cFlag(ref text, detail, 0x02, "arbitration_lost");
            AppendLnbI2cFlag(ref text, detail, 0x04, "ack_failure");
            AppendLnbI2cFlag(ref text, detail, 0x08, "overrun");
            AppendLnbI2cFlag(ref text, detail, 0x10, "pec_error");
            AppendLnbI2cFlag(ref text, detail, 0x20, "hardware_timeout");
            AppendLnbI2cFlag(ref text, detail, 0x40, "smbus_alert");
            return text.Length > 0 ? text : "unknown";
        }

        private static void AppendLnbI2cFlag(ref string text, int detail, int mask, string label)
        {
            if ((detail & mask) == 0)
            {
                return;
            }

            if (text.Length > 0)
            {
                text += "+";
            }

            text += label;
        }

        private static void EmitLnbFaultSnapshot(string source, int sequence)
        {
            if (!EnsureLnbInitialized())
            {
                WriteStructuredDebug(
                    "LNB",
                    "schema=1 sub=lnb comp=fault operation=snapshot stat=error" +
                    " code=init_failed seq=" + sequence.ToString() +
                    " source=" + source +
                    " rc=" + _lnbInitStatus.ToString());
                return;
            }

            int s1;
            int s2;
            int statusRc = ReadLnbStatusPairSafe(out s1, out s2);
            if (statusRc != (int)LNBH26.Status.Ok)
            {
                int statusLastError = LNBH26.NativeGetLastError();
                int statusLastDetail = LNBH26.NativeGetLastErrorDetail();
                WriteStructuredDebug(
                    "LNB",
                    "schema=1 sub=lnb comp=fault operation=snapshot stat=error" +
                    " code=status_read_failed seq=" + sequence.ToString() +
                    " source=" + source +
                    " rc=" + statusRc.ToString() +
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
                WriteStructuredDebug(
                    "LNB",
                    "schema=1 sub=lnb comp=fault operation=snapshot stat=error" +
                    " code=data_read_failed seq=" + sequence.ToString() +
                    " source=" + source +
                    " rc=" + dataRc.ToString() +
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

            WriteStructuredDebug(
                "LNB",
                "schema=1 sub=lnb comp=fault operation=snapshot" +
                " stat=" + (HasFaultStatus(s1) ? "fault" : "ok") +
                " seq=" + sequence.ToString() +
                " source=" + source +
                " channel=a" +
                " pol=" + PolarizationToText(pol) +
                " band=" + BandToText(band) +
                " voltage=" + VoltageSelectForChannelToText(LnbChannelA, d1) +
                " tone=" + (IsToneEnabledForChannel(LnbChannelA, d2) ? "on" : "off") +
                " lpm=" + (IsLowPowerEnabledForChannel(LnbChannelA, d2) ? "on" : "off") +
                " extm=" + (IsExtmEnabledForChannel(LnbChannelA, d2) ? "on" : "off") +
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

            if (_lnbInitStatus != (int)LNBH26.Status.Ok)
            {
                WriteStructuredDebug(
                    "LNB",
                    "schema=1 sub=lnb comp=initialize operation=init stat=error " +
                    BuildLnbInitDiagnosticData());
            }

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
                WriteStructuredDebug(
                    "LNB",
                    "schema=1 sub=lnb comp=register operation=read_status stat=error" +
                    " rc=" + rc.ToString() +
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
                        WriteStructuredDebug(
                            "LNB",
                            "schema=1 sub=lnb comp=register operation=read_status_retry stat=error" +
                            " rc=" + rc.ToString() +
                            " s1=0x" + (s1 & 0xFF).ToString("X2") +
                            " s2=0x" + (s2 & 0xFF).ToString("X2") +
                            " last=" + retryLastError.ToString() +
                            " detail=" + retryLastDetail.ToString());
                    }
                }
            }

            return rc;
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
            if (token == "a")
            {
                channel = LnbChannelA;
                return true;
            }

            if (token == "b")
            {
                channel = 1;
                return true;
            }

            channel = -1;
            return false;
        }

    }
}
