using Cubley.Interop;

namespace CubleySmokeTier2_LNBH26
{
    internal static class SmokeProfile
    {
        public const string Name = "18V ToneOn Window";
        public const bool Enable = false;
        public const int Voltage = (int)LNBH26.Voltage.V18;
        public const int Polarization = (int)LNBH26.Polarization.Horizontal;
        public const int Band = (int)LNBH26.Band.High;
        public const int Samples = 8;
        public const int Phase = 4;
        public const int ArmBreakMs = 0;
        public const int EnableWindowMs = 10000;
        public const int ScopeLoopMs = 0;
    }
}
