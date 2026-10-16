using System.Net;
using System.Text.Json;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Exceptions;
using LANCommander.SDK.Factories;
using LANCommander.SDK.Models;
using LANCommander.SDK.Providers;
using LANCommander.SDK.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace LANCommander.SDK.Tests.Helpers;

public class AuthenticationSessionTests
{
    private static readonly Uri ServerAddress = new("https://server.test");

    [Fact]
    public async Task Valid_token_does_not_refresh()
    {
        using var client = CreateClient(request =>
        {
            Assert.Equal("original", request.Headers.Authorization?.Parameter);
            return JsonResponse(new[] { "game" });
        });
        using var fixture = new SessionFixture(client, Token());

        var result = await fixture.Requests.Create().UseAuthenticationToken()
            .UseRoute("/api/Depot").GetAsync<string[]>();

        Assert.Equal("game", Assert.Single(result));
        Assert.Equal("original", fixture.Tokens.GetToken().AccessToken);
    }

    [Fact]
    public async Task Expiring_token_is_refreshed_and_persisted_before_request()
    {
        var refreshes = 0;
        using var client = CreateClient(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/Auth/Refresh")
            {
                Assert.Null(request.Headers.Authorization);
                Interlocked.Increment(ref refreshes);
                return JsonResponse(Token("renewed"));
            }

            Assert.Equal("renewed", request.Headers.Authorization?.Parameter);
            return JsonResponse(new[] { "game" });
        });
        using var fixture = new SessionFixture(client, Token(expiration: DateTime.UtcNow.AddSeconds(30)));

        await fixture.Requests.Create().UseAuthenticationToken().UseRoute("/api/Depot").GetAsync<string[]>();

