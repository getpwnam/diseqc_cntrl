using CubleyControl;
using Xunit;

namespace DiSEqC_Control.Tests;

public sealed class RestConnectionPolicyTests
{
    [Fact]
    public void Http11KeepsAliveByDefault()
    {
        Assert.True(RestConnectionPolicy.ShouldKeepAlive(
            "GET /api/v2/health HTTP/1.1\r\nHost: cubley\r\n\r\n"));
    }

    [Fact]
    public void ConnectionCloseDisablesKeepAlive()
    {
        Assert.False(RestConnectionPolicy.ShouldKeepAlive(
            "GET /api/v2/health HTTP/1.1\r\nConnection: keep-alive, close\r\n\r\n"));
    }

    [Fact]
    public void Http10DoesNotKeepAlive()
    {
        Assert.False(RestConnectionPolicy.ShouldKeepAlive(
            "GET /api/v2/health HTTP/1.0\r\nConnection: keep-alive\r\n\r\n"));
    }

    [Theory]
    [InlineData("Content-Length: 1")]
    [InlineData("Transfer-Encoding: chunked")]
    public void DetectsRequestBodiesThatWouldDesynchronizeKeepAlive(string header)
    {
        Assert.True(RestConnectionPolicy.HasRequestBody(
            "GET /api/v2/health HTTP/1.1\r\n" + header + "\r\n\r\n"));
    }

    [Fact]
    public void AllowsExplicitEmptyBody()
    {
        Assert.False(RestConnectionPolicy.HasRequestBody(
            "GET /api/v2/health HTTP/1.1\r\nContent-Length: 0\r\n\r\n"));
    }
}
