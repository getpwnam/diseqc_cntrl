namespace DiSEqC_Control
{
    internal sealed class HardwareCapabilities
    {
        public const byte W5500Bit = 0x01;
        public const byte LnbBit = 0x02;
        public const byte FramBit = 0x04;

        public static readonly HardwareCapabilities None = new HardwareCapabilities(0);

        private HardwareCapabilities(byte bitmap)
        {
            Bitmap = bitmap;
        }

        public byte Bitmap { get; }

        public bool HasW5500 { get { return (Bitmap & W5500Bit) != 0; } }

        public bool HasLnbh26 { get { return (Bitmap & LnbBit) != 0; } }

        public bool HasFram { get { return (Bitmap & FramBit) != 0; } }

        public static HardwareCapabilities FromBitmap(byte bitmap)
        {
            return new HardwareCapabilities(bitmap);
        }
    }
}
