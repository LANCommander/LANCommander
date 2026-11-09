using System.Net;
using System.Text.Json;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Factories;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using LANCommander.SDK.Providers;
using LANCommander.SDK.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace LANCommander.SDK.Tests.Helpers;

public class LauncherEventTests
{
    [Theory]
    [InlineData(HttpStatusCode.NoContent)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task ReportingUsesAuthenticatedBoundedRequestsAndToleratesOlderOrUnavailableServers(HttpStatusCode status)
    {
        var targetId = Guid.NewGuid();
        var requests = new List<(HttpMethod Method, string Path, string? Token, bool Cancelable, string Body)>();
        using var client = new HttpClient(new StubHandler(async (request, cancellationToken) =>
        {
            requests.Add((request.Method, request.RequestUri!.AbsolutePath, request.Headers.Authorization?.Parameter,
                cancellationToken.CanBeCanceled, await request.Content!.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(status);
        }));
        using var fixture = new Fixture(client);

        await LauncherEventHelper.ReportAsync(fixture.Requests, NullLogger.Instance, LauncherEventType.GameInstalled, targetId);

        var sent = Assert.Single(requests);
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal("/api/Events/Launcher", sent.Path);
        Assert.Equal("access-token", sent.Token);
        Assert.True(sent.Cancelable);
        using var body = JsonDocument.Parse(sent.Body);
        Assert.Equal((int)LauncherEventType.GameInstalled, body.RootElement.GetProperty("Type").GetInt32());
        Assert.Equal(targetId, body.RootElement.GetProperty("TargetId").GetGuid());
        Assert.Equal(2, body.RootElement.EnumerateObject().Count());
    }

    [Fact]
    public async Task DirectInstallReportsFailureWithoutReplacingTheOriginalException()
    {
        var targetId = Guid.NewGuid();
        var reports = new List<LauncherEventReport>();
        using var client = new HttpClient(new StubHandler(async (request, cancellationToken) =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/Events/Launcher")
                reports.Add(JsonSerializer.Deserialize<LauncherEventReport>(await request.Content!.ReadAsStringAsync(cancellationToken))!);
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        }));
        using var fixture = new Fixture(client);

        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            fixture.GameClient.InstallAsync(targetId, "unused-install-directory"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
        var report = Assert.Single(reports);
        Assert.Equal(LauncherEventType.GameInstallFailed, report.Type);
        Assert.Equal(targetId, report.TargetId);
    }

    [Fact]
    public async Task CanceledDirectInstallDoesNotReportFailure()
    {
        var requests = 0;
        using var client = new HttpClient(new StubHandler((_, _) =>
        {
            requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
        }));
        using var fixture = new Fixture(client);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.GameClient.InstallAsync(Guid.NewGuid(), cancellationToken: cancellation.Token));

        Assert.Equal(0, requests);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly AuthenticationSession _session;
        public ApiRequestFactory Requests { get; }
        public GameClient GameClient { get; }

        public Fixture(HttpClient client)
        {
            var settings = new TestSettingsProvider();
            settings.CurrentValue.Authentication.ServerAddress = new Uri("https://server.test");
            var tokens = new TokenProvider(settings);
            tokens.SetToken(new AuthToken { AccessToken = "access-token", Expiration = DateTime.UtcNow.AddHours(1) });
            _session = new AuthenticationSession(tokens, settings, NullLogger<AuthenticationSession>.Instance);
            Requests = new ApiRequestFactory(client, tokens, settings, _session);
            GameClient = new GameClient(NullLogger<GameClient>.Instance, Requests, null!, null!, settings,
                null!, null!, null!, null!, null!, null!, null!, []);
        }

        public void Dispose() => _session.Dispose();
    }

    private sealed class TestSettingsProvider : ISettingsProvider
    {
        public Settings CurrentValue { get; } = new();
        public void Update(Action<Settings> patch) => patch(CurrentValue);
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            respond(request, cancellationToken);
    }
}
