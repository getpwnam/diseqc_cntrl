namespace Cubley.Diseqc
{
    public sealed class DiseqcPositionEstimate
    {
        private const int MicrodegreesPerDegree = 1_000_000;
        private const int MaximumMagnitudeMicrodegrees =
            DiseqcLimits.GotoAngularMaxDegrees * MicrodegreesPerDegree;

        public bool HasEstimate { get; private set; }

        public int EstimatedAngleMicrodegrees { get; private set; }

        public string Confidence { get; private set; } = "unknown";

        public string Source { get; private set; } = "none";

        public bool HasPendingTarget { get; private set; }

        public int PendingTargetMicrodegrees { get; private set; }

        public string PendingSource { get; private set; } = "none";

        public int EastStepMicrodegrees { get; private set; }

        public int WestStepMicrodegrees { get; private set; }

        public bool HasStepCalibration
        {
            get { return EastStepMicrodegrees > 0 && WestStepMicrodegrees > 0; }
        }

        public void ConfigureStepCalibration(int eastStepMicrodegrees, int westStepMicrodegrees)
        {
            EastStepMicrodegrees = eastStepMicrodegrees;
            WestStepMicrodegrees = westStepMicrodegrees;
        }

        public void ClearStepCalibration()
        {
            EastStepMicrodegrees = 0;
            WestStepMicrodegrees = 0;
            ClearPending();
        }

        public void BeginGotoAngular(DiseqcMotorDirection direction, int magnitudeMicrodegrees)
        {
            ClearPending();
            if (!IsValidDirection(direction) || magnitudeMicrodegrees < 0 || magnitudeMicrodegrees > MaximumMagnitudeMicrodegrees)
            {
                return;
            }

            HasPendingTarget = true;
            PendingTargetMicrodegrees = direction == DiseqcMotorDirection.East
                ? magnitudeMicrodegrees
                : -magnitudeMicrodegrees;
            PendingSource = "goto_x";
        }

        public void BeginStep(DiseqcMotorDirection direction, int steps)
        {
            ClearPending();
            if (!HasEstimate || !HasStepCalibration || !IsValidDirection(direction) || steps < 1 || steps > 128)
            {
                return;
            }

            int stepSize = direction == DiseqcMotorDirection.East
                ? EastStepMicrodegrees
                : WestStepMicrodegrees;
            long signedDelta = direction == DiseqcMotorDirection.East
                ? (long)stepSize * steps
                : -((long)stepSize * steps);
            long target = (long)EstimatedAngleMicrodegrees + signedDelta;
            if (target < -MaximumMagnitudeMicrodegrees || target > MaximumMagnitudeMicrodegrees)
            {
                return;
            }

            HasPendingTarget = true;
            PendingTargetMicrodegrees = (int)target;
            PendingSource = "step";
        }

        public bool CompletePending()
        {
            return CompletePending("estimated");
        }

        public bool CompletePendingAsRfVerified()
        {
            return CompletePending("rf_verified");
        }

        private bool CompletePending(string confidence)
        {
            if (!HasPendingTarget)
            {
                Invalidate();
                return false;
            }

            HasEstimate = true;
            EstimatedAngleMicrodegrees = PendingTargetMicrodegrees;
            Confidence = confidence;
            Source = PendingSource;
            ClearPending();
            return true;
        }

        public void Invalidate()
        {
            HasEstimate = false;
            EstimatedAngleMicrodegrees = 0;
            Confidence = "unknown";
            Source = "none";
            ClearPending();
        }

        public void FailVerification()
        {
            HasEstimate = false;
            EstimatedAngleMicrodegrees = 0;
            Confidence = "verification_failed";
            Source = "none";
            ClearPending();
        }

        private static bool IsValidDirection(DiseqcMotorDirection direction)
        {
            return direction == DiseqcMotorDirection.East || direction == DiseqcMotorDirection.West;
        }

        private void ClearPending()
        {
            HasPendingTarget = false;
            PendingTargetMicrodegrees = 0;
            PendingSource = "none";
        }
    }
}