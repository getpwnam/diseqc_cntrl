namespace Cubley.Diseqc
{
    public enum DiseqcV1RoutePreset
    {
        None = 0,
        Direct = 1,
        CommittedAA = 2,
        CommittedAB = 3,
        CommittedBA = 4,
        CommittedBB = 5,
    }

    public struct DiseqcV1RouteProfile
    {
        public bool UseUncommittedSwitch;
        public byte UncommittedInputIndex;
        public bool UseCommittedSwitch;
        public DiseqcPort Position;
        public DiseqcPort Option;
    }

    public static class DiseqcV1Presets
    {
        public static bool TryGetRouteProfile(DiseqcV1RoutePreset preset, out DiseqcV1RouteProfile profile)
        {
            profile = new DiseqcV1RouteProfile();

            switch (preset)
            {
                case DiseqcV1RoutePreset.None:
                case DiseqcV1RoutePreset.Direct:
                    return true;

                case DiseqcV1RoutePreset.CommittedAA:
                    profile.UseCommittedSwitch = true;
                    profile.Position = DiseqcPort.A;
                    profile.Option = DiseqcPort.A;
                    return true;

                case DiseqcV1RoutePreset.CommittedAB:
                    profile.UseCommittedSwitch = true;
                    profile.Position = DiseqcPort.A;
                    profile.Option = DiseqcPort.B;
                    return true;

                case DiseqcV1RoutePreset.CommittedBA:
                    profile.UseCommittedSwitch = true;
                    profile.Position = DiseqcPort.B;
                    profile.Option = DiseqcPort.A;
                    return true;

                case DiseqcV1RoutePreset.CommittedBB:
                    profile.UseCommittedSwitch = true;
                    profile.Position = DiseqcPort.B;
                    profile.Option = DiseqcPort.B;
                    return true;

                default:
                    return false;
            }
        }

        public static bool TryParsePreset(string text, out DiseqcV1RoutePreset preset)
        {
            preset = DiseqcV1RoutePreset.None;

            if (text == null)
            {
                return false;
            }

            if (text == "off" || text == "none")
            {
                preset = DiseqcV1RoutePreset.None;
                return true;
            }

            if (text == "direct")
            {
                preset = DiseqcV1RoutePreset.Direct;
                return true;
            }

            if (text == "aa")
            {
                preset = DiseqcV1RoutePreset.CommittedAA;
                return true;
            }

            if (text == "ab")
            {
                preset = DiseqcV1RoutePreset.CommittedAB;
                return true;
            }

            if (text == "ba")
            {
                preset = DiseqcV1RoutePreset.CommittedBA;
                return true;
            }

            if (text == "bb")
            {
                preset = DiseqcV1RoutePreset.CommittedBB;
                return true;
            }

            return false;
        }

        public static string ToText(DiseqcV1RoutePreset preset)
        {
            switch (preset)
            {
                case DiseqcV1RoutePreset.None:
                    return "off";
                case DiseqcV1RoutePreset.Direct:
                    return "direct";
                case DiseqcV1RoutePreset.CommittedAA:
                    return "aa";
                case DiseqcV1RoutePreset.CommittedAB:
                    return "ab";
                case DiseqcV1RoutePreset.CommittedBA:
                    return "ba";
                case DiseqcV1RoutePreset.CommittedBB:
                    return "bb";
                default:
                    return "unknown";
            }
        }
    }
}