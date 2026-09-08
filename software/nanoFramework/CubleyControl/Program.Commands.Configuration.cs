using Cubley.Diseqc;

namespace CubleyControl
{
    public static partial class Program
    {
        private static bool _usbConfigurationMode;
        private static bool _usbDebugEnabled;
        private static bool _activeCommandIsSetter;

        private static string GetUsbPrompt()
        {
            string hostname = ResolveHostname(_mqttConfiguration.Hostname);
            if (!_usbConfigurationMode)
            {
                return hostname + "> ";
            }

            return IsConfigurationDirty() ? hostname + "(config*)# " : hostname + "(config)# ";
        }

        private static bool IsConfigureCommand(string[] tokens)
        {
            return (tokens.Length == 1 &&
                    (tokens[0] == "configure" || tokens[0] == "config" || tokens[0] == "conf")) ||
                (tokens.Length == 2 &&
                    (tokens[0] == "configure" || tokens[0] == "config" || tokens[0] == "conf") &&
                    (tokens[1] == "terminal" || tokens[1] == "t"));
        }

        private static void BeginUsbConfigurationSession(int reqId)
        {
            _pendingNetworkConfiguration = _networkConfiguration.Clone();
            _pendingMqttConfiguration = _mqttConfiguration.Clone();
            _networkConfigurationDirty = false;
            _mqttConfigurationDirty = false;
            _watchEnabled = false;
            _usbConfigurationMode = true;
            WriteCommandResult(reqId, true, "ok", "configuration mode", "mode=config");
        }

        private static void ResetUsbConfigurationSession()
        {
            lock (_commandLock)
            {
                _pendingNetworkConfiguration = _networkConfiguration.Clone();
                _pendingMqttConfiguration = _mqttConfiguration.Clone();
                _networkConfigurationDirty = false;
                _mqttConfigurationDirty = false;
                _watchEnabled = false;
                _usbConfigurationMode = false;
                _usbDebugEnabled = false;
            }
        }

        private static void HandleConfigurationModeCommand(string[] tokens, string[] valueTokens, int reqId)
        {
            string head = tokens[0];
            if (head == "help" || head == "h" || head == "?")
            {
                HandleConfigurationHelp(tokens, reqId);
                return;
            }

            if (head == "network" || head == "net")
            {
                HandleSetNetworkCommand(PrefixConfigurationTokens(tokens, "network"), reqId);
                return;
            }

            if (head == "hostname")
            {
                HandleSetHostnameCommand(tokens, reqId);
                return;
            }

            if (head == "mqtt" || head == "mq")
            {
                HandleSetMqttCommand(
                    PrefixConfigurationTokens(tokens, "mqtt"),
                    PrefixConfigurationTokens(valueTokens, "mqtt"),
                    reqId);
                return;
            }

            if (head == "diseqc")
            {
                HandleSetDiseqcConfigurationCommand(tokens, reqId);
                return;
            }

            if (head == "show")
            {
                if (tokens.Length == 2 &&
                    (tokens[1] == "storage" || tokens[1] == "configuration-storage" || tokens[1] == "config-storage"))
                {
                    EmitConfigurationStorageStatus();
                    return;
                }

                if (tokens.Length >= 2 && (tokens[1] == "running-config" || tokens[1] == "run"))
                {
                    HandleShowConfigurationCommand(tokens, false, reqId);
                    return;
                }

                if (tokens.Length >= 2 && (tokens[1] == "startup-config" || tokens[1] == "start"))
                {
                    HandleShowConfigurationCommand(tokens, true, reqId);
                    return;
                }

                if (tokens.Length >= 2 &&
                    (tokens[1] == "candidate-config" || tokens[1] == "candidate" || tokens[1] == "cand"))
                {
                    EmitConfigurationDocument("candidate", _pendingNetworkConfiguration, _pendingMqttConfiguration, GetConfigurationDomain(tokens, 2, reqId));
                    return;
                }

                if ((tokens.Length == 2 && tokens[1] == "diff") ||
                    (tokens.Length == 3 && tokens[1] == "config" && tokens[2] == "diff"))
                {
                    EmitConfigurationDiff(reqId);
                    return;
                }
            }

            if (head == "debug")
            {
                if (tokens.Length != 2 || (tokens[1] != "on" && tokens[1] != "off"))
                {
                    WriteCommandResult(reqId, false, "validation_error", "debug usage", "usage=debug <on|off>");
                    return;
                }

                _usbDebugEnabled = tokens[1] == "on";
                WriteCommandResult(reqId, true, "ok", "debug " + tokens[1], "debug=" + tokens[1]);
                return;
            }

            if (head == "commit" || head == "apply")
            {
                if (tokens.Length != 1)
                {
                    WriteCommandResult(reqId, false, "validation_error", "commit usage", "usage=commit");
                    return;
                }

                CommitCandidateConfiguration(reqId);
                return;
            }

            if (head == "discard" || head == "abort")
            {
                if (tokens.Length != 1)
                {
                    WriteCommandResult(reqId, false, "validation_error", "discard usage", "usage=discard");
                    return;
                }

                _pendingNetworkConfiguration = _networkConfiguration.Clone();
                _pendingMqttConfiguration = _mqttConfiguration.Clone();
                _networkConfigurationDirty = false;
                _mqttConfigurationDirty = false;
                WriteCommandResult(reqId, true, "ok", "configuration discarded", "state=clean");
                return;
            }

            if (head == "load" || head == "defaults")
            {
                HandleLoadDefaults(tokens, reqId);
                return;
            }

            if (head == "exit" || head == "end")
            {
                if (tokens.Length != 1)
                {
                    WriteCommandResult(reqId, false, "validation_error", "exit usage", "usage=exit");
                    return;
                }

                if (_networkConfigurationDirty || _mqttConfigurationDirty)
                {
                    _activeOutputSink("Warning: uncommitted changes. Use 'commit' to apply or 'discard' to abandon them.\r\n");
                    return;
                }

                _usbConfigurationMode = false;
                WriteCommandResult(reqId, true, "ok", "operational mode", "mode=operational");
                return;
            }

            WriteCommandResult(reqId, false, "unsupported", "unknown configuration command", "cmd=" + head);
        }

