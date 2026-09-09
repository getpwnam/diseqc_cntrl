using Cubley.Diseqc;

namespace CubleyControl
{
    public static partial class Program
    {
        private static void HandleSetDiseqcConfigurationCommand(string[] tokens, int reqId)
        {
            if (tokens.Length < 2)
            {
                WriteDiseqcConfigurationUsage(reqId);
                return;
            }

            ApplicationConfiguration previous = _pendingApplicationConfiguration.Clone();
            string field = tokens[1];
            if (field == "angle-limits")
            {
                if (tokens.Length == 3 && tokens[2] == "off")
                {
                    _pendingApplicationConfiguration.DiseqcEastLimitMicrodegrees = 0;
                    _pendingApplicationConfiguration.DiseqcWestLimitMicrodegrees = 0;
                    StageDiseqcConfigurationChange(reqId, field, "off", previous);
                    return;
                }

                int eastMicrodegrees;
                int eastTenths;
                int westMicrodegrees;
                int westTenths;
                string error;
                if (tokens.Length != 4 ||
                    !DiseqcGotoAngleEncoder.TryParseDegrees(tokens[2], out eastMicrodegrees, out eastTenths, out error) ||
                    !DiseqcGotoAngleEncoder.TryParseDegrees(tokens[3], out westMicrodegrees, out westTenths, out error) ||
                    eastMicrodegrees == 0 || westMicrodegrees == 0)
                {
                    WriteDiseqcConfigurationUsage(reqId);
                    return;
                }

                _pendingApplicationConfiguration.DiseqcEastLimitMicrodegrees = eastMicrodegrees;
                _pendingApplicationConfiguration.DiseqcWestLimitMicrodegrees = westMicrodegrees;
                StageDiseqcConfigurationChange(reqId, field, tokens[2] + "," + tokens[3], previous);
                return;
            }

            if (field == "step-calibration")
            {
                if (tokens.Length == 3 && tokens[2] == "off")
                {
                    _pendingApplicationConfiguration.DiseqcEastStepMicrodegrees = 0;
                    _pendingApplicationConfiguration.DiseqcWestStepMicrodegrees = 0;
                    StageDiseqcConfigurationChange(reqId, field, "off", previous);
                    return;
                }

                int eastMicrodegrees;
                int eastTenths;
                int westMicrodegrees;
                int westTenths;
                string error;
                if (tokens.Length != 4 ||
                    !DiseqcGotoAngleEncoder.TryParseDegrees(tokens[2], out eastMicrodegrees, out eastTenths, out error) ||
                    !DiseqcGotoAngleEncoder.TryParseDegrees(tokens[3], out westMicrodegrees, out westTenths, out error) ||
                    eastMicrodegrees == 0 || westMicrodegrees == 0)
                {
                    WriteDiseqcConfigurationUsage(reqId);
                    return;
                }

                _pendingApplicationConfiguration.DiseqcEastStepMicrodegrees = eastMicrodegrees;
                _pendingApplicationConfiguration.DiseqcWestStepMicrodegrees = westMicrodegrees;
                StageDiseqcConfigurationChange(reqId, field, tokens[2] + "," + tokens[3], previous);
                return;
            }

            if (field == "fixed-offset")
            {
                if (tokens.Length == 3 && tokens[2] == "off")
                {
                    _pendingApplicationConfiguration.DiseqcGotoOffsetMicrodegrees = 0;
                    StageDiseqcConfigurationChange(reqId, field, "off", previous);
                    return;
                }

                int magnitudeMicrodegrees;
                int magnitudeTenths;
                string error;
                if (tokens.Length != 4 ||
                    (tokens[2] != "east" && tokens[2] != "west") ||
                    !DiseqcGotoAngleEncoder.TryParseDegrees(tokens[3], out magnitudeMicrodegrees, out magnitudeTenths, out error))
                {
                    WriteDiseqcConfigurationUsage(reqId);
                    return;
                }

                _pendingApplicationConfiguration.DiseqcGotoOffsetMicrodegrees = tokens[2] == "east"
                    ? magnitudeMicrodegrees
                    : -magnitudeMicrodegrees;
                StageDiseqcConfigurationChange(reqId, field, tokens[2] + "," + tokens[3], previous);
                return;
            }

            if (field == "defaults" && tokens.Length == 2)
            {
                ClearDiseqcConfiguration(_pendingApplicationConfiguration);
                StageDiseqcConfigurationChange(reqId, field, "disabled", previous);
                return;
            }

            WriteDiseqcConfigurationUsage(reqId);
        }

        private static void StageDiseqcConfigurationChange(
            int reqId,
            string field,
            string value,
            ApplicationConfiguration previous)
        {
            string error;
            if (!_pendingApplicationConfiguration.TryValidate(out error))
            {
                _pendingApplicationConfiguration = previous;
                WriteCommandResult(reqId, false, "validation_error", "diseqc configuration invalid", "field=" + field + " reason=" + error);
                return;
            }

            _applicationConfigurationDirty = _pendingApplicationConfiguration.ToPayload() != _applicationConfiguration.ToPayload();
            WriteCommandResult(reqId, true, "ok", "configuration staged", "field=diseqc_" + field + " value=" + value + " state=" + (_applicationConfigurationDirty ? "staged" : "saved"));
        }

        private static void ApplyDiseqcConfiguration(ApplicationConfiguration configuration)
        {
            lock (_diseqcMotionLock)
            {
                _diseqcEastTravelLimitMicrodegrees = configuration.DiseqcEastLimitMicrodegrees;
                _diseqcWestTravelLimitMicrodegrees = configuration.DiseqcWestLimitMicrodegrees;
                _diseqcGotoOffsetMicrodegrees = configuration.DiseqcGotoOffsetMicrodegrees;
                if (configuration.DiseqcEastStepMicrodegrees > 0 && configuration.DiseqcWestStepMicrodegrees > 0)
                {
                    _diseqcPositionEstimate.ConfigureStepCalibration(
                        configuration.DiseqcEastStepMicrodegrees,
                        configuration.DiseqcWestStepMicrodegrees);
                }
                else
                {
                    _diseqcPositionEstimate.ClearStepCalibration();
                }
            }
        }

        private static bool HasDiseqcConfigurationChanged(ApplicationConfiguration first, ApplicationConfiguration second)
        {
            return first.DiseqcEastLimitMicrodegrees != second.DiseqcEastLimitMicrodegrees ||
                first.DiseqcWestLimitMicrodegrees != second.DiseqcWestLimitMicrodegrees ||
                first.DiseqcEastStepMicrodegrees != second.DiseqcEastStepMicrodegrees ||
                first.DiseqcWestStepMicrodegrees != second.DiseqcWestStepMicrodegrees ||
                first.DiseqcGotoOffsetMicrodegrees != second.DiseqcGotoOffsetMicrodegrees;
        }

        private static void ClearDiseqcConfiguration(ApplicationConfiguration configuration)
        {
            configuration.DiseqcEastLimitMicrodegrees = 0;
            configuration.DiseqcWestLimitMicrodegrees = 0;
            configuration.DiseqcEastStepMicrodegrees = 0;
            configuration.DiseqcWestStepMicrodegrees = 0;
            configuration.DiseqcGotoOffsetMicrodegrees = 0;
        }

        private static void WriteDiseqcConfigurationUsage(int reqId)
        {
            WriteCommandResult(
                reqId,
                false,
                "validation_error",
                "diseqc configuration usage",
                "usage=diseqc <angle-limits EAST WEST|step-calibration EAST WEST|fixed-offset east|west DEGREES|defaults|off>");
        }
    }
}