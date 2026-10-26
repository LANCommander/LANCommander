using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.Plugins;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Providers.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

public class PluginMetadataLookupTests
{
    [Fact]
    public async Task AvailableProvidersCanBeSearchedAndFullMetadataFetched()
    {
        var game = new Game { Title = "Quake", Description = "Full metadata" };
        var source = new FakeProvider("Example") { Game = game };
        using var services = Services(source, new FakeProvider("Unavailable") { IsAvailable = false }, new FakeProvider("Alpha"));
        var lookup = Lookup(services);

        lookup.GetProviderNames().ShouldBe(["Alpha", "Example"]);

        var page = await lookup.SearchGamesAsync("Example", "  Quake  ", limit: 3, offset: 6);

        page.Results.ShouldHaveSingleItem().Id.ShouldBe("provider-id");
        page.Results[0].Data.ShouldBeSameAs(game);
        page.More.ShouldBeTrue();
        page.Limit.ShouldBe(3);
        page.Offset.ShouldBe(6);
        source.Input.ShouldBe("Quake");
        (await lookup.GetGameAsync("Example", "provider-id")).ShouldBeSameAs(game);
        (await lookup.GetGameAsync("Example", "missing")).ShouldBeNull();
    }

    [Fact]
    public async Task MissingAndUnavailableProvidersFailExplicitly()
    {
        var source = new FakeProvider("Disabled") { IsAvailable = false };
        using var services = Services(source);
        var lookup = Lookup(services);

        foreach (var name in new[] { "Disabled", "Missing" })
        {
            await Should.ThrowAsync<InvalidOperationException>(() => lookup.SearchGamesAsync(name, "Quake"));
            await Should.ThrowAsync<InvalidOperationException>(() => lookup.GetGameAsync(name, "id"));
        }

        source.Searches.ShouldBe(0);
    }

    [Fact]
    public async Task EmptyResultsAreEmptyButProviderFailuresAreNotSwallowed()
    {
        var source = new FakeProvider("Example") { Empty = true };
        using var services = Services(source);
        var lookup = Lookup(services);

        var page = await lookup.SearchGamesAsync("Example", "Quake", 5, 10);

        page.Results.ShouldBeEmpty();
        page.More.ShouldBeFalse();
        page.Limit.ShouldBe(5);
        page.Offset.ShouldBe(10);

        source.Failure = new HttpRequestException("Provider failed");

        await Should.ThrowAsync<HttpRequestException>(() => lookup.SearchGamesAsync("Example", "Quake"));
        await Should.ThrowAsync<HttpRequestException>(() => lookup.GetGameAsync("Example", "id"));
    }

    [Fact]
    public async Task InvalidSearchArgumentsNeverReachTheProvider()
    {
        var source = new FakeProvider("Example");
        using var services = Services(source);
        var lookup = Lookup(services);

        await Should.ThrowAsync<ArgumentException>(() => lookup.SearchGamesAsync("Example", " "));
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => lookup.SearchGamesAsync("Example", "Quake", 0));
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => lookup.SearchGamesAsync("Example", "Quake", offset: -1));
        await Should.ThrowAsync<ArgumentException>(() => lookup.GetGameAsync("Example", " "));

        source.Searches.ShouldBe(0);
    }

    [Fact]
    public async Task CancellationStopsWaitingForAProvider()
    {
        var pendingSearch = new TaskCompletionSource<MetadataSearchResultsCollection<Game>?>();
        var pendingGame = new TaskCompletionSource<Game?>();
        var source = new FakeProvider("Example") { PendingSearch = pendingSearch.Task, PendingGame = pendingGame.Task };
        using var services = Services(source);
        var lookup = Lookup(services);
        using var cancellation = new CancellationTokenSource();

        var search = lookup.SearchGamesAsync("Example", "Quake", cancellationToken: cancellation.Token);
        var game = lookup.GetGameAsync("Example", "provider-id", cancellation.Token);
        cancellation.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() => search);
        await Should.ThrowAsync<OperationCanceledException>(() => game);
        await Should.ThrowAsync<OperationCanceledException>(() =>
            lookup.SearchGamesAsync("Example", "Quake", cancellationToken: cancellation.Token));

        source.Searches.ShouldBe(1);
        pendingSearch.SetResult(null);
        pendingGame.SetResult(null);
    }

    private static ServiceProvider Services(params IMetadataProvider[] providers)
    {
        var services = new ServiceCollection();
        foreach (var provider in providers)
            services.AddSingleton(provider);
        return services.BuildServiceProvider();
    }

    private static IMetadataLookup Lookup(IServiceProvider services) => new MetadataLookup(new MetadataService(services));

    private sealed class FakeProvider(string name) : IMetadataProvider
    {
        public string ProviderName => name;
        public bool IsAvailable { get; init; } = true;
        public Game? Game { get; init; }
        public bool Empty { get; init; }
        public Exception? Failure { get; set; }
        public string? Input { get; private set; }
        public int Searches { get; private set; }
        public Task<MetadataSearchResultsCollection<Game>?>? PendingSearch { get; init; }
        public Task<Game?>? PendingGame { get; init; }

        public Task<MetadataSearchResultsCollection<Game>?> SearchGamesAsync(string input, int limit = 10, int offset = 0)
        {
            Searches++;
            Input = input;
            if (Failure is not null)
                return Task.FromException<MetadataSearchResultsCollection<Game>?>(Failure);
            return PendingSearch ?? Task.FromResult<MetadataSearchResultsCollection<Game>?>(
                Empty ? null : new([new("provider-id", Game!)], true, limit, offset));
        }

        public Task<Game?> GetGameAsync(string gameId) =>
            Failure is not null
                ? Task.FromException<Game?>(Failure)
                : PendingGame ?? Task.FromResult(gameId == "provider-id" ? Game : null);
    }
}