        private static void HandleConfigurationHelp(string[] tokens, int reqId)
        {
            if (tokens.Length == 1)
            {
                WriteHumanHeading("Configuration syntax");
                _activeOutputSink(
                    "hostname <name|auto>\r\n" +
                    "network <mode dhcp|static|address IP|mask MASK|gateway IP|dns auto|dns static DNS1 [DNS2]|defaults>\r\n" +
                    "mqtt <enabled on|off|broker <HOST|clear>|port PORT|client-id <ID|auto>|username <VALUE|clear>|password <VALUE|clear>|topic-prefix PREFIX|keepalive SEC|reconnect SEC|default|defaults>\r\n" +
                    "diseqc <angle-limits EAST WEST|step-calibration EAST WEST|fixed-offset east|west DEGREES|defaults|off>\r\n" +
                    "show <running-config|run|startup-config|start|candidate-config|candidate|cand> [network|mqtt|diseqc]\r\n" +
                    "show storage|configuration-storage|config-storage\r\n" +
                    "show diff | show config diff\r\n" +
                    "debug <on|off>\r\n" +
                    "commit|apply\r\n" +
                    "discard|abort\r\n" +
                    "load defaults [network|mqtt|all]\r\n" +
                    "defaults [network|mqtt|all]\r\n" +
                    "exit|end\r\n" +
                    "help|h|? [command]\r\n" +
                    "quit|logout\r\n");
                return;
            }

            if (tokens.Length != 2)
            {
                _activeOutputSink("Usage: help [command]\r\n");
                return;
            }

            string topic = tokens[1];
            if (topic == "network" || topic == "net")
            {
                WriteHumanHeading("Network syntax");
                _activeOutputSink("network <mode dhcp|static|address IP|mask MASK|gateway IP|dns auto|dns static DNS1 [DNS2]|defaults>\r\n");
                return;
            }

            if (topic == "hostname")
            {
                WriteHumanHeading("Hostname syntax");
                _activeOutputSink("hostname <name|auto>\r\n");
                return;
            }

            if (topic == "mqtt" || topic == "mq")
            {
                WriteHumanHeading("MQTT syntax");
                _activeOutputSink("mqtt <enabled on|off|broker HOST|port PORT|client-id ID|username VALUE|password VALUE|topic-prefix PREFIX|keepalive SEC|reconnect SEC|default|defaults>\r\n");
                return;
            }

            if (topic == "diseqc")
            {
                WriteHumanHeading("DiSEqC configuration syntax");
                _activeOutputSink("diseqc <angle-limits EAST WEST|step-calibration EAST WEST|fixed-offset east|west DEGREES|defaults|off>\r\n");
                return;
            }

            if (topic == "show")
            {
                WriteHumanHeading("Show syntax");
                _activeOutputSink(
                    "show <running-config|run|startup-config|start|candidate-config|candidate|cand> [network|mqtt|diseqc]\r\n" +
                    "show storage|configuration-storage|config-storage\r\n" +
                    "show diff | show config diff\r\n");
                return;
            }

            if (topic == "debug")
            {
                WriteHumanHeading("Debug syntax");
                _activeOutputSink("debug <on|off>\r\n");
                return;
            }

            if (topic == "commit" || topic == "apply")
            {
                WriteHumanHeading("Commit syntax");
                _activeOutputSink("commit|apply\r\n");
                return;
            }

            if (topic == "discard" || topic == "abort")
            {
                WriteHumanHeading("Discard syntax");
                _activeOutputSink("discard|abort\r\n");
                return;
            }

            if (topic == "load" || topic == "defaults")
            {
                WriteHumanHeading("Defaults syntax");
                _activeOutputSink("load defaults [network|mqtt|all]\r\ndefaults [network|mqtt|all]\r\n");
                return;
            }

            if (topic == "exit" || topic == "end")
            {
                WriteHumanHeading("Exit syntax");
                _activeOutputSink("exit|end\r\n");
                return;
            }

            if (topic == "quit" || topic == "logout")
            {
                WriteHumanHeading("Session syntax");
                _activeOutputSink("quit|logout\r\n");
                return;
            }

            _activeOutputSink("No help available for '" + topic + "'.\r\n");
        }

