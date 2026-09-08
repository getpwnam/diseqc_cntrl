using System;
using System.Threading;
using Cubley.Interop;
using Cubley.Diseqc;

namespace CubleyControl
{
    public static partial class Program
    {
        // Pin encoding is portIndex * 16 + pinIndex. PD12 => 3*16+12.
        private const int DiseqcCarrierPin = 60;
        private const int DiseqcDefaultFrequencyHz = 22000;
        private const int DiseqcDefaultDutyPercent = 50;
        private const int LnbDiseqcInputDisabled = 0;
        private const int LnbDiseqcInputEnabled = 1;
        private const int DiseqcQuietGapUs = 15000;
        private const int DiseqcMotionPollIntervalMs = 250;
        private const int DiseqcMotionWorstCaseMs = 90_000;
        private const int DiseqcMotionTimeoutMinS = 5;
        private const int DiseqcMotionTimeoutMaxS = 300;
        private const int DiseqcStepBaseTimeMs = 1000;
        private const int DiseqcStepTimePerStepMs = 250;
        private const int DiseqcMotorStoredPositionMax = 60;

        private const int PositionerOpGoto = 1;
        private const int PositionerOpStepEast = 2;
        private const int PositionerOpStepWest = 3;
        private const int PositionerOpDriveEast = 4;
        private const int PositionerOpDriveWest = 5;
        private const int PositionerOpHalt = 6;

        private static bool _diseqcCarrierEnabled;
        private static int _diseqcCarrierFrequencyHz;
        private static int _diseqcCarrierDutyPercent;
        private static bool _diseqcTxBusy;
        private static DiseqcV1RoutePreset _diseqcRoutePreset = DiseqcV1RoutePreset.Direct;
        private static readonly object _diseqcMotionLock = new object();
        private static int _diseqcMotionTimeoutMs = DiseqcMotionWorstCaseMs;
        private static int _diseqcEastTravelLimitMicrodegrees;
        private static int _diseqcWestTravelLimitMicrodegrees;
        private static string _diseqcMotionCommandMode = "none";
        private static string _diseqcMotionRequestedAngle = "none";
        private static string _diseqcMotionEncodedAngle = "none";
        private static string _diseqcMotionDirection = "none";
        private static int _diseqcMotionVoltageV;
        private static readonly DiseqcPositionEstimate _diseqcPositionEstimate = new DiseqcPositionEstimate();

        private static void HandleDiseqcCommand(string[] tokens, int reqId)
        {
            if (tokens.Length < 2)
            {
                WriteCommandResult(reqId, false, "validation_error", "diseqc usage", "usage=diseqc <goto|goto-angle|reference|store|recalculate|motor-limit|angle-limits|step-calibration|step|drive|stop|preset|timeout|tx|tone|listen> ...");
                return;
            }

            string verb = tokens[1];
            if (verb == "tone")
            {
                HandleDiseqcToneCommand(tokens, reqId);
                return;
            }

            if (verb == "timeout")
            {
                HandleDiseqcTimeoutCommand(tokens, reqId);
                return;
            }

            if (verb == "angle-limits")
            {
                HandleDiseqcAngleLimitsCommand(tokens, reqId);
                return;
            }

            if (verb == "step-calibration")
            {
                HandleDiseqcStepCalibrationCommand(tokens, reqId);
                return;
            }

            if (verb == "listen")
            {
                HandleDiseqcListenCommand(tokens, reqId);
                return;
            }

            if (verb == "preset")
            {
                HandleDiseqcPresetCommand(tokens, reqId);
                return;
            }

            if (verb == "tx")
            {
                if (!EnsureDiseqcMotionIdle(reqId))
                {
                    return;
                }

                HandleDiseqcTxCommand(tokens, reqId);
                return;
            }

            if (verb == "complete")
            {
                HandleDiseqcCompleteCommand(tokens, reqId);
                return;
            }

            if (verb == "motor-limit" && tokens.Length == 3)
            {
                if (!EnsureDiseqcMotionIdle(reqId))
                {
                    return;
                }

                byte[] frame;
                if (tokens[2] == "off")
                {
                    frame = DiseqcCommandBuilder.BuildLimitsOff();
                }
                else if (tokens[2] == "east")
                {
                    frame = DiseqcCommandBuilder.BuildSetEastLimit();
                }
                else if (tokens[2] == "west")
                {
                    frame = DiseqcCommandBuilder.BuildSetWestLimit();
                }
                else
                {
                    WriteCommandResult(reqId, false, "validation_error", "diseqc motor-limit invalid", "usage=diseqc motor-limit <east|west|off>");
                    return;
                }

                EmitDiseqcPositionerSettingResult(reqId, "diseqc motor-limit", frame);
                return;
            }

            if (verb == "store" && tokens.Length == 3)
            {
                if (!EnsureDiseqcMotionIdle(reqId))
                {
                    return;
                }

                int position;
                if (!TryParseByteDec(tokens[2], out position) || position < 1 || position > DiseqcMotorStoredPositionMax)
                {
                    WriteCommandResult(reqId, false, "validation_error", "diseqc store invalid", "position=" + tokens[2] + " range=1..60");
                    return;
                }

                EmitDiseqcPositionerSettingResult(
                    reqId,
                    "diseqc store",
                    DiseqcCommandBuilder.BuildStorePosition((byte)position));
                return;
            }

            if (verb == "recalculate" && tokens.Length == 2)
            {
                if (!EnsureDiseqcMotionIdle(reqId))
                {
                    return;
                }

                EmitDiseqcPositionerSettingResult(
                    reqId,
                    "diseqc recalculate",
                    DiseqcCommandBuilder.BuildRecalculatePositions());
                return;
            }

            if (verb == "reference" && tokens.Length == 2)
            {
                if (!EnsureDiseqcMotionIdle(reqId))
                {
                    return;
                }

                EmitDiseqcPositionerTransmitResult(
                    reqId,
                    "diseqc reference",
                    DiseqcCommandBuilder.BuildGotoStoredPosition(0),
                    "goto_reference",
                    _diseqcMotionTimeoutMs);
                return;
            }

            if (verb == "goto" && tokens.Length == 3)
            {
                if (!EnsureDiseqcMotionIdle(reqId))
                {
                    return;
                }

                int position;
                if (!TryParseByteDec(tokens[2], out position) || position > DiseqcMotorStoredPositionMax)
                {
                    WriteCommandResult(reqId, false, "validation_error", "diseqc goto invalid", "position=" + tokens[2] + " range=0..60");
                    return;
                }

                byte[] frame = DiseqcCommandBuilder.BuildGotoStoredPosition((byte)position);
                EmitDiseqcPositionerTransmitResult(
                    reqId,
                    "diseqc goto",
                    frame,
                    "goto_stored",
                    _diseqcMotionTimeoutMs);
                return;
            }

            if (verb == "goto-angle" && tokens.Length == 4)
            {
                if (!EnsureDiseqcMotionIdle(reqId))
                {
                    return;
                }

                DiseqcMotorDirection direction;
                if (tokens[2] == "east")
                {
                    direction = DiseqcMotorDirection.East;
                }
                else if (tokens[2] == "west")
                {
                    direction = DiseqcMotorDirection.West;
                }
                else
                {
                    WriteCommandResult(reqId, false, "validation_error", "diseqc goto-angle direction invalid", "direction=" + tokens[2]);
                    return;
                }

                byte[] frame;
                int requestedMicrodegrees;
                int encodedAngleTenths;
                string error;
                if (!DiseqcCommandBuilder.TryBuildGotoAngularPosition(
                    direction,
                    tokens[3],
                    out frame,
                    out requestedMicrodegrees,
                    out encodedAngleTenths,
                    out error))
                {
                    WriteCommandResult(
                        reqId,
                        false,
                        "validation_error",
                        "diseqc goto-angle invalid",
                        "angle=" + SanitizeToken(tokens[3]) + " reason=" + SanitizeToken(error));
                    return;
                }

                int travelLimit = direction == DiseqcMotorDirection.East
                    ? _diseqcEastTravelLimitMicrodegrees
                    : _diseqcWestTravelLimitMicrodegrees;
                if (travelLimit <= 0)
                {
                    WriteCommandResult(
                        reqId,
                        false,
                        "validation_error",
                        "diseqc angle limits not configured",
                        "usage=diseqc angle-limits <east_degrees> <west_degrees>");
                    return;
                }

                if (!DiseqcGotoAngleEncoder.IsWithinTravelLimit(requestedMicrodegrees, travelLimit))
                {
                    WriteCommandResult(
                        reqId,
                        false,
                        "validation_error",
                        "diseqc goto-angle exceeds software limit",
                        "direction=" + tokens[2] +
                        " requested_angle_deg=" + DiseqcGotoAngleEncoder.FormatMicrodegrees(requestedMicrodegrees) +
                        " limit_deg=" + DiseqcGotoAngleEncoder.FormatMicrodegrees(travelLimit));
                    return;
                }

                EmitDiseqcPositionerTransmitResult(
                    reqId,
                    "diseqc goto-angle",
                    frame,
                    "goto_angle_" + tokens[2],
                    _diseqcMotionTimeoutMs,
                    "angular",
                    DiseqcGotoAngleEncoder.FormatMicrodegrees(requestedMicrodegrees),
                    DiseqcGotoAngleEncoder.FormatTenths(encodedAngleTenths),
                    tokens[2],
                    encodedAngleTenths * 100_000);
                return;
            }

            if (verb == "step" && tokens.Length == 4)
            {
                if (!EnsureDiseqcMotionIdle(reqId))
                {
                    return;
                }

                string dir = tokens[2];
                int steps;
                if (dir != "east" && dir != "west")
                {
                    WriteCommandResult(reqId, false, "validation_error", "diseqc step dir invalid", "dir=" + dir);
                    return;
                }

                if (!TryParseByteDec(tokens[3], out steps) || steps < 1 || steps > 128)
                {
                    WriteCommandResult(reqId, false, "validation_error", "diseqc step count invalid", "steps=" + tokens[3]);
                    return;
                }

                byte[] frame = dir == "east"
                    ? DiseqcCommandBuilder.BuildStepEast((byte)steps)
                    : DiseqcCommandBuilder.BuildStepWest((byte)steps);
                int motionTimeMs = DiseqcStepBaseTimeMs + (steps * DiseqcStepTimePerStepMs);
                EmitDiseqcPositionerTransmitResult(
                    reqId,
                    "diseqc step",
                    frame,
                    "step_" + dir,
                    motionTimeMs,
                    "step",
                    "none",
                    "none",
                    dir,
                    steps);
                return;
            }

            if (verb == "drive" && tokens.Length == 3)
            {
                if (!EnsureDiseqcMotionIdle(reqId))
                {
                    return;
                }

                string dir = tokens[2];
                if (dir != "east" && dir != "west")
                {
                    WriteCommandResult(reqId, false, "validation_error", "diseqc drive dir invalid", "dir=" + dir);
                    return;
                }

                byte[] frame = dir == "east"
                    ? DiseqcCommandBuilder.BuildDriveEast()
                    : DiseqcCommandBuilder.BuildDriveWest();
                EmitDiseqcPositionerTransmitResult(
                    reqId,
                    "diseqc drive",
                    frame,
                    "drive_" + dir,
                    _diseqcMotionTimeoutMs);
                return;
            }

            if (verb == "stop" && tokens.Length == 2)
            {
                byte[] frame = DiseqcCommandBuilder.BuildHalt();
                EmitDiseqcPositionerTransmitResult(reqId, "diseqc stop", frame, null, 0);
                return;
            }

            WriteCommandResult(reqId, false, "validation_error", "diseqc syntax invalid", "verb=" + verb);
        }

