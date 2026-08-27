using System;
using System.Net.NetworkInformation;

namespace CubleyControl
{
    internal interface INetworkConfigurationStorage
    {
        string Source { get; }
        bool RequiresApplyAfterLoad { get; }
        bool TryLoad(out NetworkConfiguration configuration, out string error);
        bool TrySave(NetworkConfiguration configuration, out string error);
    }

    internal sealed class InternalNetworkConfigurationStorage : INetworkConfigurationStorage
    {
        public string Source
        {
            get { return "internal"; }
        }

        public bool RequiresApplyAfterLoad
        {
            get { return false; }
        }

        public bool TryLoad(out NetworkConfiguration configuration, out string error)
        {
            configuration = NetworkConfiguration.CreateDefaults();
            error = null;

            try
            {
                NetworkInterface networkInterface = GetNetworkInterface();
                if (networkInterface == null)
                {
                    error = "interface_unavailable";
                    return false;
                }

                configuration.Mode = networkInterface.IsDhcpEnabled
                    ? NetworkConfiguration.ModeDhcp
                    : NetworkConfiguration.ModeStatic;
                configuration.AutomaticDns = networkInterface.IsAutomaticDnsEnabled;

                if (configuration.Mode == NetworkConfiguration.ModeStatic)
                {
                    configuration.Address = networkInterface.IPv4Address;
                    configuration.SubnetMask = networkInterface.IPv4SubnetMask;
                    configuration.Gateway = networkInterface.IPv4GatewayAddress;
                }

                if (!configuration.AutomaticDns)
                {
                    string[] dnsAddresses = networkInterface.IPv4DnsAddresses;
                    if (dnsAddresses != null && dnsAddresses.Length > 0)
                    {
                        configuration.Dns1 = dnsAddresses[0];
                    }
                    if (dnsAddresses != null && dnsAddresses.Length > 1)
                    {
                        configuration.Dns2 = dnsAddresses[1];
                    }
                }

                return configuration.TryValidate(out error);
            }
            catch (Exception)
            {
                error = "internal_load_failed";
                return false;
            }
        }

        public bool TrySave(NetworkConfiguration configuration, out string error)
        {
            error = null;
            if (configuration == null || !configuration.TryValidate(out error))
            {
                return false;
            }

            try
            {
                NetworkInterface networkInterface = GetNetworkInterface();
                if (networkInterface == null)
                {
                    error = "interface_unavailable";
                    return false;
                }

                if (configuration.Mode == NetworkConfiguration.ModeDhcp)
                {
                    networkInterface.EnableDhcp();
                }
                else
                {
                    networkInterface.EnableStaticIPv4(
                        configuration.Address,
                        configuration.SubnetMask,
                        configuration.Gateway);
                }

                if (configuration.AutomaticDns)
                {
                    networkInterface.EnableAutomaticDns();
                }
                else if (configuration.Dns2 == "0.0.0.0")
                {
                    networkInterface.EnableStaticIPv4Dns(new string[] { configuration.Dns1 });
                }
                else
                {
                    networkInterface.EnableStaticIPv4Dns(new string[] { configuration.Dns1, configuration.Dns2 });
                }

                return true;
            }
            catch (Exception)
            {
                error = "internal_save_failed";
                return false;
            }
        }

        private static NetworkInterface GetNetworkInterface()
        {
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
            return interfaces == null || interfaces.Length == 0 ? null : interfaces[0];
        }
    }
}