        private static void EmitConfigurationStorageStatus()
        {
            WriteHumanHeading("Configuration storage");
            WriteHumanField("Network backend", string.IsNullOrEmpty(_networkConfigurationSource) ? "Unknown" : _networkConfigurationSource);
            WriteHumanField("Network status", string.IsNullOrEmpty(_networkConfigurationError) ? "OK" : _networkConfigurationError);
            WriteHumanField("Application backend", string.IsNullOrEmpty(_mqttConfigurationSource) ? "Unknown" : _mqttConfigurationSource);
            WriteHumanField("Application status", string.IsNullOrEmpty(_mqttConfigurationError) ? "OK" : _mqttConfigurationError);
        }

        private static void HandleLoadDefaults(string[] tokens, int reqId)
        {
            int domainIndex = tokens[0] == "load" ? 2 : 1;
            if ((tokens[0] == "load" && (tokens.Length < 2 || tokens[1] != "defaults")) ||
                tokens.Length > domainIndex + 1)
            {
                WriteCommandResult(reqId, false, "validation_error", "load defaults usage", "usage=load defaults [network|mqtt|all]");
                return;
            }

            string domain = tokens.Length > domainIndex ? tokens[domainIndex] : "all";
            if (domain == "network" || domain == "net" || domain == "all")
            {
                _pendingNetworkConfiguration = NetworkConfiguration.CreateDefaults();
                _networkConfigurationDirty = _pendingNetworkConfiguration.ToPayload() != _networkConfiguration.ToPayload();
            }

            if (domain == "mqtt" || domain == "mq" || domain == "all")
            {
                int eastLimit = _pendingMqttConfiguration.DiseqcEastLimitMicrodegrees;
                int westLimit = _pendingMqttConfiguration.DiseqcWestLimitMicrodegrees;
                int eastStep = _pendingMqttConfiguration.DiseqcEastStepMicrodegrees;
                int westStep = _pendingMqttConfiguration.DiseqcWestStepMicrodegrees;
                int offset = _pendingMqttConfiguration.DiseqcGotoOffsetMicrodegrees;
                string hostname = _pendingMqttConfiguration.Hostname;
                _pendingMqttConfiguration = MqttConfiguration.CreateDefaults();
                if (domain != "all")
                {
                    _pendingMqttConfiguration.Hostname = hostname;
                    _pendingMqttConfiguration.DiseqcEastLimitMicrodegrees = eastLimit;
                    _pendingMqttConfiguration.DiseqcWestLimitMicrodegrees = westLimit;
                    _pendingMqttConfiguration.DiseqcEastStepMicrodegrees = eastStep;
                    _pendingMqttConfiguration.DiseqcWestStepMicrodegrees = westStep;
                    _pendingMqttConfiguration.DiseqcGotoOffsetMicrodegrees = offset;
                }
                _mqttConfigurationDirty = _pendingMqttConfiguration.ToPayload() != _mqttConfiguration.ToPayload();
            }

            if (domain == "diseqc")
            {
                ClearDiseqcConfiguration(_pendingMqttConfiguration);
                _mqttConfigurationDirty = _pendingMqttConfiguration.ToPayload() != _mqttConfiguration.ToPayload();
            }

            if (domain != "network" && domain != "net" && domain != "mqtt" && domain != "mq" && domain != "diseqc" && domain != "all")
            {
                WriteCommandResult(reqId, false, "validation_error", "defaults domain invalid", "domain=" + domain);
                return;
            }

            WriteCommandResult(reqId, true, "ok", "defaults staged", "domain=" + domain + " state=" + (IsConfigurationDirty() ? "staged" : "saved"));
        }