        private static void HandleDiseqcStepCalibrationCommand(string[] tokens, int reqId)
        {
            if (tokens.Length == 3 && tokens[2] == "status")
            {
                WriteCommandResult(reqId, true, "ok", "diseqc step-calibration", BuildDiseqcPositionEstimateData());
                return;
            }

            if (tokens.Length == 3 && tokens[2] == "off")
            {
                if (!EnsureDiseqcMotionIdle(reqId))
                {
                    return;
                }

                lock (_diseqcMotionLock)
                {
                    _diseqcPositionEstimate.ClearStepCalibration();
                }

                WriteCommandResult(reqId, true, "ok", "diseqc step-calibration disabled", BuildDiseqcPositionEstimateData());
                return;
            }

            if (tokens.Length != 4)
            {
                WriteCommandResult(reqId, false, "validation_error", "diseqc step-calibration usage", "usage=diseqc step-calibration <status|off|east_deg west_deg>");
                return;
            }

            int eastMicrodegrees;
            int eastTenths;
            string eastError;
            int westMicrodegrees;
            int westTenths;
            string westError;
            if (!DiseqcGotoAngleEncoder.TryParseDegrees(tokens[2], out eastMicrodegrees, out eastTenths, out eastError) ||
                !DiseqcGotoAngleEncoder.TryParseDegrees(tokens[3], out westMicrodegrees, out westTenths, out westError) ||
                eastMicrodegrees <= 0 || westMicrodegrees <= 0)
            {
                WriteCommandResult(
                    reqId,
                    false,
                    "validation_error",
                    "diseqc step-calibration invalid",
                    "east_step_deg=" + SanitizeToken(tokens[2]) + " west_step_deg=" + SanitizeToken(tokens[3]));
                return;
            }

            if (!EnsureDiseqcMotionIdle(reqId))
            {
                return;
            }

            lock (_diseqcMotionLock)
            {
                _diseqcPositionEstimate.ConfigureStepCalibration(eastMicrodegrees, westMicrodegrees);
            }

            WriteCommandResult(reqId, true, "ok", "diseqc step-calibration", BuildDiseqcPositionEstimateData());
        }

