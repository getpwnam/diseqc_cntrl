using System;

namespace DiSEqC_Control.Mqtt
{
    internal static class MqttConfigCommandProcessor
    {
        private static bool TopicMatches(string topic, string commandSuffix)
        {
            return topic.EndsWith(commandSuffix);
        }

        public static bool TryHandle(
            string topic,
            string payload,
            RuntimeConfiguration runtimeConfig,
            Action<string, string> publishStatus,
            Action<string> publishError,
            Action publishEffectiveConfig,
            Action handleConfigSave,
            Action handleConfigReset,
            Action handleConfigReload,
            Action<string> handleConfigFramClear)
        {
            if (topic == null)
            {
                return false;
            }

            if (TopicMatches(topic, "/command/config/get"))
            {
                publishEffectiveConfig();
                return true;
            }

            if (TopicMatches(topic, "/command/config/set"))
            {
                if (!TryParseConfigPayload(payload, out string key, out string value))
                {
                    publishError("Config set payload must be key=value");
                    return true;
                }

                if (!runtimeConfig.TrySetValue(key, value, out string error))
                {
                    publishError(error);
                    return true;
                }

                publishStatus("config/updated", key);
                publishEffectiveConfig();
                return true;
            }

            if (TopicMatches(topic, "/command/config/save"))
            {
                handleConfigSave();
                return true;
            }

            if (TopicMatches(topic, "/command/config/reset"))
            {
                handleConfigReset();
                return true;
            }

            if (TopicMatches(topic, "/command/config/reload"))
            {
                handleConfigReload();
                return true;
            }

            if (TopicMatches(topic, "/command/config/fram_clear"))
            {
                handleConfigFramClear(payload ?? string.Empty);
                return true;
            }

            return false;
        }

        private static bool TryParseConfigPayload(string payload, out string key, out string value)
        {
            key = string.Empty;
            value = string.Empty;

            if (string.IsNullOrEmpty(payload))
            {
                return false;
            }

            int separator = payload.IndexOf('=');
            if (separator <= 0 || separator >= payload.Length - 1)
            {
                return false;
            }

            key = payload.Substring(0, separator).Trim();
            value = payload.Substring(separator + 1).Trim();
            return true;
        }
    }
}