        private static string[] PrefixConfigurationTokens(string[] tokens, string domain)
        {
            string[] result = new string[tokens.Length + 1];
            result[0] = "set";
            result[1] = domain;
            for (int index = 1; index < tokens.Length; index++)
            {
                result[index + 1] = tokens[index];
            }

            return result;
        }

        private static bool IsConfigurationDirty()
        {
            return _networkConfigurationDirty || _mqttConfigurationDirty;
        }

        private static void HandleShowConfigurationCommand(string[] tokens, bool startup, int reqId)
        {
            string domain = GetConfigurationDomain(tokens, 2, reqId);
            if (domain == null)
            {
                return;
            }

            NetworkConfiguration network = _networkConfiguration.Clone();
            MqttConfiguration mqtt = _mqttConfiguration.Clone();
            if (startup)
            {
                string error;
                uint generation;
                if ((domain == "all" || domain == "network") &&
                    !_networkConfigurationStorage.TryLoad(out network, out error))
                {
                    WriteCommandResult(reqId, false, "persist_failed", "startup network unavailable", "reason=" + error);
                    return;
                }

                if ((domain == "all" || domain == "mqtt" || domain == "diseqc") &&
                    !_applicationConfigurationStorage.TryLoad(out mqtt, out generation, out error))
                {
                    if (_mqttConfigurationSource == "defaults")
                    {
                        mqtt = MqttConfiguration.CreateDefaults();
                    }
                    else
                    {
                        WriteCommandResult(reqId, false, "persist_failed", "startup mqtt unavailable", "reason=" + error);
                        return;
                    }
                }
            }

            EmitConfigurationDocument(startup ? "startup" : "running", network, mqtt, domain);
        }

        private static string GetConfigurationDomain(string[] tokens, int index, int reqId)
        {
            if (tokens.Length == index)
            {
                return "all";
            }

            if (tokens.Length != index + 1)
            {
                WriteCommandResult(reqId, false, "validation_error", "configuration display usage", "usage=show <running-config|startup-config|candidate-config> [network|mqtt|diseqc]");
                return null;
            }

            string domain = tokens[index];
            if (domain == "net")
            {
                return "network";
            }

            if (domain == "mq")
            {
                return "mqtt";
            }

            if (domain != "network" && domain != "mqtt" && domain != "diseqc" && domain != "all")
            {
                WriteCommandResult(reqId, false, "validation_error", "configuration domain invalid", "domain=" + domain);
                return null;
            }

            return domain;
        }