        private static void RunPositionerOperation(int operation, int value)
        {
            int reqId = NextRequestId();
            _activeCommandIsSetter = true;

            if (operation == PositionerOpHalt)
            {
                _activeCommand = "positioner halt";
                EmitDiseqcPositionerTransmitResult(reqId, "diseqc stop", DiseqcCommandBuilder.BuildHalt(), null, 0);
                return;
            }

            if (!EnsureDiseqcMotionIdle(reqId))
            {
                return;
            }

            if (operation == PositionerOpGoto)
            {
                _activeCommand = "positioner goto";
                EmitDiseqcPositionerTransmitResult(
                    reqId,
                    "diseqc goto",
                    DiseqcCommandBuilder.BuildGotoStoredPosition((byte)value),
                    "goto",
                    _diseqcMotionTimeoutMs);
                return;
            }

            if (operation == PositionerOpStepEast || operation == PositionerOpStepWest)
            {
                bool east = operation == PositionerOpStepEast;
                _activeCommand = "positioner step";
                EmitDiseqcPositionerTransmitResult(
                    reqId,
                    "diseqc step",
                    east ? DiseqcCommandBuilder.BuildStepEast((byte)value) : DiseqcCommandBuilder.BuildStepWest((byte)value),
                    east ? "step_east" : "step_west",
                    DiseqcStepBaseTimeMs + (value * DiseqcStepTimePerStepMs));
                return;
            }

            if (operation == PositionerOpDriveEast || operation == PositionerOpDriveWest)
            {
                bool east = operation == PositionerOpDriveEast;
                _activeCommand = "positioner drive";
                EmitDiseqcPositionerTransmitResult(
                    reqId,
                    "diseqc drive",
                    east ? DiseqcCommandBuilder.BuildDriveEast() : DiseqcCommandBuilder.BuildDriveWest(),
                    east ? "drive_east" : "drive_west",
                    _diseqcMotionTimeoutMs);
                return;
            }

            WriteCommandResult(reqId, false, "unsupported", "unknown positioner operation", "operation=" + operation.ToString());
        }

        private static void EmitDiseqcShowSummaryLine()
        {
            bool toneEnabled = _diseqcCarrierEnabled;
            bool motionBusy;
            int motionId;
            string motionOperation;
            int motionRemainingMs;
            string motionCompletionSource;
            GetDiseqcMotionSnapshot(
                out motionBusy,
                out motionId,
                out motionOperation,
                out motionRemainingMs,
                out motionCompletionSource);
            string positionConfidence;
            string estimatedAngle;
            string positionSource;
            string pendingTarget;
            string eastStep;
            string westStep;
            lock (_diseqcMotionLock)
            {
                positionConfidence = _diseqcPositionEstimate.Confidence;
                estimatedAngle = _diseqcPositionEstimate.HasEstimate
                    ? FormatSignedDiseqcAngle(_diseqcPositionEstimate.EstimatedAngleMicrodegrees)
                    : "Unknown";
                positionSource = _diseqcPositionEstimate.Source;
                pendingTarget = _diseqcPositionEstimate.HasPendingTarget
                    ? FormatSignedDiseqcAngle(_diseqcPositionEstimate.PendingTargetMicrodegrees)
                    : "None";
                eastStep = _diseqcPositionEstimate.HasStepCalibration
                    ? DiseqcGotoAngleEncoder.FormatMicrodegrees(_diseqcPositionEstimate.EastStepMicrodegrees)
                    : "Disabled";
                westStep = _diseqcPositionEstimate.HasStepCalibration
                    ? DiseqcGotoAngleEncoder.FormatMicrodegrees(_diseqcPositionEstimate.WestStepMicrodegrees)
                    : "Disabled";
            }
            if (_activeCommandTransport == CommandTransport.Usb)
            {
                WriteHumanHeading("DiSEqC");
                WriteHumanField("Preset", DiseqcV1Presets.ToText(_diseqcRoutePreset));
                WriteHumanField("Tone", toneEnabled ? "On" : "Off");
                WriteHumanField("Frequency", toneEnabled ? _diseqcCarrierFrequencyHz.ToString() + " Hz" : "Not active");
                WriteHumanField("Duty cycle", toneEnabled ? _diseqcCarrierDutyPercent.ToString() + "%" : "Not active");
                WriteHumanField("Transmitter", _diseqcTxBusy ? "Busy" : "Idle");
                WriteHumanField("Motion", motionBusy ? "Busy" : "Idle");
                WriteHumanField("Motion ID", motionId == 0 ? "None" : motionId.ToString());
                WriteHumanField("Operation", motionOperation);
                WriteHumanField("Remaining", motionBusy ? ((motionRemainingMs + 999) / 1000).ToString() + " s" : "0 s");
                WriteHumanField("Completion source", motionCompletionSource);
                WriteHumanField("Command mode", _diseqcMotionCommandMode);
                WriteHumanField("Direction", _diseqcMotionDirection);
                WriteHumanField("Requested angle", _diseqcMotionRequestedAngle == "none" ? "Not applicable" : _diseqcMotionRequestedAngle + " deg");
                WriteHumanField("Encoded angle", _diseqcMotionEncodedAngle == "none" ? "Not applicable" : _diseqcMotionEncodedAngle + " deg");
                WriteHumanField("Movement voltage", _diseqcMotionVoltageV == 0 ? "Unknown" : _diseqcMotionVoltageV.ToString() + " V");
                WriteHumanField("Estimated angle", estimatedAngle == "Unknown" ? estimatedAngle : estimatedAngle + " deg");
                WriteHumanField("Position confidence", positionConfidence);
                WriteHumanField("Position source", positionSource);
                WriteHumanField("Pending target", pendingTarget == "None" ? pendingTarget : pendingTarget + " deg");
                WriteHumanField("East step calibration", eastStep == "Disabled" ? eastStep : eastStep + " deg");
                WriteHumanField("West step calibration", westStep == "Disabled" ? westStep : westStep + " deg");
                WriteHumanField("East software limit", FormatDiseqcTravelLimit(_diseqcEastTravelLimitMicrodegrees));
                WriteHumanField("West software limit", FormatDiseqcTravelLimit(_diseqcWestTravelLimitMicrodegrees));
                WriteHumanField("Watchdog timeout", (_diseqcMotionTimeoutMs / 1000).ToString() + " s");
                return;
            }

            _activeOutputSink(
                "diseqc preset=" + DiseqcV1Presets.ToText(_diseqcRoutePreset) +
                " tone=" + (toneEnabled ? "on" : "off") +
                " frequency_hz=" + (toneEnabled ? _diseqcCarrierFrequencyHz.ToString() : "0") +
                " duty_percent=" + (toneEnabled ? _diseqcCarrierDutyPercent.ToString() : "0") +
                " tx_busy=" + (_diseqcTxBusy ? "1" : "0") +
                " motion_busy=" + (motionBusy ? "1" : "0") +
                " motion_id=" + motionId.ToString() +
                " motion_operation=" + motionOperation +
                " motion_remaining_ms=" + motionRemainingMs.ToString() +
                " motion_completion=" + motionCompletionSource +
                " " + BuildDiseqcMotionMetadataData() +
                " " + BuildDiseqcTravelLimitsData() +
                " motion_timeout_ms=" + _diseqcMotionTimeoutMs.ToString() +
                "\r\n");
        }

