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

        public static int GetEncodedBitCount(byte[] frame)
        {
            return frame == null ? 0 : frame.Length * DiseqcLimits.EncodedBitsPerByte;
        }
    }
}
