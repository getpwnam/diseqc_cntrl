using System;

namespace DiSEqC_Control
{
    internal sealed class RuntimeConfiguration
    {
        public bool UseDhcp = true;
        public string StaticIp = "192.168.1.100";
        public string StaticSubnetMask = "255.255.255.0";
        public string StaticGateway = "192.168.1.1";

        public string MqttBroker = "192.168.1.50";
        public int MqttPort = 1883;
        public string MqttClientId = "diseqc_controller";
        public string MqttUsername = "";
        public string MqttPassword = "";
        public string MqttTopicPrefix = "diseqc";

        public string DeviceName = "diseqc-ctrl";
        public string DeviceLocation = "default";

        public static RuntimeConfiguration CreateDefaults()
        {
            return new RuntimeConfiguration();
        }

        public RuntimeConfiguration Clone()
        {
            return new RuntimeConfiguration
            {
                UseDhcp = UseDhcp,
                StaticIp = StaticIp,
                StaticSubnetMask = StaticSubnetMask,
                StaticGateway = StaticGateway,
                MqttBroker = MqttBroker,
                MqttPort = MqttPort,
                MqttClientId = MqttClientId,
                MqttUsername = MqttUsername,
                MqttPassword = MqttPassword,
                MqttTopicPrefix = MqttTopicPrefix,
                DeviceName = DeviceName,
                DeviceLocation = DeviceLocation
            };
        }

        public bool TrySetValue(string key, string value, out string error)
        {
            if (string.IsNullOrEmpty(key))
            {
                error = "Config key is required";
                return false;
            }

            if (value == null)
            {
                value = string.Empty;
            }

            string normalizedKey = key.ToLower();

            switch (normalizedKey)
            {
                case "network.use_dhcp":
                    if (!TryParseBoolean(value, out bool useDhcp))
                    {
                        error = "network.use_dhcp must be true/false";
                        return false;
                    }
                    UseDhcp = useDhcp;
                    break;

                case "network.static_ip":
                    if (!IsValidIpv4(value))
                    {
                        error = "network.static_ip is not a valid IPv4 address";
                        return false;
                    }
                    StaticIp = value;
                    break;

                case "network.static_subnet":
                    if (!IsValidIpv4(value))
                    {
                        error = "network.static_subnet is not a valid IPv4 address";
                        return false;
                    }
                    StaticSubnetMask = value;
                    break;

                case "network.static_gateway":
                    if (!IsValidIpv4(value))
                    {
                        error = "network.static_gateway is not a valid IPv4 address";
                        return false;
                    }
                    StaticGateway = value;
                    break;

                case "mqtt.broker":
                    if (string.IsNullOrEmpty(value))
                    {
                        error = "mqtt.broker cannot be empty";
                        return false;
                    }
                    MqttBroker = value;
                    break;

                case "mqtt.port":
                    if (!int.TryParse(value, out int mqttPort) || mqttPort < 1 || mqttPort > 65535)
                    {
                        error = "mqtt.port must be 1..65535";
                        return false;
                    }
                    MqttPort = mqttPort;
                    break;

                case "mqtt.client_id":
                    if (string.IsNullOrEmpty(value))
                    {
                        error = "mqtt.client_id cannot be empty";
                        return false;
                    }
                    MqttClientId = value;
                    break;

                case "mqtt.username":
                    MqttUsername = value;
                    break;

                case "mqtt.password":
                    MqttPassword = value;
                    break;

                case "mqtt.topic_prefix":
                    if (string.IsNullOrEmpty(value))
                    {
                        error = "mqtt.topic_prefix cannot be empty";
                        return false;
                    }
                    MqttTopicPrefix = value;
                    break;

                case "system.device_name":
                    if (string.IsNullOrEmpty(value))
                    {
                        error = "system.device_name cannot be empty";
                        return false;
                    }
                    DeviceName = value;
                    break;

                case "system.location":
                    DeviceLocation = value;
                    break;

                default:
                    error = $"Unknown config key: {key}";
                    return false;
            }

            error = null;
            return true;
        }

        public string ToKeyValueLines()
        {
            return
                "network.use_dhcp=" + (UseDhcp ? "true" : "false") + "\n" +
                "network.static_ip=" + StaticIp + "\n" +
                "network.static_subnet=" + StaticSubnetMask + "\n" +
                "network.static_gateway=" + StaticGateway + "\n" +
                "mqtt.broker=" + MqttBroker + "\n" +
                "mqtt.port=" + MqttPort + "\n" +
                "mqtt.client_id=" + MqttClientId + "\n" +
                "mqtt.username=" + MqttUsername + "\n" +
                "mqtt.password=" + MqttPassword + "\n" +
                "mqtt.topic_prefix=" + MqttTopicPrefix + "\n" +
                "system.device_name=" + DeviceName + "\n" +
                "system.location=" + DeviceLocation;
        }

        public static bool TryParseKeyValueLines(string content, out RuntimeConfiguration configuration, out string error)
        {
            configuration = CreateDefaults();

            if (string.IsNullOrEmpty(content))
            {
                error = "Persisted config payload is empty";
                return false;
            }

            string[] lines = content.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                int separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    error = $"Invalid persisted config line: {line}";
                    return false;
                }

                string key = line.Substring(0, separator).Trim();
                string value = line.Substring(separator + 1).Trim();

                if (!configuration.TrySetValue(key, value, out error))
                {
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static bool TryParseBoolean(string value, out bool result)
        {
            string normalized = value.ToLower();

            if (normalized == "true" || normalized == "1" || normalized == "on")
            {
                result = true;
                return true;
            }

            if (normalized == "false" || normalized == "0" || normalized == "off")
            {
                result = false;
                return true;
            }

            result = false;
            return false;
        }

        private static bool IsValidIpv4(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            string[] parts = value.Split('.');
            if (parts.Length != 4)
            {
                return false;
            }

            for (int i = 0; i < parts.Length; i++)
            {
                if (!int.TryParse(parts[i], out int octet) || octet < 0 || octet > 255)
                {
                    return false;
                }
            }

            return true;
        }
    }
}