        Assert.Equal(1, refreshes);
        Assert.Equal("renewed", fixture.Settings.CurrentValue.Authentication.Token.AccessToken);
        Assert.Equal("renewed-refresh", fixture.Settings.CurrentValue.Authentication.Token.RefreshToken);
    }

    [Fact]
    public async Task Concurrent_requests_share_one_refresh()
    {
        var refreshes = 0;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new HttpClient(new StubHandler(async (request, cancellationToken) =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/Auth/Refresh")
            {
                Interlocked.Increment(ref refreshes);
                started.SetResult();
                await release.Task.WaitAsync(cancellationToken);
                return JsonResponse(Token("renewed"));
            }

            Assert.Equal("renewed", request.Headers.Authorization?.Parameter);
            return JsonResponse(Array.Empty<string>());
        }));
        using var fixture = new SessionFixture(client, Token(expiration: DateTime.UtcNow.AddMinutes(-1)));

        var requests = Enumerable.Range(0, 10).Select(_ =>
            fixture.Requests.Create().UseAuthenticationToken().UseRoute("/api/Depot").GetAsync<string[]>()).ToArray();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        release.SetResult();
        await Task.WhenAll(requests);

        Assert.Equal(1, refreshes);
    }

    [Fact]
    public async Task Concurrent_authentication_rejections_reuse_the_rotated_token()
    {
        var refreshes = 0;
        var originalRequests = 0;
        var allStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new HttpClient(new StubHandler(async (request, cancellationToken) =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/Auth/Refresh")
            {
                Interlocked.Increment(ref refreshes);
                return JsonResponse(Token("renewed"));
            }

            if (request.Headers.Authorization?.Parameter == "original")
            {
                if (Interlocked.Increment(ref originalRequests) == 10)
                    allStarted.SetResult();
                await allStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            Assert.Equal("renewed", request.Headers.Authorization?.Parameter);
            return JsonResponse(Array.Empty<string>());
        }));
        using var fixture = new SessionFixture(client, Token());

        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ =>
            fixture.Requests.Create().UseAuthenticationToken().UseRoute("/api/Depot").GetAsync<string[]>()));

        Assert.Equal(1, refreshes);
        Assert.Equal(0, fixture.ExpiredEvents);
    }

    [Theory]
    [InlineData(401, null)]
    [InlineData(302, "/Login?ReturnUrl=%2Fapi%2FDepot")]
    [InlineData(302, "http://server.test/Login?ReturnUrl=%2Fapi%2FDepot")]
    public async Task Authentication_rejection_refreshes_and_retries_once(int status, string? location)
    {
        var attempts = 0;
        using var client = CreateClient(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/Auth/Refresh")
                return JsonResponse(Token("renewed"));

            if (++attempts == 1)
            {
                var response = new HttpResponseMessage((HttpStatusCode)status);
                if (location != null)
                    response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
                return response;
            }

            Assert.Equal("renewed", request.Headers.Authorization?.Parameter);
            return JsonResponse(Array.Empty<string>());
        });
        using var fixture = new SessionFixture(client, Token());

        await fixture.Requests.Create().UseAuthenticationToken().UseRoute("/api/Depot").GetAsync<string[]>();

        Assert.Equal(2, attempts);
        Assert.Equal(0, fixture.ExpiredEvents);
    }

    [Fact]
    public async Task Followed_login_redirect_is_also_an_authentication_failure()
    {
        using var client = CreateClient(request =>
            request.RequestUri!.AbsolutePath == "/api/Auth/Refresh"
                ? new HttpResponseMessage(HttpStatusCode.BadRequest)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://server.test/Login"),
                    Content = new StringContent("<html>Login</html>"),
                });
        using var fixture = new SessionFixture(client, Token());

        await Assert.ThrowsAsync<AuthFailedException>(() =>
            fixture.Requests.Create().UseAuthenticationToken().UseRoute("/api/Depot").GetAsync<string[]>());

        Assert.Equal(1, fixture.ExpiredEvents);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    public async Task Rejected_refresh_clears_credentials_and_notifies_once(int status)
    {
        var calls = 0;
        using var client = CreateClient(_ =>
        {
            calls++;
            return new HttpResponseMessage((HttpStatusCode)status);
        });
        using var fixture = new SessionFixture(client, Token(expiration: DateTime.UtcNow.AddMinutes(-1)));

        for (var i = 0; i < 2; i++)
        {
            var error = await Assert.ThrowsAsync<AuthFailedException>(() =>
                fixture.Requests.Create().UseAuthenticationToken().UseRoute("/api/Depot").GetAsync<string[]>());
            Assert.Equal(AuthFailedException.AuthenticationErrorCode.TokenExpired, error.ErrorCode);
        }

        Assert.Null(fixture.Tokens.GetToken());
        Assert.Equal(1, fixture.ExpiredEvents);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Transient_refresh_failure_preserves_credentials(bool networkError)
    {
        using var client = CreateClient(_ => networkError
            ? throw new HttpRequestException("Server unavailable")
            : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var fixture = new SessionFixture(client, Token(expiration: DateTime.UtcNow.AddMinutes(-1)));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            fixture.Requests.Create().UseAuthenticationToken().UseRoute("/api/Depot").GetAsync<string[]>());

        Assert.Equal("original", fixture.Tokens.GetToken().AccessToken);
        Assert.Equal(0, fixture.ExpiredEvents);
    }

    [Fact]
    public async Task Rejected_renewed_token_does_not_loop()
    {
        var calls = 0;
        using var client = CreateClient(request =>
        {
            calls++;
            return request.RequestUri!.AbsolutePath == "/api/Auth/Refresh"
                ? JsonResponse(Token("renewed"))
                : new HttpResponseMessage(HttpStatusCode.Unauthorized);
        });
        using var fixture = new SessionFixture(client, Token());

        await Assert.ThrowsAsync<AuthFailedException>(() =>
            fixture.Requests.Create().UseAuthenticationToken().UseRoute("/api/Depot").GetAsync<string[]>());

        Assert.Equal(3, calls);
        Assert.Equal(1, fixture.ExpiredEvents);
    }

    [Fact]
    public async Task Expired_token_without_refresh_token_requires_sign_in_without_sending()
    {
        using var client = CreateClient(_ => throw new InvalidOperationException("No HTTP request expected"));
        var token = Token(expiration: DateTime.UtcNow.AddMinutes(-1));
        token.RefreshToken = string.Empty;
        using var fixture = new SessionFixture(client, token);

        await Assert.ThrowsAsync<AuthFailedException>(() =>
            fixture.Requests.Create().UseAuthenticationToken().UseRoute("/api/Depot").GetAsync<string[]>());

        Assert.Null(fixture.Tokens.GetToken());
        Assert.Equal(1, fixture.ExpiredEvents);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"AccessToken\":\"new\",\"RefreshToken\":\"new-refresh\",\"Expiration\":\"2000-01-01T00:00:00Z\"}")]
    public async Task Invalid_renewal_response_does_not_replace_credentials(string body)
    {
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body),
        });
        using var fixture = new SessionFixture(client, Token(expiration: DateTime.UtcNow.AddMinutes(-1)));

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            fixture.Requests.Create().UseAuthenticationToken().UseRoute("/api/Depot").GetAsync<string[]>());

        Assert.Equal("original", fixture.Tokens.GetToken().AccessToken);
        Assert.Equal(0, fixture.ExpiredEvents);
    }

    [Fact]
    public async Task Logout_during_refresh_does_not_restore_the_session()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new HttpClient(new StubHandler(async (_, cancellationToken) =>
        {
            started.SetResult();
            await release.Task.WaitAsync(cancellationToken);
            return JsonResponse(Token("renewed"));
        }));
        using var fixture = new SessionFixture(client, Token(expiration: DateTime.UtcNow.AddMinutes(-1)));

        var pending = fixture.Requests.Create().UseAuthenticationToken().UseRoute("/api/Depot").GetAsync<string[]>();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        fixture.Tokens.SetToken(null!);
        release.SetResult();

        await Assert.ThrowsAsync<OperationCanceledException>(() => pending);
        Assert.Null(fixture.Tokens.GetToken());
        Assert.Equal(0, fixture.ExpiredEvents);
    }

    [Fact]
    public async Task Cancelled_refresh_preserves_credentials()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new HttpClient(new StubHandler(async (_, cancellationToken) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Unreachable");
        }));
        using var fixture = new SessionFixture(client, Token(expiration: DateTime.UtcNow.AddMinutes(-1)));
        using var cancellation = new CancellationTokenSource();

        var pending = fixture.Requests.Create().UseAuthenticationToken().UseRoute("/api/Depot")
            .UseCancellationToken(cancellation.Token).GetAsync<string[]>();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal("original", fixture.Tokens.GetToken().AccessToken);
        Assert.Equal(0, fixture.ExpiredEvents);
    }

    [Fact]
    public async Task Forbidden_does_not_refresh_or_expire_the_session()
    {
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        using var fixture = new SessionFixture(client, Token());

        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            fixture.Requests.Create().UseAuthenticationToken().UseRoute("/api/Depot").GetAsync<string[]>());

        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
        Assert.Equal("original", fixture.Tokens.GetToken().AccessToken);
        Assert.Equal(0, fixture.ExpiredEvents);
    }

    [Fact]
    public async Task Public_requests_do_not_try_to_refresh()
    {
        using var client = CreateClient(request =>
        {
            Assert.Equal("/api/Auth/Login", request.RequestUri!.AbsolutePath);
            Assert.Null(request.Headers.Authorization);
            return JsonResponse(Token());
        });
        using var fixture = new SessionFixture(client, null);

        await fixture.Requests.Create().UseRoute("/api/Auth/Login").AddBody(new { Username = "user" })
            .PostAsync<AuthToken>();

        Assert.Equal(0, fixture.ExpiredEvents);
    }

    [Fact]
    public async Task Retry_preserves_post_body_and_method()
    {
        var attempts = 0;
        using var client = new HttpClient(new StubHandler(async (request, cancellationToken) =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/Auth/Refresh")
                return JsonResponse(Token("renewed"));

            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("{\"Value\":42}", await request.Content!.ReadAsStringAsync(cancellationToken));
            return ++attempts == 1
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : new HttpResponseMessage(HttpStatusCode.OK);
        }));
        using var fixture = new SessionFixture(client, Token());

        await fixture.Requests.Create().UseAuthenticationToken().UseRoute("/api/Profile")
            .AddBody(new { Value = 42 }).PostAsync();

        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task Failed_download_does_not_create_a_zip_file()
    {
        using var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        using var fixture = new SessionFixture(client, Token());
        var destination = Path.Combine(Path.GetTempPath(), $"LANCommander-test-{Guid.NewGuid()}.zip");

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            fixture.Requests.Create().UseAuthenticationToken().UseRoute("/api/Modules/Download")
                .DownloadAsync(destination));

        Assert.False(File.Exists(destination));
    }

    private static AuthToken Token(string accessToken = "original", DateTime? expiration = null) => new()
    {
        AccessToken = accessToken,
        RefreshToken = accessToken + "-refresh",
        Expiration = expiration ?? DateTime.UtcNow.AddHours(1),
    };

    private static HttpResponseMessage JsonResponse<T>(T value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(value)),
    };

    private static HttpClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(new StubHandler((request, _) => Task.FromResult(respond(request))));

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) => respond(request, cancellationToken);
    }

    private sealed class TestSettingsProvider : ISettingsProvider
    {
        public Settings CurrentValue { get; } = new();
        public void Update(Action<Settings> patch) => patch(CurrentValue);
    }

    private sealed class SessionFixture : IDisposable
    {
        public TestSettingsProvider Settings { get; } = new();
        public ITokenProvider Tokens { get; }
        public AuthenticationSession Session { get; }
        public ApiRequestFactory Requests { get; }
        public int ExpiredEvents { get; private set; }

        public SessionFixture(HttpClient client, AuthToken? token)
        {
            Settings.CurrentValue.Authentication.ServerAddress = ServerAddress;
            Tokens = new TokenProvider(Settings);
            Tokens.SetToken(token!);
            Session = new AuthenticationSession(Tokens, Settings, NullLogger<AuthenticationSession>.Instance);
            Session.SessionExpired += (_, _) => ExpiredEvents++;
            Requests = new ApiRequestFactory(client, Tokens, Settings, Session);
        }

        public void Dispose() => Session.Dispose();
    }
}
