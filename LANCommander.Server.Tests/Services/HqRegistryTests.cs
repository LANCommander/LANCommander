using System.Net;
using System.Text;
using LANCommander.HQ.SDK.Authentication;
using LANCommander.PluginManagement;
using LANCommander.Server.Services.HQ;
using LANCommander.Server.Services.HQ.Registry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using ServerSettings = LANCommander.Server.Settings.Settings;

namespace LANCommander.Server.Tests.Services;

public class HqRegistryTests : IDisposable
{
    private const string BaseUrl = "https://hq.test";

    private readonly string _settingsPath = Path.Combine(Path.GetTempPath(), $"lc-hq-registry-{Guid.NewGuid():N}.yml");

    public void Dispose()
    {
        if (File.Exists(_settingsPath))
            File.Delete(_settingsPath);
    }

    private const string ServiceIndex = """
        {
          "version": "3.0.0",
          "resources": [
            { "@id": "https://hq.test/nuget/v3/flatcontainer/", "@type": "PackageBaseAddress/3.0.0" },
            { "@id": "https://hq.test/nuget/v3/search", "@type": "SearchQueryService" },
            { "@id": "https://hq.test/nuget/v3/search", "@type": "SearchQueryService/3.5.0" }
          ]
        }
        """;

    private const string SearchResults = """
        {
          "totalHits": 2,
          "data": [
            {
              "id": "LANCommander.Redistributables.VisualCppV14",
              "version": "14.44.35211",
              "title": "Visual C++ 2015-2022",
              "description": "Microsoft Visual C++ runtime",
              "authors": ["Microsoft", "LANCommander"],
              "totalDownloads": 42,
              "packageTypes": [{ "name": "LANCommander.LCX" }, { "name": "LANCommander.LCX.Redistributable" }],
              "versions": [{ "version": "14.40.0", "downloads": 2 }, { "version": "14.44.35211", "downloads": 40 }],
              "lancommanderKind": "Redistributable",
              "manifestId": "2f9a6e0c-6d5f-4d3c-9b1f-0a4c7e6b1d22"
            },
            {
              "id": "LANCommander.Plugins.Wrong",
              "version": "1.0.0",
              "packageTypes": [{ "name": "LANCommander.Plugin" }],
              "lancommanderKind": "Plugin"
            }
          ]
        }
        """;

    // ── Fakes ────────────────────────────────────────────────────────────────────────────────

    private sealed class FakeTokens : ITokenProvider, IInvalidatableTokenProvider
    {
        public Queue<string?> Tokens { get; } = new();
        public string? Current { get; set; } = "access-1";
        public int Invalidations;

        public Task<string?> GetTokenAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Current);

