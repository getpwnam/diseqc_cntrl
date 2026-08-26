using System;
using System.Diagnostics;

namespace CubleyControl
{
    public static partial class Program
    {
        private static void HandleGetMqttCommand(string[] tokens, int reqId)
        {
            if (tokens.Length != 2)
            {
                WriteCommandResult(reqId, false, "validation_error", "get mqtt usage", "usage=get mqtt");
                return;
            }

            MqttConfiguration configuration = _pendingMqttConfiguration.Clone();
            _activeOutputSink("mqtt enabled=" + (configuration.Enabled ? "on" : "off") + " broker=" + ValueOrUnset(configuration.Broker) + " port=" + configuration.Port.ToString() + "\r\n");
            _activeOutputSink("mqtt client_id=" + (string.IsNullOrEmpty(configuration.ClientId) ? "auto" : configuration.ClientId) + " username_set=" + (string.IsNullOrEmpty(configuration.Username) ? "0" : "1") + " password_set=" + (string.IsNullOrEmpty(configuration.Password) ? "0" : "1") + "\r\n");
            _activeOutputSink("mqtt topic_prefix=" + configuration.TopicPrefix + " keepalive_seconds=" + configuration.KeepAliveSeconds.ToString() + " reconnect_seconds=" + configuration.ReconnectSeconds.ToString() + "\r\n");
            _activeOutputSink("config source=" + _mqttConfigurationSource + " generation=" + _mqttConfigurationGeneration.ToString() + " state=" + (_mqttConfigurationDirty ? "staged" : "saved") + "\r\n");
        }

        private static void EmitMqttStatus(int reqId)
        {
            _activeOutputSink("mqtt state=" + _mqttRuntimeState + " enabled=" + (_mqttConfiguration.Enabled ? "on" : "off") + " connected=" + (_mqttClient != null && _mqttClient.IsConnected ? "1" : "0") + "\r\n");
            _activeOutputSink("mqtt broker=" + ValueOrUnset(_mqttConfiguration.Broker) + " attempts=" + _mqttReconnectAttempts.ToString() + " last_error=" + (string.IsNullOrEmpty(_mqttLastError) ? "none" : _mqttLastError) + "\r\n");
            WriteCommandResult(reqId, true, "ok", "show mqtt", "state=" + _mqttRuntimeState);
        }

        private static void HandleSetMqttCommand(string[] tokens, string[] valueTokens, int reqId)
        {
            if (tokens.Length < 3 || valueTokens.Length != tokens.Length)
            {
                WriteMqttSetUsage(reqId);
                return;
            }

            string field = tokens[2];
            if ((field == "save" || field == "apply") && tokens.Length == 3)
            {
                SavePendingMqttConfiguration(reqId, field);
                return;
            }

            if (field == "discard" && tokens.Length == 3)
            {
                _pendingMqttConfiguration = _mqttConfiguration.Clone();
                _mqttConfigurationDirty = false;
                WriteCommandResult(reqId, true, "ok", "mqtt discarded", "state=saved");
                return;
            }

            if (field == "defaults" && tokens.Length == 3)
            {
                _pendingMqttConfiguration = MqttConfiguration.CreateDefaults();
                StageMqttChange(reqId, "defaults", "disabled");
                return;
            }

            if (tokens.Length != 4)
            {
                WriteMqttSetUsage(reqId);
                return;
            }

            string rawValue = valueTokens[3];
            string lowerValue = tokens[3];
            int number;
            if (field == "enabled")
            {
                bool enabled;
                if (!TryParseOnOff(lowerValue, out enabled))
                {
                    WriteCommandResult(reqId, false, "validation_error", "mqtt enabled invalid", "value=" + lowerValue);
                    return;
                }
                _pendingMqttConfiguration.Enabled = enabled;
                StageMqttChange(reqId, field, enabled ? "on" : "off");
                return;
            }

            if (field == "broker")
            {
                _pendingMqttConfiguration.Broker = lowerValue == "clear" ? string.Empty : rawValue;
                StageMqttChange(reqId, field, ValueOrUnset(_pendingMqttConfiguration.Broker));
                return;
            }

            if (field == "port" && int.TryParse(rawValue, out number))
            {
                _pendingMqttConfiguration.Port = number;
                StageMqttChange(reqId, field, rawValue);
                return;
            }

            if (field == "client-id")
            {
                _pendingMqttConfiguration.ClientId = lowerValue == "auto" ? string.Empty : rawValue;
                StageMqttChange(reqId, field, string.IsNullOrEmpty(_pendingMqttConfiguration.ClientId) ? "auto" : rawValue);
                return;
            }

            if (field == "username")
            {
                _pendingMqttConfiguration.Username = lowerValue == "clear" ? string.Empty : rawValue;
                StageMqttChange(reqId, field, string.IsNullOrEmpty(_pendingMqttConfiguration.Username) ? "clear" : "configured");
                return;
            }

            if (field == "password")
            {
                _pendingMqttConfiguration.Password = lowerValue == "clear" ? string.Empty : rawValue;
                StageMqttChange(reqId, field, string.IsNullOrEmpty(_pendingMqttConfiguration.Password) ? "clear" : "configured");
                return;
            }

            if (field == "topic-prefix")
            {
                _pendingMqttConfiguration.TopicPrefix = rawValue;
                StageMqttChange(reqId, field, rawValue);
                return;
            }

            if (field == "keepalive" && int.TryParse(rawValue, out number))
            {
                _pendingMqttConfiguration.KeepAliveSeconds = number;
                StageMqttChange(reqId, field, rawValue);
                return;
            }

            if (field == "reconnect" && int.TryParse(rawValue, out number))
            {
                _pendingMqttConfiguration.ReconnectSeconds = number;
                StageMqttChange(reqId, field, rawValue);
                return;
            }

            WriteMqttSetUsage(reqId);
        }