        private static void HandleDiseqcAngleLimitsCommand(string[] tokens, int reqId)
        {
            if (tokens.Length == 3 && tokens[2] == "status")
            {
                WriteCommandResult(reqId, true, "ok", "diseqc angle-limits", BuildDiseqcTravelLimitsData());
                return;
            }

            if (tokens.Length == 3 && tokens[2] == "off")
            {
                if (!EnsureDiseqcMotionIdle(reqId))
                {
                    return;
                }

                _diseqcEastTravelLimitMicrodegrees = 0;
                _diseqcWestTravelLimitMicrodegrees = 0;
                WriteCommandResult(reqId, true, "ok", "diseqc angle-limits disabled", BuildDiseqcTravelLimitsData());
                return;
            }

            if (tokens.Length != 4)
            {
                WriteCommandResult(
                    reqId,
                    false,
                    "validation_error",
                    "diseqc angle-limits usage",
                    "usage=diseqc angle-limits <status|off|east_degrees west_degrees>");
                return;
            }

            int eastMicrodegrees;
            int eastTenths;
            string eastError;
            int westMicrodegrees;
            int westTenths;
            string westError;
            if (!DiseqcGotoAngleEncoder.TryParseDegrees(tokens[2], out eastMicrodegrees, out eastTenths, out eastError) ||
                !DiseqcGotoAngleEncoder.TryParseDegrees(tokens[3], out westMicrodegrees, out westTenths, out westError) ||
                eastMicrodegrees <= 0 || westMicrodegrees <= 0)
            {
                WriteCommandResult(
                    reqId,
                    false,
                    "validation_error",
                    "diseqc angle-limits invalid",
                    "east=" + SanitizeToken(tokens[2]) +
                    " west=" + SanitizeToken(tokens[3]) +
                    " max_deg=" + DiseqcLimits.GotoAngularMaxDegrees.ToString());
                return;
            }

            if (!EnsureDiseqcMotionIdle(reqId))
            {
                return;
            }

            _diseqcEastTravelLimitMicrodegrees = eastMicrodegrees;
            _diseqcWestTravelLimitMicrodegrees = westMicrodegrees;
            WriteCommandResult(reqId, true, "ok", "diseqc angle-limits", BuildDiseqcTravelLimitsData());
        }

        private static string BuildDiseqcTravelLimitsData()
        {
            return "angle_limits_configured=" +
                (_diseqcEastTravelLimitMicrodegrees > 0 && _diseqcWestTravelLimitMicrodegrees > 0 ? "1" : "0") +
                " east_limit_deg=" + FormatDiseqcTravelLimit(_diseqcEastTravelLimitMicrodegrees) +
                " west_limit_deg=" + FormatDiseqcTravelLimit(_diseqcWestTravelLimitMicrodegrees);
        }

        private static string FormatDiseqcTravelLimit(int microdegrees)
        {
            return microdegrees <= 0 ? "disabled" : DiseqcGotoAngleEncoder.FormatMicrodegrees(microdegrees);
        }

        private static void HandleDiseqcTimeoutCommand(string[] tokens, int reqId)
        {
            if (tokens.Length != 3)
            {
                WriteCommandResult(
                    reqId,
                    false,
                    "validation_error",
                    "diseqc timeout usage",
                    "usage=diseqc timeout <status|" + DiseqcMotionTimeoutMinS.ToString() + ".." + DiseqcMotionTimeoutMaxS.ToString() + ">");
                return;
            }

            if (tokens[2] == "status")
            {
                WriteCommandResult(reqId, true, "ok", "diseqc timeout", "value_s=" + (_diseqcMotionTimeoutMs / 1000).ToString());
                return;
            }

            int seconds;
            if (!TryParsePositiveInt(tokens[2], out seconds) ||
                seconds < DiseqcMotionTimeoutMinS ||
                seconds > DiseqcMotionTimeoutMaxS)
            {
                WriteCommandResult(
                    reqId,
                    false,
                    "validation_error",
                    "diseqc timeout invalid",
                    "value=" + tokens[2] +
                    " min_s=" + DiseqcMotionTimeoutMinS.ToString() +
                    " max_s=" + DiseqcMotionTimeoutMaxS.ToString());
                return;
            }

            if (!EnsureDiseqcMotionIdle(reqId))
            {
                return;
            }

            _diseqcMotionTimeoutMs = seconds * 1000;
            WriteCommandResult(reqId, true, "ok", "diseqc timeout", "value_s=" + (_diseqcMotionTimeoutMs / 1000).ToString());
        }

        private static void HandleDiseqcPresetCommand(string[] tokens, int reqId)
        {
            if (tokens.Length != 3)
            {
                WriteCommandResult(reqId, false, "validation_error", "diseqc preset usage", "usage=diseqc preset <status|off|direct|aa|ab|ba|bb>");
                return;
            }

            if (tokens[2] == "status")
            {
                WriteCommandResult(reqId, true, "ok", "diseqc preset", "value=" + DiseqcV1Presets.ToText(_diseqcRoutePreset));
                return;
            }

            DiseqcV1RoutePreset preset;
            if (!DiseqcV1Presets.TryParsePreset(tokens[2], out preset))
            {
                WriteCommandResult(reqId, false, "validation_error", "diseqc preset invalid", "value=" + tokens[2]);
                return;
            }

            _diseqcRoutePreset = preset;
            WriteCommandResult(reqId, true, "ok", "diseqc preset", "value=" + DiseqcV1Presets.ToText(_diseqcRoutePreset));
        }

        private static void HandleDiseqcTxCommand(string[] tokens, int reqId)
        {
            if (tokens.Length < 5)
            {
                WriteCommandResult(reqId, false, "validation_error", "diseqc tx usage", "usage=diseqc tx <framing> <address> <command> [data_byte]...");
                return;
            }

            if (tokens.Length > 8)
            {
                WriteCommandResult(reqId, false, "validation_error", "diseqc tx length invalid", "max_bytes=6");
                return;
            }

            byte[] frame = new byte[tokens.Length - 2];
            for (int i = 2; i < tokens.Length; i++)
            {
                int value;
                if (!TryParseByteHex(tokens[i], out value))
                {
                    WriteCommandResult(reqId, false, "validation_error", "diseqc tx byte invalid", "token=" + tokens[i]);
                    return;
                }

                frame[i - 2] = (byte)value;
            }

            EmitDiseqcTransmitResult(reqId, "diseqc tx", frame);
        }

        private static void EmitDiseqcTransmitResult(int reqId, string source, byte[] frame)
        {
            string error;
            if (!TryTransmitDiseqcFrame(frame, out error))
            {
                WriteCommandResult(reqId, false, "hw_fault", source + " failed", "reason=" + SanitizeToken(error));
                return;
            }

            bool positionInvalidated = false;
            if (IsRawPositionerCommand(frame))
            {
                lock (_diseqcMotionLock)
                {
                    _diseqcPositionEstimate.Invalidate();
                }
                positionInvalidated = true;
            }

            WriteCommandResult(reqId, true, "ok", source, "bytes=" + BytesToHex(frame) + " encoded_bits=" + (frame.Length * 9).ToString());
            if (positionInvalidated)
            {
                PublishMqttDiseqcState();
            }
        }

        private static bool IsRawPositionerCommand(byte[] frame)
        {
            return frame != null && frame.Length >= 3 &&
                (frame[1] == DiseqcAddress.AnyPositioner || frame[1] == DiseqcAddress.AnyPolarizerOrPositioner) &&
                frame[2] >= DiseqcCommand.Halt && frame[2] <= DiseqcCommand.RecalculatePositions;
        }

