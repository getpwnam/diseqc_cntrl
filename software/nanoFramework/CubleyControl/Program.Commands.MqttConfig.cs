using System;

namespace CubleyControl
{
    public static partial class Program
    {
        private static void EmitMqttStatus(int reqId)
        {
            if (_activeCommandTransport == CommandTransport.Usb)
            {
                WriteHumanHeading("MQTT");
                WriteHumanField("State", _mqttRuntimeState);
                WriteHumanField("Enabled", _mqttConfiguration.Enabled ? "Yes" : "No");
                WriteHumanField("Connection", _mqttClient != null && _mqttClient.IsConnected ? "Connected" : "Disconnected");
                WriteHumanField("Broker", ValueOrUnset(_mqttConfiguration.Broker));
                WriteHumanField("Reconnect attempts", _mqttReconnectAttempts.ToString());
                WriteHumanField("Last error", string.IsNullOrEmpty(_mqttLastError) ? "None" : _mqttLastError);
                return;
            }

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
            MqttConfiguration previous = _pendingMqttConfiguration.Clone();
            if ((field == "default" || field == "defaults") && tokens.Length == 3)
            {
                _pendingMqttConfiguration = MqttConfiguration.CreateDefaults();
                StageMqttChange(reqId, "defaults", "disabled", previous);
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
            if (field == "enabled" || field == "enable")
            {
                bool enabled;
                if (!TryParseOnOff(lowerValue, out enabled))
                {
                    WriteCommandResult(reqId, false, "validation_error", "mqtt enabled invalid", "value=" + lowerValue);
                    return;
                }
                _pendingMqttConfiguration.Enabled = enabled;
                StageMqttChange(reqId, "enabled", enabled ? "on" : "off", previous);
                return;
            }

            if (field == "broker" || field == "host")
            {
                _pendingMqttConfiguration.Broker = lowerValue == "clear" ? string.Empty : rawValue;
                StageMqttChange(reqId, "broker", ValueOrUnset(_pendingMqttConfiguration.Broker), previous);
                return;
            }

            if (field == "port" && int.TryParse(rawValue, out number))
            {
                _pendingMqttConfiguration.Port = number;
                StageMqttChange(reqId, field, rawValue, previous);
                return;
            }

            if (field == "client-id" || field == "client")
            {
                _pendingMqttConfiguration.ClientId = lowerValue == "auto" ? string.Empty : rawValue;
                StageMqttChange(reqId, "client-id", string.IsNullOrEmpty(_pendingMqttConfiguration.ClientId) ? "auto" : rawValue, previous);
                return;
            }

            if (field == "username" || field == "user")
            {
                _pendingMqttConfiguration.Username = lowerValue == "clear" ? string.Empty : rawValue;
                StageMqttChange(reqId, "username", string.IsNullOrEmpty(_pendingMqttConfiguration.Username) ? "clear" : "configured", previous);
                return;
            }

            if (field == "password" || field == "pass")
            {
                _pendingMqttConfiguration.Password = lowerValue == "clear" ? string.Empty : rawValue;
                StageMqttChange(reqId, "password", string.IsNullOrEmpty(_pendingMqttConfiguration.Password) ? "clear" : "configured", previous);
                return;
            }

            if (field == "topic-prefix" || field == "topic")
            {
                _pendingMqttConfiguration.TopicPrefix = rawValue;
                StageMqttChange(reqId, "topic-prefix", rawValue, previous);
                return;
            }

            if ((field == "keepalive" || field == "keep-alive") && int.TryParse(rawValue, out number))
            {
                _pendingMqttConfiguration.KeepAliveSeconds = number;
                StageMqttChange(reqId, "keepalive", rawValue, previous);
                return;
            }

            if (field == "reconnect" && int.TryParse(rawValue, out number))
            {
                _pendingMqttConfiguration.ReconnectSeconds = number;
                StageMqttChange(reqId, field, rawValue, previous);
                return;
            }

            WriteMqttSetUsage(reqId);
        }

        private static void StageMqttChange(int reqId, string field, string value, MqttConfiguration previous)
        {
            string error;
            if (!_pendingMqttConfiguration.TryValidate(out error) && error == "broker_required")
            {
                MqttConfiguration withoutBrokerRequirement = _pendingMqttConfiguration.Clone();
                withoutBrokerRequirement.Enabled = false;
                withoutBrokerRequirement.TryValidate(out error);
            }

            if (!string.IsNullOrEmpty(error))
            {
                _pendingMqttConfiguration = previous;
                WriteCommandResult(reqId, false, "validation_error", "mqtt value invalid", "field=" + field + " reason=" + error);
                return;
            }

            _mqttConfigurationDirty = _pendingMqttConfiguration.ToPayload() != _mqttConfiguration.ToPayload();
            WriteCommandResult(reqId, true, "ok", "mqtt staged", "field=" + field + " value=" + value + " state=" + (_mqttConfigurationDirty ? "staged" : "saved"));
        }

        private static void WriteMqttSetUsage(int reqId)
        {
            WriteCommandResult(reqId, false, "validation_error", "mqtt usage", "usage=mqtt <enabled on|off|broker HOST|port PORT|client-id ID|username VALUE|password VALUE|topic-prefix PREFIX|keepalive SEC|reconnect SEC|defaults>");
        }
    }
}