        private static void EmitConfigurationDocument(
            string source,
            NetworkConfiguration network,
            MqttConfiguration mqtt,
            string domain)
        {
            if (domain == null)
            {
                return;
            }

            _activeOutputSink("! cubley-config v3 " + source + "\r\n");
            bool hideDefaults = source == "running";
            NetworkConfiguration defaultNetwork = NetworkConfiguration.CreateDefaults();
            MqttConfiguration defaultMqtt = MqttConfiguration.CreateDefaults();
            if (domain == "all" || domain == "mqtt")
            {
                EmitConfigurationLine(hideDefaults, mqtt.Hostname != defaultMqtt.Hostname,
                    "hostname " + (string.IsNullOrEmpty(mqtt.Hostname) ? "auto" : mqtt.Hostname) + "\r\n");
            }
            if (domain == "all" || domain == "network")
            {
                EmitConfigurationLine(hideDefaults, network.Mode != defaultNetwork.Mode, "network mode " + network.Mode + "\r\n");
                EmitConfigurationLine(hideDefaults, network.Address != defaultNetwork.Address, "network address " + network.Address + "\r\n");
                EmitConfigurationLine(hideDefaults, network.SubnetMask != defaultNetwork.SubnetMask, "network mask " + network.SubnetMask + "\r\n");
                EmitConfigurationLine(hideDefaults, network.Gateway != defaultNetwork.Gateway, "network gateway " + network.Gateway + "\r\n");
                EmitConfigurationLine(hideDefaults,
                    network.AutomaticDns != defaultNetwork.AutomaticDns ||
                    network.Dns1 != defaultNetwork.Dns1 || network.Dns2 != defaultNetwork.Dns2,
                    network.AutomaticDns
                        ? "network dns auto\r\n"
                        : "network dns static " + network.Dns1 +
                            (network.Dns2 == "0.0.0.0" ? string.Empty : " " + network.Dns2) + "\r\n");
            }

            if (domain == "all" || domain == "mqtt")
            {
                EmitConfigurationLine(hideDefaults, mqtt.Enabled != defaultMqtt.Enabled, "mqtt enabled " + (mqtt.Enabled ? "on" : "off") + "\r\n");
                EmitConfigurationLine(hideDefaults, mqtt.Broker != defaultMqtt.Broker, "mqtt broker " + (string.IsNullOrEmpty(mqtt.Broker) ? "clear" : mqtt.Broker) + "\r\n");
                EmitConfigurationLine(hideDefaults, mqtt.Port != defaultMqtt.Port, "mqtt port " + mqtt.Port.ToString() + "\r\n");
                EmitConfigurationLine(hideDefaults, mqtt.ClientId != defaultMqtt.ClientId, "mqtt client-id " + (string.IsNullOrEmpty(mqtt.ClientId) ? "auto" : mqtt.ClientId) + "\r\n");
                EmitConfigurationLine(hideDefaults, mqtt.Username != defaultMqtt.Username, "mqtt username " + (string.IsNullOrEmpty(mqtt.Username) ? "clear" : mqtt.Username) + "\r\n");
                EmitConfigurationLine(hideDefaults, mqtt.Password != defaultMqtt.Password, string.IsNullOrEmpty(mqtt.Password) ? "mqtt password clear\r\n" : "! mqtt password configured\r\n");
                EmitConfigurationLine(hideDefaults, mqtt.TopicPrefix != defaultMqtt.TopicPrefix, "mqtt topic-prefix " + mqtt.TopicPrefix + "\r\n");
                EmitConfigurationLine(hideDefaults, mqtt.KeepAliveSeconds != defaultMqtt.KeepAliveSeconds, "mqtt keepalive " + mqtt.KeepAliveSeconds.ToString() + "\r\n");
                EmitConfigurationLine(hideDefaults, mqtt.ReconnectSeconds != defaultMqtt.ReconnectSeconds, "mqtt reconnect " + mqtt.ReconnectSeconds.ToString() + "\r\n");
            }
            if (domain == "all" || domain == "diseqc")
            {
                EmitConfigurationLine(hideDefaults,
                    mqtt.DiseqcEastLimitMicrodegrees != 0 || mqtt.DiseqcWestLimitMicrodegrees != 0,
                    mqtt.DiseqcEastLimitMicrodegrees == 0
                        ? "diseqc angle-limits off\r\n"
                        : "diseqc angle-limits " + DiseqcGotoAngleEncoder.FormatMicrodegrees(mqtt.DiseqcEastLimitMicrodegrees) + " " + DiseqcGotoAngleEncoder.FormatMicrodegrees(mqtt.DiseqcWestLimitMicrodegrees) + "\r\n");
                EmitConfigurationLine(hideDefaults,
                    mqtt.DiseqcEastStepMicrodegrees != 0 || mqtt.DiseqcWestStepMicrodegrees != 0,
                    mqtt.DiseqcEastStepMicrodegrees == 0
                        ? "diseqc step-calibration off\r\n"
                        : "diseqc step-calibration " + DiseqcGotoAngleEncoder.FormatMicrodegrees(mqtt.DiseqcEastStepMicrodegrees) + " " + DiseqcGotoAngleEncoder.FormatMicrodegrees(mqtt.DiseqcWestStepMicrodegrees) + "\r\n");
                EmitConfigurationLine(hideDefaults,
                    mqtt.DiseqcGotoOffsetMicrodegrees != 0,
                    mqtt.DiseqcGotoOffsetMicrodegrees == 0
                        ? "diseqc fixed-offset off\r\n"
                        : "diseqc fixed-offset " + (mqtt.DiseqcGotoOffsetMicrodegrees > 0 ? "east " : "west ") + DiseqcGotoAngleEncoder.FormatMicrodegrees(mqtt.DiseqcGotoOffsetMicrodegrees > 0 ? mqtt.DiseqcGotoOffsetMicrodegrees : -mqtt.DiseqcGotoOffsetMicrodegrees) + "\r\n");
            }
        }