        private static void EmitDiseqcPositionerSettingResult(int reqId, string source, byte[] positionerFrame)
        {
            string error;
            byte[][] prefixFrames;
            if (!TryBuildPresetPrefixFrames(out prefixFrames, out error))
            {
                WriteCommandResult(reqId, false, "hw_fault", source + " failed", "reason=" + SanitizeToken(error));
                return;
            }

            if (prefixFrames.Length == 0)
            {
                if (!TryTransmitDiseqcFrame(positionerFrame, out error))
                {
                    WriteCommandResult(reqId, false, "hw_fault", source + " failed", "reason=" + SanitizeToken(error));
                    return;
                }

                WriteCommandResult(reqId, true, "ok", source, "bytes=" + BytesToHex(positionerFrame));
                return;
            }

            byte[][] sequence = new byte[prefixFrames.Length + 1][];
            for (int i = 0; i < prefixFrames.Length; i++)
            {
                sequence[i] = prefixFrames[i];
            }

            sequence[prefixFrames.Length] = positionerFrame;
            if (!TryTransmitDiseqcSequence(sequence, out error))
            {
                WriteCommandResult(reqId, false, "hw_fault", source + " failed", "reason=" + SanitizeToken(error));
                return;
            }

            WriteCommandResult(
                reqId,
                true,
                "ok",
                source,
                "preset=" + DiseqcV1Presets.ToText(_diseqcRoutePreset) + " bytes=" + BytesToHex(positionerFrame));
        }

        private static void EmitDiseqcPositionerTransmitResult(
            int reqId,
            string source,
            byte[] positionerFrame,
            string motionOperation,
            int motionDurationMs)
        {
            EmitDiseqcPositionerTransmitResult(
                reqId,
                source,
                positionerFrame,
                motionOperation,
                motionDurationMs,
                motionOperation == null ? "halt" : motionOperation,
                "none",
                "none",
                MotionDirectionFromOperation(motionOperation),
                0);
        }

        private static void EmitDiseqcPositionerTransmitResult(
            int reqId,
            string source,
            byte[] positionerFrame,
            string motionOperation,
            int motionDurationMs,
            string commandMode,
            string requestedAngle,
            string encodedAngle,
            string direction,
            int positionValue)
        {
            string error;
            byte[][] prefixFrames;
            if (!TryBuildPresetPrefixFrames(out prefixFrames, out error))
            {
                WriteCommandResult(reqId, false, "hw_fault", source + " failed", "reason=" + SanitizeToken(error));
                return;
            }

            if (prefixFrames.Length == 0)
            {
                if (!TryTransmitDiseqcFrame(positionerFrame, out error))
                {
                    WriteCommandResult(reqId, false, "hw_fault", source + " failed", "reason=" + SanitizeToken(error));
                    return;
                }

                CompleteOrBeginDiseqcMotion(
                    motionOperation,
                    motionDurationMs,
                    commandMode,
                    requestedAngle,
                    encodedAngle,
                    direction,
                    GetDiseqcMotionVoltageV(),
                    positionValue);
                WriteCommandResult(
                    reqId,
                    true,
                    "ok",
                    source,
                    "bytes=" + BytesToHex(positionerFrame) +
                    " encoded_bits=" + DiseqcFrameCodec.GetEncodedBitCount(positionerFrame).ToString() +
                    BuildDiseqcMotionResultData());
                return;
            }

            byte[][] sequence = new byte[prefixFrames.Length + 1][];
            for (int i = 0; i < prefixFrames.Length; i++)
            {
                sequence[i] = prefixFrames[i];
            }

            sequence[prefixFrames.Length] = positionerFrame;

            if (!TryTransmitDiseqcSequence(sequence, out error))
            {
                WriteCommandResult(reqId, false, "hw_fault", source + " failed", "reason=" + SanitizeToken(error));
                return;
            }

            CompleteOrBeginDiseqcMotion(
                motionOperation,
                motionDurationMs,
                commandMode,
                requestedAngle,
                encodedAngle,
                direction,
                GetDiseqcMotionVoltageV(),
                positionValue);

            WriteCommandResult(
                reqId,
                true,
                "ok",
                source,
                "preset=" + DiseqcV1Presets.ToText(_diseqcRoutePreset) +
                " bytes=" + BytesToHex(positionerFrame) +
                " encoded_bits=" + DiseqcFrameCodec.GetEncodedBitCount(positionerFrame).ToString() +
                BuildDiseqcMotionResultData());
        }

        private static string MotionDirectionFromOperation(string operation)
        {
            if (operation == null)
            {
                return "none";
            }

            if (operation.IndexOf("east") >= 0)
            {
                return "east";
            }

            if (operation.IndexOf("west") >= 0)
            {
                return "west";
            }

            return "none";
        }

        private static int GetDiseqcMotionVoltageV()
        {
            if (!EnsureLnbInitialized())
            {
                return 0;
            }

            return LNBH26.NativeGetPolarizationForChannel(LnbChannelA) == (int)LNBH26.Polarization.Horizontal
                ? 18
                : 13;
        }

        private static bool EnsureDiseqcMotionIdle(int reqId)
        {
            int motionId = GetActiveDiseqcJobId();
            if (motionId == 0)
            {
                return true;
            }

            string operation;
            string state;
            int remainingMs;
            int timeoutMs;
            string detail;
            TryGetDiseqcJobSnapshot(motionId, out operation, out state, out remainingMs, out timeoutMs, out detail);
            _blockingDiseqcJobId = motionId;

            WriteCommandResult(
                reqId,
                false,
                "busy",
                "diseqc motion busy",
                "motion_id=" + motionId.ToString() +
                " operation=" + operation +
                " remaining_ms=" + remainingMs.ToString());
            return false;
        }

        private static void HandleDiseqcCompleteCommand(string[] tokens, int reqId)
        {
            int requestedMotionId;
            if (tokens.Length != 3 || !TryParsePositiveInt(tokens[2], out requestedMotionId))
            {
                WriteCommandResult(reqId, false, "validation_error", "diseqc complete usage", "usage=diseqc complete <motion_id>");
                return;
            }

            int activeJobId = GetActiveDiseqcJobId();
            if (activeJobId == 0)
            {
                WriteCommandResult(reqId, false, "validation_error", "no diseqc motion", "motion_id=0");
                return;
            }

            if (activeJobId != requestedMotionId ||
                !TryEndDiseqcJob(requestedMotionId, JobStateReleased, string.Empty))
            {
                WriteCommandResult(reqId, false, "validation_error", "diseqc motion id mismatch", "motion_id=" + activeJobId.ToString());
                return;
            }

            lock (_diseqcMotionLock)
            {
                _diseqcPositionEstimate.CompletePending();
            }

            PublishMqttDiseqcJobTransition("end", requestedMotionId);
            WriteCommandResult(reqId, true, "ok", "diseqc complete", "motion_id=" + requestedMotionId.ToString());
        }

