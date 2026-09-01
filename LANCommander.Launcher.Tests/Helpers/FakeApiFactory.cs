using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Factories;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;

namespace LANCommander.Launcher.Tests.Helpers;

/// <summary>
/// An <see cref="HttpMessageHandler"/> that records the request path+query of every call and
/// answers from explicitly registered routes (404 for anything else), so view-model tests can
/// drive a real <see cref="GameClient"/>/<see cref="Services.InstallService"/> graph without
/// touching the network and still assert what the launcher actually asked the server for.
/// </summary>
internal sealed class RecordingHttpMessageHandler : HttpMessageHandler
{
    private readonly List<(string PathAndQuery, Func<HttpRequestMessage, HttpResponseMessage> Respond)> _routes = new();
    private readonly ConcurrentQueue<string> _requests = new();

    public IReadOnlyList<string> Requests => _requests.ToArray();

    public RecordingHttpMessageHandler Map(string pathAndQuery, Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        _routes.RemoveAll(r => string.Equals(r.PathAndQuery, pathAndQuery, StringComparison.OrdinalIgnoreCase));
        _routes.Add((pathAndQuery, respond));

        return this;
    }

    public RecordingHttpMessageHandler MapJson(string pathAndQuery, object payload) =>
        Map(pathAndQuery, _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        });

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var pathAndQuery = request.RequestUri?.PathAndQuery ?? string.Empty;

        _requests.Enqueue(pathAndQuery);

        var route = _routes.FirstOrDefault(r => string.Equals(r.PathAndQuery, pathAndQuery, StringComparison.OrdinalIgnoreCase));

        var response = route.Respond?.Invoke(request)
            ?? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent(string.Empty) };

        response.RequestMessage = request;

        return Task.FromResult(response);
    }
}

internal sealed class FakeTokenProvider : ITokenProvider
{
    private AuthToken _token = new() { AccessToken = "test-token", RefreshToken = "test-refresh", Expiration = DateTime.UtcNow.AddHours(1) };

    public AuthToken GetToken() => _token;

    public void SetToken(AuthToken token) => _token = token;
}

internal sealed class FakeSettingsProvider : ISettingsProvider
{
    public FakeSettingsProvider(string? installDirectory = null)
    {
        CurrentValue = new SDK.Models.Settings();
        CurrentValue.Authentication.ServerAddress = new Uri("http://localhost:1337");
        CurrentValue.Games.InstallDirectories = [installDirectory ?? Path.Combine(Path.GetTempPath(), "lc-launcher-tests-games")];
    }

    public SDK.Models.Settings CurrentValue { get; }

    public void Update(Action<SDK.Models.Settings> patch) => patch(CurrentValue);
}

internal static class FakeApiFactory
{
    /// <summary>
    /// A real <see cref="GameClient"/> whose only live dependency is an HTTP stack backed by
    /// <paramref name="handler"/>. Every other collaborator is null, so only HTTP + file-system
    /// call paths are safe on it.
    /// </summary>
    public static GameClient CreateGameClient(RecordingHttpMessageHandler handler, ISettingsProvider settingsProvider)
    {
        var httpClient = new HttpClient(handler);
        var factory = new ApiRequestFactory(httpClient, new FakeTokenProvider(), settingsProvider);

        return new GameClient(
            logger: null!,
            apiRequestFactory: factory,
            processExecutionContextFactory: null!,
            networkInformationProvider: null!,
            settingsProvider: settingsProvider,
            connectionClient: null!,
            redistributableClient: null!,
            saveClient: null!,
            scriptClient: null!,
            profileClient: null!,
            lobbyClient: null!,
            toolClient: null!);
    }

    public static string GameRoute(Guid gameId) => $"/api/Games/{gameId}";

    public static string ResolveArchiveRoute(Guid gameId) => $"/api/Games/{gameId}/Archives/Resolve";

    public static string ResolveArchiveRoute(Guid gameId, Guid archiveId) => $"/api/Games/{gameId}/Archives/Resolve?archiveId={archiveId}";
}
