using nanoFramework.Hardware.Stm32;

namespace CubleyControl
{
    public static partial class Program
    {
        private static void HandleSetHostnameCommand(string[] tokens, int reqId)
        {
            if (tokens.Length != 2)
            {
                WriteCommandResult(reqId, false, "validation_error", "hostname usage", "usage=hostname <name|auto>");
                return;
            }

            ApplicationConfiguration previous = _pendingApplicationConfiguration.Clone();
            _pendingApplicationConfiguration.Hostname = tokens[1] == "auto" ? string.Empty : tokens[1];
            string error;
            if (!_pendingApplicationConfiguration.TryValidate(out error))
            {
                _pendingApplicationConfiguration = previous;
                WriteCommandResult(reqId, false, "validation_error", "hostname invalid", "reason=" + error);
                return;
            }

            _applicationConfigurationDirty =
                _pendingApplicationConfiguration.ToPayload() != _applicationConfiguration.ToPayload();
            WriteCommandResult(
                reqId,
                true,
                "ok",
                "configuration staged",
                "field=hostname value=" +
                    (string.IsNullOrEmpty(_pendingApplicationConfiguration.Hostname)
                        ? "auto"
                        : _pendingApplicationConfiguration.Hostname) +
                    " state=" + (_applicationConfigurationDirty ? "staged" : "saved"));
        }

        private static string ResolveHostname(string configuredHostname)
        {
            if (!string.IsNullOrEmpty(configuredHostname))
            {
                return configuredHostname;
            }

            byte[] uniqueDeviceId = Utilities.UniqueDeviceId;
            if (uniqueDeviceId == null || uniqueDeviceId.Length == 0)
            {
                return "cubley";
            }

            uint hash = 2166136261;
            for (int index = 0; index < uniqueDeviceId.Length; index++)
            {
                hash ^= uniqueDeviceId[index];
                hash = unchecked(hash * 16777619);
            }

            return "cubley-" + ToLowerHex((byte)(hash >> 16)) +
                ToLowerHex((byte)(hash >> 8)) + ToLowerHex((byte)hash);
        }

        private static string ToLowerHex(byte value)
        {
            const string digits = "0123456789abcdef";
            return new string(new char[] { digits[(value >> 4) & 0x0F], digits[value & 0x0F] });
        }
    }
}