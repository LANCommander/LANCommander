using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Services.Tests.Tests;

public class AuthenticationServiceTests
{
    private static AuthenticationService CreateSubject(ITokenProvider tokenProvider)
    {
        // The methods exercised here (DecodeToken / GetUserId / GetCurrentUserName /
        // HasStoredCredentials) only touch tokenProvider. The remaining ctor args are
        // never dereferenced so we pass null! rather than build a mock graph for each.
        return new AuthenticationService(
            tokenProvider,
            settingsProvider: null!,
            scopeFactory: null!,
            connectionClient: null!,
            authenticationClient: null!,
            logger: NullLogger<AuthenticationService>.Instance);
    }

    private static string IssueJwt(Guid userId, string userName)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name,            userName),
        };

        var token = new JwtSecurityToken(
            issuer: "test-issuer",
            audience: "test-audience",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public void HasStoredCredentials_returns_false_when_token_is_empty()
    {
        var provider = new Mock<ITokenProvider>();
        provider.Setup(p => p.GetToken()).Returns(new AuthToken { AccessToken = string.Empty });

        CreateSubject(provider.Object).HasStoredCredentials().ShouldBeFalse();
    }

    [Fact]
    public void HasStoredCredentials_returns_false_when_token_is_garbage()
    {
        var provider = new Mock<ITokenProvider>();
        provider.Setup(p => p.GetToken())
                .Returns(new AuthToken { AccessToken = "not.a.real.jwt" });

        CreateSubject(provider.Object).HasStoredCredentials().ShouldBeFalse();
    }

    [Fact]
    public void HasStoredCredentials_returns_true_for_well_formed_jwt()
    {
        var jwt = IssueJwt(Guid.NewGuid(), "alice");
        var provider = new Mock<ITokenProvider>();
        provider.Setup(p => p.GetToken()).Returns(new AuthToken { AccessToken = jwt });

        CreateSubject(provider.Object).HasStoredCredentials().ShouldBeTrue();
    }

    [Fact]
    public void GetUserId_extracts_NameIdentifier_claim()
    {
        var expectedId = Guid.NewGuid();
        var jwt = IssueJwt(expectedId, "bob");
        var provider = new Mock<ITokenProvider>();
        provider.Setup(p => p.GetToken()).Returns(new AuthToken { AccessToken = jwt });

        CreateSubject(provider.Object).GetUserId().ShouldBe(expectedId);
    }

    [Fact]
    public void GetUserId_returns_Empty_when_no_token()
    {
        var provider = new Mock<ITokenProvider>();
        provider.Setup(p => p.GetToken()).Returns(new AuthToken { AccessToken = string.Empty });

        CreateSubject(provider.Object).GetUserId().ShouldBe(Guid.Empty);
    }

    [Fact]
    public void GetCurrentUserName_extracts_Name_claim()
    {
        var jwt = IssueJwt(Guid.NewGuid(), "carol");
        var provider = new Mock<ITokenProvider>();
        provider.Setup(p => p.GetToken()).Returns(new AuthToken { AccessToken = jwt });

        CreateSubject(provider.Object).GetCurrentUserName().ShouldBe("carol");
    }

    [Fact]
    public void GetCurrentUserName_returns_empty_when_no_token()
    {
        var provider = new Mock<ITokenProvider>();
        provider.Setup(p => p.GetToken()).Returns(new AuthToken { AccessToken = string.Empty });

        CreateSubject(provider.Object).GetCurrentUserName().ShouldBe(string.Empty);
    }
}
