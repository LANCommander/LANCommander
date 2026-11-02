using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Providers.Metadata;

namespace LANCommander.Server.Plugins;

/// <summary>Puts the metadata sources plugins register alongside the server's own.</summary>
internal sealed class PluginMetadataProviders(IEnumerable<IMetadataProviderExtension> extensions) : IMetadataProviderSource
{
    public IEnumerable<IMetadataProvider> GetProviders() => extensions.Select(e => new PluginMetadataProvider(e));
}

/// <summary>Looks games up through a plugin's <see cref="IMetadataProviderExtension"/>.</summary>
internal sealed class PluginMetadataProvider(IMetadataProviderExtension extension) : IMetadataProvider
{
    public string ProviderName => extension.Name;

    public bool IsAvailable => extension.IsAvailable;

    public Task<MetadataSearchResultsCollection<Game>?> SearchGamesAsync(string input, int limit = 10, int offset = 0) =>
        SearchGamesAsync(input, null, limit, offset);

    public async Task<MetadataSearchResultsCollection<Game>?> SearchGamesAsync(string input, string? subProvider, int limit = 10, int offset = 0)
    {
        var page = await extension.SearchGamesAsync(
            input.Trim(),
            String.IsNullOrWhiteSpace(subProvider) ? null : subProvider,
            limit,
            offset,
            CancellationToken.None);

        return new MetadataSearchResultsCollection<Game>(
            page.Results.Select(r => new MetadataSearchResult<Game>(r.Id, r.Data)).ToList(),
            page.More,
            page.Limit,
            page.Offset);
    }

    public Task<Game?> GetGameAsync(string gameId) => extension.GetGameAsync(gameId, CancellationToken.None);

    public async Task<IEnumerable<(string Slug, string Name)>?> GetSubProvidersAsync()
    {
        var subProviders = await extension.GetSubProvidersAsync(CancellationToken.None);

        return subProviders.Count == 0 ? null : subProviders.Select(sp => (sp.Slug, sp.Name)).ToList();
    }

    public async Task<MetadataSearchResult<Game>?> ResolveByExternalIdAsync(string provider, string externalId)
    {
        var game = await extension.ResolveByExternalIdAsync(provider, externalId, CancellationToken.None);

        return game == null ? null : new MetadataSearchResult<Game>(game.Id, game.Data);
    }
}