        private static void CompleteOrBeginDiseqcMotion(
            string operation,
            int durationMs,
            string commandMode,
            string requestedAngle,
            string encodedAngle,
            string direction,
            int voltageV,
            int positionValue)
        {
            if (operation == null)
            {
                int haltedJobId = EndActiveDiseqcJob(JobStateHalted, string.Empty);
                lock (_diseqcMotionLock)
                {
                    _diseqcPositionEstimate.Invalidate();
                }

                if (haltedJobId != 0)
                {
                    _lastStartedDiseqcJobId = haltedJobId;
                    PublishMqttDiseqcJobTransition("end", haltedJobId);
                }
                return;
            }

            lock (_diseqcMotionLock)
            {
                _diseqcMotionCommandMode = commandMode;
                _diseqcMotionRequestedAngle = requestedAngle;
                _diseqcMotionEncodedAngle = encodedAngle;
                _diseqcMotionDirection = direction;
                _diseqcMotionVoltageV = voltageV;
                if (commandMode == "angular")
                {
                    _diseqcPositionEstimate.BeginGotoAngular(
                        direction == "east" ? DiseqcMotorDirection.East : DiseqcMotorDirection.West,
                        positionValue);
                }
                else if (commandMode == "step")
                {
                    _diseqcPositionEstimate.BeginStep(
                        direction == "east" ? DiseqcMotorDirection.East : DiseqcMotorDirection.West,
                        positionValue);
                }
                else
                {
                    _diseqcPositionEstimate.Invalidate();
                }
            }

            int jobId = BeginDiseqcJob(operation, durationMs);
            _lastStartedDiseqcJobId = jobId;
            PublishMqttDiseqcJobTransition("start", jobId);
        }

        private static string BuildDiseqcMotionResultData()
        {
            bool busy;
            int motionId;
            string operation;
            int remainingMs;
            string completionSource;
            GetDiseqcMotionSnapshot(out busy, out motionId, out operation, out remainingMs, out completionSource);
            return " motion_busy=" + (busy ? "1" : "0") +
                " motion_id=" + motionId.ToString() +
                " motion_remaining_ms=" + remainingMs.ToString() +
                " " + BuildDiseqcMotionMetadataData();
        }

        private static string BuildDiseqcMotionMetadataData()
        {
            lock (_diseqcMotionLock)
            {
                return "command_mode=" + _diseqcMotionCommandMode +
                    " requested_angle_deg=" + _diseqcMotionRequestedAngle +
                    " encoded_angle_deg=" + _diseqcMotionEncodedAngle +
                    " direction=" + _diseqcMotionDirection +
                    " movement_voltage_v=" + (_diseqcMotionVoltageV == 0 ? "unknown" : _diseqcMotionVoltageV.ToString()) +
                    " " + BuildDiseqcPositionEstimateDataLocked();
            }
        }

        private static string BuildDiseqcPositionEstimateData()
        {
            lock (_diseqcMotionLock)
            {
                return BuildDiseqcPositionEstimateDataLocked();
            }
        }

        private static string BuildDiseqcPositionEstimateDataLocked()
        {
            return "position_confidence=" + _diseqcPositionEstimate.Confidence +
                " estimated_angle_deg=" + (_diseqcPositionEstimate.HasEstimate
                    ? FormatSignedDiseqcAngle(_diseqcPositionEstimate.EstimatedAngleMicrodegrees)
                    : "unknown") +
                " position_source=" + _diseqcPositionEstimate.Source +
                " pending_target_deg=" + (_diseqcPositionEstimate.HasPendingTarget
                    ? FormatSignedDiseqcAngle(_diseqcPositionEstimate.PendingTargetMicrodegrees)
                    : "none") +
                " step_calibration_configured=" + (_diseqcPositionEstimate.HasStepCalibration ? "1" : "0") +
                " east_step_deg=" + (_diseqcPositionEstimate.HasStepCalibration
                    ? DiseqcGotoAngleEncoder.FormatMicrodegrees(_diseqcPositionEstimate.EastStepMicrodegrees)
                    : "none") +
                " west_step_deg=" + (_diseqcPositionEstimate.HasStepCalibration
                    ? DiseqcGotoAngleEncoder.FormatMicrodegrees(_diseqcPositionEstimate.WestStepMicrodegrees)
                    : "none");
        }

        private static string FormatSignedDiseqcAngle(int signedMicrodegrees)
        {
            return signedMicrodegrees < 0
                ? "-" + DiseqcGotoAngleEncoder.FormatMicrodegrees(-signedMicrodegrees)
                : DiseqcGotoAngleEncoder.FormatMicrodegrees(signedMicrodegrees);
        }

        private static void DiseqcMotionMonitorLoop()
        {
            while (true)
            {
                Thread.Sleep(DiseqcMotionPollIntervalMs);

                int expiredMotionId = GetExpiredDiseqcJobId();
                if (expiredMotionId == 0)
                {
                    continue;
                }

                bool ended;
                lock (_commandLock)
                {
                    if (GetExpiredDiseqcJobId() != expiredMotionId)
                    {
                        continue;
                    }

                    string error;
                    BeginLnbIoOperation();
                    try
                    {
                        lock (_lnbIoLock)
                        {
                            TryTransmitDiseqcFrame(DiseqcCommandBuilder.BuildHalt(), out error);
                        }
                    }
                    finally
                    {
                        EndLnbIoOperation();
                    }

                    lock (_diseqcMotionLock)
                    {
                        _diseqcPositionEstimate.FailVerification();
                    }

                    ended = error.Length == 0
                        ? TryEndDiseqcJob(expiredMotionId, JobStateTimeout, string.Empty)
                        : TryEndDiseqcJob(expiredMotionId, JobStateTimeoutHaltFailed, SanitizeToken(error));
                }

                if (ended)
                {
                    PublishMqttDiseqcJobTransition("end", expiredMotionId);
                }
            }
        }

        private static bool TryBuildPresetPrefixFrames(out byte[][] frames, out string error)
        {
            frames = new byte[0][];
            error = string.Empty;

            DiseqcV1RouteProfile profile;
            if (!DiseqcV1Presets.TryGetRouteProfile(_diseqcRoutePreset, out profile))
            {
                error = "invalid_preset";
                return false;
            }

            if (!profile.UseCommittedSwitch && !profile.UseUncommittedSwitch)
            {
                return true;
            }

            if (!EnsureLnbInitialized())
            {
                error = "lnb_init_failed";
                return false;
            }

            int pol = LNBH26.NativeGetPolarizationForChannel(LnbChannelA);
            int band = LNBH26.NativeGetBandForChannel(LnbChannelA);

            DiseqcPolarization diseqcPol = pol == (int)LNBH26.Polarization.Horizontal
                ? DiseqcPolarization.Horizontal
                : DiseqcPolarization.Vertical;

            DiseqcBand diseqcBand = band == (int)LNBH26.Band.High
                ? DiseqcBand.High
                : DiseqcBand.Low;

            if (profile.UseUncommittedSwitch && profile.UseCommittedSwitch)
            {
                frames = DiseqcCommandBuilder.BuildSwitchCascadeSequence(
                    profile.UncommittedInputIndex,
                    profile.Position,
                    profile.Option,
                    diseqcPol,
                    diseqcBand);
                return true;
            }

            if (profile.UseUncommittedSwitch)
            {
                frames = new byte[][]
                {
                    DiseqcCommandBuilder.BuildUncommittedSwitch(profile.UncommittedInputIndex),
                };

                return true;
            }

            frames = new byte[][]
            {
                DiseqcCommandBuilder.BuildCommittedSwitch(profile.Position, profile.Option, diseqcPol, diseqcBand),
            };

            return true;
        }

        private static bool TryTransmitDiseqcFrame(byte[] frame, out string error)
        {
            byte[][] sequence = new byte[][] { frame };
            return TryTransmitDiseqcSequence(sequence, out error);
        }

