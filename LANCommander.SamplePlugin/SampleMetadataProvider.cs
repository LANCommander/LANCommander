using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.Plugins;

namespace LANCommander.SamplePlugin;

/// <summary>
/// Minimal metadata source added by the sample plugin. Returns no results; its purpose is to
/// prove that a plugin-registered <see cref="IMetadataProviderExtension"/> is listed among the
/// server's metadata providers.
/// </summary>
public sealed class SampleMetadataProvider : IMetadataProviderExtension
{
    public string Name => "Sample Plugin Provider";

    public Task<MetadataSearchPage> SearchGamesAsync(string input, string? subProvider, int limit, int offset, CancellationToken cancellationToken)
        => Task.FromResult(new MetadataSearchPage([], More: false, limit, offset));

    public Task<Game?> GetGameAsync(string gameId, CancellationToken cancellationToken) => Task.FromResult<Game?>(null);
}
