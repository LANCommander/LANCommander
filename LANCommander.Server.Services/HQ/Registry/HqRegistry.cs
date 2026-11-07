using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using LANCommander.HQ.SDK.Authentication;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services.HQ.Registry;

/// <summary>
/// The package registry LANCommander HQ hosts at <c>/nuget/v3/index.json</c>: a NuGet v3 feed that
/// only signed-in HQ users can read. Plugins on it are ordinary packages, read through the plugin
/// feed client with <see cref="CreateHandler"/>; the rest are LCX files, which NuGet's client can't
/// download, so they're found and fetched here.
/// </summary>
public sealed class HqRegistry : IDisposable
{
    public const string SourceName = "LANCommander HQ";

    /// <summary>LCX packages carry <c>LANCommander.LCX</c> plus <c>LANCommander.LCX.{Kind}</c>.</summary>
    public const string RedistributablePackageType = "LANCommander.LCX.Redistributable";

    private const int SearchPageSize = 100;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ITokenProvider _tokens;
    private readonly HqConnectionService _connection;
    private readonly SettingsProvider<Settings.Settings> _settingsProvider;
    private readonly ILogger<HqRegistry> _logger;
    private readonly HttpClient _http;

    private readonly SemaphoreSlim _indexGate = new(1, 1);
    private (string FeedUrl, ServiceEndpoints Endpoints)? _endpoints;

    public HqRegistry(
        RefreshingTokenProvider tokens,
        HqConnectionService connection,
        SettingsProvider<Settings.Settings> settingsProvider,
        ILogger<HqRegistry> logger)
        : this((ITokenProvider)tokens, connection, settingsProvider, logger, null)
    {
    }

    internal HqRegistry(
        ITokenProvider tokens,
        HqConnectionService connection,
        SettingsProvider<Settings.Settings> settingsProvider,
        ILogger<HqRegistry> logger,
        HttpMessageHandler? innerHandler)
    {
        _tokens = tokens;
        _connection = connection;
        _settingsProvider = settingsProvider;
        _logger = logger;

        var handler = CreateHandler();

        // Lives for the process, so connections are recycled to notice DNS changes
        handler.InnerHandler = innerHandler ?? new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };

