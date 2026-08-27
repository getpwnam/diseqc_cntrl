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
                WriteHumanField("Hostname", ResolveHostname(_mqttConfiguration.Hostname));
                WriteHumanField("Client ID", ResolveMqttClientId(_mqttConfiguration));
                WriteHumanField("Topic root", BuildMqttTopicRoot(_mqttConfiguration));
                WriteHumanField("Reconnect attempts", _mqttReconnectAttempts.ToString());
                WriteHumanField("Last error", string.IsNullOrEmpty(_mqttLastError) ? "None" : _mqttLastError);
                return;
            }

            _activeOutputSink("mqtt state=" + _mqttRuntimeState + " enabled=" + (_mqttConfiguration.Enabled ? "on" : "off") + " connected=" + (_mqttClient != null && _mqttClient.IsConnected ? "1" : "0") + "\r\n");
            _activeOutputSink("mqtt broker=" + ValueOrUnset(_mqttConfiguration.Broker) + " hostname=" + ResolveHostname(_mqttConfiguration.Hostname) + " client_id=" + ResolveMqttClientId(_mqttConfiguration) + " topic_root=" + BuildMqttTopicRoot(_mqttConfiguration) + " attempts=" + _mqttReconnectAttempts.ToString() + " last_error=" + (string.IsNullOrEmpty(_mqttLastError) ? "none" : _mqttLastError) + "\r\n");
            WriteCommandResult(reqId, true, "ok", "show mqtt", "state=" + _mqttRuntimeState);
        }

        private static void HandleSetHostnameCommand(string[] tokens, int reqId)
        {
            if (tokens.Length != 2)
            {
                WriteCommandResult(reqId, false, "validation_error", "hostname usage", "usage=hostname <name|auto>");
                return;
            }

            MqttConfiguration previous = _pendingMqttConfiguration.Clone();
            _pendingMqttConfiguration.Hostname = tokens[1] == "auto" ? string.Empty : tokens[1];
            StageApplicationChange(
                reqId,
                "hostname",
                string.IsNullOrEmpty(_pendingMqttConfiguration.Hostname) ? "auto" : _pendingMqttConfiguration.Hostname,
                previous);
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
                string hostname = _pendingMqttConfiguration.Hostname;
                _pendingMqttConfiguration = MqttConfiguration.CreateDefaults();
                _pendingMqttConfiguration.Hostname = hostname;
                StageApplicationChange(reqId, "mqtt-defaults", "disabled", previous);
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
                StageApplicationChange(reqId, "enabled", enabled ? "on" : "off", previous);
                return;
            }

            if (field == "broker" || field == "host")
            {
                _pendingMqttConfiguration.Broker = lowerValue == "clear" ? string.Empty : rawValue;
                StageApplicationChange(reqId, "broker", ValueOrUnset(_pendingMqttConfiguration.Broker), previous);
                return;
            }

            if (field == "port" && int.TryParse(rawValue, out number))
            {
                _pendingMqttConfiguration.Port = number;
                StageApplicationChange(reqId, field, rawValue, previous);
                return;
            }

            if (field == "client-id" || field == "client")
            {
                _pendingMqttConfiguration.ClientId = lowerValue == "auto" ? string.Empty : rawValue;
                StageApplicationChange(reqId, "client-id", string.IsNullOrEmpty(_pendingMqttConfiguration.ClientId) ? "auto" : rawValue, previous);
                return;
            }

            if (field == "username" || field == "user")
            {
                _pendingMqttConfiguration.Username = lowerValue == "clear" ? string.Empty : rawValue;
                StageApplicationChange(reqId, "username", string.IsNullOrEmpty(_pendingMqttConfiguration.Username) ? "clear" : "configured", previous);
                return;
            }

            if (field == "password" || field == "pass")
            {
                _pendingMqttConfiguration.Password = lowerValue == "clear" ? string.Empty : rawValue;
                StageApplicationChange(reqId, "password", string.IsNullOrEmpty(_pendingMqttConfiguration.Password) ? "clear" : "configured", previous);
                return;
            }

            if (field == "topic-prefix" || field == "topic")
            {
                _pendingMqttConfiguration.TopicPrefix = rawValue;
                StageApplicationChange(reqId, "topic-prefix", rawValue, previous);
                return;
            }

            if ((field == "keepalive" || field == "keep-alive") && int.TryParse(rawValue, out number))
            {
                _pendingMqttConfiguration.KeepAliveSeconds = number;
                StageApplicationChange(reqId, "keepalive", rawValue, previous);
                return;
            }

            if (field == "reconnect" && int.TryParse(rawValue, out number))
            {
                _pendingMqttConfiguration.ReconnectSeconds = number;
                StageApplicationChange(reqId, field, rawValue, previous);
                return;
            }

            WriteMqttSetUsage(reqId);
        }

        private static void StageApplicationChange(int reqId, string field, string value, MqttConfiguration previous)
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
            WriteCommandResult(reqId, true, "ok", "configuration staged", "field=" + field + " value=" + value + " state=" + (_mqttConfigurationDirty ? "staged" : "saved"));
        }

        private static void WriteMqttSetUsage(int reqId)
        {
            WriteCommandResult(reqId, false, "validation_error", "mqtt usage", "usage=mqtt <enabled on|off|broker HOST|port PORT|client-id ID|username VALUE|password VALUE|topic-prefix PREFIX|keepalive SEC|reconnect SEC|defaults>");
        }
    }
}