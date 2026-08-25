using System;

namespace CubleyControl
{
    internal sealed class NetworkConfiguration
    {
        public const string ModeDhcp = "dhcp";
        public const string ModeStatic = "static";

        public string Mode = ModeDhcp;
        public string Address = "0.0.0.0";
        public string SubnetMask = "0.0.0.0";
        public string Gateway = "0.0.0.0";
        public bool AutomaticDns = true;
        public string Dns1 = "0.0.0.0";
        public string Dns2 = "0.0.0.0";

        public static NetworkConfiguration CreateDefaults()
        {
            return new NetworkConfiguration();
        }

        public NetworkConfiguration Clone()
        {
            return new NetworkConfiguration
            {
                Mode = Mode,
                Address = Address,
                SubnetMask = SubnetMask,
                Gateway = Gateway,
                AutomaticDns = AutomaticDns,
                Dns1 = Dns1,
                Dns2 = Dns2
            };
        }

        public bool TryValidate(out string error)
        {
            if (Mode != ModeDhcp && Mode != ModeStatic)
            {
                error = "mode_invalid";
                return false;
            }

            if (Mode == ModeStatic)
            {
                uint address;
                uint mask;
                uint gateway;
                if (!TryParseIpv4(Address, out address) || address == 0 || (address >> 24) >= 224)
                {
                    error = "address_invalid";
                    return false;
                }

                if (!TryParseIpv4(SubnetMask, out mask) || !IsContiguousMask(mask))
                {
                    error = "mask_invalid";
                    return false;
                }

                if (!TryParseIpv4(Gateway, out gateway) || gateway == 0 || (address & mask) != (gateway & mask))
                {
                    error = "gateway_invalid";
                    return false;
                }

                uint hostMask = ~mask;
                uint host = address & hostMask;
                if (host == 0 || host == hostMask)
                {
                    error = "address_not_host";
                    return false;
                }
            }

            if (!AutomaticDns)
            {
                uint dns1;
                if (!TryParseIpv4(Dns1, out dns1) || dns1 == 0)
                {
                    error = "dns1_invalid";
                    return false;
                }

                if (Dns2 != "0.0.0.0")
                {
                    uint dns2;
                    if (!TryParseIpv4(Dns2, out dns2) || dns2 == 0)
                    {
                        error = "dns2_invalid";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }

        public string ToPayload()
        {
            return
                "mode=" + Mode + "\n" +
                "address=" + Address + "\n" +
                "mask=" + SubnetMask + "\n" +
                "gateway=" + Gateway + "\n" +
                "dns_mode=" + (AutomaticDns ? "auto" : "static") + "\n" +
                "dns1=" + Dns1 + "\n" +
                "dns2=" + Dns2;
        }

        public static bool TryParsePayload(string payload, out NetworkConfiguration configuration, out string error)
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
                if (line.Length == 0)
                {
                    continue;
                }

                int separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    error = "payload_line_invalid";
                    return false;
                }

                string key = line.Substring(0, separator);
                string value = line.Substring(separator + 1);
                if (key == "mode")
                {
                    configuration.Mode = value;
                }
                else if (key == "address")
                {
                    configuration.Address = value;
                }
                else if (key == "mask")
                {
                    configuration.SubnetMask = value;
                }
                else if (key == "gateway")
                {
                    configuration.Gateway = value;
                }
                else if (key == "dns_mode")
                {
                    if (value != "auto" && value != "static")
                    {
                        error = "dns_mode_invalid";
                        return false;
                    }

                    configuration.AutomaticDns = value == "auto";
                }
                else if (key == "dns1")
                {
                    configuration.Dns1 = value;
                }
                else if (key == "dns2")
                {
                    configuration.Dns2 = value;
                }
                else
                {
                    error = "payload_key_unknown";
                    return false;
                }
            }

            return configuration.TryValidate(out error);
        }

        private static bool TryParseIpv4(string value, out uint address)
        {
            address = 0;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            string[] parts = value.Split('.');
            if (parts.Length != 4)
            {
                return false;
            }

            for (int index = 0; index < parts.Length; index++)
            {
                int octet;
                if (!int.TryParse(parts[index], out octet) || octet < 0 || octet > 255)
                {
                    return false;
                }

                address = (address << 8) | (uint)octet;
            }

            return true;
        }

        private static bool IsContiguousMask(uint mask)
        {
            if (mask == 0 || mask == uint.MaxValue)
            {
                return false;
            }

            uint inverted = ~mask;
            return (inverted & (inverted + 1)) == 0;
        }
    }
}