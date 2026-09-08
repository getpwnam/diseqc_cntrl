using Cubley.Diseqc;
using Xunit;

namespace DiSEqC_Control.Tests;

public sealed class DiseqcGotoAngleEncoderTests
{
    [Theory]
    [InlineData(DiseqcMotorDirection.East, "0", "E0-31-6E-E0-00", 0, "0")]
    [InlineData(DiseqcMotorDirection.East, "28.2", "E0-31-6E-E1-C3", 282, "28.2")]
    [InlineData(DiseqcMotorDirection.West, "28.2", "E0-31-6E-D1-C3", 282, "28.2")]
    [InlineData(DiseqcMotorDirection.East, "36.5625", "E0-31-6E-E2-4A", 366, "36.6")]
    [InlineData(DiseqcMotorDirection.East, "180", "E0-31-6E-EB-40", 1800, "180")]
    public void BuildsStandardGotoXVectors(
        DiseqcMotorDirection direction,
        string requestedDegrees,
        string expectedFrame,
        int expectedTenths,
        string expectedEncodedDegrees)
    {
        bool success = DiseqcGotoAngleEncoder.TryBuildFrame(
            direction,
            requestedDegrees,
            out byte[] frame,
            out int requestedMicrodegrees,
            out int encodedTenths,
            out string error);

        Assert.True(success, error);
        Assert.Equal(expectedFrame, BitConverter.ToString(frame));
        Assert.Equal(expectedTenths, encodedTenths);
        Assert.Equal(expectedEncodedDegrees, DiseqcGotoAngleEncoder.FormatTenths(encodedTenths));
        Assert.Equal(requestedDegrees, DiseqcGotoAngleEncoder.FormatMicrodegrees(requestedMicrodegrees));
    }

    [Theory]
    [InlineData("12.34", 123, "12.3")]
    [InlineData("12.349999", 123, "12.3")]
    [InlineData("12.35", 124, "12.4")]
    [InlineData("12.45", 125, "12.5")]
    public void RoundsToNearestTenthWithHalfUpTies(
        string requestedDegrees,
        int expectedTenths,
        string expectedEncodedDegrees)
    {
        bool success = DiseqcGotoAngleEncoder.TryParseDegrees(
            requestedDegrees,
            out _,
            out int encodedTenths,
            out string error);

        Assert.True(success, error);
        Assert.Equal(expectedTenths, encodedTenths);
        Assert.Equal(expectedEncodedDegrees, DiseqcGotoAngleEncoder.FormatTenths(encodedTenths));
    }

    [Theory]
    [InlineData(null, "angle_empty")]
    [InlineData("", "angle_empty")]
    [InlineData(".5", "angle_format")]
    [InlineData("5.", "angle_format")]
    [InlineData("-1", "angle_format")]
    [InlineData("1.1234567", "angle_precision")]
    [InlineData("180.000001", "angle_out_of_range")]
    public void RejectsMalformedAndOutOfRangeAngles(string requestedDegrees, string expectedError)
    {
        bool success = DiseqcGotoAngleEncoder.TryBuildFrame(
            DiseqcMotorDirection.East,
            requestedDegrees,
            out byte[] frame,
            out _,
            out _,
            out string error);

        Assert.False(success);
        Assert.Empty(frame);
        Assert.Equal(expectedError, error);
    }
}