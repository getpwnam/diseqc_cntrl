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
            if (_networkConfigurationStorage.TryLoad(out loaded, out error) && loaded != null)
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
    }
}