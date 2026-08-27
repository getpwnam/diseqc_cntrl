namespace CubleyControl
{
    public static partial class Program
    {
        private static readonly object _mqttConfigurationLock = new object();
        private static readonly IApplicationConfigurationStorage _applicationConfigurationStorage =
            new InternalFlashApplicationConfigurationStorage();
        private static MqttConfiguration _mqttConfiguration = MqttConfiguration.CreateDefaults();
        private static MqttConfiguration _pendingMqttConfiguration = MqttConfiguration.CreateDefaults();
        private static bool _mqttConfigurationDirty;
        private static uint _mqttConfigurationGeneration;
        private static int _mqttConfigurationRevision;
        private static string _mqttConfigurationSource = "defaults";
        private static string _mqttConfigurationError = string.Empty;

        private static void InitializeMqttConfiguration()
        {
            MqttConfiguration loaded;
            uint generation;
            string error;
            if (_applicationConfigurationStorage.TryLoad(out loaded, out generation, out error))
            {
                _mqttConfiguration = loaded;
                _mqttConfigurationGeneration = generation;
                _mqttConfigurationSource = _applicationConfigurationStorage.Source;
                _mqttConfigurationError = string.Empty;
            }
            else
            {
                _mqttConfiguration = MqttConfiguration.CreateDefaults();
                _mqttConfigurationGeneration = 0;
                _mqttConfigurationSource = "defaults";
                _mqttConfigurationError = error;
            }

            _pendingMqttConfiguration = _mqttConfiguration.Clone();
            _mqttConfigurationDirty = false;
            _mqttConfigurationRevision = 1;
            WriteStructuredDebug(
                "CONFIG",
                "schema=1 sub=config comp=storage domain=mqtt operation=load" +
                " stat=" + (string.IsNullOrEmpty(_mqttConfigurationError) ? "ok" : "error") +
                " source=" + SanitizeToken(_mqttConfigurationSource) +
                " generation=" + _mqttConfigurationGeneration.ToString() +
                " enabled=" + (_mqttConfiguration.Enabled ? "1" : "0") +
                (string.IsNullOrEmpty(_mqttConfigurationError)
                    ? string.Empty
                    : " code=" + SanitizeToken(_mqttConfigurationError)));
        }
    }
}