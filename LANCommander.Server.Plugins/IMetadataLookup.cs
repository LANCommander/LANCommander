using LANCommander.SDK.Models.Manifest;

namespace LANCommander.Server.Plugins;

/// <summary>A game returned by one of the server's configured metadata providers.</summary>
/// <param name="Id">The provider's game id, used to fetch the full metadata.</param>
/// <param name="Data">The metadata supplied by the provider, not an imported server game.</param>
public sealed record MetadataGame(string Id, Game Data);

/// <summary>A page of metadata search results.</summary>
public sealed record MetadataSearchPage(IReadOnlyList<MetadataGame> Results, bool More, int Limit, int Offset);

/// <summary>
/// Looks up game metadata through the server's configured providers. Provided by the server;
/// inject it into a scoped plugin service or page. Lookups do not import or change games.
/// </summary>
public interface IMetadataLookup
{
    /// <summary>The names of available providers, ordered case-insensitively.</summary>
    IReadOnlyList<string> GetProviderNames();

    /// <summary>Searches a configured, available provider.</summary>
    /// <exception cref="InvalidOperationException">The provider is missing or unavailable.</exception>
    /// <remarks>Cancellation stops waiting; providers without cancellation support may finish in the background.</remarks>
    Task<MetadataSearchPage> SearchGamesAsync(
        string providerName,
        string input,
        int limit = 10,
        int offset = 0,
        CancellationToken cancellationToken = default);

    /// <summary>Fetches full metadata by the provider's game id, or null when it has no such game.</summary>
    /// <exception cref="InvalidOperationException">The provider is missing or unavailable.</exception>
    /// <remarks>Cancellation stops waiting; providers without cancellation support may finish in the background.</remarks>
    Task<Game?> GetGameAsync(
        string providerName,
        string gameId,
        CancellationToken cancellationToken = default);
}
