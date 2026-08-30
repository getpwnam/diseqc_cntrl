using System;

namespace CubleyControl
{
    internal sealed class MqttConfiguration
    {
        public const int MaximumBrokerLength = 63;
        public const int MaximumClientIdLength = 64;
        public const int MaximumHostnameLength = 63;
        public const int MaximumUsernameLength = 48;
        public const int MaximumPasswordLength = 96;
        public const int MaximumTopicPrefixLength = 64;

        public bool Enabled;
        public string Broker = string.Empty;
        public int Port = 1883;
        public string ClientId = string.Empty;
        public string Hostname = string.Empty;
        public string Username = string.Empty;
        public string Password = string.Empty;
        public string TopicPrefix = "diseqc";
        public int KeepAliveSeconds = 60;
        public int ReconnectSeconds = 5;
        public int DiseqcLnbChannel;

        public static MqttConfiguration CreateDefaults()
        {
            return new MqttConfiguration();
        }

        public MqttConfiguration Clone()
        {
            return new MqttConfiguration
            {
                Enabled = Enabled,
                Broker = Broker,
                Port = Port,
                ClientId = ClientId,
                Hostname = Hostname,
                Username = Username,
                Password = Password,
                TopicPrefix = TopicPrefix,
                KeepAliveSeconds = KeepAliveSeconds,
                ReconnectSeconds = ReconnectSeconds,
                DiseqcLnbChannel = DiseqcLnbChannel
            };
        }

        public bool TryValidate(out string error)
        {
            if (Enabled && string.IsNullOrEmpty(Broker))
            {
                error = "broker_required";
                return false;
            }

            if (!IsValidToken(Broker, MaximumBrokerLength, true))
            {
                error = "broker_invalid";
                return false;
            }

            if (Port < 1 || Port > 65535)
            {
                error = "port_invalid";
                return false;
            }

            if (!IsValidToken(ClientId, MaximumClientIdLength, true))
            {
                error = "client_id_invalid";
                return false;
            }

            if (!IsValidHostname(Hostname))
            {
                error = "hostname_invalid";
                return false;
            }

            if (!IsValidToken(Username, MaximumUsernameLength, true))
            {
                error = "username_invalid";
                return false;
            }

            if (!IsValidToken(Password, MaximumPasswordLength, true))
            {
                error = "password_invalid";
                return false;
            }

            if (!IsValidToken(TopicPrefix, MaximumTopicPrefixLength, false) ||
                TopicPrefix.IndexOf('#') >= 0 || TopicPrefix.IndexOf('+') >= 0 ||
                TopicPrefix[0] == '/' || TopicPrefix[TopicPrefix.Length - 1] == '/')
            {
                error = "topic_prefix_invalid";
                return false;
            }

            if (KeepAliveSeconds < 15 || KeepAliveSeconds > 3600)
            {
                error = "keepalive_invalid";
                return false;
            }

            if (ReconnectSeconds < 1 || ReconnectSeconds > 60)
            {
                error = "reconnect_invalid";
                return false;
            }

            if (DiseqcLnbChannel != 0 && DiseqcLnbChannel != 1)
            {
                error = "diseqc_lnb_channel_invalid";
                return false;
            }

            if (ToPayload().Length > ApplicationConfigurationRecord.RecordSize - ApplicationConfigurationRecord.HeaderSize)
            {
                error = "payload_too_large";
                return false;
            }

            error = null;
            return true;
        }

        public string ToPayload()
        {
            return
                "enabled=" + (Enabled ? "true" : "false") + "\n" +
                "broker=" + Broker + "\n" +
                "port=" + Port.ToString() + "\n" +
                "client_id=" + ClientId + "\n" +
                "hostname=" + Hostname + "\n" +
                "username=" + Username + "\n" +
                "password=" + Password + "\n" +
                "topic_prefix=" + TopicPrefix + "\n" +
                "keepalive_seconds=" + KeepAliveSeconds.ToString() + "\n" +
                "reconnect_seconds=" + ReconnectSeconds.ToString() + "\n" +
                "diseqc_lnb_channel=" + (DiseqcLnbChannel == 1 ? "b" : "a");
        }

        public static bool TryParsePayload(string payload, out MqttConfiguration configuration, out string error)
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
                int number;
                if (key == "enabled")
                {
                    if (value != "true" && value != "false")
                    {
                        error = "enabled_invalid";
                        return false;
                    }
                    configuration.Enabled = value == "true";
                }
                else if (key == "broker")
                {
                    configuration.Broker = value;
                }
                else if (key == "port")
                {
                    if (!int.TryParse(value, out number))
                    {
                        error = "port_invalid";
                        return false;
                    }
                    configuration.Port = number;
                }
                else if (key == "client_id")
                {
                    configuration.ClientId = value;
                }
                else if (key == "hostname")
                {
                    configuration.Hostname = value;
                }
                else if (key == "username")
                {
                    configuration.Username = value;
                }
                else if (key == "password")
                {
                    configuration.Password = value;
                }
                else if (key == "topic_prefix")
                {
                    configuration.TopicPrefix = value;
                }
                else if (key == "keepalive_seconds")
                {
                    if (!int.TryParse(value, out number))
                    {
                        error = "keepalive_invalid";
                        return false;
                    }
                    configuration.KeepAliveSeconds = number;
                }
                else if (key == "reconnect_seconds")
                {
                    if (!int.TryParse(value, out number))
                    {
                        error = "reconnect_invalid";
                        return false;
                    }
                    configuration.ReconnectSeconds = number;
                }
                else if (key == "diseqc_lnb_channel")
                {
                    if (value == "a")
                    {
                        configuration.DiseqcLnbChannel = 0;
                    }
                    else if (value == "b")
                    {
                        configuration.DiseqcLnbChannel = 1;
                    }
                    else
                    {
                        error = "diseqc_lnb_channel_invalid";
                        return false;
                    }
                }
                else
                {
                    error = "payload_key_unknown";
                    return false;
                }
            }

            return configuration.TryValidate(out error);
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

        private static bool IsValidToken(string value, int maximumLength, bool allowEmpty)
        {
            if (string.IsNullOrEmpty(value))
            {
                return allowEmpty;
            }

            if (value.Length > maximumLength)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (character < '!' || character > '~')
                {
                    return false;
                }
            }

            return true;
        }
    }
}