        private static void StageMqttChange(int reqId, string field, string value)
        {
            string error;
            if (!_pendingMqttConfiguration.TryValidate(out error) && error != "broker_required")
            {
                WriteCommandResult(reqId, false, "validation_error", "mqtt value invalid", "field=" + field + " reason=" + error);
                return;
            }

            _mqttConfigurationDirty = _pendingMqttConfiguration.ToPayload() != _mqttConfiguration.ToPayload();
            WriteCommandResult(reqId, true, "ok", "mqtt staged", "field=" + field + " value=" + value + " state=" + (_mqttConfigurationDirty ? "staged" : "saved"));
        }

        private static void SavePendingMqttConfiguration(int reqId, string action)
        {
            MqttConfiguration candidate = _pendingMqttConfiguration.Clone();
            string error;
            if (!candidate.TryValidate(out error))
            {
                WriteCommandResult(reqId, false, "validation_error", "mqtt not saved", "reason=" + error);
                return;
            }

            uint savedGeneration;
            bool saved;
            try
            {
                saved = _applicationConfigurationStorage.TrySave(
                    candidate,
                    _mqttConfigurationGeneration,
                    out savedGeneration,
                    out error);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[MQTT-CONFIG] save exception: " + ex.Message);
                WriteCommandResult(reqId, false, "persist_failed", "mqtt not saved", "reason=storage_exception");
                return;
            }

            if (!saved)
            {
                WriteCommandResult(reqId, false, "persist_failed", "mqtt not saved", "reason=" + error);
                return;
            }

            lock (_mqttConfigurationLock)
            {
                _mqttConfiguration = candidate;
                _mqttConfigurationGeneration = savedGeneration;
                _mqttConfigurationRevision++;
            }
            _pendingMqttConfiguration = candidate.Clone();
            _mqttConfigurationSource = _applicationConfigurationStorage.Source;
            _mqttConfigurationError = string.Empty;
            _mqttConfigurationDirty = false;
            WriteCommandResult(reqId, true, "ok", "mqtt " + action, "persisted=1 generation=" + savedGeneration.ToString() + " reconnect_required=1");
        }

        private static void WriteMqttSetUsage(int reqId)
        {
            WriteCommandResult(reqId, false, "validation_error", "set mqtt usage", "usage=set mqtt <enabled on|off|broker HOST|port PORT|client-id ID|username VALUE|password VALUE|topic-prefix PREFIX|keepalive SEC|reconnect SEC|save|apply|discard|defaults>");
        }
    }
}