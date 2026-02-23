using System;
using System.Device.I2c;
using System.Text;

namespace DiSEqC_Control
{
    internal sealed class FramConfigurationStorage
    {
        private const int BaseDeviceAddress = 0x50;
        private const int DeviceBlockCount = 8;
        private const int FramSizeBytes = 2048;
        private const int HeaderSizeBytes = 9;
        private const int PayloadCapacity = FramSizeBytes - HeaderSizeBytes;

        private const byte Version = 1;

        private readonly I2cDevice[] _devices;

        public FramConfigurationStorage(int busId)
        {
            _devices = new I2cDevice[DeviceBlockCount];

            for (int i = 0; i < DeviceBlockCount; i++)
            {
                _devices[i] = new I2cDevice(new I2cConnectionSettings(busId, BaseDeviceAddress + i));
            }
        }

        public bool TrySave(RuntimeConfiguration configuration, out string error)
        {
            try
            {
                string payloadText = configuration.ToKeyValueLines();
                byte[] payload = Encoding.UTF8.GetBytes(payloadText);

                if (payload.Length > PayloadCapacity)
                {
                    error = $"Config payload too large for FRAM ({payload.Length} bytes)";
                    return false;
                }

                ushort checksum = CalculateChecksum(payload);
                byte[] header = new byte[HeaderSizeBytes]
                {
                    (byte)'D', (byte)'C', (byte)'F', (byte)'G',
                    Version,
                    (byte)(payload.Length & 0xFF), (byte)((payload.Length >> 8) & 0xFF),
                    (byte)(checksum & 0xFF), (byte)((checksum >> 8) & 0xFF)
                };

                WriteBytes(0, header, 0, header.Length);
                WriteBytes(HeaderSizeBytes, payload, 0, payload.Length);

                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = $"FRAM save failed: {ex.Message}";
                return false;
            }
        }

        public bool TryLoad(out RuntimeConfiguration configuration, out string error)
        {
            configuration = RuntimeConfiguration.CreateDefaults();

            try
            {
                byte[] header = new byte[HeaderSizeBytes];
                ReadBytes(0, header, 0, header.Length);

                if (header[0] != (byte)'D' || header[1] != (byte)'C' || header[2] != (byte)'F' || header[3] != (byte)'G')
                {
                    error = "No valid FRAM config header found";
                    return false;
                }

                if (header[4] != Version)
                {
                    error = $"Unsupported FRAM config version: {header[4]}";
                    return false;
                }

                int payloadLength = header[5] | (header[6] << 8);
                if (payloadLength <= 0 || payloadLength > PayloadCapacity)
                {
                    error = $"Invalid FRAM config length: {payloadLength}";
                    return false;
                }

                ushort expectedChecksum = (ushort)(header[7] | (header[8] << 8));
                byte[] payload = new byte[payloadLength];
                ReadBytes(HeaderSizeBytes, payload, 0, payloadLength);

                ushort actualChecksum = CalculateChecksum(payload);
                if (actualChecksum != expectedChecksum)
                {
                    error = "FRAM config checksum mismatch";
                    return false;
                }

                string payloadText = Encoding.UTF8.GetString(payload, 0, payloadLength);
                if (!RuntimeConfiguration.TryParseKeyValueLines(payloadText, out configuration, out error))
                {
                    return false;
                }

                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = $"FRAM load failed: {ex.Message}";
                return false;
            }
        }

        public bool TryReadRaw(int address, int count, out byte[] data, out string error)
        {
            data = null;

            if (address < 0 || address >= FramSizeBytes)
            {
                error = $"Address out of range: {address}";
                return false;
            }

            if (count <= 0)
            {
                error = "Read length must be > 0";
                return false;
            }

            if (address + count > FramSizeBytes)
            {
                error = $"Read exceeds FRAM size ({FramSizeBytes} bytes)";
                return false;
            }

            try
            {
                data = new byte[count];
                ReadBytes(address, data, 0, count);
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = $"FRAM raw read failed: {ex.Message}";
                return false;
            }
        }

        public bool TryClear(out string error)
        {
            try
            {
                byte[] clearChunk = new byte[32];
                for (int i = 0; i < clearChunk.Length; i++)
                {
                    clearChunk[i] = 0xFF;
                }

                int address = 0;
                while (address < FramSizeBytes)
                {
                    int chunk = FramSizeBytes - address;
                    if (chunk > clearChunk.Length)
                    {
                        chunk = clearChunk.Length;
                    }

                    WriteBytes(address, clearChunk, 0, chunk);
                    address += chunk;
                }

                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = $"FRAM clear failed: {ex.Message}";
                return false;
            }
        }

        private void WriteBytes(int address, byte[] source, int offset, int count)
        {
            int remaining = count;
            int sourceOffset = offset;
            int currentAddress = address;

            while (remaining > 0)
            {
                int blockOffset = currentAddress & 0xFF;
                int chunk = remaining;

                int bytesUntilBlockEnd = 256 - blockOffset;
                if (chunk > bytesUntilBlockEnd)
                {
                    chunk = bytesUntilBlockEnd;
                }

                byte[] buffer = new byte[chunk + 1];
                buffer[0] = (byte)blockOffset;
                Array.Copy(source, sourceOffset, buffer, 1, chunk);

                int block = (currentAddress >> 8) & 0x07;
                _devices[block].Write(buffer);

                currentAddress += chunk;
                sourceOffset += chunk;
                remaining -= chunk;
            }
        }

        private void ReadBytes(int address, byte[] destination, int offset, int count)
        {
            int remaining = count;
            int destinationOffset = offset;
            int currentAddress = address;

            while (remaining > 0)
            {
                int blockOffset = currentAddress & 0xFF;
                int chunk = remaining;

                int bytesUntilBlockEnd = 256 - blockOffset;
                if (chunk > bytesUntilBlockEnd)
                {
                    chunk = bytesUntilBlockEnd;
                }

                byte[] writeBuffer = new byte[1] { (byte)blockOffset };
                byte[] readBuffer = new byte[chunk];

                int block = (currentAddress >> 8) & 0x07;
                _devices[block].WriteRead(writeBuffer, readBuffer);

                Array.Copy(readBuffer, 0, destination, destinationOffset, chunk);

                currentAddress += chunk;
                destinationOffset += chunk;
                remaining -= chunk;
            }
        }

        private static ushort CalculateChecksum(byte[] data)
        {
            uint sum = 0;
            for (int i = 0; i < data.Length; i++)
            {
                sum += data[i];
            }

            return (ushort)(sum & 0xFFFF);
        }
    }
}