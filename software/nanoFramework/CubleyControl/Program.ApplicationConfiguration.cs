namespace CubleyControl
{
    public static partial class Program
    {
        private static readonly object _applicationConfigurationLock = new object();
        private static readonly IApplicationConfigurationStorage _applicationConfigurationStorage =
            new InternalFlashApplicationConfigurationStorage();
        private static ApplicationConfiguration _applicationConfiguration = ApplicationConfiguration.CreateDefaults();
        private static ApplicationConfiguration _pendingApplicationConfiguration = ApplicationConfiguration.CreateDefaults();
        private static bool _applicationConfigurationDirty;
        private static uint _applicationConfigurationGeneration;
        private static string _applicationConfigurationSource = "defaults";
        private static string _applicationConfigurationError = string.Empty;

        private static void InitializeApplicationConfiguration()
        {
            ApplicationConfiguration loaded;
            uint generation;
            string error;
            if (_applicationConfigurationStorage.TryLoad(out loaded, out generation, out error))
            {
                _applicationConfiguration = loaded;
                _applicationConfigurationGeneration = generation;
                _applicationConfigurationSource = _applicationConfigurationStorage.Source;
                _applicationConfigurationError = string.Empty;
            }
            else
            {
                _applicationConfiguration = ApplicationConfiguration.CreateDefaults();
                _applicationConfigurationGeneration = 0;
                _applicationConfigurationSource = "defaults";
                _applicationConfigurationError = error;
            }

            _pendingApplicationConfiguration = _applicationConfiguration.Clone();
            _applicationConfigurationDirty = false;
            ApplyDiseqcConfiguration(_applicationConfiguration);
            WriteStructuredDebug(
                "CONFIG",
                "schema=1 sub=config comp=storage domain=application operation=load" +
                " stat=" + (string.IsNullOrEmpty(_applicationConfigurationError) ? "ok" : "error") +
                " source=" + SanitizeToken(_applicationConfigurationSource) +
                " generation=" + _applicationConfigurationGeneration.ToString() +
                (string.IsNullOrEmpty(_applicationConfigurationError)
                    ? string.Empty
                    : " code=" + SanitizeToken(_applicationConfigurationError)));
        }

        private static bool TryPersistDiseqcAngleLimits(
            int eastMicrodegrees,
            int westMicrodegrees,
            out string error)
        {
            ApplicationConfiguration candidate;
            uint currentGeneration;
            lock (_applicationConfigurationLock)
            {
                candidate = _applicationConfiguration.Clone();
                currentGeneration = _applicationConfigurationGeneration;
            }

            candidate.DiseqcEastLimitMicrodegrees = eastMicrodegrees;
            candidate.DiseqcWestLimitMicrodegrees = westMicrodegrees;
            if (!candidate.TryValidate(out error))
            {
                return false;
            }

            uint savedGeneration;
            if (!TryPersistApplicationConfiguration(
                candidate,
                currentGeneration,
                out savedGeneration,
                out error))
            {
                return false;
            }

            lock (_applicationConfigurationLock)
            {
                _applicationConfiguration = candidate;
                _applicationConfigurationGeneration = savedGeneration;
            }

            _pendingApplicationConfiguration = candidate.Clone();
            _applicationConfigurationDirty = false;
            _applicationConfigurationSource = _applicationConfigurationStorage.Source;
            _applicationConfigurationError = string.Empty;
            lock (_diseqcMotionLock)
            {
                _diseqcEastTravelLimitMicrodegrees = eastMicrodegrees;
                _diseqcWestTravelLimitMicrodegrees = westMicrodegrees;
            }
            return true;
        }
    }
}