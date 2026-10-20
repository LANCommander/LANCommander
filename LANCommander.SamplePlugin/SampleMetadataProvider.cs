using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.Services.Providers.Metadata;

namespace LANCommander.SamplePlugin;

/// <summary>
/// Minimal metadata provider added by the sample plugin. Returns no results; its purpose is to
/// prove that a plugin-registered <see cref="IMetadataProvider"/> is picked up by the server's
/// provider enumeration.
/// </summary>
public sealed class SampleMetadataProvider : IMetadataProvider
{
    public string ProviderName => "Sample Plugin Provider";

    public Task<MetadataSearchResultsCollection<Game>?> SearchGamesAsync(string input, int limit = 10, int offset = 0)
        => Task.FromResult<MetadataSearchResultsCollection<Game>?>(
            new MetadataSearchResultsCollection<Game>(new List<MetadataSearchResult<Game>>(), More: false, limit, offset));

    public Task<Game?> GetGameAsync(string gameId) => Task.FromResult<Game?>(null);
}
