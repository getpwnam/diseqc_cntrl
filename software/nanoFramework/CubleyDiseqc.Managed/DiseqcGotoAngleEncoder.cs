namespace Cubley.Diseqc
{
    // Encodes command 0x6E using the DiSEqC GoToX degree nibbles and the
    // specified fractional-degree lookup. Decimal text is parsed without
    // floating point so host and nanoFramework builds quantize identically.
    public static class DiseqcGotoAngleEncoder
    {
        private const int MicrodegreesPerDegree = 1_000_000;
        private const int TenthsPerDegree = 10;
        private static readonly byte[] FractionCodeByTenth =
            { 0x00, 0x02, 0x03, 0x05, 0x06, 0x08, 0x0A, 0x0B, 0x0D, 0x0E };

        public static bool TryBuildFrame(
            DiseqcMotorDirection direction,
            string degrees,
            out byte[] frame,
            out int requestedMicrodegrees,
            out int encodedAngleTenths,
            out string error)
        {
            DiseqcMotorDirection effectiveDirection;
            int effectiveMicrodegrees;
            return TryBuildFrame(
                direction,
                degrees,
                0,
                out frame,
                out requestedMicrodegrees,
                out effectiveDirection,
                out effectiveMicrodegrees,
                out encodedAngleTenths,
                out error);
        }

        public static bool TryBuildFrame(
            DiseqcMotorDirection direction,
            string degrees,
            int signedOffsetMicrodegrees,
            out byte[] frame,
            out int requestedMicrodegrees,
            out DiseqcMotorDirection effectiveDirection,
            out int effectiveMicrodegrees,
            out int encodedAngleTenths,
            out string error)
        {
            frame = new byte[0];
            requestedMicrodegrees = 0;
            effectiveDirection = DiseqcMotorDirection.East;
            effectiveMicrodegrees = 0;
            encodedAngleTenths = 0;
            error = string.Empty;

            if (direction != DiseqcMotorDirection.East && direction != DiseqcMotorDirection.West)
            {
                error = "invalid_direction";
                return false;
            }

            int ignoredTenths;
            if (!TryParseDegrees(degrees, out requestedMicrodegrees, out ignoredTenths, out error))
            {
                return false;
            }

            long signedRequestedMicrodegrees = direction == DiseqcMotorDirection.East
                ? requestedMicrodegrees
                : -((long)requestedMicrodegrees);
            long signedEffectiveMicrodegrees = signedRequestedMicrodegrees + signedOffsetMicrodegrees;
            long maximumMicrodegrees = (long)DiseqcLimits.GotoAngularMaxDegrees * MicrodegreesPerDegree;
            if (signedEffectiveMicrodegrees < -maximumMicrodegrees || signedEffectiveMicrodegrees > maximumMicrodegrees)
            {
                error = "offset_out_of_range";
                return false;
            }

            effectiveDirection = signedEffectiveMicrodegrees < 0
                ? DiseqcMotorDirection.West
                : DiseqcMotorDirection.East;
            effectiveMicrodegrees = (int)(signedEffectiveMicrodegrees < 0
                ? -signedEffectiveMicrodegrees
                : signedEffectiveMicrodegrees);
            long scaled = ((long)effectiveMicrodegrees * TenthsPerDegree) +
                (MicrodegreesPerDegree / 2);
            encodedAngleTenths = (int)(scaled / MicrodegreesPerDegree);

            int directionWord = effectiveDirection == DiseqcMotorDirection.East ? 0xE000 : 0xD000;
            int wholeDegrees = encodedAngleTenths / TenthsPerDegree;
            int fractionalTenth = encodedAngleTenths % TenthsPerDegree;
            int positionWord = directionWord | (wholeDegrees << 4) | FractionCodeByTenth[fractionalTenth];
            frame = new byte[]
            {
                (byte)DiseqcFraming.FirstTransmissionNoReply,
                DiseqcAddress.AnyPolarizerOrPositioner,
                DiseqcCommand.GotoAngularPosition,
                (byte)(positionWord >> 8),
                (byte)positionWord,
            };

            return true;
        }

        public static bool TryParseDegrees(
            string text,
            out int requestedMicrodegrees,
            out int encodedAngleTenths,
            out string error)
        {
            requestedMicrodegrees = 0;
            encodedAngleTenths = 0;
            error = string.Empty;

            if (string.IsNullOrEmpty(text))
            {
                error = "angle_empty";
                return false;
            }

            int decimalIndex = -1;
            int wholeDegrees = 0;
            int fractionalMicrodegrees = 0;
            int fractionalDigits = 0;

            for (int i = 0; i < text.Length; i++)
            {
                char value = text[i];
                if (value == '.')
                {
                    if (decimalIndex >= 0 || i == 0 || i == text.Length - 1)
                    {
                        error = "angle_format";
                        return false;
                    }

                    decimalIndex = i;
                    continue;
                }

                if (value < '0' || value > '9')
                {
                    error = "angle_format";
                    return false;
                }

                int digit = value - '0';
                if (decimalIndex < 0)
                {
                    wholeDegrees = (wholeDegrees * 10) + digit;
                    if (wholeDegrees > DiseqcLimits.GotoAngularMaxDegrees)
                    {
                        error = "angle_out_of_range";
                        return false;
                    }
                }
                else
                {
                    fractionalDigits++;
                    if (fractionalDigits > 6)
                    {
                        error = "angle_precision";
                        return false;
                    }

                    fractionalMicrodegrees = (fractionalMicrodegrees * 10) + digit;
                }
            }

            while (fractionalDigits < 6)
            {
                fractionalMicrodegrees *= 10;
                fractionalDigits++;
            }

            requestedMicrodegrees = (wholeDegrees * MicrodegreesPerDegree) + fractionalMicrodegrees;
            if (requestedMicrodegrees > DiseqcLimits.GotoAngularMaxDegrees * MicrodegreesPerDegree)
            {
                requestedMicrodegrees = 0;
                error = "angle_out_of_range";
                return false;
            }

            long scaled = ((long)requestedMicrodegrees * TenthsPerDegree) +
                (MicrodegreesPerDegree / 2);
            encodedAngleTenths = (int)(scaled / MicrodegreesPerDegree);
            if (encodedAngleTenths > DiseqcLimits.GotoAngularMaxDegrees * TenthsPerDegree)
            {
                requestedMicrodegrees = 0;
                encodedAngleTenths = 0;
                error = "angle_out_of_range";
                return false;
            }

            return true;
        }

        public static string FormatMicrodegrees(int microdegrees)
        {
            int whole = microdegrees / MicrodegreesPerDegree;
            int fraction = microdegrees % MicrodegreesPerDegree;
            if (fraction == 0)
            {
                return whole.ToString();
            }

            string fractionText = fraction.ToString();
            while (fractionText.Length < 6)
            {
                fractionText = "0" + fractionText;
            }

            int end = fractionText.Length;
            while (end > 0 && fractionText[end - 1] == '0')
            {
                end--;
            }

            return whole.ToString() + "." + fractionText.Substring(0, end);
        }

        public static bool IsWithinTravelLimit(int requestedMicrodegrees, int limitMicrodegrees)
        {
            return requestedMicrodegrees >= 0 &&
                limitMicrodegrees > 0 &&
                requestedMicrodegrees <= limitMicrodegrees;
        }

        public static string FormatTenths(int tenths)
        {
            int whole = tenths / TenthsPerDegree;
            int remainder = tenths % TenthsPerDegree;
            if (remainder == 0)
            {
                return whole.ToString();
            }

            return whole.ToString() + "." + remainder.ToString();
        }
    }
}