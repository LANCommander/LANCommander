using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Net;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Exceptions;
using LANCommander.SDK.Factories;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;
using LANCommander.Launcher.Services.Tests.Helpers;
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

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, false)]
    public async Task Login_does_not_report_success_when_token_validation_fails(
        HttpStatusCode responseStatus, bool expired)
    {
        var serverAddress = new Uri("https://server.test");
        var settings = TestSettingsProvider.Create();
        settings.CurrentValue.Authentication.ServerAddress = serverAddress;
        var token = new AuthToken
        {
            AccessToken = IssueJwt(Guid.NewGuid(), "alice"),
            RefreshToken = "refresh-token",
            Expiration = expired ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddHours(1),
        };
        var tokens = new Mock<ITokenProvider>();
        tokens.Setup(p => p.GetToken()).Returns(() => settings.CurrentValue.Authentication.Token);
        tokens.Setup(p => p.SetToken(It.IsAny<AuthToken>()))
            .Callback<AuthToken>(value => settings.CurrentValue.Authentication.Token = value);
        var connection = new Mock<IConnectionClient>();
        connection.Setup(c => c.UpdateServerAddressAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        connection.Setup(c => c.IsConnected()).Returns(true);
        using var client = new HttpClient(new FailureHandler(responseStatus));
        using var session = new AuthenticationSession(
            tokens.Object, settings, NullLogger<AuthenticationSession>.Instance);
        var requests = new ApiRequestFactory(client, tokens.Object, settings, session);
        var authentication = new AuthenticationClient(
            NullLogger<AuthenticationClient>.Instance, tokens.Object, null!, settings,
            requests, connection.Object, null!,
            new LANCommander.SDK.Plugins.PluginEventBus(NullLogger<LANCommander.SDK.Plugins.PluginEventBus>.Instance));
        var subject = new AuthenticationService(
            tokens.Object, settings, null!, connection.Object, authentication,
            NullLogger<AuthenticationService>.Instance);

        var error = await Should.ThrowAsync<AuthFailedException>(() => subject.Login(serverAddress, token));

        if (expired)
            error.ErrorCode.ShouldBe(AuthFailedException.AuthenticationErrorCode.TokenExpired);
        else
            settings.CurrentValue.Authentication.Token.ShouldBe(token);
    }

    private sealed class FailureHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode));
    }
}
