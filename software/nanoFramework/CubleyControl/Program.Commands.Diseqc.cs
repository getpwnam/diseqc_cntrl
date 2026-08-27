using System;
using System.Device.Pwm;
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
        private const int DiseqcBitOneMarkUs = 500;
        private const int DiseqcBitOneSpaceUs = 1000;
        private const int DiseqcBitZeroMarkUs = 1000;
        private const int DiseqcBitZeroSpaceUs = 500;
        private const int DiseqcQuietGapUs = 15000;
        private const int DiseqcMotionPollIntervalMs = 250;
        private const int DiseqcMotionWorstCaseMs = 90_000;
        private const int DiseqcStepBaseTimeMs = 1000;
        private const int DiseqcStepTimePerStepMs = 250;

        private static PwmChannel _diseqcCarrier;
        private static int _diseqcCarrierFrequencyHz;
        private static int _diseqcCarrierDutyPercent;
        private static bool _diseqcTxBusy;
        private static DiseqcV1RoutePreset _diseqcRoutePreset = DiseqcV1RoutePreset.Direct;
        private static readonly object _diseqcMotionLock = new object();
        private static bool _diseqcMotionBusy;
        private static int _diseqcMotionId;
        private static int _diseqcNextMotionId;
        private static string _diseqcMotionOperation = "idle";
        private static long _diseqcMotionDeadlineMs;
        private static string _diseqcMotionCompletionSource = "none";

        private static void HandleDiseqcCommand(string[] tokens, int reqId)
        {
            if (tokens.Length < 2)
            {
                WriteCommandResult(reqId, false, "validation_error", "diseqc usage", "usage=diseqc <goto|step|drive|stop|preset|tx|tone|listen> ...");
                return;
            }

            string verb = tokens[1];
            if (verb == "tone")
            {
                HandleDiseqcToneCommand(tokens, reqId);
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

            if (verb == "goto" && tokens.Length == 3)
            {
                if (!EnsureDiseqcMotionIdle(reqId))
                {
                    return;
                }

                int position;
                if (!TryParseByteDec(tokens[2], out position))
                {
                    WriteCommandResult(reqId, false, "validation_error", "diseqc goto invalid", "pos=" + tokens[2]);
                    return;
                }

                byte[] frame = DiseqcCommandBuilder.BuildGotoStoredPosition((byte)position);
                EmitDiseqcPositionerTransmitResult(
                    reqId,
                    "diseqc goto",
                    frame,
                    "goto",
                    DiseqcMotionWorstCaseMs);
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
                EmitDiseqcPositionerTransmitResult(reqId, "diseqc step", frame, "step_" + dir, motionTimeMs);
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
                    DiseqcMotionWorstCaseMs);
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

        private static void EmitDiseqcShowSummaryLine()
        {
            bool toneEnabled = _diseqcCarrier != null;
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
                "\r\n");
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
            if (tokens.Length < 4)
            {
                WriteCommandResult(reqId, false, "validation_error", "diseqc tx usage", "usage=diseqc tx <hex_byte> <hex_byte> [hex_byte]...");
                return;
            }

            if (tokens.Length > 9)
            {
                WriteCommandResult(reqId, false, "validation_error", "diseqc tx length invalid", "max_bytes=7");
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

            WriteCommandResult(reqId, true, "ok", source, "bytes=" + BytesToHex(frame) + " encoded_bits=" + (frame.Length * 9).ToString());
        }

        private static void EmitDiseqcPositionerTransmitResult(
            int reqId,
            string source,
            byte[] positionerFrame,
            string motionOperation,
            int motionDurationMs)
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

                CompleteOrBeginDiseqcMotion(motionOperation, motionDurationMs);
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

            CompleteOrBeginDiseqcMotion(motionOperation, motionDurationMs);

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

        private static bool EnsureDiseqcMotionIdle(int reqId)
        {
            bool busy;
            int motionId;
            string operation;
            int remainingMs;
            string completionSource;
            GetDiseqcMotionSnapshot(out busy, out motionId, out operation, out remainingMs, out completionSource);
            if (!busy)
            {
                return true;
            }

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

            lock (_diseqcMotionLock)
            {
                if (!_diseqcMotionBusy)
                {
                    WriteCommandResult(reqId, false, "validation_error", "no diseqc motion", "motion_id=0");
                    return;
                }

                if (_diseqcMotionId != requestedMotionId)
                {
                    WriteCommandResult(
                        reqId,
                        false,
                        "validation_error",
                        "diseqc motion id mismatch",
                        "motion_id=" + _diseqcMotionId.ToString());
                    return;
                }

                ClearDiseqcMotionLocked("external");
            }

            PublishMqttDiseqcMotionTransition("complete");
            WriteCommandResult(reqId, true, "ok", "diseqc complete", "motion_id=" + requestedMotionId.ToString());
        }

        private static void CompleteOrBeginDiseqcMotion(string operation, int durationMs)
        {
            if (operation == null)
            {
                bool wasBusy;
                lock (_diseqcMotionLock)
                {
                    wasBusy = _diseqcMotionBusy;
                    ClearDiseqcMotionLocked("halt");
                }

                if (wasBusy)
                {
                    PublishMqttDiseqcMotionTransition("complete");
                }
                return;
            }

            lock (_diseqcMotionLock)
            {
                _diseqcNextMotionId++;
                if (_diseqcNextMotionId <= 0)
                {
                    _diseqcNextMotionId = 1;
                }

                long nowMs = Environment.TickCount64;
                _diseqcMotionBusy = true;
                _diseqcMotionId = _diseqcNextMotionId;
                _diseqcMotionOperation = operation;
                _diseqcMotionDeadlineMs = nowMs +
                    (durationMs < DiseqcMotionWorstCaseMs ? durationMs : DiseqcMotionWorstCaseMs);
                _diseqcMotionCompletionSource = "pending";
            }

            PublishMqttDiseqcMotionTransition("start");
        }

        private static void ClearDiseqcMotionLocked(string completionSource)
        {
            _diseqcMotionBusy = false;
            _diseqcMotionOperation = "idle";
            _diseqcMotionDeadlineMs = 0;
            _diseqcMotionCompletionSource = completionSource;
        }

        private static void GetDiseqcMotionSnapshot(
            out bool busy,
            out int motionId,
            out string operation,
            out int remainingMs,
            out string completionSource)
        {
            lock (_diseqcMotionLock)
            {
                busy = _diseqcMotionBusy;
                motionId = _diseqcMotionId;
                operation = _diseqcMotionOperation;
                completionSource = _diseqcMotionCompletionSource;
                long remaining = busy ? _diseqcMotionDeadlineMs - Environment.TickCount64 : 0;
                remainingMs = remaining <= 0 ? 0 : (remaining > int.MaxValue ? int.MaxValue : (int)remaining);
            }
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
                " motion_remaining_ms=" + remainingMs.ToString();
        }

        private static void DiseqcMotionMonitorLoop()
        {
            while (true)
            {
                Thread.Sleep(DiseqcMotionPollIntervalMs);

                int expiredMotionId = 0;
                lock (_diseqcMotionLock)
                {
                    if (_diseqcMotionBusy && Environment.TickCount64 >= _diseqcMotionDeadlineMs)
                    {
                        expiredMotionId = _diseqcMotionId;
                    }
                }

                if (expiredMotionId == 0)
                {
                    continue;
                }

                lock (_commandLock)
                {
                    lock (_diseqcMotionLock)
                    {
                        if (!_diseqcMotionBusy || _diseqcMotionId != expiredMotionId ||
                            Environment.TickCount64 < _diseqcMotionDeadlineMs)
                        {
                            continue;
                        }
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
                        if (_diseqcMotionBusy && _diseqcMotionId == expiredMotionId)
                        {
                            ClearDiseqcMotionLocked(error.Length == 0 ? "timeout" : "timeout_halt_failed");
                        }
                    }
                }

                PublishMqttDiseqcMotionTransition("complete");
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

            try
            {
                if (!EnsureLnbInitialized())
                {
                    error = "lnb_init_failed";
                    return false;
                }

                // For this board, EXTM + TEN yields the external DiSEqC gating path.
                if (LNBH26.NativeSetEnable(LnbChannelA, true) != (int)LNBH26.Status.Ok)
                {
                    error = "lnb_enable_failed";
                    return false;
                }

                if (LNBH26.NativeSetDiseqcInputModeForChannel(LnbChannelA, LnbDiseqcInputEnabled) != (int)LNBH26.Status.Ok)
                {
                    error = "lnb_extm_failed";
                    return false;
                }

                if (LNBH26.NativeSetBandForChannel(LnbChannelA, (int)LNBH26.Band.High) != (int)LNBH26.Status.Ok)
                {
                    error = "lnb_ten_failed";
                    return false;
                }

                if (!EnsureDiseqcCarrierChannel(DiseqcDefaultFrequencyHz, DiseqcDefaultDutyPercent, out error))
                {
                    return false;
                }

                // Guard gap before transmit.
                SetDiseqcCarrierGate(false);
                DelayMicroseconds(DiseqcQuietGapUs);

                for (int frameIndex = 0; frameIndex < sequence.Length; frameIndex++)
                {
                    byte[] frame = sequence[frameIndex];
                    if (!DiseqcFrameCodec.TryValidateFrame(frame, out error))
                    {
                        return false;
                    }

                    bool[] bits = DiseqcFrameCodec.EncodeBits(frame);
                    for (int j = 0; j < bits.Length; j++)
                    {
                        EmitDiseqcBit(bits[j]);
                    }

                    SetDiseqcCarrierGate(false);
                    DelayMicroseconds(DiseqcQuietGapUs);
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
                SetDiseqcCarrierGate(false);
                _diseqcTxBusy = false;
            }
        }

        private static void EmitDiseqcBit(bool one)
        {
            int markUs = one ? DiseqcBitOneMarkUs : DiseqcBitZeroMarkUs;
            int spaceUs = one ? DiseqcBitOneSpaceUs : DiseqcBitZeroSpaceUs;

            SetDiseqcCarrierGate(true);
            DelayMicroseconds(markUs);
            SetDiseqcCarrierGate(false);
            DelayMicroseconds(spaceUs);
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

        private static void SetDiseqcCarrierGate(bool on)
        {
            if (_diseqcCarrier == null)
            {
                return;
            }

            try
            {
                if (on)
                {
                    _diseqcCarrier.Start();
                }
                else
                {
                    _diseqcCarrier.Stop();
                }
            }
            catch
            {
            }
        }

        private static bool EnsureDiseqcCarrierChannel(int frequencyHz, int dutyPercent, out string error)
        {
            error = string.Empty;

            if (_diseqcCarrier != null && _diseqcCarrierFrequencyHz == frequencyHz && _diseqcCarrierDutyPercent == dutyPercent)
            {
                return true;
            }

            StopDiseqcCarrier();

            PwmChannel channel = PwmChannel.CreateFromPin(DiseqcCarrierPin, frequencyHz, dutyPercent / 100.0d);
            if (channel == null)
            {
                error = "pwm_channel_unavailable";
                return false;
            }

            _diseqcCarrier = channel;
            _diseqcCarrierFrequencyHz = frequencyHz;
            _diseqcCarrierDutyPercent = dutyPercent;
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
                StopDiseqcCarrier();
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

            PwmChannel channel = PwmChannel.CreateFromPin(DiseqcCarrierPin, frequencyHz, dutyPercent / 100.0d);
            if (channel == null)
            {
                WriteCommandResult(reqId, false, "hw_fault", "diseqc tone start failed", "reason=pwm_channel_unavailable pin_id=" + DiseqcCarrierPin.ToString());
                return;
            }

            try
            {
                StopDiseqcCarrier();
                _diseqcCarrier = channel;
                _diseqcCarrierFrequencyHz = frequencyHz;
                _diseqcCarrierDutyPercent = dutyPercent;
                _diseqcCarrier.Start();
            }
            catch (Exception ex)
            {
                try
                {
                    channel.Dispose();
                }
                catch
                {
                }

                WriteCommandResult(reqId, false, "hw_fault", "diseqc tone start exception", "msg=" + SanitizeToken(ex.Message));
                return;
            }

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
            string tone = _diseqcCarrier == null ? "off" : "on";
            string payload =
                "tone=" + tone +
                " pin=pd12" +
                " pin_id=" + DiseqcCarrierPin.ToString() +
                " freq_hz=" + (_diseqcCarrier == null ? "0" : _diseqcCarrierFrequencyHz.ToString()) +
                " duty_pct=" + (_diseqcCarrier == null ? "0" : _diseqcCarrierDutyPercent.ToString());

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

        private static void StopDiseqcCarrier()
        {
            if (_diseqcCarrier == null)
            {
                return;
            }

            try
            {
                _diseqcCarrier.Stop();
            }
            catch
            {
            }

            try
            {
                _diseqcCarrier.Dispose();
            }
            catch
            {
            }

            _diseqcCarrier = null;
            _diseqcCarrierFrequencyHz = 0;
            _diseqcCarrierDutyPercent = 0;
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
