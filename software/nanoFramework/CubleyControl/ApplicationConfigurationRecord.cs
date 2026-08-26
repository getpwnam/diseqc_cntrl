using System;

namespace CubleyControl
{
    internal static class ApplicationConfigurationRecord
    {
        public const int RecordSize = 512;
        public const int HeaderSize = 16;
        public const byte SchemaVersion = 2;

        public static bool TryEncode(MqttConfiguration configuration, uint generation, out byte[] record, out string error)
        {
            record = null;
            error = null;
            if (configuration == null || !configuration.TryValidate(out error))
            {
                return false;
            }

            byte[] payload = AsciiStringToBytes(configuration.ToPayload());
            if (payload.Length == 0 || payload.Length > RecordSize - HeaderSize)
            {
                error = "payload_too_large";
                return false;
            }

            record = new byte[RecordSize];
            for (int index = 0; index < record.Length; index++)
            {
                record[index] = 0xFF;
            }

            record[0] = (byte)'C';
            record[1] = (byte)'C';
            record[2] = (byte)'F';
            record[3] = (byte)'G';
            record[4] = SchemaVersion;
            record[5] = 0;
            WriteUInt16(record, 6, (ushort)payload.Length);
            WriteUInt32(record, 8, generation);
            WriteUInt32(record, 12, CalculateCrc32(payload));
            Array.Copy(payload, 0, record, HeaderSize, payload.Length);
            error = null;
            return true;
        }

        public static bool TryDecode(byte[] record, out MqttConfiguration configuration, out uint generation, out string error)
        {
            configuration = MqttConfiguration.CreateDefaults();
            generation = 0;
            if (record == null || record.Length != RecordSize)
            {
                error = "record_size_invalid";
                return false;
            }

            if (record[0] != (byte)'C' || record[1] != (byte)'C' ||
                record[2] != (byte)'F' || record[3] != (byte)'G')
            {
                error = "record_magic_invalid";
                return false;
            }

            if (record[4] != SchemaVersion)
            {
                error = "record_version_unsupported";
                return false;
            }

            int payloadLength = ReadUInt16(record, 6);
            if (payloadLength <= 0 || payloadLength > RecordSize - HeaderSize)
            {
                error = "record_length_invalid";
                return false;
            }

            byte[] payload = new byte[payloadLength];
            Array.Copy(record, HeaderSize, payload, 0, payload.Length);
            if (CalculateCrc32(payload) != ReadUInt32(record, 12))
            {
                error = "record_crc_invalid";
                return false;
            }

            generation = ReadUInt32(record, 8);
            string text = AsciiBytesToString(payload);
            return MqttConfiguration.TryParsePayload(text, out configuration, out error);
        }

        private static byte[] AsciiStringToBytes(string text)
        {
            byte[] result = new byte[text.Length];
            for (int index = 0; index < text.Length; index++)
            {
                result[index] = (byte)text[index];
            }

            return result;
        }

        private static string AsciiBytesToString(byte[] data)
        {
            char[] result = new char[data.Length];
            for (int index = 0; index < data.Length; index++)
            {
                result[index] = (char)data[index];
            }

            return new string(result);
        }

        private static uint CalculateCrc32(byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            for (int index = 0; index < data.Length; index++)
            {
                crc ^= data[index];
                for (int bit = 0; bit < 8; bit++)
                {
                    crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
                }
            }
            return ~crc;
        }

        private static ushort ReadUInt16(byte[] data, int offset)
        {
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }

        private static uint ReadUInt32(byte[] data, int offset)
        {
            return (uint)(data[offset] | (data[offset + 1] << 8) |
                (data[offset + 2] << 16) | (data[offset + 3] << 24));
        }

        private static void WriteUInt16(byte[] data, int offset, ushort value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
        }

        private static void WriteUInt32(byte[] data, int offset, uint value)
        {
            data[offset] = (byte)value;
            data[offset + 1] = (byte)(value >> 8);
            data[offset + 2] = (byte)(value >> 16);
            data[offset + 3] = (byte)(value >> 24);
        }
    }
}