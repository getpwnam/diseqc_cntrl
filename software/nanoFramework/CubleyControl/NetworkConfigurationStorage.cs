using System;
using System.Text;
using Cubley.Interop;

namespace CubleyControl
{
    internal interface INetworkConfigurationStorage
    {
        string Source { get; }
        bool TryLoad(out NetworkConfiguration configuration, out string error);
        bool TrySave(NetworkConfiguration configuration, out string error);
    }

    internal sealed class FramNetworkConfigurationStorage : INetworkConfigurationStorage
    {
        private const int SlotAAddress = 0x0400;
        private const int SlotBAddress = 0x0600;
        private const int SlotSize = 0x0200;
        private const int HeaderSize = 16;
        private const byte RecordVersion = 1;

        private readonly UTF8Encoding _encoding = new UTF8Encoding();

        public string Source
        {
            get { return "fram"; }
        }

        public bool TryLoad(out NetworkConfiguration configuration, out string error)
        {
            configuration = NetworkConfiguration.CreateDefaults();
            if (Fram24C128.NativeInit() != (int)Fram24C128.Status.Ok)
            {
                error = "fram_init_failed";
                return false;
            }

            SlotRecord slotA;
            SlotRecord slotB;
            bool validA = TryReadSlot(SlotAAddress, out slotA);
            bool validB = TryReadSlot(SlotBAddress, out slotB);
            NetworkConfiguration configA = null;
            NetworkConfiguration configB = null;
            string parseError;
            validA = validA && NetworkConfiguration.TryParsePayload(slotA.Payload, out configA, out parseError);
            validB = validB && NetworkConfiguration.TryParsePayload(slotB.Payload, out configB, out parseError);
            if (!validA && !validB)
            {
                error = "no_valid_record";
                return false;
            }

            SlotRecord selected = !validB || (validA && slotA.Generation >= slotB.Generation) ? slotA : slotB;
            configuration = selected == slotA ? configA : configB;

            error = null;
            return true;
        }

        public bool TrySave(NetworkConfiguration configuration, out string error)
        {
            error = null;
            if (configuration == null || !configuration.TryValidate(out error))
            {
                return false;
            }

            if (Fram24C128.NativeInit() != (int)Fram24C128.Status.Ok)
            {
                error = "fram_init_failed";
                return false;
            }

            byte[] payload = _encoding.GetBytes(configuration.ToPayload());
            if (payload.Length == 0 || payload.Length > SlotSize - HeaderSize)
            {
                error = "payload_too_large";
                return false;
            }

            SlotRecord slotA;
            SlotRecord slotB;
            bool validA = TryReadSlot(SlotAAddress, out slotA);
            bool validB = TryReadSlot(SlotBAddress, out slotB);
            uint generation = 1;
            if (validA && slotA.Generation >= generation)
            {
                generation = slotA.Generation + 1;
            }
            if (validB && slotB.Generation >= generation)
            {
                generation = slotB.Generation + 1;
            }

            int targetAddress = !validA || (validB && slotA.Generation <= slotB.Generation) ? SlotAAddress : SlotBAddress;
            byte[] invalidMagic = new byte[4];
            if (!Write(targetAddress, invalidMagic, invalidMagic.Length) ||
                !Write(targetAddress + HeaderSize, payload, payload.Length))
            {
                error = "fram_write_failed";
                return false;
            }

            byte[] header = BuildHeader(payload, generation);
            if (!Write(targetAddress, header, header.Length))
            {
                error = "fram_header_failed";
                return false;
            }

            SlotRecord verified;
            if (!TryReadSlot(targetAddress, out verified) || verified.Generation != generation)
            {
                error = "fram_verify_failed";
                return false;
            }

            error = null;
            return true;
        }

        private bool TryReadSlot(int address, out SlotRecord record)
        {
            record = new SlotRecord();
            byte[] header = new byte[HeaderSize];
            if (Fram24C128.NativeRead(address, header, 0, header.Length) != (int)Fram24C128.Status.Ok ||
                header[0] != (byte)'D' || header[1] != (byte)'C' || header[2] != (byte)'F' || header[3] != (byte)'G' ||
                header[4] != RecordVersion)
            {
                return false;
            }

            int length = ReadUInt16(header, 6);
            if (length <= 0 || length > SlotSize - HeaderSize)
            {
                return false;
            }

            byte[] payload = new byte[length];
            if (Fram24C128.NativeRead(address + HeaderSize, payload, 0, payload.Length) != (int)Fram24C128.Status.Ok ||
                CalculateCrc32(payload) != ReadUInt32(header, 12))
            {
                return false;
            }

            record.Generation = ReadUInt32(header, 8);
            record.Payload = new string(_encoding.GetChars(payload));
            return true;
        }

        private static byte[] BuildHeader(byte[] payload, uint generation)
        {
            byte[] header = new byte[HeaderSize];
            header[0] = (byte)'D';
            header[1] = (byte)'C';
            header[2] = (byte)'F';
            header[3] = (byte)'G';
            header[4] = RecordVersion;
            WriteUInt16(header, 6, (ushort)payload.Length);
            WriteUInt32(header, 8, generation);
            WriteUInt32(header, 12, CalculateCrc32(payload));
            return header;
        }

        private static bool Write(int address, byte[] data, int count)
        {
            return Fram24C128.NativeWrite(address, data, 0, count) == (int)Fram24C128.Status.Ok;
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
            return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
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

        private sealed class SlotRecord
        {
            public uint Generation;
            public string Payload;
        }
    }
}