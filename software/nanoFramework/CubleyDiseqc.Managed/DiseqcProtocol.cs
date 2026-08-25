namespace Cubley.Diseqc
{
    // DiSEqC framing bytes for command/repeat/reply-expected command classes.
    public enum DiseqcFraming : byte
    {
        FirstTransmissionNoReply = 0xE0,
        RepeatedTransmissionNoReply = 0xE1,
        FirstTransmissionReplyExpected = 0xE2,
        RepeatedTransmissionReplyExpected = 0xE3,
    }

    public enum DiseqcPolarization
    {
        Vertical = 0,
        Horizontal = 1,
    }

    public enum DiseqcBand
    {
        Low = 0,
        High = 1,
    }

    public enum DiseqcPort
    {
        A = 0,
        B = 1,
    }

    public enum DiseqcBurst
    {
        MiniA = 0,
        MiniB = 1,
    }

    public static class DiseqcAddress
    {
        public const byte AnyLnbSwitchSmatv = 0x10;
        public const byte AnyPolarizerOrPositioner = 0x31;
        public const byte AnyPositioner = 0x30;
    }

    public static class DiseqcCommand
    {
        // 1.0/1.1 switch and LNB command set.
        public const byte WriteN0 = 0x38;
        public const byte WriteN1 = 0x39;

        // 1.2 positioner command set.
        public const byte Halt = 0x60;
        public const byte DriveEastOrStep = 0x68;
        public const byte DriveWestOrStep = 0x69;
        public const byte GotoStoredPosition = 0x6B;
    }

    public static class DiseqcLimits
    {
        public const int MinFrameBytes = 3;
        public const int MaxFrameBytes = 6;
        public const int EncodedBitsPerByte = 9;
    }
}
