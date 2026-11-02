using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.Plugins;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Providers.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

/// <summary>
/// Metadata sources plugins add through <see cref="IMetadataProviderExtension"/>, as the server's
/// metadata service, its endpoints and UI, and <see cref="IMetadataLookup"/> see them.
/// </summary>
[Collection("Application")]
public class PluginMetadataProviderTests
{
    [Fact]
    public async Task PluginSourcesAreListedAndSearchedLikeBuiltInOnes()
    {
        var game = new Game { Title = "Quake" };
        var extension = new FakeExtension("Plugin Source") { Game = game };
        using var services = Services([new BuiltIn("Built In")], extension, new FakeExtension("Offline") { IsAvailable = false });
        var metadata = new MetadataService(services);

        metadata.GetProviderNames().ShouldBe(["Built In", "Plugin Source"]);

        var provider = metadata.GetProvider("Plugin Source").ShouldNotBeNull();
        var results = (await provider.SearchGamesAsync("  Quake ", 3, 6)).ShouldNotBeNull();

        results.Results.ShouldHaveSingleItem().Id.ShouldBe("quake");
        results.Results.Single().Data.ShouldBeSameAs(game);
        results.More.ShouldBeTrue();
        results.Limit.ShouldBe(3);
        results.Offset.ShouldBe(6);
        extension.Input.ShouldBe("Quake");
        extension.SubProvider.ShouldBeNull();

        (await provider.GetGameAsync("quake")).ShouldBeSameAs(game);
        (await provider.GetGameAsync("missing")).ShouldBeNull();

        // Through the lookup plugins use
        var page = await new MetadataLookup(metadata).SearchGamesAsync("Plugin Source", "Quake");
        page.Results.ShouldHaveSingleItem().Data.ShouldBeSameAs(game);
    }

    [Fact]
    public async Task SubProvidersAndExternalIdsPassThrough()
    {
        var game = new Game { Title = "Quake" };
        var extension = new FakeExtension("Aggregator")
        {
            Game = game,
            SubProviders = [new("steam", "Steam"), new("gog", "GOG")],
        };
        using var services = Services([], extension);
        var provider = new MetadataService(services).GetProvider("Aggregator").ShouldNotBeNull();

        (await provider.GetSubProvidersAsync()).ShouldBe([("steam", "Steam"), ("gog", "GOG")]);

        await provider.SearchGamesAsync("Quake", "gog", 10, 0);
        extension.SubProvider.ShouldBe("gog");

        await provider.SearchGamesAsync("Quake", " ", 10, 0);
        extension.SubProvider.ShouldBeNull();

        var resolved = (await provider.ResolveByExternalIdAsync("Steam", "2310")).ShouldNotBeNull();
        resolved.Id.ShouldBe("quake");
        resolved.Data.ShouldBeSameAs(game);
        (await provider.ResolveByExternalIdAsync("Steam", "0")).ShouldBeNull();
    }

    [Fact]
    public async Task ASourceWithoutSubProvidersOrExternalIdsUsesTheDefaults()
    {
        using var services = Services([], new MinimalExtension());
        var provider = new MetadataService(services).GetProvider("Minimal").ShouldNotBeNull();

        (await provider.GetSubProvidersAsync()).ShouldBeNull();
        (await provider.ResolveByExternalIdAsync("Steam", "2310")).ShouldBeNull();
    }

    [Fact]
    public void APluginSourceCannotTakeABuiltInProvidersName()
    {
        var impostor = new FakeExtension("igdb");
        using var services = Services([new BuiltIn("IGDB")], impostor);
        var metadata = new MetadataService(services);

        metadata.GetProviderNames().ShouldBe(["IGDB"]);
        metadata.GetProvider("IGDB").ShouldBeOfType<BuiltIn>();
    }

    [Fact]
    public async Task ExtensionFailuresReachTheCaller()
    {
        var extension = new FakeExtension("Broken") { Failure = new HttpRequestException("Source is down") };
        using var services = Services([], extension);
        var provider = new MetadataService(services).GetProvider("Broken").ShouldNotBeNull();

        await Should.ThrowAsync<HttpRequestException>(() => provider.SearchGamesAsync("Quake"));
        await Should.ThrowAsync<HttpRequestException>(() => provider.GetGameAsync("quake"));
    }

    private static ServiceProvider Services(IMetadataProvider[] builtIn, params IMetadataProviderExtension[] extensions)
    {
        var services = new ServiceCollection();

        foreach (var provider in builtIn)
            services.AddSingleton(provider);

        foreach (var extension in extensions)
            services.AddSingleton(extension);

        services.AddSingleton<IMetadataProviderSource, PluginMetadataProviders>();

        return services.BuildServiceProvider();
    }

    private sealed class BuiltIn(string name) : IMetadataProvider
    {
        public string ProviderName => name;

        public Task<MetadataSearchResultsCollection<Game>?> SearchGamesAsync(string input, int limit = 10, int offset = 0) =>
            Task.FromResult<MetadataSearchResultsCollection<Game>?>(null);

        public Task<Game?> GetGameAsync(string gameId) => Task.FromResult<Game?>(null);
    }

    private sealed class MinimalExtension : IMetadataProviderExtension
    {
        public string Name => "Minimal";

        public Task<MetadataSearchPage> SearchGamesAsync(string input, string? subProvider, int limit, int offset, CancellationToken cancellationToken) =>
            Task.FromResult(new MetadataSearchPage([], false, limit, offset));

        public Task<Game?> GetGameAsync(string gameId, CancellationToken cancellationToken) => Task.FromResult<Game?>(null);
    }

    private sealed class FakeExtension(string name) : IMetadataProviderExtension
    {
        public string Name => name;
        public bool IsAvailable { get; init; } = true;
        public Game? Game { get; init; }
        public IReadOnlyList<MetadataSubProviderInfo> SubProviders { get; init; } = [];
        public Exception? Failure { get; init; }
        public string? Input { get; private set; }
        public string? SubProvider { get; private set; }

        public Task<MetadataSearchPage> SearchGamesAsync(string input, string? subProvider, int limit, int offset, CancellationToken cancellationToken)
        {
            if (Failure != null)
                return Task.FromException<MetadataSearchPage>(Failure);

            Input = input;
            SubProvider = subProvider;

            return Task.FromResult(new MetadataSearchPage([new MetadataGame("quake", Game!)], true, limit, offset));
        }

        public Task<Game?> GetGameAsync(string gameId, CancellationToken cancellationToken) =>
            Failure != null
                ? Task.FromException<Game?>(Failure)
                : Task.FromResult(gameId == "quake" ? Game : null);

        public Task<IReadOnlyList<MetadataSubProviderInfo>> GetSubProvidersAsync(CancellationToken cancellationToken) =>
            Task.FromResult(SubProviders);

        public Task<MetadataGame?> ResolveByExternalIdAsync(string provider, string externalId, CancellationToken cancellationToken) =>
            Task.FromResult(provider == "Steam" && externalId == "2310" ? new MetadataGame("quake", Game!) : null);
    }
}