        private static bool TryTransmitDiseqcSequence(byte[][] sequence, out string error)
        {
            error = string.Empty;

            if (sequence == null || sequence.Length == 0)
            {
                error = "empty_sequence";
                return false;
            }

            if (_diseqcTxBusy)
            {
                error = "tx_busy";
                return false;
            }

            _diseqcTxBusy = true;
            bool resumeTone = _diseqcCarrierEnabled;
            int resumeFrequencyHz = _diseqcCarrierFrequencyHz;
            int resumeDutyPercent = _diseqcCarrierDutyPercent;
            bool toneRestored = false;
            bool lnbStateCaptured = false;
            bool lnbStateRestored = false;
            int resumeBand = (int)LNBH26.Band.Low;
            int resumeDiseqcInputMode = LnbDiseqcInputDisabled;

            try
            {
                if (!EnsureLnbInitialized())
                {
                    error = "lnb_init_failed";
                    return false;
                }

                int d1;
                int d2;
                int d3;
                int d4;
                int readStateRc = ReadLnbDataRegistersSafe(out d1, out d2, out d3, out d4);
                if (readStateRc != (int)LNBH26.Status.Ok)
                {
                    error = "lnb_state_read_" + readStateRc.ToString();
                    return false;
                }

                // Motor power must be explicit and remain available for the
                // full movement, not just while the DiSEqC frame is sent.
                if (!IsLnbChannelEnabled(LnbChannelA, d1))
                {
                    error = "lnb_disabled";
                    return false;
                }

                resumeBand = IsToneEnabledForChannel(LnbChannelA, d2)
                    ? (int)LNBH26.Band.High
                    : (int)LNBH26.Band.Low;
                resumeDiseqcInputMode = IsExtmEnabledForChannel(LnbChannelA, d2)
                    ? LnbDiseqcInputEnabled
                    : LnbDiseqcInputDisabled;
                lnbStateCaptured = true;

                // Band selection first establishes a valid internal-tone state
                // and a high DSQIN idle. DiSEqC then takes ownership of PD12 and
                // switches the LNBH26 to accept the external 22 kHz waveform.
                if (LNBH26.NativeSetBandForChannel(LnbChannelA, (int)LNBH26.Band.High) != (int)LNBH26.Status.Ok)
                {
                    error = "lnb_ten_failed";
                    return false;
                }

                if (LNBH26.NativeSetDiseqcInputModeForChannel(LnbChannelA, LnbDiseqcInputEnabled) != (int)LNBH26.Status.Ok)
                {
                    error = "lnb_extm_failed";
                    return false;
                }

                if (!EnsureDiseqcCarrierChannel(DiseqcDefaultFrequencyHz, DiseqcDefaultDutyPercent, out error))
                {
                    return false;
                }

                // Guard gap before transmit.
                DelayMicroseconds(DiseqcQuietGapUs);

                for (int frameIndex = 0; frameIndex < sequence.Length; frameIndex++)
                {
                    byte[] frame = sequence[frameIndex];
                    if (!DiseqcFrameCodec.TryValidateFrame(frame, out error))
                    {
                        return false;
                    }

                    int txStatus = ZZDiseqcTransmitter.NativeTransmit(frame, 0, frame.Length);
                    if (txStatus != (int)ZZDiseqcTransmitter.Status.Ok)
                    {
                        error = "native_tx_" + txStatus.ToString();
                        return false;
                    }

                    DelayMicroseconds(DiseqcQuietGapUs);
                }

                if (!TryRestoreDiseqcLnbState(resumeBand, resumeDiseqcInputMode, out error))
                {
                    return false;
                }

                lnbStateRestored = true;

                if (resumeTone)
                {
                    int restoreStatus = ZZDiseqcTransmitter.NativeSetTone(
                        resumeFrequencyHz,
                        resumeDutyPercent,
                        true);
                    if (restoreStatus != (int)ZZDiseqcTransmitter.Status.Ok)
                    {
                        _diseqcCarrierEnabled = false;
                        error = "tone_restore_" + restoreStatus.ToString();
                        return false;
                    }

                    toneRestored = true;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = "tx_exception_" + SanitizeToken(ex.Message);
                return false;
            }
            finally
            {
                // Stop any failed/incomplete external transmission before
                // restoring the GPIO idle level for the requested LNB band.
                if (!lnbStateRestored)
                {
                    ZZDiseqcTransmitter.NativeSetTone(
                        DiseqcDefaultFrequencyHz,
                        DiseqcDefaultDutyPercent,
                        false);
                }

                if (lnbStateCaptured && !lnbStateRestored)
                {
                    string restoreError;
                    if (TryRestoreDiseqcLnbState(resumeBand, resumeDiseqcInputMode, out restoreError))
                    {
                        lnbStateRestored = true;
                    }
                    else
                    {
                        WriteStructuredDebug(
                            "DISEQC",
                            "schema=1 sub=diseqc comp=control operation=restore_lnb" +
                            " stat=error reason=" + SanitizeToken(restoreError) +
                            " level=error");
                    }
                }

                // External continuous tone must be enabled only after EXTM/TEN
                // and the requested LNB state have been restored. Enabling it
                // earlier would be undone when band restoration reclaims PD12.
                if (resumeTone && !toneRestored)
                {
                    if (lnbStateRestored)
                    {
                        int restoreStatus = ZZDiseqcTransmitter.NativeSetTone(
                            resumeFrequencyHz,
                            resumeDutyPercent,
                            true);
                        if (restoreStatus != (int)ZZDiseqcTransmitter.Status.Ok)
                        {
                            _diseqcCarrierEnabled = false;
                        }
                    }
                    else
                    {
                        _diseqcCarrierEnabled = false;
                    }
                }

                _diseqcTxBusy = false;
            }
        }

        private static bool TryRestoreDiseqcLnbState(int band, int inputMode, out string error)
        {
            int bandRc = LNBH26.NativeSetBandForChannel(LnbChannelA, band);
            int inputRc = LNBH26.NativeSetDiseqcInputModeForChannel(LnbChannelA, inputMode);
            if (bandRc == (int)LNBH26.Status.Ok && inputRc == (int)LNBH26.Status.Ok)
            {
                error = string.Empty;
                return true;
            }

            error = "lnb_restore_band_" + bandRc.ToString() + "_extm_" + inputRc.ToString();
            return false;
        }

        private static void DelayMicroseconds(int microseconds)
        {
            if (microseconds <= 0)
            {
                return;
            }

            // Coarse sleep for millisecond chunks, then busy wait the remainder.
            if (microseconds >= 2000)
            {
                int sleepMs = (microseconds / 1000) - 1;
                if (sleepMs > 0)
                {
                    Thread.Sleep(sleepMs);
                    microseconds -= sleepMs * 1000;
                }
            }

            long targetTicks = DateTime.UtcNow.Ticks + (microseconds * 10L);
            while (DateTime.UtcNow.Ticks < targetTicks)
            {
            }
        }

        private static bool EnsureDiseqcCarrierChannel(int frequencyHz, int dutyPercent, out string error)
        {
            error = string.Empty;

            int status = ZZDiseqcTransmitter.NativeSetTone(frequencyHz, dutyPercent, false);
            if (status != (int)ZZDiseqcTransmitter.Status.Ok)
            {
                error = "native_tone_" + status.ToString();
                return false;
            }

            return true;
        }

        private static string BytesToHex(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                return string.Empty;
            }

            string output = string.Empty;
            for (int i = 0; i < bytes.Length; i++)
            {
                if (i > 0)
                {
                    output += "-";
                }

                output += bytes[i].ToString("X2");
            }

            return output;
        }

