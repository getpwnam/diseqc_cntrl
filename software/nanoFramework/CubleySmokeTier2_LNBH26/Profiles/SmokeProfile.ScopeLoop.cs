using Cubley.Interop;

namespace CubleySmokeTier2_LNBH26
{
    internal static class SmokeProfile
    {
        public const string Name = "Scope I2C Loop";
        public const bool Enable = false;
        public const int Voltage = (int)LNBH26.Voltage.V13;
        public const int Polarization = (int)LNBH26.Polarization.Vertical;
        public const int Band = (int)LNBH26.Band.Low;
        public const int Samples = 8;
        public const int Phase = 1;
        public const int ArmBreakMs = 2000;
        public const int EnableWindowMs = 0;
        public const int ScopeLoopMs = 200;
    }
}
