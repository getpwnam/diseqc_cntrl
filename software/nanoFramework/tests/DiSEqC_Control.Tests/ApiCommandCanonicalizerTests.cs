using CubleyControl;
using Xunit;

namespace DiSEqC_Control.Tests;

public sealed class ApiCommandCanonicalizerTests
{
    [Fact]
    public void NormalizesMemberOrderWhitespaceAndDefaultVersion()
    {
        Assert.True(Json.TryParseObject(
            "{\"id\":\"same\",\"op\":\"positioner.step\",\"direction\":\"east\",\"count\":2}",
            out JsonObject first,
            out _));
        Assert.True(Json.TryParseObject(
            "{ \"count\" : 2, \"v\":2, \"direction\":\"east\", \"op\":\"positioner.step\", \"id\":\"same\" }",
            out JsonObject second,
            out _));

        Assert.Equal(
            ApiCommandCanonicalizer.Build(first, 2),
            ApiCommandCanonicalizer.Build(second, 2));
    }

    [Fact]
    public void PreservesScalarTypeAndValueDifferences()
    {
        Assert.True(Json.TryParseObject("{\"id\":\"same\",\"op\":\"test\",\"value\":1}", out JsonObject number, out _));
        Assert.True(Json.TryParseObject("{\"id\":\"same\",\"op\":\"test\",\"value\":\"1\"}", out JsonObject text, out _));

        Assert.NotEqual(
            ApiCommandCanonicalizer.Build(number, 2),
            ApiCommandCanonicalizer.Build(text, 2));
    }

    [Fact]
    public void UsesOrdinalMemberOrdering()
    {
        Assert.True(Json.TryParseObject(
            "{\"id\":\"same\",\"op\":\"test\",\"a\":1,\"a\\u0000\":2}",
            out JsonObject first,
            out _));
        Assert.True(Json.TryParseObject(
            "{\"a\\u0000\":2,\"a\":1,\"op\":\"test\",\"id\":\"same\"}",
            out JsonObject second,
            out _));

        Assert.Equal(
            ApiCommandCanonicalizer.Build(first, 2),
            ApiCommandCanonicalizer.Build(second, 2));
    }
}
