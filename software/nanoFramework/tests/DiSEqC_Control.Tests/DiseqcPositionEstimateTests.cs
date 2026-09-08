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