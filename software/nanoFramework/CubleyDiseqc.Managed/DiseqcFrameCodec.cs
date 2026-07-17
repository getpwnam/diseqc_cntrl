namespace Cubley.Diseqc
{
    public static class DiseqcFrameCodec
    {
        public static bool TryValidateFrame(byte[] frame, out string reason)
        {
            if (frame == null)
            {
                reason = "null_frame";
                return false;
            }

            if (frame.Length < DiseqcLimits.MinFrameBytes)
            {
                reason = "frame_too_short";
                return false;
            }

            if (frame.Length > DiseqcLimits.MaxFrameBytes)
            {
                reason = "frame_too_long";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public static bool TryValidateDiseqc1xFrame(byte[] frame, out string reason)
        {
            if (!TryValidateFrame(frame, out reason))
            {
                return false;
            }

            byte framing = frame[0];
            if (framing < (byte)DiseqcFraming.FirstTransmissionNoReply || framing > (byte)DiseqcFraming.RepeatedTransmissionReplyExpected)
            {
                reason = "invalid_framing";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public static int GetEncodedBitCount(byte[] frame)
        {
            return frame == null ? 0 : frame.Length * DiseqcLimits.EncodedBitsPerByte;
        }

        public static bool ComputeOddParityBit(byte value)
        {
            int ones = 0;
            int temp = value;

            for (int i = 0; i < 8; i++)
            {
                if ((temp & 0x01) != 0)
                {
                    ones++;
                }

                temp >>= 1;
            }

            return (ones & 0x01) == 0;
        }

        public static bool[] EncodeBits(byte[] frame)
        {
            if (!TryValidateFrame(frame, out _))
            {
                return new bool[0];
            }

            bool[] bits = new bool[GetEncodedBitCount(frame)];
            int offset = 0;

            for (int i = 0; i < frame.Length; i++)
            {
                byte value = frame[i];

                for (int bit = 7; bit >= 0; bit--)
                {
                    bits[offset++] = ((value >> bit) & 0x01) != 0;
                }

                bits[offset++] = ComputeOddParityBit(value);
            }

            return bits;
        }
    }
}
