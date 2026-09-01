using System.IdentityModel.Tokens.Jwt;
using LANCommander.Server.Services.HQ;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

public class HqTokenReaderTests
{
    private static string TokenExpiring(DateTime expiresUtc) =>
        new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(expires: expiresUtc));

    [Fact]
    public void ReadsTheExpiryClaim()
    {
        var expected = DateTime.UtcNow.AddDays(30);

        var expiry = HqTokenReader.GetExpiry(TokenExpiring(expected));

        expiry.ShouldNotBeNull();
        // JWT exp has one-second resolution.
        expiry.Value.UtcDateTime.ShouldBe(expected, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ExpiryIsAlwaysUtc()
    {
        // A DateTimeKind of Unspecified silently shifts the comparison by the server's offset,
        // which would make expiry checks wrong by hours.
        var expiry = HqTokenReader.GetExpiry(TokenExpiring(DateTime.UtcNow.AddDays(1)));

        expiry!.Value.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void TokenWithoutAnExpiryClaimHasUnknownExpiry()
    {
        var noExpiry = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken());

        HqTokenReader.GetExpiry(noExpiry).ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-jwt")]
    [InlineData("aaa.bbb.ccc")]
    public void UnreadableTokensYieldNoExpiryRatherThanThrowing(string? token)
    {
        // HQ's token format is not our contract, so anything unparseable means "expiry unknown".
        HqTokenReader.GetExpiry(token).ShouldBeNull();
    }

    [Fact]
    public void ReadsExpiryFromTheTokenShapeHqActuallyIssues()
    {
        // Claim set taken from a real Settings.yml. This is the legacy path: an install upgraded
        // from a version that stored a bare access token with no recorded expiry.
        var handler = new JwtSecurityTokenHandler();
        var expires = new DateTime(2026, 8, 25, 0, 45, 50, DateTimeKind.Utc);

        var token = handler.WriteToken(new JwtSecurityToken(
            claims:
            [
                new("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", "abc123"),
                new("member", "true"),
                new("premium", "true"),
                new("editor", "true"),
            ],
            expires: expires));

        HqTokenReader.GetExpiry(token)!.Value.UtcDateTime.ShouldBe(expires, TimeSpan.FromSeconds(1));
    }
}
