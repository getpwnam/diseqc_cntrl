namespace DiSEqC_Control
{
    internal static class DiagnosticsStatusWord
    {
        internal const byte ResultRunning = 0x00;
        internal const byte ResultPass = 0x01;
        internal const byte ResultWarn = 0x02;
        internal const byte ResultFail = 0x0E;

        internal static uint Compose(byte stage, byte result, byte detail)
        {
            return ((uint)0xD5 << 24) | ((uint)stage << 16) | ((uint)result << 8) | detail;
        }

        internal static byte ComputeAggregateResult(byte failureCount, byte skippedCount)
        {
            if (failureCount > 0)
            {
                return ResultFail;
            }

            if (skippedCount > 0)
            {
                return ResultWarn;
            }

            return ResultPass;
        }
    }
}
