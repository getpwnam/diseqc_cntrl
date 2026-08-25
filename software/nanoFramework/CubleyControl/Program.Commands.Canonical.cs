using Cubley.Interop;

namespace CubleyControl
{
    public static partial class Program
    {
        private static bool HandleCanonicalCommand(string lower, int reqId)
        {
            if (lower == "system.version.get")
            {
                WriteCommandResult(reqId, true, "ok", "system.version.get", "iface=cubley_v1_serial shell=main");
                return true;
            }

            if (lower == "system.capabilities.get")
            {
                WriteCommandResult(reqId, true, "ok", "system.capabilities.get", "domain.diseqc=1 domain.system=1 domain.azel=0 transport.serial=1 transport.mqtt=0 feature.status_bar=1 feature.config_fram=0");
                return true;
            }

            if (lower == "diseqc.lnb.get.status")
            {
                if (!EnsureLnbInitialized())
                {
                    WriteCommandResult(reqId, false, "hw_fault", "diseqc.lnb.get.status", "lnb_init_rc=" + _lnbInitStatus.ToString());
                    return true;
                }

                int s1;
                int s2;
                int rc = ReadLnbStatusPairSafe(out s1, out s2);
                if (rc == 0)
                {
                    WriteCommandResult(reqId, true, "ok", "diseqc.lnb.get.status", "s1=0x" + (s1 & 0xFF).ToString("X2") + " s2=0x" + (s2 & 0xFF).ToString("X2"));
                }
                else
                {
                    WriteCommandResult(reqId, false, "hw_fault", "diseqc.lnb.get.status", "rc=" + rc.ToString());
                }

                return true;
            }

            if (lower == "diseqc.lnb.get.pol")
            {
                if (!EnsureLnbInitialized())
                {
                    WriteCommandResult(reqId, false, "hw_fault", "diseqc.lnb.get.pol", "lnb_init_rc=" + _lnbInitStatus.ToString());
                    return true;
                }

                int pol = LNBH26.NativeGetPolarizationForChannel(LnbChannelA);
                WriteCommandResult(reqId, true, "ok", "diseqc.lnb.get.pol", "value=" + PolarizationToText(pol));
                return true;
            }

            if (lower == "diseqc.lnb.get.band")
            {
                if (!EnsureLnbInitialized())
                {
                    WriteCommandResult(reqId, false, "hw_fault", "diseqc.lnb.get.band", "lnb_init_rc=" + _lnbInitStatus.ToString());
                    return true;
                }

                int band = LNBH26.NativeGetBandForChannel(LnbChannelA);
                WriteCommandResult(reqId, true, "ok", "diseqc.lnb.get.band", "value=" + BandToText(band));
                return true;
            }

            string[] tokens = SplitTokens(lower);
            if (tokens.Length == 2 && tokens[0] == "diseqc.lnb.set.enable")
            {
                if (!EnsureLnbInitialized())
                {
                    WriteCommandResult(reqId, false, "hw_fault", "diseqc.lnb.set.enable", "lnb_init_rc=" + _lnbInitStatus.ToString());
                    return true;
                }

                bool enable;
                if (!TryParseOnOff(tokens[1], out enable))
                {
                    WriteCommandResult(reqId, false, "validation_error", "diseqc.lnb.set.enable", "value=" + tokens[1]);
                    return true;
                }

                int rc = LNBH26.NativeSetEnable(enable);
                if (rc == 0)
                {
                    WriteCommandResult(reqId, true, "ok", "diseqc.lnb.set.enable", "value=" + (enable ? "on" : "off"));
                }
                else
                {
                    WriteCommandResult(reqId, false, "hw_fault", "diseqc.lnb.set.enable", "rc=" + rc.ToString());
                }

                return true;
            }

            if (tokens.Length == 2 && tokens[0] == "diseqc.lnb.set.pol")
            {
                if (!EnsureLnbInitialized())
                {
                    WriteCommandResult(reqId, false, "hw_fault", "diseqc.lnb.set.pol", "lnb_init_rc=" + _lnbInitStatus.ToString());
                    return true;
                }

                int pol;
                if (!TryParsePolarization(tokens[1], out pol))
                {
                    WriteCommandResult(reqId, false, "validation_error", "diseqc.lnb.set.pol", "value=" + tokens[1]);
                    return true;
                }

                int rc = LNBH26.NativeSetPolarizationForChannel(LnbChannelA, pol);
                if (rc == 0)
                {
                    WriteCommandResult(reqId, true, "ok", "diseqc.lnb.set.pol", "value=" + PolarizationToText(pol));
                }
                else
                {
                    WriteCommandResult(reqId, false, "hw_fault", "diseqc.lnb.set.pol", "rc=" + rc.ToString());
                }

                return true;
            }

            if (tokens.Length == 2 && tokens[0] == "diseqc.lnb.set.band")
            {
                if (!EnsureLnbInitialized())
                {
                    WriteCommandResult(reqId, false, "hw_fault", "diseqc.lnb.set.band", "lnb_init_rc=" + _lnbInitStatus.ToString());
                    return true;
                }

                int band;
                if (!TryParseBand(tokens[1], out band))
                {
                    WriteCommandResult(reqId, false, "validation_error", "diseqc.lnb.set.band", "value=" + tokens[1]);
                    return true;
                }

                int rc = LNBH26.NativeSetBandForChannel(LnbChannelA, band);
                if (rc == 0)
                {
                    WriteCommandResult(reqId, true, "ok", "diseqc.lnb.set.band", "value=" + BandToText(band));
                }
                else
                {
                    WriteCommandResult(reqId, false, "hw_fault", "diseqc.lnb.set.band", "rc=" + rc.ToString());
                }

                return true;
            }

            return false;
        }
    }
}