        private static void EmitConfigurationLine(bool hideDefaults, bool differsFromDefault, string line)
        {
            if (!hideDefaults || differsFromDefault)
            {
                _activeOutputSink(line);
            }
        }

        private static void EmitConfigurationDiff(int reqId)
        {
            bool changed = false;
            changed |= EmitConfigurationDiffLine("hostname ", string.IsNullOrEmpty(_mqttConfiguration.Hostname) ? "auto" : _mqttConfiguration.Hostname, string.IsNullOrEmpty(_pendingMqttConfiguration.Hostname) ? "auto" : _pendingMqttConfiguration.Hostname);
            changed |= EmitConfigurationDiffLine("network mode ", _networkConfiguration.Mode, _pendingNetworkConfiguration.Mode);
            changed |= EmitConfigurationDiffLine("network address ", _networkConfiguration.Address, _pendingNetworkConfiguration.Address);
            changed |= EmitConfigurationDiffLine("network mask ", _networkConfiguration.SubnetMask, _pendingNetworkConfiguration.SubnetMask);
            changed |= EmitConfigurationDiffLine("network gateway ", _networkConfiguration.Gateway, _pendingNetworkConfiguration.Gateway);
            changed |= EmitConfigurationDiffLine("network dns ", FormatConfiguredDnsCommand(_networkConfiguration), FormatConfiguredDnsCommand(_pendingNetworkConfiguration));
            changed |= EmitConfigurationDiffLine("mqtt enabled ", _mqttConfiguration.Enabled ? "on" : "off", _pendingMqttConfiguration.Enabled ? "on" : "off");
            changed |= EmitConfigurationDiffLine("mqtt broker ", ValueOrClear(_mqttConfiguration.Broker), ValueOrClear(_pendingMqttConfiguration.Broker));
            changed |= EmitConfigurationDiffLine("mqtt port ", _mqttConfiguration.Port.ToString(), _pendingMqttConfiguration.Port.ToString());
            changed |= EmitConfigurationDiffLine("mqtt client-id ", string.IsNullOrEmpty(_mqttConfiguration.ClientId) ? "auto" : _mqttConfiguration.ClientId, string.IsNullOrEmpty(_pendingMqttConfiguration.ClientId) ? "auto" : _pendingMqttConfiguration.ClientId);
            changed |= EmitConfigurationDiffLine("mqtt username ", ValueOrClear(_mqttConfiguration.Username), ValueOrClear(_pendingMqttConfiguration.Username));
            if (_mqttConfiguration.Password != _pendingMqttConfiguration.Password)
            {
                _activeOutputSink("! mqtt password changed\r\n");
                changed = true;
            }
            changed |= EmitConfigurationDiffLine("mqtt topic-prefix ", _mqttConfiguration.TopicPrefix, _pendingMqttConfiguration.TopicPrefix);
            changed |= EmitConfigurationDiffLine("mqtt keepalive ", _mqttConfiguration.KeepAliveSeconds.ToString(), _pendingMqttConfiguration.KeepAliveSeconds.ToString());
            changed |= EmitConfigurationDiffLine("mqtt reconnect ", _mqttConfiguration.ReconnectSeconds.ToString(), _pendingMqttConfiguration.ReconnectSeconds.ToString());
            changed |= EmitConfigurationDiffLine("diseqc angle-limits ", FormatDiseqcPair(_mqttConfiguration.DiseqcEastLimitMicrodegrees, _mqttConfiguration.DiseqcWestLimitMicrodegrees), FormatDiseqcPair(_pendingMqttConfiguration.DiseqcEastLimitMicrodegrees, _pendingMqttConfiguration.DiseqcWestLimitMicrodegrees));
            changed |= EmitConfigurationDiffLine("diseqc step-calibration ", FormatDiseqcPair(_mqttConfiguration.DiseqcEastStepMicrodegrees, _mqttConfiguration.DiseqcWestStepMicrodegrees), FormatDiseqcPair(_pendingMqttConfiguration.DiseqcEastStepMicrodegrees, _pendingMqttConfiguration.DiseqcWestStepMicrodegrees));
            changed |= EmitConfigurationDiffLine("diseqc fixed-offset ", FormatDiseqcOffset(_mqttConfiguration.DiseqcGotoOffsetMicrodegrees), FormatDiseqcOffset(_pendingMqttConfiguration.DiseqcGotoOffsetMicrodegrees));

            if (!changed)
            {
                _activeOutputSink("No configuration changes.\r\n");
            }
        }