        private static void HandleDiseqcToneCommand(string[] tokens, int reqId)
        {
            if (tokens.Length < 3)
            {
                WriteCommandResult(reqId, false, "validation_error", "diseqc tone usage", "usage=diseqc tone on [freq_hz] [duty_pct]|off|status");
                return;
            }

            string action = tokens[2];
            if (action == "status")
            {
                EmitDiseqcToneStatus(reqId);
                return;
            }

            if (action == "off")
            {
                int stopStatus = StopDiseqcCarrier();
                if (stopStatus != (int)ZZDiseqcTransmitter.Status.Ok)
                {
                    WriteCommandResult(
                        reqId,
                        false,
                        "hw_fault",
                        "diseqc tone off failed",
                        "reason=native_tone_" + stopStatus.ToString() + " pin_id=" + DiseqcCarrierPin.ToString());
                    return;
                }

                WriteCommandResult(reqId, true, "ok", "diseqc tone off", "tone=off pin=pd12 pin_id=" + DiseqcCarrierPin.ToString());
                return;
            }

            if (action != "on")
            {
                WriteCommandResult(reqId, false, "validation_error", "diseqc tone action invalid", "action=" + action);
                return;
            }

            int frequencyHz = DiseqcDefaultFrequencyHz;
            int dutyPercent = DiseqcDefaultDutyPercent;

            if (tokens.Length >= 4)
            {
                if (!TryParsePositiveInt(tokens[3], out frequencyHz) || frequencyHz < 1000 || frequencyHz > 100000)
                {
                    WriteCommandResult(reqId, false, "validation_error", "diseqc tone frequency invalid", "value=" + tokens[3]);
                    return;
                }
            }

            if (tokens.Length >= 5)
            {
                if (!TryParsePositiveInt(tokens[4], out dutyPercent) || dutyPercent <= 0 || dutyPercent >= 100)
                {
                    WriteCommandResult(reqId, false, "validation_error", "diseqc tone duty invalid", "value=" + tokens[4]);
                    return;
                }
            }

            if (tokens.Length > 5)
            {
                WriteCommandResult(reqId, false, "validation_error", "diseqc tone usage", "usage=diseqc tone on [freq_hz] [duty_pct]");
                return;
            }

            int toneStatus = ZZDiseqcTransmitter.NativeSetTone(frequencyHz, dutyPercent, true);
            if (toneStatus != (int)ZZDiseqcTransmitter.Status.Ok)
            {
                WriteCommandResult(
                    reqId,
                    false,
                    "hw_fault",
                    "diseqc tone start failed",
                    "reason=native_tone_" + toneStatus.ToString() + " pin_id=" + DiseqcCarrierPin.ToString());
                return;
            }

            _diseqcCarrierEnabled = true;
            _diseqcCarrierFrequencyHz = frequencyHz;
            _diseqcCarrierDutyPercent = dutyPercent;

            WriteCommandResult(
                reqId,
                true,
                "ok",
                "diseqc tone on",
                "tone=on pin=pd12 pin_id=" + DiseqcCarrierPin.ToString() +
                " freq_hz=" + _diseqcCarrierFrequencyHz.ToString() +
                " duty_pct=" + _diseqcCarrierDutyPercent.ToString());
        }

        private static void HandleDiseqcListenCommand(string[] tokens, int reqId)
        {
            if (tokens.Length != 3)
            {
                WriteCommandResult(reqId, false, "validation_error", "diseqc listen usage", "usage=diseqc listen on|off");
                return;
            }

            if (!EnsureLnbInitialized())
            {
                WriteCommandResult(reqId, false, "hw_fault", "diseqc listen", BuildLnbInitDiagnosticData());
                return;
            }

            bool enable;
            if (!TryParseOnOff(tokens[2], out enable))
            {
                WriteCommandResult(reqId, false, "validation_error", "diseqc listen value invalid", "value=" + tokens[2]);
                return;
            }

            int mode = enable ? LnbDiseqcInputEnabled : LnbDiseqcInputDisabled;
            int rc = LNBH26.NativeSetDiseqcInputModeForChannel(LnbChannelA, mode);
            if (rc != (int)LNBH26.Status.Ok)
            {
                WriteCommandResult(reqId, false, "hw_fault", "diseqc listen failed", "rc=" + rc.ToString());
                return;
            }

            WriteCommandResult(reqId, true, "ok", "diseqc listen", "extm=" + (enable ? "on" : "off") + " channel=a");
        }

        private static void EmitDiseqcToneStatus(int reqId)
        {
            string tone = _diseqcCarrierEnabled ? "on" : "off";
            string payload =
                "tone=" + tone +
                " pin=pd12" +
                " pin_id=" + DiseqcCarrierPin.ToString() +
                " freq_hz=" + (_diseqcCarrierEnabled ? _diseqcCarrierFrequencyHz.ToString() : "0") +
                " duty_pct=" + (_diseqcCarrierEnabled ? _diseqcCarrierDutyPercent.ToString() : "0");

            if (EnsureLnbInitialized())
            {
                int d1;
                int d2;
                int d3;
                int d4;
                int rc = ReadLnbDataRegistersSafe(out d1, out d2, out d3, out d4);
                if (rc == (int)LNBH26.Status.Ok)
                {
                    payload += " extm=" + (IsExtmEnabledForChannel(LnbChannelA, d2) ? "on" : "off");
                }
                else
                {
                    payload += " extm=unknown";
                }
            }

            WriteCommandResult(reqId, true, "ok", "diseqc tone status", payload);
        }

        private static int StopDiseqcCarrier()
        {
            int frequencyHz = _diseqcCarrierFrequencyHz > 0
                ? _diseqcCarrierFrequencyHz
                : DiseqcDefaultFrequencyHz;
            int dutyPercent = _diseqcCarrierDutyPercent > 0
                ? _diseqcCarrierDutyPercent
                : DiseqcDefaultDutyPercent;
            int status = ZZDiseqcTransmitter.NativeSetTone(frequencyHz, dutyPercent, false);
            if (status != (int)ZZDiseqcTransmitter.Status.Ok)
            {
                return status;
            }

            _diseqcCarrierEnabled = false;
            _diseqcCarrierFrequencyHz = 0;
            _diseqcCarrierDutyPercent = 0;
            return status;
        }

        private static bool TryParsePositiveInt(string value, out int number)
        {
            number = 0;
            if (value == null || value.Length == 0)
            {
                return false;
            }

            try
            {
                number = int.Parse(value);
            }
            catch
            {
                return false;
            }

            return number > 0;
        }

        private static bool TryParseByteDec(string value, out int number)
        {
            number = 0;

            if (!TryParsePositiveInt(value, out number) && value != "0")
            {
                return false;
            }

            return number >= 0 && number <= 255;
        }

        private static bool TryParseByteHex(string value, out int number)
        {
            number = 0;
            if (value == null || value.Length == 0)
            {
                return false;
            }

            string token = value;
            if (token.Length > 2 && token[0] == '0' && (token[1] == 'x' || token[1] == 'X'))
            {
                token = token.Substring(2);
            }

            try
            {
                number = Convert.ToInt32(token, 16);
            }
            catch
            {
                return false;
            }

            return number >= 0 && number <= 255;
        }
    }
}
