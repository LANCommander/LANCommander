using LANCommander.SDK.Models.Manifest;

namespace LANCommander.Server.Services.Providers.Metadata;

public interface IMetadataProvider
{
    public string ProviderName { get; }
    public Task<MetadataSearchResultsCollection<Game>?> SearchGamesAsync(string input, int limit = 10, int offset = 0);
    public Task<Game?> GetGameAsync(string gameId);
}

public record MetadataSearchResult<T>(string Id, T Data);
public record MetadataSearchResultsCollection<T>(ICollection<MetadataSearchResult<T>> Results, bool More, int Limit = 10, int Offset = 0);