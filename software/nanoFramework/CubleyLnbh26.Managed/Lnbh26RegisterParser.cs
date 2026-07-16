namespace Cubley.Lnbh26
{
    public sealed class Lnbh26BitField
    {
        public string Name;
        public string Value;
        public string Description;
        public int Bit;
        public int Mask;
    }

    public sealed class Lnbh26RegisterDecode
    {
        public int RawU8;
        public string RawHex;
        public Lnbh26BitField[] Bits;
    }

    public sealed class Lnbh26ParsedPayload
    {
        public string Channel;
        public Lnbh26RegisterDecode Status1;
        public Lnbh26RegisterDecode Status2;
        public Lnbh26RegisterDecode Data1;
        public Lnbh26RegisterDecode Data2;
        public Lnbh26RegisterDecode Data3;
        public Lnbh26RegisterDecode Data4;
    }

    public static class LNBH26RegisterParser
    {
        private const int Status1OlfA = 1 << 0;
        private const int Status1OlfB = 1 << 1;
        private const int Status1VmonA = 1 << 2;
        private const int Status1VmonB = 1 << 3;
        private const int Status1PdoA = 1 << 4;
        private const int Status1PdoB = 1 << 5;
        private const int Status1Otf = 1 << 6;
        private const int Status1Png = 1 << 7;

        private const int Status2TdetA = 1 << 0;
        private const int Status2TdetB = 1 << 1;
        private const int Status2TmonA = 1 << 2;
        private const int Status2TmonB = 1 << 3;
        private const int Status2ImonA = 1 << 4;
        private const int Status2ImonB = 1 << 5;

        private const int Data2TenA = 1 << 0;
        private const int Data2LpmA = 1 << 1;
        private const int Data2ExtmA = 1 << 2;
        private const int Data2TenB = 1 << 4;
        private const int Data2LpmB = 1 << 5;
        private const int Data2ExtmB = 1 << 6;

        private const int Data3IsetA = 1 << 0;
        private const int Data3IswA = 1 << 1;
        private const int Data3PclA = 1 << 2;
        private const int Data3TimerA = 1 << 3;
        private const int Data3IsetB = 1 << 4;
        private const int Data3IswB = 1 << 5;
        private const int Data3PclB = 1 << 6;
        private const int Data3TimerB = 1 << 7;

        private const int Data4EnImonA = 1 << 0;
        private const int Data4Olr = 1 << 3;
        private const int Data4EnImonB = 1 << 4;
        private const int Data4Therm = 1 << 6;
        private const int Data4Comp = 1 << 7;

        public static Lnbh26ParsedPayload Parse(string channel, int status1, int status2, int data1, int data2, int data3, int data4)
        {
            return new Lnbh26ParsedPayload
            {
                Channel = channel,
                Status1 = ParseStatus1(status1),
                Status2 = ParseStatus2(status2),
                Data1 = ParseData1(data1),
                Data2 = ParseData2(data2),
                Data3 = ParseData3(data3),
                Data4 = ParseData4(data4),
            };
        }

        private static Lnbh26RegisterDecode ParseStatus1(int raw)
        {
            return BuildRegister(raw, new Lnbh26BitField[]
            {
                BuildFlag("OLF_A", IsFlagSet(raw, Status1OlfA), "Overload fault flag for channel A output.", 0, 0x01),
                BuildFlag("OLF_B", IsFlagSet(raw, Status1OlfB), "Overload fault flag for channel B output.", 1, 0x02),
                BuildFlag("VMON_A", IsFlagSet(raw, Status1VmonA), "Voltage monitor status for channel A.", 2, 0x04),
                BuildFlag("VMON_B", IsFlagSet(raw, Status1VmonB), "Voltage monitor status for channel B.", 3, 0x08),
                BuildFlag("PDO_A", IsFlagSet(raw, Status1PdoA), "Overcurrent detected on output pull-down stage for channel A.", 4, 0x10),
                BuildFlag("PDO_B", IsFlagSet(raw, Status1PdoB), "Overcurrent detected on output pull-down stage for channel B.", 5, 0x20),
                BuildFlag("OTF", IsFlagSet(raw, Status1Otf), "Junction overtemperature detected.", 6, 0x40),
                BuildFlag("PNG", IsFlagSet(raw, Status1Png), "Input supply at VCC is below the LPD threshold.", 7, 0x80),
            });
        }

        private static Lnbh26RegisterDecode ParseStatus2(int raw)
        {
            return BuildRegister(raw, new Lnbh26BitField[]
            {
                BuildFlag("TDET_A", IsFlagSet(raw, Status2TdetA), "22kHz tone detect status on channel A path.", 0, 0x01),
                BuildFlag("TDET_B", IsFlagSet(raw, Status2TdetB), "22kHz tone detect status on channel B path.", 1, 0x02),
                BuildFlag("TMON_A", IsFlagSet(raw, Status2TmonA), "Tone monitor out-of-threshold flag for channel A (frequency or amplitude).", 2, 0x04),
                BuildFlag("TMON_B", IsFlagSet(raw, Status2TmonB), "Tone monitor out-of-threshold flag for channel B (frequency or amplitude).", 3, 0x08),
                BuildFlag("IMON_A", IsFlagSet(raw, Status2ImonA), "Output current monitor flag for channel A (1 = current below IMON threshold).", 4, 0x10),
                BuildFlag("IMON_B", IsFlagSet(raw, Status2ImonB), "Output current monitor flag for channel B (1 = current below IMON threshold).", 5, 0x20),
            });
        }

        private static Lnbh26RegisterDecode ParseData1(int raw)
        {
            int vselA = raw & 0x0F;
            int vselB = (raw >> 4) & 0x0F;

            return BuildRegister(raw, new Lnbh26BitField[]
            {
                BuildField("VSEL_A", VoltageSelectValueToText(vselA), "Channel A voltage selection nibble (disabled, 13V, 18V, or unknown).", -1, 0x0F),
                BuildField("VSEL_B", VoltageSelectValueToText(vselB), "Channel B voltage selection nibble (disabled, 13V, 18V, or unknown).", -1, 0xF0),
            });
        }

        private static Lnbh26RegisterDecode ParseData2(int raw)
        {
            return BuildRegister(raw, new Lnbh26BitField[]
            {
                BuildFlag("TEN_A", IsFlagSet(raw, Data2TenA), "Enable 22kHz tone on channel A.", 0, 0x01),
                BuildFlag("LPM_A", IsFlagSet(raw, Data2LpmA), "Enable low-power mode on channel A.", 1, 0x02),
                BuildFlag("EXTM_A", IsFlagSet(raw, Data2ExtmA), "Enable external DiSEqC input path for channel A.", 2, 0x04),
                BuildFlag("TEN_B", IsFlagSet(raw, Data2TenB), "Enable 22kHz tone on channel B.", 4, 0x10),
                BuildFlag("LPM_B", IsFlagSet(raw, Data2LpmB), "Enable low-power mode on channel B.", 5, 0x20),
                BuildFlag("EXTM_B", IsFlagSet(raw, Data2ExtmB), "Enable external DiSEqC input path for channel B.", 6, 0x40),
            });
        }

        private static Lnbh26RegisterDecode ParseData3(int raw)
        {
            return BuildRegister(raw, new Lnbh26BitField[]
            {
                BuildFlag("ISET_A", IsFlagSet(raw, Data3IsetA), "Channel A output current-limit range select (0 = default, 1 = lower range).", 0, 0x01),
                BuildFlag("ISW_A", IsFlagSet(raw, Data3IswA), "Channel A inductor switching current limit (0 = 4A typ, 1 = 2.5A typ).", 1, 0x02),
                BuildFlag("PCL_A", IsFlagSet(raw, Data3PclA), "Channel A pulsed dynamic current limiting control (0 = active, 1 = deactivated).", 2, 0x04),
                BuildFlag("TIMER_A", IsFlagSet(raw, Data3TimerA), "Channel A dynamic current-limit TON select (0 = 90ms typ, 1 = 180ms typ).", 3, 0x08),
                BuildFlag("ISET_B", IsFlagSet(raw, Data3IsetB), "Channel B output current-limit range select (0 = default, 1 = lower range).", 4, 0x10),
                BuildFlag("ISW_B", IsFlagSet(raw, Data3IswB), "Channel B inductor switching current limit (0 = 4A typ, 1 = 2.5A typ).", 5, 0x20),
                BuildFlag("PCL_B", IsFlagSet(raw, Data3PclB), "Channel B pulsed dynamic current limiting control (0 = active, 1 = deactivated).", 6, 0x40),
                BuildFlag("TIMER_B", IsFlagSet(raw, Data3TimerB), "Channel B dynamic current-limit TON select (0 = 90ms typ, 1 = 180ms typ).", 7, 0x80),
            });
        }

        private static Lnbh26RegisterDecode ParseData4(int raw)
        {
            return BuildRegister(raw, new Lnbh26BitField[]
            {
                BuildFlag("EN_IMON_A", IsFlagSet(raw, Data4EnImonA), "Enable IMON diagnostic mode for channel A (VOUT forced to ~21V typ).", 0, 0x01),
                BuildFlag("OLR", IsFlagSet(raw, Data4Olr), "Overload recovery mode (0 = auto-retry, 1 = keep output off until reprogrammed).", 3, 0x08),
                BuildFlag("EN_IMON_B", IsFlagSet(raw, Data4EnImonB), "Enable IMON diagnostic mode for channel B (VOUT forced to ~21V typ).", 4, 0x10),
                BuildFlag("THERM", IsFlagSet(raw, Data4Therm), "Thermal recovery mode (0 = auto-retry, 1 = keep outputs off until reprogrammed).", 6, 0x40),
                BuildFlag("COMP", IsFlagSet(raw, Data4Comp), "DC-DC compensation selection (0 = low ESR output caps, 1 = high ESR output caps).", 7, 0x80),
            });
        }

        private static Lnbh26RegisterDecode BuildRegister(int raw, Lnbh26BitField[] bits)
        {
            int value = raw & 0xFF;
            return new Lnbh26RegisterDecode
            {
                RawU8 = value,
                RawHex = ToHexU8(value),
                Bits = bits,
            };
        }

        private static Lnbh26BitField BuildFlag(string name, bool value, string description, int bit, int mask)
        {
            return BuildField(name, value ? "true" : "false", description, bit, mask);
        }

        private static Lnbh26BitField BuildField(string name, string value, string description, int bit, int mask)
        {
            return new Lnbh26BitField
            {
                Name = name,
                Value = value,
                Description = description,
                Bit = bit,
                Mask = mask,
            };
        }

        private static bool IsFlagSet(int raw, int mask)
        {
            return (raw & mask) != 0;
        }

        private static string VoltageSelectValueToText(int value)
        {
            if (value == 0x00)
            {
                return "disabled";
            }

            if (value == 0x01)
            {
                return "13V";
            }

            if (value == 0x08)
            {
                return "18V";
            }

            return "unknown_0x" + (value & 0x0F).ToString("X");
        }

        private static string ToHexU8(int value)
        {
            return "0x" + (value & 0xFF).ToString("X2");
        }
    }
}