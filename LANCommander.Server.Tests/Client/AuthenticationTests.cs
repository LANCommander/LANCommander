using LANCommander.SDK.Extensions;
using LANCommander.SDK.Models;
using System.Net;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Shouldly;

namespace LANCommander.Server.Tests.Client;

[Collection("Application")]
public class AuthenticationTests : IClassFixture<ApplicationFixture>
{
    private readonly ApplicationFixture _fixture;

    public AuthenticationTests(ApplicationFixture fixture)
    {
        _fixture = ApplicationFixture.Instance;
    }

    // Quarantined: depends on the removed monolithic SDK.Client facade (PingAsync).
    // Needs rewiring to the per-domain DI clients introduced in commit 1936f505.
    [Fact(Skip = "Pending migration to per-domain SDK clients (monolithic SDK.Client removed)")]
    public async Task PingShouldWork()
    {
        // ConnectionClient.PingAsync uses a static HttpClient that does real network I/O and so
        // cannot reach the in-memory test server. Exercise the server's PingMiddleware directly
        // through the in-memory handler instead, asserting the same X-Ping/X-Pong contract.
        var pingId = Guid.NewGuid().ToString();

        var request = new HttpRequestMessage(HttpMethod.Head, _fixture.ServerAddress);
        request.Headers.Add("X-Ping", pingId);

        var response = await _fixture.HttpClient.SendAsync(request);

        response.IsSuccessStatusCode.ShouldBeTrue();
        response.Headers.Contains("X-Pong").ShouldBeTrue();
        response.Headers.GetValues("X-Pong").First().ShouldBe(pingId.FastReverse());
    }

    [Fact]
    public async Task InvalidRefreshTokenReturnsUnauthorizedRatherThanSerializingAnException()
    {
        using var response = await _fixture.HttpClient.PostAsJsonAsync("/api/Auth/Refresh", new AuthToken
        {
            AccessToken = "invalid-token",
            RefreshToken = "invalid-refresh",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExpiredAccessTokenCanBeRenewedAndRefreshTokenIsRotated()
    {
        await using var scope = _fixture.ServiceProvider.CreateAsyncScope();
        var userService = scope.ServiceProvider.GetRequiredService<LANCommander.Server.Services.UserService>();
        var user = await userService.AddAsync(new LANCommander.Server.Data.Models.User
        {
            UserName = $"refresh-test-{Guid.NewGuid():N}",
            Approved = true,
        });
        var authentication = scope.ServiceProvider.GetRequiredService<LANCommander.Server.Services.AuthenticationService>();
        var token = await authentication.LoginAsync(user.UserName);
        var settings = _fixture.ServiceProvider
            .GetRequiredService<SettingsProvider<Server.Settings.Settings>>();
        var handler = new JwtSecurityTokenHandler();
        var expired = new JwtSecurityToken(
            claims: handler.ReadJwtToken(token.AccessToken).Claims,
            expires: DateTime.UtcNow.AddMinutes(-10),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.CurrentValue.Server.Authentication.TokenSecret)),
                SecurityAlgorithms.HmacSha256));
        token.AccessToken = handler.WriteToken(expired);

        using var refreshResponse = await _fixture.HttpClient.PostAsJsonAsync("/api/Auth/Refresh", token);
        refreshResponse.EnsureSuccessStatusCode();
        var renewed = (await refreshResponse.Content.ReadFromJsonAsync<AuthToken>())!;
        renewed.RefreshToken.ShouldNotBe(token.RefreshToken);
        renewed.Expiration.ShouldBeGreaterThan(DateTime.UtcNow);

        using var validate = new HttpRequestMessage(HttpMethod.Post, "/api/Auth/Validate");
        validate.Headers.Authorization = new AuthenticationHeaderValue("Bearer", renewed.AccessToken);
        using var validated = await _fixture.HttpClient.SendAsync(validate);
        validated.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var reused = await _fixture.HttpClient.PostAsJsonAsync("/api/Auth/Refresh", token);
        reused.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}