using CubleyControl;
using Xunit;

namespace DiSEqC_Control.Tests;

public sealed class ApiTokenAuthenticationTests
{
    private const string Token = "0123456789abcdef0123456789ABCDEF";

    [Fact]
    public void ReadsBearerTokenCaseInsensitively()
    {
        string headers = "GET /api/v2/health HTTP/1.1\r\nhost: cubley\r\naUtHoRiZaTiOn: bEaReR " + Token + "\r\n\r\n";

        Assert.True(ApiTokenAuthentication.TryReadBearerToken(headers, out string actual));
        Assert.Equal(Token, actual);
    }

    [Fact]
    public void ReadsBearerTokenWithTabWhitespace()
    {
        string headers = "GET /api/v2/health HTTP/1.1\r\nAuthorization:\tBearer\t" + Token + "\t\r\n\r\n";

        Assert.True(ApiTokenAuthentication.TryReadBearerToken(headers, out string actual));
        Assert.Equal(Token, actual);
    }

    [Theory]
    [InlineData("GET / HTTP/1.1\r\nHost: cubley\r\n\r\n")]
    [InlineData("GET / HTTP/1.1\r\nAuthorization: Basic abc\r\n\r\n")]
    [InlineData("GET / HTTP/1.1\r\nAuthorization: Bearer short\r\n\r\n")]
    [InlineData("GET / HTTP/1.1\r\nAuthorization: Bearer 0123456789abcdef0123456789ABCDE!\r\n\r\n")]
    [InlineData("GET / HTTP/1.1\r\nAuthorization: Bearer 0123456789abcdef0123456789ABCDEF\r\nAuthorization: Bearer 0123456789abcdef0123456789ABCDEF\r\n\r\n")]
    public void RejectsMissingOrMalformedAuthorization(string headers)
    {
        Assert.False(ApiTokenAuthentication.TryReadBearerToken(headers, out _));
    }

    [Fact]
    public void ComparesTokensExactly()
    {
        Assert.True(ApiTokenAuthentication.FixedTimeEquals(Token, Token));
        Assert.False(ApiTokenAuthentication.FixedTimeEquals(Token, Token.ToLowerInvariant()));
        Assert.False(ApiTokenAuthentication.FixedTimeEquals(Token, Token + "A"));
        Assert.False(ApiTokenAuthentication.FixedTimeEquals(Token, null));
    }

    [Fact]
    public void AuthorizationRequiresConfiguredMatchingToken()
    {
        string validHeaders = "GET /api/v2/health HTTP/1.1\r\nAuthorization: Bearer " + Token + "\r\n\r\n";
        string wrongHeaders = "GET /api/v2/health HTTP/1.1\r\nAuthorization: Bearer ABCDEFGHIJKLMNOPQRSTUVWXYZabcdef\r\n\r\n";

        Assert.False(ApiTokenAuthentication.IsAuthorized(validHeaders, string.Empty));
        Assert.False(ApiTokenAuthentication.IsAuthorized(wrongHeaders, Token));
        Assert.True(ApiTokenAuthentication.IsAuthorized(validHeaders, Token));
    }
}