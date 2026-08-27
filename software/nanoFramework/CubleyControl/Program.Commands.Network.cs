using System;
using System.Net;
using System.Net.NetworkInformation;

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

                if (_activeCommandTransport == CommandTransport.Usb)
                {
                    WriteHumanHeading("Network");
                    WriteHumanField("Link", NetworkInterface.GetIsNetworkAvailable() ? "Up" : "Down");
                    WriteHumanField("Mode", networkInterface.IsDhcpEnabled ? "DHCP" : "Static");
                    WriteHumanField("MAC address", FormatMacAddress(networkInterface.PhysicalAddress));
                    WriteHumanField("IPv4 address", ValueOrUnset(networkInterface.IPv4Address));
                    WriteHumanField("Subnet mask", ValueOrUnset(networkInterface.IPv4SubnetMask));
                    WriteHumanField("Gateway", ValueOrUnset(networkInterface.IPv4GatewayAddress));
                    WriteHumanField("DNS mode", networkInterface.IsAutomaticDnsEnabled ? "Automatic" : "Static");
                    WriteHumanField("DNS servers", FormatDnsAddresses(dnsAddresses));
                    return;
                }

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
            }
            catch (Exception ex)
            {
                WriteStructuredDebug(
                    "NETWORK",
                    "schema=1 subsystem=network component=interface operation=read status=error" +
                    " code=read_failed detail=" + SanitizeToken(ex.Message));
                WriteCommandResult(reqId, false, "unavailable", "network unavailable", "reason=read_failed");
            }
        }

        private static void HandleDnsCommand(string[] tokens, int reqId)
        {
            if (tokens.Length != 3 || tokens[1] != "lookup" || string.IsNullOrEmpty(tokens[2]))
            {
                WriteCommandResult(reqId, false, "validation_error", "dns lookup usage", "usage=dns lookup <hostname>");
                return;
            }

            string host = tokens[2];
            try
            {
                IPHostEntry entry = Dns.GetHostEntry(host);
                IPAddress[] addresses = entry == null ? null : entry.AddressList;
                if (addresses == null || addresses.Length == 0)
                {
                    WriteCommandResult(reqId, false, "unavailable", "dns lookup failed", "host=" + host + " reason=no_addresses");
                    return;
                }

                string addressList = string.Empty;
                for (int index = 0; index < addresses.Length; index++)
                {
                    if (index > 0)
                    {
                        addressList += ",";
                    }

                    addressList += addresses[index].ToString();
                }

                if (_activeCommandTransport == CommandTransport.Usb)
                {
                    WriteHumanHeading("DNS lookup");
                    WriteHumanField("Host", host);
                    WriteHumanField("Addresses", addressList);
                }
                else
                {
                    _activeOutputSink("dns host=" + host + " addresses=" + addressList + "\r\n");
                }
            }
            catch (Exception ex)
            {
                WriteStructuredDebug(
                    "NETWORK",
                    "schema=1 subsystem=network component=dns operation=lookup status=error" +
                    " code=lookup_failed host=" + SanitizeToken(host) +
                    " detail=" + SanitizeToken(ex.Message));
                WriteCommandResult(reqId, false, "unavailable", "dns lookup failed", "host=" + host);
            }
        }

        private static void HandleSetNetworkCommand(string[] tokens, int reqId)
        {
            if (tokens.Length < 3)
            {
                WriteNetworkSetUsage(reqId);
                return;
            }

            string field = tokens[2];
            if (field == "default" || field == "defaults")
            {
                if (tokens.Length != 3)
                {
                    WriteNetworkSetUsage(reqId);
                    return;
                }

                _pendingNetworkConfiguration = NetworkConfiguration.CreateDefaults();
                _networkConfigurationDirty = _pendingNetworkConfiguration.ToPayload() != _networkConfiguration.ToPayload();
                WriteCommandResult(reqId, true, "ok", "network staged", "field=defaults mode=dhcp state=" + (_networkConfigurationDirty ? "staged" : "saved"));
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
                if (mode == NetworkConfiguration.ModeDhcp)
                {
                    _pendingNetworkConfiguration.Address = "0.0.0.0";
                    _pendingNetworkConfiguration.SubnetMask = "0.0.0.0";
                    _pendingNetworkConfiguration.Gateway = "0.0.0.0";
                }
                StageNetworkChange(reqId, "mode", mode);
                return;
            }

            if ((field == "address" || field == "addr" || field == "ip") && tokens.Length == 4)
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

            if ((field == "gateway" || field == "gw") && tokens.Length == 4)
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

        private static void WriteNetworkSetUsage(int reqId)
        {
            WriteCommandResult(
                reqId,
                false,
                "validation_error",
                "network usage",
                "usage=network <mode dhcp|static|address IP|mask MASK|gateway IP|dns auto|dns static DNS1 [DNS2]|defaults>");
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