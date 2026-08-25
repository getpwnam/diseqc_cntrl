using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading;
using nanoFramework.Runtime.Native;

namespace CubleyControl
{
    public static partial class Program
    {
        private static void EmitNetworkStatus(int reqId)
        {
            try
            {
                NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
                if (interfaces == null || interfaces.Length == 0)
                {
                    WriteCommandResult(reqId, false, "unavailable", "network unavailable", "interfaces=0");
                    return;
                }

                NetworkInterface networkInterface = interfaces[0];
                string[] dnsAddresses = networkInterface.IPv4DnsAddresses;
                string configurationSource = _networkConfigurationSource;
                string configurationError = _networkConfigurationError;

                _activeOutputSink(
                    "network link=" + (NetworkInterface.GetIsNetworkAvailable() ? "up" : "down") +
                    " mode=" + (networkInterface.IsDhcpEnabled ? "dhcp" : "static") + "\r\n");
                _activeOutputSink("mac " + FormatMacAddress(networkInterface.PhysicalAddress) + "\r\n");
                _activeOutputSink(
                    "ipv4 address=" + ValueOrUnset(networkInterface.IPv4Address) +
                    " mask=" + ValueOrUnset(networkInterface.IPv4SubnetMask) +
                    " gateway=" + ValueOrUnset(networkInterface.IPv4GatewayAddress) + "\r\n");
                _activeOutputSink(
                    "dns mode=" + (networkInterface.IsAutomaticDnsEnabled ? "auto" : "static") +
                    " servers=" + FormatDnsAddresses(dnsAddresses) + "\r\n");
                _activeOutputSink(
                    "config source=" + (string.IsNullOrEmpty(configurationSource) ? "unknown" : configurationSource) +
                    " status=" + (string.IsNullOrEmpty(configurationError) ? "ok" : configurationError) + "\r\n");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[NETWORK] show failed: " + ex.Message);
                WriteCommandResult(reqId, false, "unavailable", "network unavailable", "reason=read_failed");
            }
        }

        private static void HandleGetNetworkCommand(string[] tokens, int reqId)
        {
            if (tokens.Length != 2)
            {
                WriteCommandResult(reqId, false, "validation_error", "get network usage", "usage=get network");
                return;
            }

            NetworkConfiguration configuration = _pendingNetworkConfiguration.Clone();
            _activeOutputSink("network mode=" + configuration.Mode + "\r\n");
            _activeOutputSink(
                "ipv4 address=" + ValueOrUnset(configuration.Address) +
                " mask=" + ValueOrUnset(configuration.SubnetMask) +
                " gateway=" + ValueOrUnset(configuration.Gateway) + "\r\n");
            _activeOutputSink(
                "dns mode=" + (configuration.AutomaticDns ? "auto" : "static") +
                " servers=" + FormatConfiguredDns(configuration) + "\r\n");
            _activeOutputSink(
                "config source=" + _networkConfigurationSource +
                " state=" + (_networkConfigurationDirty ? "staged" : "saved") + "\r\n");
        }

        private static void HandleSetNetworkCommand(string[] tokens, int reqId)
        {
            if (tokens.Length < 3)
            {
                WriteNetworkSetUsage(reqId);
                return;
            }

            string field = tokens[2];
            if (field == "save" || field == "apply")
            {
                if (tokens.Length != 3)
                {
                    WriteNetworkSetUsage(reqId);
                    return;
                }

                SavePendingNetworkConfiguration(reqId, field);
                return;
            }

            if (field == "discard")
            {
                if (tokens.Length != 3)
                {
                    WriteNetworkSetUsage(reqId);
                    return;
                }

                _pendingNetworkConfiguration = _networkConfiguration.Clone();
                _networkConfigurationDirty = false;
                WriteCommandResult(reqId, true, "ok", "network discarded", "state=saved");
                return;
            }

            if (field == "defaults")
            {
                if (tokens.Length != 3)
                {
                    WriteNetworkSetUsage(reqId);
                    return;
                }

                _pendingNetworkConfiguration = NetworkConfiguration.CreateDefaults();
                _networkConfigurationDirty = true;
                WriteCommandResult(reqId, true, "ok", "network staged", "field=defaults mode=dhcp");
                return;
            }

            if (field == "reboot")
            {
                if (tokens.Length != 3)
                {
                    WriteNetworkSetUsage(reqId);
                    return;
                }

                if (_networkConfigurationDirty)
                {
                    WriteCommandResult(reqId, false, "validation_error", "network reboot blocked", "reason=unsaved_changes");
                    return;
                }

                WriteCommandResult(reqId, true, "ok", "network reboot", "state=saved");
                Thread.Sleep(100);
                Power.RebootDevice(RebootOption.NormalReboot);
                return;
            }

            if (field == "mode" && tokens.Length == 4)
            {
                string mode = tokens[3];
                if (mode != NetworkConfiguration.ModeDhcp && mode != NetworkConfiguration.ModeStatic)
                {
                    WriteCommandResult(reqId, false, "validation_error", "network mode invalid", "value=" + mode);
                    return;
                }

                _pendingNetworkConfiguration.Mode = mode;
                StageNetworkChange(reqId, "mode", mode);
                return;
            }

            if (field == "address" && tokens.Length == 4)
            {
                string address = tokens[3];
                if (!NetworkConfiguration.IsValidIpv4Address(address))
                {
                    WriteCommandResult(reqId, false, "validation_error", "network address invalid", "value=" + address);
                    return;
                }

                _pendingNetworkConfiguration.Address = address;
                StageNetworkChange(reqId, "address", address);
                return;
            }

            if (field == "mask" && tokens.Length == 4)
            {
                string mask = tokens[3];
                if (!NetworkConfiguration.IsValidSubnetMask(mask))
                {
                    WriteCommandResult(reqId, false, "validation_error", "network mask invalid", "value=" + mask);
                    return;
                }

                _pendingNetworkConfiguration.SubnetMask = mask;
                StageNetworkChange(reqId, "mask", mask);
                return;
            }

            if (field == "gateway" && tokens.Length == 4)
            {
                string gateway = tokens[3];
                if (!NetworkConfiguration.IsValidIpv4Address(gateway))
                {
                    WriteCommandResult(reqId, false, "validation_error", "network gateway invalid", "value=" + gateway);
                    return;
                }

                _pendingNetworkConfiguration.Gateway = gateway;
                StageNetworkChange(reqId, "gateway", gateway);
                return;
            }

            if (field == "dns" && tokens.Length >= 4)
            {
                string mode = tokens[3];
                if (mode == "auto" && tokens.Length == 4)
                {
                    _pendingNetworkConfiguration.AutomaticDns = true;
                    _pendingNetworkConfiguration.Dns1 = "0.0.0.0";
                    _pendingNetworkConfiguration.Dns2 = "0.0.0.0";
                    StageNetworkChange(reqId, "dns", "auto");
                    return;
                }

                if (mode == "static" && (tokens.Length == 5 || tokens.Length == 6))
                {
                    string dns1 = tokens[4];
                    string dns2 = tokens.Length == 6 ? tokens[5] : "0.0.0.0";
                    if (!NetworkConfiguration.IsValidDnsAddress(dns1) ||
                        (dns2 != "0.0.0.0" && !NetworkConfiguration.IsValidDnsAddress(dns2)))
                    {
                        WriteCommandResult(reqId, false, "validation_error", "network dns invalid", "servers=" + dns1 + "," + dns2);
                        return;
                    }

                    _pendingNetworkConfiguration.AutomaticDns = false;
                    _pendingNetworkConfiguration.Dns1 = dns1;
                    _pendingNetworkConfiguration.Dns2 = dns2;
                    StageNetworkChange(reqId, "dns", dns2 == "0.0.0.0" ? dns1 : dns1 + "," + dns2);
                    return;
                }
            }

            WriteNetworkSetUsage(reqId);
        }

