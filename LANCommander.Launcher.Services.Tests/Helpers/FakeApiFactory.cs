using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Factories;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;

namespace LANCommander.Launcher.Services.Tests.Helpers;

/// <summary>
/// An <see cref="HttpMessageHandler"/> that records every request path+query and answers from
/// explicitly registered routes. Anything unregistered comes back as 404, so tests that exercise
/// real <see cref="GameClient"/> behavior over HTTP fail loudly instead of silently escaping to
/// the network — and can assert exactly which endpoints were (and were not) used.
/// </summary>
internal sealed class RecordingHttpMessageHandler : HttpMessageHandler
{
    private readonly List<(string PathAndQuery, Func<HttpRequestMessage, HttpResponseMessage> Respond)> _routes = new();
    private readonly ConcurrentQueue<string> _requests = new();

    public IReadOnlyList<string> Requests => _requests.ToArray();

    /// <summary>
    /// Registers (or replaces) the response for an exact path+query. Re-mapping a route overrides
    /// the previous registration so a test can override a shared fixture's default.
    /// </summary>
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

    public RecordingHttpMessageHandler MapStatus(string pathAndQuery, HttpStatusCode statusCode) =>
        Map(pathAndQuery, _ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(string.Empty),
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
        CurrentValue.Games.InstallDirectories = [installDirectory ?? Path.Combine(Path.GetTempPath(), "lc-tests-games")];
    }

    public SDK.Models.Settings CurrentValue { get; }

    public void Update(Action<SDK.Models.Settings> patch) => patch(CurrentValue);
}

/// <summary>
/// Builds a real <see cref="GameClient"/> whose only live dependency is an HTTP stack backed by a
/// <see cref="RecordingHttpMessageHandler"/>. Used by the execution-level install/modify tests
/// that need the client to genuinely talk (and genuinely fail) over HTTP rather than being
/// stubbed out — <see cref="GameClient"/> exposes only non-virtual methods, so it cannot be
/// mocked. Every other collaborator is null: only HTTP + file-system call paths are safe on it.
/// </summary>
internal static class FakeApiFactory
{
    public static GameClient CreateGameClient(RecordingHttpMessageHandler handler, string? installDirectory = null, int? maxInstallAttempts = null)
    {
        var settingsProvider = new FakeSettingsProvider(installDirectory);

        if (maxInstallAttempts.HasValue)
            settingsProvider.CurrentValue.Games.MaxInstallAttempts = maxInstallAttempts.Value;

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
            scriptClient: CreateScriptClient(settingsProvider),
            profileClient: null!,
            lobbyClient: null!,
            toolClient: null!);
    }

    /// <summary>
    /// A real <see cref="ScriptClient"/> that is safe as long as no script files exist on disk:
    /// every Run*ScriptAsync method short-circuits on <c>File.Exists</c> before it touches the
    /// service provider, PowerShell factory, or connection client, and swallows anything else.
    /// Needed because the uninstall path calls into it unconditionally, so a null would turn a
    /// genuine add-on removal into a NullReferenceException the caller silently logs.
    /// </summary>
    private static ScriptClient CreateScriptClient(FakeSettingsProvider settingsProvider) =>
        new(
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ScriptClient>.Instance,
            serviceProvider: null!,
            settingsProvider: settingsProvider,
            powerShellScriptFactory: null!,
            connectionClient: null!);

    public static string GameRoute(Guid gameId) => $"/api/Games/{gameId}";

    public static string ResolveArchiveRoute(Guid gameId) => $"/api/Games/{gameId}/Archives/Resolve";

    public static string ResolveArchiveRoute(Guid gameId, Guid archiveId) => $"/api/Games/{gameId}/Archives/Resolve?archiveId={archiveId}";

    public static string ManifestRoute(Guid gameId, Guid archiveId) => $"/api/Games/{gameId}/Manifest?archiveId={archiveId}";

    public static string ManifestRoute(Guid gameId) => $"/api/Games/{gameId}/Manifest";

    public static string ScriptsRoute(Guid gameId) => $"/api/Games/{gameId}/Scripts";

    /// <summary>
    /// The archive-contents listing that <c>ValidateFilesAsync</c>/<c>RestoreFilesAsync</c> need in
    /// order to repair base-game files. Keyed by the installed manifest's version, so it 404s once
    /// an administrator deletes the archive an installation is pinned to.
    /// </summary>
    public static string ArchiveContentsRoute(Guid gameId, string version) => $"/api/Archives/Contents/{gameId}/{version}";

    public static string DownloadRoute(Guid gameId) => $"/api/Games/{gameId}/Download";

    public static string DownloadRoute(Guid gameId, Guid archiveId) => $"/api/Games/{gameId}/Download?archiveId={archiveId}";

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
}