        private static bool EmitConfigurationDiffLine(string prefix, string currentValue, string candidateValue)
        {
            if (currentValue == candidateValue)
            {
                return false;
            }

            _activeOutputSink("- " + prefix + currentValue + "\r\n");
            _activeOutputSink("+ " + prefix + candidateValue + "\r\n");
            return true;
        }

        private static string FormatConfiguredDnsCommand(NetworkConfiguration configuration)
        {
            if (configuration.AutomaticDns)
            {
                return "auto";
            }

            return "static " + configuration.Dns1 +
                (configuration.Dns2 == "0.0.0.0" ? string.Empty : " " + configuration.Dns2);
        }

        private static string ValueOrClear(string value)
        {
            return string.IsNullOrEmpty(value) ? "clear" : value;
        }

        private static string FormatDiseqcPair(int eastMicrodegrees, int westMicrodegrees)
        {
            return eastMicrodegrees == 0
                ? "off"
                : DiseqcGotoAngleEncoder.FormatMicrodegrees(eastMicrodegrees) + " " + DiseqcGotoAngleEncoder.FormatMicrodegrees(westMicrodegrees);
        }

        private static string FormatDiseqcOffset(int signedMicrodegrees)
        {
            if (signedMicrodegrees == 0)
            {
                return "off";
            }

            return (signedMicrodegrees > 0 ? "east " : "west ") +
                DiseqcGotoAngleEncoder.FormatMicrodegrees(signedMicrodegrees > 0 ? signedMicrodegrees : -signedMicrodegrees);
        }

        private static void CommitCandidateConfiguration(int reqId)
        {
            if (!IsConfigurationDirty())
            {
                WriteCommandResult(reqId, true, "ok", "configuration unchanged", "state=clean");
                return;
            }

            NetworkConfiguration candidateNetwork = _pendingNetworkConfiguration.Clone();
            MqttConfiguration candidateMqtt = _pendingMqttConfiguration.Clone();
            string error;
            if (!candidateNetwork.TryValidate(out error))
            {
                WriteCommandResult(reqId, false, "validation_error", "network candidate invalid", "reason=" + error);
                return;
            }

            if (!candidateMqtt.TryValidate(out error))
            {
                WriteCommandResult(reqId, false, "validation_error", "mqtt candidate invalid", "reason=" + error);
                return;
            }

            NetworkConfiguration previousNetwork = _networkConfiguration.Clone();
            MqttConfiguration previousMqtt = _mqttConfiguration.Clone();
            uint previousMqttGeneration = _mqttConfigurationGeneration;
            uint savedMqttGeneration = previousMqttGeneration;
            bool mqttChanged = _mqttConfigurationDirty;
            bool networkChanged = _networkConfigurationDirty;

            if (HasDiseqcConfigurationChanged(candidateMqtt, previousMqtt) && GetActiveDiseqcJobId() != 0)
            {
                WriteCommandResult(reqId, false, "busy", "diseqc motion active", "motion_id=" + GetActiveDiseqcJobId().ToString());
                return;
            }

            if (mqttChanged && !TryPersistMqttConfiguration(candidateMqtt, previousMqttGeneration, out savedMqttGeneration, out error))
            {
                uint restoredGeneration;
                bool restored = TryPersistMqttConfiguration(previousMqtt, previousMqttGeneration, out restoredGeneration, out error);
                if (restored)
                {
                    _mqttConfigurationGeneration = restoredGeneration;
                }
                else
                {
                    ReconcilePartialConfiguration(candidateNetwork, candidateMqtt);
                }
                WriteCommandResult(reqId, false, restored ? "persist_failed" : "persist_partial", "configuration commit failed", "domain=mqtt rollback=" + (restored ? "ok" : "failed"));
                return;
            }

            if (networkChanged && !TryPersistNetworkConfiguration(candidateNetwork, out error))
            {
                string rollbackError;
                bool networkRestored = TryPersistNetworkConfiguration(previousNetwork, out rollbackError);
                bool mqttRestored = true;
                uint restoredGeneration = savedMqttGeneration;
                if (mqttChanged)
                {
                    mqttRestored = TryPersistMqttConfiguration(previousMqtt, savedMqttGeneration, out restoredGeneration, out rollbackError);
                    if (mqttRestored)
                    {
                        _mqttConfigurationGeneration = restoredGeneration;
                    }
                }

                bool restored = networkRestored && mqttRestored;
                if (!restored)
                {
                    ReconcilePartialConfiguration(candidateNetwork, candidateMqtt);
                }
                WriteCommandResult(reqId, false, restored ? "persist_failed" : "persist_partial", "configuration commit failed", "domain=network rollback=" + (restored ? "ok" : "failed"));
                return;
            }

            if (networkChanged)
            {
                _networkConfiguration = candidateNetwork;
                _networkConfigurationSource = _networkConfigurationStorage.Source;
                _networkConfigurationError = string.Empty;
            }

            if (mqttChanged)
            {
                lock (_mqttConfigurationLock)
                {
                    _mqttConfiguration = candidateMqtt;
                    _mqttConfigurationGeneration = savedMqttGeneration;
                    _mqttConfigurationRevision++;
                }
                _mqttConfigurationSource = _applicationConfigurationStorage.Source;
                _mqttConfigurationError = string.Empty;
                ApplyDiseqcConfiguration(_mqttConfiguration);
            }

            _pendingNetworkConfiguration = _networkConfiguration.Clone();
            _pendingMqttConfiguration = _mqttConfiguration.Clone();
            _networkConfigurationDirty = false;
            _mqttConfigurationDirty = false;
            WriteCommandResult(reqId, true, "ok", "configuration committed", "network=" + (networkChanged ? "changed" : "unchanged") + " mqtt=" + (mqttChanged ? "changed" : "unchanged"));
        }

