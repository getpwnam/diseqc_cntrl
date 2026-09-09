using Cubley.Diseqc;
using Xunit;

namespace DiSEqC_Control.Tests;

public sealed class DiseqcPositionEstimateTests
{
    [Fact]
    public void GotoTargetIsAdoptedOnlyAfterCompletion()
    {
        var estimate = new DiseqcPositionEstimate();

        estimate.BeginGotoAngular(DiseqcMotorDirection.West, 36_600_000);

        Assert.False(estimate.HasEstimate);
        Assert.Equal(-36_600_000, estimate.PendingTargetMicrodegrees);

        Assert.True(estimate.CompletePending());
        Assert.Equal(-36_600_000, estimate.EstimatedAngleMicrodegrees);
        Assert.Equal("estimated", estimate.Confidence);
        Assert.Equal("goto_x", estimate.Source);
    }

    [Fact]
    public void RestGotoAngleAdoptsOffsetAdjustedEncodedTargetOnRelease()
    {
        bool success = DiseqcCommandBuilder.TryBuildGotoAngularPosition(
            DiseqcMotorDirection.East,
            "36.6",
            -3_380_000,
            out _,
            out int requestedMicrodegrees,
            out DiseqcMotorDirection effectiveDirection,
            out _,
            out int encodedAngleTenths,
            out string error);
        var estimate = new DiseqcPositionEstimate();

        Assert.True(success, error);
        Assert.Equal(36_600_000, requestedMicrodegrees);
        Assert.Equal(DiseqcMotorDirection.East, effectiveDirection);

        estimate.BeginGotoAngular(effectiveDirection, encodedAngleTenths * 100_000);

        Assert.False(estimate.HasEstimate);
        Assert.Equal(33_200_000, estimate.PendingTargetMicrodegrees);
        Assert.True(estimate.CompletePending());
        Assert.Equal(33_200_000, estimate.EstimatedAngleMicrodegrees);
        Assert.Equal("estimated", estimate.Confidence);
        Assert.Equal("goto_x", estimate.Source);
    }

    [Fact]
    public void CalibratedStepsRetainSubTenthDegreePrecision()
    {
        var estimate = CreateEstimatedPosition(DiseqcMotorDirection.East, 36_600_000);
        estimate.ConfigureStepCalibration(25_000, 30_000);

        estimate.BeginStep(DiseqcMotorDirection.West, 30);

        Assert.Equal(35_700_000, estimate.PendingTargetMicrodegrees);
        Assert.True(estimate.CompletePending());
        Assert.Equal(35_700_000, estimate.EstimatedAngleMicrodegrees);
        Assert.Equal("step", estimate.Source);
    }

    [Fact]
    public void TimeoutClearsPendingAndCurrentPosition()
    {
        var estimate = CreateEstimatedPosition(DiseqcMotorDirection.East, 36_600_000);
        estimate.BeginGotoAngular(DiseqcMotorDirection.West, 10_000_000);

        estimate.FailVerification();

        Assert.False(estimate.HasEstimate);
        Assert.False(estimate.HasPendingTarget);
        Assert.Equal("verification_failed", estimate.Confidence);
    }

    [Fact]
    public void StepTargetOutsideRangeDoesNotOverflowIntoPendingTarget()
    {
        var estimate = CreateEstimatedPosition(DiseqcMotorDirection.East, 1_000_000);
        estimate.ConfigureStepCalibration(180_000_000, 180_000_000);

        estimate.BeginStep(DiseqcMotorDirection.East, 128);

        Assert.False(estimate.HasPendingTarget);
        Assert.Equal(1_000_000, estimate.EstimatedAngleMicrodegrees);
    }

    private static DiseqcPositionEstimate CreateEstimatedPosition(
        DiseqcMotorDirection direction,
        int magnitudeMicrodegrees)
    {
        var estimate = new DiseqcPositionEstimate();
        estimate.BeginGotoAngular(direction, magnitudeMicrodegrees);
        estimate.CompletePending();
        return estimate;
    }
}