        // Downloads can run for minutes, so each call sets its own limit instead
        _http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }

    /// <summary>The registry's service index, on the HQ this server is set to use.</summary>
    public string FeedUrl => $"{_settingsProvider.CurrentValue.Server.HQ.BaseUrl.Trim().TrimEnd('/')}/nuget/v3/index.json";

    /// <summary>
    /// Whether the registry can be read: the server has an HQ session that hasn't been refused. Like
    /// the rest of HQ this is optimistic while HQ is merely unreachable.
    /// </summary>
    public bool IsAvailable => _connection.IsUsable;

    /// <summary>A handler that signs requests with this server's HQ session. Each call returns a new one.</summary>
    public DelegatingHandler CreateHandler() => new HqRegistryAuthenticationHandler(_tokens, _connection);

    /// <summary>
    /// Searches the registry for packages of <paramref name="packageType"/>, newest version of each,
    /// most downloaded first. An empty term lists them all.
    /// </summary>
    public async Task<IReadOnlyList<HqRegistryPackage>> SearchAsync(
        string? term,
        string packageType,
        bool includePrerelease = false,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);

        var endpoints = await GetEndpointsAsync(timeout.Token);

        var query = $"{endpoints.Search}?q={Uri.EscapeDataString(term?.Trim() ?? string.Empty)}&skip=0&take={SearchPageSize}"
            + $"&prerelease={(includePrerelease ? "true" : "false")}&semVerLevel=2.0.0&packageType={Uri.EscapeDataString(packageType)}";

        var response = await GetJsonAsync<SearchResponse>(query, timeout.Token);

        return (response?.Data ?? [])
            // The registry filters by package type itself; this guards against one that doesn't
            .Where(r => r.PackageTypes.Count == 0 || r.PackageTypes.Any(t => string.Equals(t.Name, packageType, StringComparison.OrdinalIgnoreCase)))
            .Select(r => new HqRegistryPackage(
                r.Id,
                r.Version,
                string.IsNullOrWhiteSpace(r.Title) ? r.Id : r.Title,
                r.Description,
                r.Authors.Count == 0 ? null : string.Join(", ", r.Authors),
                r.IconUrl,
                r.ProjectUrl,
                r.TotalDownloads,
                r.ManifestId,
                r.Versions.Select(v => v.Version).Reverse().ToList()))
            .ToList();
    }

    /// <summary>
    /// Copies an LCX package from the registry into <paramref name="destination"/>, reporting the
    /// bytes copied so far.
    /// </summary>
    public async Task DownloadLcxAsync(
        string id,
        string version,
        Stream destination,
        IProgress<long>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var endpoints = await GetEndpointsAsync(cancellationToken);

        var lowerId = id.ToLowerInvariant();
        // Search returns versions as the registry stores them, which is how the flat container names them
        var lowerVersion = version.Trim().ToLowerInvariant();
        var url = $"{endpoints.PackageBase}{Uri.EscapeDataString(lowerId)}/{Uri.EscapeDataString(lowerVersion)}/{Uri.EscapeDataString($"{lowerId}.{lowerVersion}.lcx")}";

        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new HqRegistryException($"{id} {version} was not found on {SourceName}.");

        EnsureSuccess(response);

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);

        var buffer = new byte[81920];
        long copied = 0;
        int read;

        while ((read = await body.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);

            copied += read;
            progress?.Report(copied);
        }
    }

    /// <summary>Where the search and download services are, read once from the service index.</summary>
    private async Task<ServiceEndpoints> GetEndpointsAsync(CancellationToken cancellationToken)
    {
        var feedUrl = FeedUrl;

        if (_endpoints is { } cached && cached.FeedUrl == feedUrl)
            return cached.Endpoints;

        await _indexGate.WaitAsync(cancellationToken);

        try
        {
            if (_endpoints is { } raced && raced.FeedUrl == feedUrl)
                return raced.Endpoints;

            var index = await GetJsonAsync<ServiceIndex>(feedUrl, cancellationToken)
                ?? throw new HqRegistryException($"{SourceName} did not return a package registry index.");

            string Find(string type) =>
                index.Resources.FirstOrDefault(r => r.Type.Equals(type, StringComparison.OrdinalIgnoreCase))?.Id
                ?? index.Resources.FirstOrDefault(r => r.Type.StartsWith(type + "/", StringComparison.OrdinalIgnoreCase))?.Id
                ?? throw new HqRegistryException($"The {SourceName} package registry doesn't offer {type}.");

            var packageBase = Find("PackageBaseAddress");

            var endpoints = new ServiceEndpoints(
                Find("SearchQueryService"),
                packageBase.EndsWith('/') ? packageBase : packageBase + "/");

            _endpoints = (feedUrl, endpoints);

            return endpoints;
        }
        finally
        {
            _indexGate.Release();
        }
    }

    private async Task<T?> GetJsonAsync<T>(string url, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        EnsureSuccess(response);

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);

        return await JsonSerializer.DeserializeAsync<T>(body, Json, cancellationToken);
    }

    private void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        _logger.LogWarning("The {Source} package registry returned {StatusCode} for {Url}", SourceName, (int)response.StatusCode, response.RequestMessage?.RequestUri);

        throw response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new HqRegistryException(
                $"{SourceName} refused this server's session ({(int)response.StatusCode}). Reconnect it in Settings."),
            _ => new HqRegistryException($"{SourceName} returned {(int)response.StatusCode} ({response.StatusCode})."),
        };
    }

    public void Dispose()
    {
        _http.Dispose();
        _indexGate.Dispose();
    }

    private sealed record ServiceEndpoints(string Search, string PackageBase);

    private sealed record ServiceIndex(
        [property: JsonPropertyName("resources")] IReadOnlyList<ServiceIndexResource> Resources);

    private sealed record ServiceIndexResource(
        [property: JsonPropertyName("@id")] string Id,
        [property: JsonPropertyName("@type")] string Type);

    private sealed record SearchResponse(
        [property: JsonPropertyName("data")] IReadOnlyList<SearchResult>? Data);

    private sealed record SearchResult
    {
        public string Id { get; init; } = string.Empty;
        public string Version { get; init; } = string.Empty;
        public string? Title { get; init; }
        public string? Description { get; init; }
        public IReadOnlyList<string> Authors { get; init; } = [];
        public string? IconUrl { get; init; }
        public string? ProjectUrl { get; init; }
        public long TotalDownloads { get; init; }
        public IReadOnlyList<PackageType> PackageTypes { get; init; } = [];
        public IReadOnlyList<SearchResultVersion> Versions { get; init; } = [];
        public Guid? ManifestId { get; init; }
    }

    private sealed record PackageType(string Name);

    private sealed record SearchResultVersion(string Version);
}

/// <summary>A package on the HQ registry, at its newest version.</summary>
/// <param name="ManifestId">For an LCX package, the ID of the record its manifest describes.</param>
/// <param name="Versions">Every listed version, newest first.</param>
public sealed record HqRegistryPackage(
    string Id,
    string Version,
    string Title,
    string? Description,
    string? Authors,
    string? IconUrl,
    string? ProjectUrl,
    long Downloads,
    Guid? ManifestId,
    IReadOnlyList<string> Versions);

public sealed class HqRegistryException(string message) : Exception(message);