        private static void StageNetworkChange(int reqId, string field, string value)
        {
            _networkConfigurationDirty = _pendingNetworkConfiguration.ToPayload() != _networkConfiguration.ToPayload();
            WriteCommandResult(
                reqId,
                true,
                "ok",
                "network staged",
                "field=" + field + " value=" + value + " state=" + (_networkConfigurationDirty ? "staged" : "saved"));
        }

        private static void SavePendingNetworkConfiguration(int reqId, string action)
        {
            NetworkConfiguration candidate = _pendingNetworkConfiguration.Clone();
            string error;
            if (!candidate.TryValidate(out error))
            {
                WriteCommandResult(reqId, false, "validation_error", "network not saved", "reason=" + error);
                return;
            }

            if (!_networkConfigurationStorage.TrySave(candidate, out error))
            {
                WriteCommandResult(reqId, false, "persist_failed", "network not saved", "reason=" + error);
                return;
            }

            NetworkConfiguration verified;
            if (!_networkConfigurationStorage.TryLoad(out verified, out error) ||
                verified.ToPayload() != candidate.ToPayload())
            {
                WriteCommandResult(
                    reqId,
                    false,
                    "persist_failed",
                    "network verify failed",
                    "reason=" + (string.IsNullOrEmpty(error) ? "readback_mismatch" : error));
                return;
            }

            _networkConfiguration = verified;
            _pendingNetworkConfiguration = verified.Clone();
            _networkConfigurationSource = _networkConfigurationStorage.Source;
            _networkConfigurationError = string.Empty;
            _networkConfigurationDirty = false;
            WriteCommandResult(reqId, true, "ok", "network " + action, "persisted=1 applied=1 reboot_required=0");
        }

        private static void WriteNetworkSetUsage(int reqId)
        {
            WriteCommandResult(
                reqId,
                false,
                "validation_error",
                "set network usage",
                "usage=set network <mode dhcp|static|address IP|mask MASK|gateway IP|dns auto|dns static DNS1 [DNS2]|save|apply|discard|defaults|reboot>");
        }

        private static string FormatConfiguredDns(NetworkConfiguration configuration)
        {
            if (configuration.AutomaticDns)
            {
                return "auto";
            }

            return configuration.Dns2 == "0.0.0.0"
                ? configuration.Dns1
                : configuration.Dns1 + "," + configuration.Dns2;
        }

        private static string FormatMacAddress(byte[] address)
        {
            if (address == null || address.Length == 0)
            {
                return "unset";
            }

            string value = string.Empty;
            for (int index = 0; index < address.Length; index++)
            {
                if (index > 0)
                {
                    value += ":";
                }

                value += address[index].ToString("X2");
            }

            return value;
        }

        private static string FormatDnsAddresses(string[] addresses)
        {
            if (addresses == null || addresses.Length == 0)
            {
                return "unset";
            }

            string value = string.Empty;
            for (int index = 0; index < addresses.Length; index++)
            {
                string address = addresses[index];
                if (string.IsNullOrEmpty(address) || address == "0.0.0.0")
                {
                    continue;
                }

                if (value.Length > 0)
                {
                    value += ",";
                }

                value += address;
            }

            return value.Length == 0 ? "unset" : value;
        }

        private static string ValueOrUnset(string value)
        {
            return string.IsNullOrEmpty(value) || value == "0.0.0.0" ? "unset" : value;
        }
    }
}