        public Task InvalidateAsync(CancellationToken cancellationToken = default)
        {
            Invalidations++;

            if (Tokens.Count > 0)
                Current = Tokens.Dequeue();

            return Task.CompletedTask;
        }
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<(string Url, string? Authorization)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.RequestUri!.ToString(), request.Headers.Authorization?.ToString()));

            return Task.FromResult(respond(request));
        }
    }

    private sealed class FakeAuthApi : IHqAuthApi
    {
        public Task<HqUserProfile?> GetCurrentUserAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<HqUserProfile?>(new HqUserProfile("tester", false, false, null));

        public Task ExchangeCodeAsync(string code, string clientName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RevokeSessionAsync(string refreshToken, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ClearCredentialAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

    private (HqRegistry Registry, HqConnectionService Connection, FakeTokens Tokens, StubHandler Http) Create(
        Func<HttpRequestMessage, HttpResponseMessage> respond,
        bool connected = true)
    {
        var settings = new ServerSettings();
        settings.Server.HQ.BaseUrl = BaseUrl + "/";

        if (connected)
            settings.Server.HQ.RefreshToken = "refresh-token";

        var provider = new SettingsProvider<ServerSettings>(new StubOptionsMonitor<ServerSettings>(settings), _settingsPath);
        var connection = new HqConnectionService(new FakeAuthApi(), provider, NullLogger<HqConnectionService>.Instance);
        var tokens = new FakeTokens();
        var http = new StubHandler(respond);

        var registry = new HqRegistry(tokens, connection, provider, NullLogger<HqRegistry>.Instance, http);

        return (registry, connection, tokens, http);
    }

    private static HttpResponseMessage Registry(HttpRequestMessage request)
    {
        var path = request.RequestUri!.AbsolutePath;

        if (path == "/nuget/v3/index.json")
            return Json(ServiceIndex);

        if (path == "/nuget/v3/search")
            return Json(SearchResults);

        if (path.EndsWith(".lcx") && path.Contains("visualcppv14"))
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3, 4, 5]) };

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    // ── Tests ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TheFeedIsUnderTheConfiguredHq()
    {
        var (registry, _, _, _) = Create(Registry);

        registry.FeedUrl.ShouldBe("https://hq.test/nuget/v3/index.json");
    }

    [Fact]
    public async Task SearchSignsInWithTheHqSessionAndFiltersByPackageType()
    {
        var (registry, _, _, http) = Create(Registry);

        var results = await registry.SearchAsync("visual", HqRegistry.RedistributablePackageType);

        http.Requests.ShouldAllBe(r => r.Authorization == "Bearer access-1");

        var search = http.Requests.Single(r => r.Url.Contains("/search"));
        search.Url.ShouldContain("q=visual");
        search.Url.ShouldContain("packageType=LANCommander.LCX.Redistributable");
        search.Url.ShouldContain("prerelease=false");

        var package = results.ShouldHaveSingleItem();
        package.Id.ShouldBe("LANCommander.Redistributables.VisualCppV14");
        package.Title.ShouldBe("Visual C++ 2015-2022");
        package.Authors.ShouldBe("Microsoft, LANCommander");
        package.Downloads.ShouldBe(42);
        package.ManifestId.ShouldBe(Guid.Parse("2f9a6e0c-6d5f-4d3c-9b1f-0a4c7e6b1d22"));
        package.Versions.ShouldBe(["14.44.35211", "14.40.0"]);
    }

    [Fact]
    public async Task TheServiceIndexIsReadOnce()
    {
        var (registry, _, _, http) = Create(Registry);

        await registry.SearchAsync(null, HqRegistry.RedistributablePackageType);
        await registry.SearchAsync(null, HqRegistry.RedistributablePackageType);

        http.Requests.Count(r => r.Url.EndsWith("index.json")).ShouldBe(1);
    }

    [Fact]
    public async Task DownloadFetchesTheLcxFromTheFlatContainer()
    {
        var (registry, _, _, http) = Create(Registry);
        using var destination = new MemoryStream();
        long reported = 0;

        await registry.DownloadLcxAsync("LANCommander.Redistributables.VisualCppV14", "14.44.35211", destination,
            new SynchronousProgress(bytes => reported = bytes));

        http.Requests.Last().Url.ShouldBe(
            "https://hq.test/nuget/v3/flatcontainer/lancommander.redistributables.visualcppv14/14.44.35211/lancommander.redistributables.visualcppv14.14.44.35211.lcx");

        destination.ToArray().ShouldBe(new byte[] { 1, 2, 3, 4, 5 });
        reported.ShouldBe(5);
    }

    [Fact]
    public async Task AMissingPackageSaysSo()
    {
        var (registry, _, _, _) = Create(Registry);

        var ex = await Should.ThrowAsync<HqRegistryException>(() =>
            registry.DownloadLcxAsync("Nope", "1.0.0", Stream.Null));

        ex.Message.ShouldContain("Nope 1.0.0 was not found");
    }

    [Fact]
    public async Task ARefusedTokenIsRenewedOnceAndRetried()
    {
        var (registry, connection, tokens, http) = Create(request =>
            request.Headers.Authorization?.Parameter == "access-1"
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : Registry(request));

        tokens.Tokens.Enqueue("access-2");

        var results = await registry.SearchAsync(null, HqRegistry.RedistributablePackageType);

        results.ShouldHaveSingleItem();
        tokens.Invalidations.ShouldBe(1);
        http.Requests.First().Authorization.ShouldBe("Bearer access-1");
        http.Requests.Last().Authorization.ShouldBe("Bearer access-2");
        connection.Current.Status.ShouldNotBe(HqConnectionStatus.Unauthorized);
    }

    [Fact]
    public async Task ASessionStillRefusedMarksHqUnauthorized()
    {
        var (registry, connection, tokens, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        tokens.Tokens.Enqueue("access-2");

        await Should.ThrowAsync<HqRegistryException>(() => registry.SearchAsync(null, HqRegistry.RedistributablePackageType));

        connection.Current.Status.ShouldBe(HqConnectionStatus.Unauthorized);
        registry.IsAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task WithoutATokenNothingIsSent()
    {
        var (registry, _, tokens, http) = Create(Registry);

        tokens.Current = null;

        await Should.ThrowAsync<HqRegistryUnavailableException>(() => registry.SearchAsync(null, HqRegistry.RedistributablePackageType));

        http.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void TheRegistryIsUnavailableWithoutAnHqCredential()
    {
        var (registry, _, _, _) = Create(Registry, connected: false);

        registry.IsAvailable.ShouldBeFalse();
    }

    /// <summary>
    /// The plugin feed client hands HQ's handler to NuGet, which must then send every request through
    /// it, so the registry sees the session's bearer token rather than nothing.
    /// </summary>
    [Fact]
    public async Task PluginFeedsSendRequestsThroughTheirHandler()
    {
        var authorizations = new List<string?>();

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        await using var app = builder.Build();

        app.Use(async (context, next) =>
        {
            lock (authorizations)
                authorizations.Add(context.Request.Headers.Authorization.ToString());
            await next();
        });

        app.MapGet("/nuget/v3/index.json", (HttpContext context) => Results.Text($$"""
            { "version": "3.0.0", "resources": [
              { "@id": "{{Origin(context)}}/nuget/v3/search", "@type": "SearchQueryService" },
              { "@id": "{{Origin(context)}}/nuget/v3/search", "@type": "SearchQueryService/3.0.0-rc" },
              { "@id": "{{Origin(context)}}/nuget/v3/search", "@type": "SearchQueryService/3.0.0-beta" },
              { "@id": "{{Origin(context)}}/nuget/v3/search", "@type": "SearchQueryService/3.5.0" },
              { "@id": "{{Origin(context)}}/nuget/v3/registration/", "@type": "RegistrationsBaseUrl" },
              { "@id": "{{Origin(context)}}/nuget/v3/registration/", "@type": "RegistrationsBaseUrl/3.6.0" },
              { "@id": "{{Origin(context)}}/nuget/v3/flatcontainer/", "@type": "PackageBaseAddress/3.0.0" }
            ] }
            """, "application/json"));

        app.MapGet("/nuget/v3/search", () => Results.Text("""
            { "totalHits": 1, "data": [ {
              "id": "LANCommander.Plugins.Example", "version": "1.0.0", "title": "Example",
              "packageTypes": [ { "name": "LANCommander.Plugin.Server" } ],
              "versions": [ { "version": "1.0.0", "downloads": 0 } ]
            } ] }
            """, "application/json"));

        await app.StartAsync();

        var origin = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

        var (registry, _, _, _) = Create(Registry);

        var feed = new PluginFeed(HqRegistry.SourceName, $"{origin}/nuget/v3/index.json")
        {
            CreateHandler = registry.CreateHandler,
            IsOfficialRegistry = true,
        };

        var client = new PluginFeedClient(() => [feed], () => false, NullLogger.Instance);

        var result = await client.SearchAsync(null, "LANCommander.Plugin.Server");

        result.Errors.ShouldBeEmpty(string.Join("; ", result.Errors));

        var listing = result.Packages.ShouldHaveSingleItem();
        listing.Id.ShouldBe("LANCommander.Plugins.Example");
        listing.SourceName.ShouldBe(HqRegistry.SourceName);
        listing.IsOfficial.ShouldBeTrue();

        authorizations.ShouldNotBeEmpty();
        authorizations.ShouldAllBe(a => a == "Bearer access-1");
    }

    private static string Origin(HttpContext context) => $"{context.Request.Scheme}://{context.Request.Host}";

    private sealed class SynchronousProgress(Action<long> report) : IProgress<long>
    {
        public void Report(long value) => report(value);
    }

    private sealed class StubOptionsMonitor<T>(T value) : Microsoft.Extensions.Options.IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
