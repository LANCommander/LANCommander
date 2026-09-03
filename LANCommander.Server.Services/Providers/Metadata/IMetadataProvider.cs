using LANCommander.SDK.Models.Manifest;

namespace LANCommander.Server.Services.Providers.Metadata;

public interface IMetadataProvider
{
    public string ProviderName { get; }
    bool IsAvailable => true;
    public Task<MetadataSearchResultsCollection<Game>?> SearchGamesAsync(string input, int limit = 10, int offset = 0);
    public Task<Game?> GetGameAsync(string gameId);

    Task<IEnumerable<(string Slug, string Name)>?> GetSubProvidersAsync() => Task.FromResult<IEnumerable<(string Slug, string Name)>?>(null);

    Task<MetadataSearchResultsCollection<Game>?> SearchGamesAsync(string input, string? subProvider, int limit = 10, int offset = 0)
        => SearchGamesAsync(input, limit, offset);

    /// <summary>
    /// Resolves a single result from an identifier on another service (e.g. a Steam AppID), so a
    /// game that already carries an external ID can be matched exactly instead of by title.
    /// Providers that can't do this return null and the caller falls back to searching.
    /// </summary>
    Task<MetadataSearchResult<Game>?> ResolveByExternalIdAsync(string provider, string externalId)
        => Task.FromResult<MetadataSearchResult<Game>?>(null);
}

public record MetadataSearchResult<T>(string Id, T Data);
public record MetadataSearchResultsCollection<T>(ICollection<MetadataSearchResult<T>> Results, bool More, int Limit = 10, int Offset = 0);