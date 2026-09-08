namespace Cubley.Diseqc
{
    public static class DiseqcCommandBuilder
    {
        // Command byte high nibble for Write N0/N1 uses option bits in the low nibble.
        private const byte SwitchOptionBase = 0xF0;

        public static byte[] BuildFrame(DiseqcFraming framing, byte address, byte command)
        {
            return new byte[] { (byte)framing, address, command };
        }

        public static byte[] BuildFrame(DiseqcFraming framing, byte address, byte command, byte data)
        {
            return new byte[] { (byte)framing, address, command, data };
        }

        public static byte[] BuildHalt()
        {
            return BuildFrame(DiseqcFraming.FirstTransmissionNoReply, DiseqcAddress.AnyPolarizerOrPositioner, DiseqcCommand.Halt);
        }

        public static byte[] BuildDriveEast()
        {
            return BuildFrame(DiseqcFraming.FirstTransmissionNoReply, DiseqcAddress.AnyPolarizerOrPositioner, DiseqcCommand.DriveEastOrStep, 0x00);
        }

        public static byte[] BuildDriveWest()
        {
            return BuildFrame(DiseqcFraming.FirstTransmissionNoReply, DiseqcAddress.AnyPolarizerOrPositioner, DiseqcCommand.DriveWestOrStep, 0x00);
        }

        public static byte[] BuildStepEast(byte steps)
        {
            return BuildFrame(DiseqcFraming.FirstTransmissionNoReply, DiseqcAddress.AnyPolarizerOrPositioner, DiseqcCommand.DriveEastOrStep, NormalizeSteps(steps));
        }

        public static byte[] BuildStepWest(byte steps)
        {
            return BuildFrame(DiseqcFraming.FirstTransmissionNoReply, DiseqcAddress.AnyPolarizerOrPositioner, DiseqcCommand.DriveWestOrStep, NormalizeSteps(steps));
        }

        public static byte[] BuildGotoStoredPosition(byte position)
        {
            return BuildFrame(DiseqcFraming.FirstTransmissionNoReply, DiseqcAddress.AnyPolarizerOrPositioner, DiseqcCommand.GotoStoredPosition, position);
        }

        // DiSEqC 1.0 committed switch control (port/pol/band matrix).
        public static byte[] BuildCommittedSwitch(
            DiseqcPort position,
            DiseqcPort option,
            DiseqcPolarization polarization,
            DiseqcBand band,
            DiseqcFraming framing = DiseqcFraming.FirstTransmissionNoReply)
        {
            byte optionBits = 0;

            if (band == DiseqcBand.High)
            {
                optionBits |= 0x01;
            }

            if (polarization == DiseqcPolarization.Horizontal)
            {
                optionBits |= 0x02;
            }

            if (position == DiseqcPort.B)
            {
                optionBits |= 0x04;
            }

            if (option == DiseqcPort.B)
            {
                optionBits |= 0x08;
            }

            byte data = (byte)(SwitchOptionBase | optionBits);

            return BuildFrame(framing, DiseqcAddress.AnyLnbSwitchSmatv, DiseqcCommand.WriteN0, data);
        }

        // DiSEqC 1.1 uncommitted switch control (16-way input index).
        public static byte[] BuildUncommittedSwitch(
            byte inputIndex,
            DiseqcFraming framing = DiseqcFraming.FirstTransmissionNoReply)
        {
            byte index4bit = (byte)(inputIndex & 0x0F);
            byte data = (byte)(SwitchOptionBase | index4bit);

            return BuildFrame(framing, DiseqcAddress.AnyLnbSwitchSmatv, DiseqcCommand.WriteN1, data);
        }

        // Utility sequence for common 1.1 cascade setup: uncommitted then committed.
        public static byte[][] BuildSwitchCascadeSequence(
            byte uncommittedInputIndex,
            DiseqcPort position,
            DiseqcPort option,
            DiseqcPolarization polarization,
            DiseqcBand band)
        {
            return new byte[][]
            {
                BuildUncommittedSwitch(uncommittedInputIndex),
                BuildCommittedSwitch(position, option, polarization, band),
            };
        }

        private static byte NormalizeSteps(byte steps)
        {
            // DiSEqC positioner step counts are negative 8-bit values: FF is
            // one step, FE is two steps, ... and 80 is 128 steps.
            int normalizedSteps = steps == 0 ? 1 : steps;
            return (byte)(256 - normalizedSteps);
        }
    }
}
