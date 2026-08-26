using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Factories;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;

namespace LANCommander.SDK.Tests.Helpers;

/// <summary>
/// An <see cref="HttpMessageHandler"/> that records every request path+query it is asked for and
/// answers from a list of registered route handlers. Anything unregistered comes back as 404, so
/// a test that asserts "endpoint X was used and endpoint Y was not" fails loudly rather than
/// silently hitting the network.
/// </summary>
internal sealed class RecordingHttpMessageHandler : HttpMessageHandler
{
    private readonly List<(Func<HttpRequestMessage, bool> Match, Func<HttpRequestMessage, HttpResponseMessage> Respond)> _routes = new();
    private readonly ConcurrentQueue<string> _requests = new();

    /// <summary>Every request URI (path + query) in the order it was issued.</summary>
    public IReadOnlyList<string> Requests => _requests.ToArray();

    public RecordingHttpMessageHandler Map(string pathAndQuery, Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        _routes.Add((r => string.Equals(r.RequestUri?.PathAndQuery, pathAndQuery, StringComparison.OrdinalIgnoreCase), respond));

        return this;
    }

    public RecordingHttpMessageHandler MapJson(string pathAndQuery, object payload) =>
        Map(pathAndQuery, _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        });

    public RecordingHttpMessageHandler MapBytes(string pathAndQuery, byte[] payload) =>
        Map(pathAndQuery, _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(payload),
        });

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _requests.Enqueue(request.RequestUri?.PathAndQuery ?? string.Empty);

        var route = _routes.FirstOrDefault(r => r.Match(request));

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
    public FakeSettingsProvider(string serverAddress = "http://localhost:1337")
    {
        CurrentValue = new Settings();
        CurrentValue.Authentication.ServerAddress = new Uri(serverAddress);
    }

    public Settings CurrentValue { get; }

    public void Update(Action<Settings> patch) => patch(CurrentValue);
}

internal static class FakeApi
{
    /// <summary>
    /// A real <see cref="GameClient"/> whose only live dependency is an HTTP stack backed by
    /// <paramref name="handler"/>. Every other collaborator is null: only call paths that are
    /// HTTP + file-system only (ValidateFilesAsync/RestoreFilesAsync/DownloadFilesAsync/
    /// UpdateGameInstallationAsync against manifests with no save paths) are safe on it.
    /// </summary>
    public static GameClient CreateGameClient(RecordingHttpMessageHandler handler, int maxInstallAttempts = 10)
    {
        var settingsProvider = new FakeSettingsProvider();
        settingsProvider.CurrentValue.Games.MaxInstallAttempts = maxInstallAttempts;

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

    /// <summary>Builds a small, real zip archive so extraction paths run for real in tests.</summary>
    public static byte[] CreateZip(params (string Name, string Content)[] files)
    {
        using var buffer = new MemoryStream();

        using (var zip = new System.IO.Compression.ZipArchive(buffer, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in files)
            {
                var entry = zip.CreateEntry(name);

                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream);

                writer.Write(content);
            }
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// A real <see cref="ToolClient"/> whose only live dependency is an HTTP stack backed by
    /// <paramref name="handler"/>. Safe for the download/extract plan-item path, which never
    /// touches the script client.
    /// </summary>
    public static ToolClient CreateToolClient(RecordingHttpMessageHandler handler)
    {
        var settingsProvider = new FakeSettingsProvider();
        var httpClient = new HttpClient(handler);
        var factory = new ApiRequestFactory(httpClient, new FakeTokenProvider(), settingsProvider);

        return new ToolClient(
            logger: null!,
            settingsProvider: settingsProvider,
            apiRequestFactory: factory,
            scriptClient: null!);
    }
}
