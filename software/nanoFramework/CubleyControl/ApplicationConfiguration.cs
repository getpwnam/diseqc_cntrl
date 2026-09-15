using System;

namespace CubleyControl
{
    internal sealed class ApplicationConfiguration
    {
        public const int MaximumHostnameLength = 63;
        public const int MaximumDiseqcAngleMicrodegrees = 180_000_000;

        public string Hostname = string.Empty;
        public int DiseqcEastLimitMicrodegrees;
        public int DiseqcWestLimitMicrodegrees;
        public int DiseqcEastStepMicrodegrees;
        public int DiseqcWestStepMicrodegrees;
        public int DiseqcGotoOffsetMicrodegrees;

        public static ApplicationConfiguration CreateDefaults()
        {
            return new ApplicationConfiguration();
        }

        public ApplicationConfiguration Clone()
        {
            return new ApplicationConfiguration
            {
                Hostname = Hostname,
                DiseqcEastLimitMicrodegrees = DiseqcEastLimitMicrodegrees,
                DiseqcWestLimitMicrodegrees = DiseqcWestLimitMicrodegrees,
                DiseqcEastStepMicrodegrees = DiseqcEastStepMicrodegrees,
                DiseqcWestStepMicrodegrees = DiseqcWestStepMicrodegrees,
                DiseqcGotoOffsetMicrodegrees = DiseqcGotoOffsetMicrodegrees
            };
        }

        public bool TryValidate(out string error)
        {
            if (!IsValidHostname(Hostname))
            {
                error = "hostname_invalid";
                return false;
            }

            if (!IsValidDiseqcPair(DiseqcEastLimitMicrodegrees, DiseqcWestLimitMicrodegrees))
            {
                error = "diseqc_limits_invalid";
                return false;
            }

            if (!IsValidDiseqcPair(DiseqcEastStepMicrodegrees, DiseqcWestStepMicrodegrees))
            {
                error = "diseqc_steps_invalid";
                return false;
            }

            if (DiseqcGotoOffsetMicrodegrees < -MaximumDiseqcAngleMicrodegrees ||
                DiseqcGotoOffsetMicrodegrees > MaximumDiseqcAngleMicrodegrees)
            {
                error = "diseqc_offset_invalid";
                return false;
            }

            error = null;
            return true;
        }

        public string ToPayload()
        {
            return
                "hostname=" + Hostname + "\n" +
                "de_lim=" + DiseqcEastLimitMicrodegrees.ToString() + "\n" +
                "dw_lim=" + DiseqcWestLimitMicrodegrees.ToString() + "\n" +
                "de_step=" + DiseqcEastStepMicrodegrees.ToString() + "\n" +
                "dw_step=" + DiseqcWestStepMicrodegrees.ToString() + "\n" +
                "d_offset=" + DiseqcGotoOffsetMicrodegrees.ToString();
        }

        public static bool TryParsePayload(string payload, out ApplicationConfiguration configuration, out string error)
        {
            configuration = CreateDefaults();
            if (string.IsNullOrEmpty(payload))
            {
                error = "payload_empty";
                return false;
            }

            string[] lines = payload.Split('\n');
            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index].Trim();
                int separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    error = "payload_line_invalid";
                    return false;
                }

                string key = line.Substring(0, separator);
                string value = line.Substring(separator + 1);
                int number;
                if (key == "hostname")
                {
                    configuration.Hostname = value;
                }
                else if (key == "de_lim")
                {
                    if (!int.TryParse(value, out number))
                    {
                        error = key + "_invalid";
                        return false;
                    }
                    configuration.DiseqcEastLimitMicrodegrees = number;
                }
                else if (key == "dw_lim")
                {
                    if (!int.TryParse(value, out number))
                    {
                        error = key + "_invalid";
                        return false;
                    }
                    configuration.DiseqcWestLimitMicrodegrees = number;
                }
                else if (key == "de_step")
                {
                    if (!int.TryParse(value, out number))
                    {
                        error = key + "_invalid";
                        return false;
                    }
                    configuration.DiseqcEastStepMicrodegrees = number;
                }
                else if (key == "dw_step")
                {
                    if (!int.TryParse(value, out number))
                    {
                        error = key + "_invalid";
                        return false;
                    }
                    configuration.DiseqcWestStepMicrodegrees = number;
                }
                else if (key == "d_offset")
                {
                    if (!int.TryParse(value, out number))
                    {
                        error = key + "_invalid";
                        return false;
                    }
                    configuration.DiseqcGotoOffsetMicrodegrees = number;
                }
                else
                {
                    error = "payload_key_unknown";
                    return false;
                }
            }

            return configuration.TryValidate(out error);
        }

        private static bool IsValidDiseqcPair(int eastMicrodegrees, int westMicrodegrees)
        {
            if (eastMicrodegrees == 0 && westMicrodegrees == 0)
            {
                return true;
            }

            return eastMicrodegrees > 0 && eastMicrodegrees <= MaximumDiseqcAngleMicrodegrees &&
                westMicrodegrees > 0 && westMicrodegrees <= MaximumDiseqcAngleMicrodegrees;
        }

        private static bool IsValidHostname(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return true;
            }

            if (value.Length > MaximumHostnameLength || value[0] == '-' || value[value.Length - 1] == '-')
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if ((character < 'a' || character > 'z') &&
                    (character < '0' || character > '9') && character != '-')
                {
                    return false;
                }
            }

            return true;
        }
    }
}