        private static void ReconcilePartialConfiguration(
            NetworkConfiguration candidateNetwork,
            MqttConfiguration candidateMqtt)
        {
            string error;
            NetworkConfiguration actualNetwork;
            if (_networkConfigurationStorage.TryLoad(out actualNetwork, out error))
            {
                _networkConfiguration = actualNetwork;
                _networkConfigurationSource = _networkConfigurationStorage.Source;
                _networkConfigurationError = "commit_partial";
            }
            else
            {
                _networkConfigurationError = "commit_recovery_failed";
            }

            MqttConfiguration startupMqtt;
            uint startupGeneration;
            if (_applicationConfigurationStorage.TryLoad(out startupMqtt, out startupGeneration, out error))
            {
                _mqttConfigurationGeneration = startupGeneration;
                _mqttConfigurationSource = _applicationConfigurationStorage.Source;
                _mqttConfigurationError = startupMqtt.ToPayload() == _mqttConfiguration.ToPayload()
                    ? string.Empty
                    : "startup_differs_from_running";
            }
            else
            {
                _mqttConfigurationError = "commit_recovery_failed";
            }

            _pendingNetworkConfiguration = candidateNetwork;
            _pendingMqttConfiguration = candidateMqtt;
            _networkConfigurationDirty = _pendingNetworkConfiguration.ToPayload() != _networkConfiguration.ToPayload();
            _mqttConfigurationDirty = _pendingMqttConfiguration.ToPayload() != _mqttConfiguration.ToPayload();
        }

        private static bool TryPersistNetworkConfiguration(NetworkConfiguration configuration, out string error)
        {
            if (!_networkConfigurationStorage.TrySave(configuration, out error))
            {
                return false;
            }

            NetworkConfiguration verified;
            return _networkConfigurationStorage.TryLoad(out verified, out error) &&
                verified.ToPayload() == configuration.ToPayload();
        }

        private static bool TryPersistMqttConfiguration(
            MqttConfiguration configuration,
            uint currentGeneration,
            out uint savedGeneration,
            out string error)
        {
            try
            {
                return _applicationConfigurationStorage.TrySave(
                    configuration,
                    currentGeneration,
                    out savedGeneration,
                    out error);
            }
            catch
            {
                savedGeneration = currentGeneration;
                error = "storage_exception";
                return false;
            }
        }

    }
}