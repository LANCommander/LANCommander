using LANCommander.SDK.Models.Manifest;

namespace LANCommander.Server.Services.Providers.Metadata;

public interface IMetadataProvider
{
    public string ProviderName { get; }
    public Task<MetadataSearchResultsCollection<Game>?> SearchGamesAsync(string input, int limit = 10, int offset = 0);
    public Task<Game?> GetGameAsync(string gameId);

    Task<IEnumerable<(string Slug, string Name)>?> GetSubProvidersAsync() => Task.FromResult<IEnumerable<(string Slug, string Name)>?>(null);

    Task<MetadataSearchResultsCollection<Game>?> SearchGamesAsync(string input, string? subProvider, int limit = 10, int offset = 0)
        => SearchGamesAsync(input, limit, offset);
}

public record MetadataSearchResult<T>(string Id, T Data);
public record MetadataSearchResultsCollection<T>(ICollection<MetadataSearchResult<T>> Results, bool More, int Limit = 10, int Offset = 0);