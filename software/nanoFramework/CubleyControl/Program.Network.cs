using System;
using System.Net.NetworkInformation;

namespace CubleyControl
{
    public static partial class Program
    {
        private static readonly INetworkConfigurationStorage _networkConfigurationStorage =
            new InternalNetworkConfigurationStorage();
        private static NetworkConfiguration _networkConfiguration = NetworkConfiguration.CreateDefaults();
        private static NetworkConfiguration _pendingNetworkConfiguration = NetworkConfiguration.CreateDefaults();
        private static bool _networkConfigurationDirty;
        private static string _networkConfigurationSource = "defaults";
        private static string _networkConfigurationError = string.Empty;

        private static void InitializeNetworkConfiguration()
        {
            NetworkConfiguration loaded;
            string error;
            if (_networkConfigurationStorage.TryLoad(out loaded, out error))
            {
                _networkConfiguration = loaded;
                _networkConfigurationSource = _networkConfigurationStorage.Source;
                _networkConfigurationError = string.Empty;
            }
            else
            {
                _networkConfiguration = NetworkConfiguration.CreateDefaults();
                _networkConfigurationSource = "defaults";
                _networkConfigurationError = string.IsNullOrEmpty(error) ? "load_failed" : error;

                string saveError;
                NetworkConfiguration verified;
                if (_networkConfigurationStorage.TrySave(_networkConfiguration, out saveError) &&
                    _networkConfigurationStorage.TryLoad(out verified, out saveError))
                {
                    _networkConfiguration = verified;
                    _networkConfigurationSource = _networkConfigurationStorage.Source;
                    _networkConfigurationError = string.Empty;
                }
                else
                {
                    _networkConfigurationError = string.IsNullOrEmpty(saveError) ? "save_failed" : saveError;
                }
            }

            if (_networkConfigurationStorage.RequiresApplyAfterLoad &&
                !TryApplyNetworkConfiguration(_networkConfiguration, out error))
            {
                _networkConfigurationError = string.IsNullOrEmpty(error) ? "apply_failed" : error;
                _networkConfiguration = NetworkConfiguration.CreateDefaults();
                _networkConfigurationSource = "defaults";
                TryApplyNetworkConfiguration(_networkConfiguration, out error);
            }

            WriteStructuredDebug(
                "CONFIG",
                "schema=1 sub=config comp=storage domain=network operation=load" +
                " stat=" + (string.IsNullOrEmpty(_networkConfigurationError) ? "ok" : "error") +
                " source=" + SanitizeToken(_networkConfigurationSource) +
                " mode=" + _networkConfiguration.Mode +
                (string.IsNullOrEmpty(_networkConfigurationError)
                    ? string.Empty
                    : " code=" + SanitizeToken(_networkConfigurationError)));

            _pendingNetworkConfiguration = _networkConfiguration.Clone();
            _networkConfigurationDirty = false;
        }

        private static bool TryApplyNetworkConfiguration(NetworkConfiguration configuration, out string error)
        {
            error = null;
            if (configuration == null || !configuration.TryValidate(out error))
            {
                return false;
            }

            try
            {
                NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
                if (interfaces == null || interfaces.Length == 0)
                {
                    error = "interface_unavailable";
                    return false;
                }

                NetworkInterface networkInterface = interfaces[0];
                if (configuration.Mode == NetworkConfiguration.ModeDhcp)
                {
                    networkInterface.EnableDhcp();
                }
                else
                {
                    networkInterface.EnableStaticIPv4(configuration.Address, configuration.SubnetMask, configuration.Gateway);
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

                error = null;
                return true;
            }
            catch (Exception ex)
            {
                WriteStructuredDebug(
                    "CONFIG",
                    "schema=1 sub=config comp=apply domain=network operation=apply stat=error" +
                    " code=apply_failed detail=" + SanitizeToken(ex.Message));
                error = "apply_failed";
                return false;
            }
        }
    }
}