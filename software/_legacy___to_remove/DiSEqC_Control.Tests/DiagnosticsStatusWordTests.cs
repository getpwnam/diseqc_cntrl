namespace DiSEqC_Control.Tests;

public class DiagnosticsStatusWordTests
{
    [Fact]
    public void Compose_PacksMagicStageResultAndDetail()
    {
        uint word = DiagnosticsStatusWord.Compose(0xF0, DiagnosticsStatusWord.ResultPass, 0x07);

        Assert.Equal(0xD5F00107u, word);
    }

    [Theory]
    [InlineData(0, 0, DiagnosticsStatusWord.ResultPass)]
    [InlineData(0, 1, DiagnosticsStatusWord.ResultWarn)]
    [InlineData(1, 0, DiagnosticsStatusWord.ResultFail)]
    [InlineData(2, 3, DiagnosticsStatusWord.ResultFail)]
    public void ComputeAggregateResult_ReflectsComponentOutcomes(byte failures, byte skipped, byte expected)
    {
        byte result = DiagnosticsStatusWord.ComputeAggregateResult(failures, skipped);

        Assert.Equal(expected, result);
    }
}
