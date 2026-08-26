using Cubley.Interop;

namespace CubleyControl
{
    internal interface IApplicationConfigurationStorage
    {
        string Source { get; }
        bool TryLoad(out MqttConfiguration configuration, out uint generation, out string error);
        bool TrySave(MqttConfiguration configuration, uint currentGeneration, out uint savedGeneration, out string error);
    }

    internal sealed class InternalFlashApplicationConfigurationStorage : IApplicationConfigurationStorage
    {
        public string Source
        {
            get { return "internal"; }
        }

        public bool TryLoad(out MqttConfiguration configuration, out uint generation, out string error)
        {
            configuration = MqttConfiguration.CreateDefaults();
            generation = 0;
            byte[] record = new byte[ApplicationConfigurationRecord.RecordSize];
            int status = ZPersistentConfiguration.NativeRead(record, 0, record.Length);
            if (status != (int)ZPersistentConfiguration.Status.Ok)
            {
                error = "storage_read_" + status.ToString();
                return false;
            }

            return ApplicationConfigurationRecord.TryDecode(record, out configuration, out generation, out error);
        }

        public bool TrySave(
            MqttConfiguration configuration,
            uint currentGeneration,
            out uint savedGeneration,
            out string error)
        {
            savedGeneration = currentGeneration == uint.MaxValue ? 1 : currentGeneration + 1;
            byte[] record;
            if (!ApplicationConfigurationRecord.TryEncode(configuration, savedGeneration, out record, out error))
            {
                return false;
            }

            int status = ZPersistentConfiguration.NativeWrite(record, 0, record.Length);
            if (status != (int)ZPersistentConfiguration.Status.Ok)
            {
                error = "storage_write_" + status.ToString();
                return false;
            }

            MqttConfiguration verified;
            uint verifiedGeneration;
            if (!TryLoad(out verified, out verifiedGeneration, out error) ||
                verifiedGeneration != savedGeneration || verified.ToPayload() != configuration.ToPayload())
            {
                error = string.IsNullOrEmpty(error) ? "storage_verify_failed" : error;
                return false;
            }

            error = null;
            return true;
        }
    }
}