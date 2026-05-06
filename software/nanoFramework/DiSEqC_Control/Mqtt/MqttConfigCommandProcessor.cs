namespace DiSEqC_Control.Mqtt
{
    internal static class MqttConfigCommandProcessor
    {
        private static bool TopicMatches(string topic, string commandSuffix)
        {
            return topic.EndsWith(commandSuffix);
        }

        public static bool TryHandle(string topic, string payload, RuntimeConfiguration runtimeConfig, IMqttConfigSink sink)
        {
            if (topic == null || sink == null)
            {
                return false;
            }

            if (TopicMatches(topic, "/command/config/get"))
            {
                sink.PublishEffectiveConfig();
                return true;
            }

            if (TopicMatches(topic, "/command/config/set"))
            {
                if (!TryParseConfigPayload(payload, out string key, out string value))
                {
                    sink.PublishError("Config set payload must be key=value");
                    return true;
                }

                if (!runtimeConfig.TrySetValue(key, value, out string error))
                {
                    sink.PublishError(error);
                    return true;
                }

                sink.PublishStatus("config/updated", key);
                sink.PublishEffectiveConfig();
                return true;
            }

            if (TopicMatches(topic, "/command/config/save")) { sink.HandleConfigSave(); return true; }
            if (TopicMatches(topic, "/command/config/reset")) { sink.HandleConfigReset(); return true; }
            if (TopicMatches(topic, "/command/config/reload")) { sink.HandleConfigReload(); return true; }
            if (TopicMatches(topic, "/command/config/fram_clear")) { sink.HandleConfigFramClear(payload ?? string.Empty); return true; }

            return false;
        }

        private static bool TryParseConfigPayload(string payload, out string key, out string value)
        {
            key = string.Empty;
            value = string.Empty;
            if (string.IsNullOrEmpty(payload)) return false;
            int separator = payload.IndexOf('=');
            if (separator <= 0 || separator >= payload.Length - 1) return false;
            key = payload.Substring(0, separator).Trim();
            value = payload.Substring(separator + 1).Trim();
            return true;
        }
    }
}
