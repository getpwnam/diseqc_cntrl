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
                WriteHumanHeading("Configuration commands");
                WriteHelpCommand("hostname <name|auto>", "Set the device hostname");
                WriteHelpCommand("network <setting> <value>", "Stage network configuration");
                WriteHelpCommand("mqtt <setting> <value>", "Stage MQTT configuration");
                WriteHelpCommand("show <topic>", "Display configuration and storage state");
                WriteHelpCommand("debug <on|off>", "Control successful setter output");
                WriteHelpCommand("commit", "Persist and activate the candidate");
                WriteHelpCommand("discard", "Abandon candidate changes");
                WriteHelpCommand("load defaults [domain]", "Stage default values");
                WriteHelpCommand("exit", "Leave configuration mode when clean");
                WriteHelpCommand("quit", "Release the console when clean (alias: logout)");
                WriteHelpCommand("help [command]", "Show command help (alias: ?)");
                _activeOutputSink("\r\nA '*' in the prompt marks uncommitted changes.\r\n");
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
                WriteHumanHeading("Network configuration");
                WriteHelpCommand("network mode <dhcp|static>", "Set address assignment mode");
                WriteHelpCommand("network address <ipv4>", "Set static IPv4 address");
                WriteHelpCommand("network mask <mask>", "Set static subnet mask");
                WriteHelpCommand("network gateway <ipv4>", "Set static gateway");
                WriteHelpCommand("network dns auto", "Use automatic DNS");
                WriteHelpCommand("network dns static <dns1> [dns2]", "Set static DNS servers");
                WriteHelpCommand("network defaults", "Stage network defaults");
                return;
            }

            if (topic == "hostname")
            {
                WriteHumanHeading("Device hostname");
                WriteHelpCommand("hostname <name|auto>", "Set a DNS-label hostname or derive one from the STM32 unique ID");
                return;
            }

            if (topic == "mqtt" || topic == "mq")
            {
                WriteHumanHeading("MQTT configuration");
                WriteHelpCommand("mqtt enabled <on|off>", "Enable or disable the service");
                WriteHelpCommand("mqtt broker <host|clear>", "Set or clear broker address");
                WriteHelpCommand("mqtt port <1..65535>", "Set broker port");
                WriteHelpCommand("mqtt client-id <id|auto>", "Set client identifier");
                WriteHelpCommand("mqtt username <value|clear>", "Set or clear username");
                WriteHelpCommand("mqtt password <value|clear>", "Set or clear password");
                WriteHelpCommand("mqtt topic-prefix <prefix>", "Set MQTT base topic prefix");
                WriteHelpCommand("mqtt keepalive <15..3600>", "Set keepalive seconds");
                WriteHelpCommand("mqtt reconnect <1..60>", "Set reconnect delay seconds");
                WriteHelpCommand("mqtt defaults", "Stage MQTT defaults");
                return;
            }

            if (topic == "show")
            {
                WriteHumanHeading("Configuration show commands");
                WriteHelpCommand("show candidate-config [domain]", "Display the staged candidate");
                WriteHelpCommand("show config diff", "Compare candidate and running state");
                WriteHelpCommand("show running-config [domain]", "Display active configuration");
                WriteHelpCommand("show startup-config [domain]", "Display persisted configuration");
                WriteHelpCommand("show storage", "Display backend and load status");
                return;
            }

            if (topic == "debug")
            {
                WriteHumanHeading("Debug output");
                WriteHelpCommand("debug on", "Show successful setter results");
                WriteHelpCommand("debug off", "Keep successful setters silent");
                return;
            }

            if (topic == "commit" || topic == "apply")
            {
                WriteHumanHeading("Commit candidate");
                WriteHelpCommand("commit", "Validate, persist, and activate changes");
                WriteHelpCommand("apply", "Alias for commit");
                return;
            }

            if (topic == "discard" || topic == "abort")
            {
                WriteHumanHeading("Discard candidate");
                WriteHelpCommand("discard", "Restore the committed candidate");
                WriteHelpCommand("abort", "Alias for discard");
                return;
            }

            if (topic == "load" || topic == "defaults")
            {
                WriteHumanHeading("Load defaults");
                WriteHelpCommand("load defaults [network|mqtt|all]", "Stage defaults without committing");
                WriteHelpCommand("defaults [network|mqtt|all]", "Short form");
                return;
            }

            if (topic == "exit" || topic == "end")
            {
                WriteHumanHeading("Exit configuration mode");
                WriteHelpCommand("exit | end | Ctrl+D", "Leave only when the candidate is clean");
                WriteHelpCommand("commit", "Apply uncommitted changes first");
                WriteHelpCommand("discard", "Abandon uncommitted changes first");
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
                string hostname = _pendingMqttConfiguration.Hostname;
                _pendingMqttConfiguration = MqttConfiguration.CreateDefaults();
                if (domain != "all")
                {
                    _pendingMqttConfiguration.Hostname = hostname;
                }
                _mqttConfigurationDirty = _pendingMqttConfiguration.ToPayload() != _mqttConfiguration.ToPayload();
            }

            if (domain != "network" && domain != "net" && domain != "mqtt" && domain != "mq" && domain != "all")
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

                if ((domain == "all" || domain == "mqtt") &&
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
                WriteCommandResult(reqId, false, "validation_error", "configuration display usage", "usage=show <running-config|startup-config|candidate-config> [network|mqtt]");
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

            if (domain != "network" && domain != "mqtt" && domain != "all")
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

            _activeOutputSink("! cubley-config v2 " + source + "\r\n");
            if (domain == "all" || domain == "mqtt")
            {
                _activeOutputSink("hostname " + (string.IsNullOrEmpty(mqtt.Hostname) ? "auto" : mqtt.Hostname) + "\r\n");
            }
            if (domain == "all" || domain == "network")
            {
                _activeOutputSink("network mode " + network.Mode + "\r\n");
                _activeOutputSink("network address " + network.Address + "\r\n");
                _activeOutputSink("network mask " + network.SubnetMask + "\r\n");
                _activeOutputSink("network gateway " + network.Gateway + "\r\n");
                _activeOutputSink(
                    network.AutomaticDns
                        ? "network dns auto\r\n"
                        : "network dns static " + network.Dns1 +
                            (network.Dns2 == "0.0.0.0" ? string.Empty : " " + network.Dns2) + "\r\n");
            }

            if (domain == "all" || domain == "mqtt")
            {
                _activeOutputSink("mqtt enabled " + (mqtt.Enabled ? "on" : "off") + "\r\n");
                _activeOutputSink("mqtt broker " + (string.IsNullOrEmpty(mqtt.Broker) ? "clear" : mqtt.Broker) + "\r\n");
                _activeOutputSink("mqtt port " + mqtt.Port.ToString() + "\r\n");
                _activeOutputSink("mqtt client-id " + (string.IsNullOrEmpty(mqtt.ClientId) ? "auto" : mqtt.ClientId) + "\r\n");
                _activeOutputSink("mqtt username " + (string.IsNullOrEmpty(mqtt.Username) ? "clear" : mqtt.Username) + "\r\n");
                _activeOutputSink(string.IsNullOrEmpty(mqtt.Password) ? "mqtt password clear\r\n" : "! mqtt password configured\r\n");
                _activeOutputSink("mqtt topic-prefix " + mqtt.TopicPrefix + "\r\n");
                _activeOutputSink("mqtt keepalive " + mqtt.KeepAliveSeconds.ToString() + "\r\n");
                _activeOutputSink("mqtt reconnect " + mqtt.ReconnectSeconds.ToString() + "\r\n");
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

        private static bool IsMqttOperationalCommand(string[] tokens)
        {
            string head = tokens[0];
            if (head == "status" || head == "st" || head == "capabilities" || head == "caps" ||
                head == "version" || head == "ver" || head == "lnb" || head == "l" || head == "diseqc")
            {
                return true;
            }

            if (head == "show")
            {
                return tokens.Length == 1 ||
                    (tokens.Length >= 2 &&
                        (tokens[1] == "lnb" || tokens[1] == "diseqc" || tokens[1] == "status" ||
                            tokens[1] == "version" || tokens[1] == "ver" ||
                            tokens[1] == "capabilities" || tokens[1] == "caps"));
            }

            return false;
        }
    }
}