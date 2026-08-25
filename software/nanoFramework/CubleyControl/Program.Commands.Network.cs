using System;
using System.Diagnostics;
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
                Debug.WriteLine("[NETWORK] show failed: " + ex.Message);
                WriteCommandResult(reqId, false, "unavailable", "network unavailable", "reason=read_failed");
            }
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