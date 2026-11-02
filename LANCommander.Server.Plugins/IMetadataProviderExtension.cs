using LANCommander.SDK.Models.Manifest;

namespace LANCommander.Server.Plugins;

/// <summary>
/// Adds a source of game metadata, alongside the built-in ones such as HQ, IGDB and PCGamingWiki.
/// Administrators pick it when looking up a game, the launcher's packaging wizard lists it, and
/// <see cref="IMetadataLookup"/> searches it. Register an implementation in <c>IPlugin.ConfigureServices</c>.
/// </summary>
/// <remarks>
/// The extension only looks games up. The server decides what to import from the
/// <see cref="Game"/> it returns, so fill in what the source knows and leave the rest empty.
/// </remarks>
public interface IMetadataProviderExtension
{
    /// <summary>
    /// The name the source is shown and chosen by. Must be unique among the server's metadata
    /// providers; a plugin source named like a built-in one is ignored.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Whether the source can be used right now, e.g. false until it has been given an API key.
    /// Unavailable sources aren't listed. Read each time the providers are listed, so keep it cheap.
    /// </summary>
    bool IsAvailable => true;

    /// <summary>Searches the source for games. A failure is shown to the administrator searching.</summary>
    /// <param name="input">What was searched for, usually a game's title, trimmed.</param>
    /// <param name="subProvider">
    /// The <see cref="MetadataSubProviderInfo.Slug"/> to search within, or null to search the whole
    /// source. Always null for a source without sub-providers.
    /// </param>
    /// <param name="limit">The most results to return.</param>
    /// <param name="offset">How many results to skip, for paging.</param>
    /// <param name="cancellationToken">Cancelled when the search is abandoned.</param>
    Task<MetadataSearchPage> SearchGamesAsync(string input, string? subProvider, int limit, int offset, CancellationToken cancellationToken);

    /// <summary>Fetches a game's full metadata by the id a search returned, or null if the source has no such game.</summary>
    Task<Game?> GetGameAsync(string gameId, CancellationToken cancellationToken);

    /// <summary>
    /// Parts of the source a search can be narrowed to, e.g. the services an aggregator draws on.
    /// Empty when there are none.
    /// </summary>
    Task<IReadOnlyList<MetadataSubProviderInfo>> GetSubProvidersAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MetadataSubProviderInfo>>([]);

    /// <summary>
    /// Finds the game a game's id on another service (its external id, e.g. a Steam AppID) refers to,
    /// so a game that already has one is matched exactly instead of by title. Null when the source
    /// can't, and the server falls back to searching.
    /// </summary>
    /// <param name="provider">The service the id belongs to, as stored on the game's external ids, e.g. "Steam".</param>
    /// <param name="externalId">The game's id on that service.</param>
    /// <param name="cancellationToken">Cancelled when the lookup is abandoned.</param>
    Task<MetadataGame?> ResolveByExternalIdAsync(string provider, string externalId, CancellationToken cancellationToken) =>
        Task.FromResult<MetadataGame?>(null);
}

/// <summary>A part of an <see cref="IMetadataProviderExtension"/> a search can be narrowed to.</summary>
/// <param name="Slug">Identifies it to <see cref="IMetadataProviderExtension.SearchGamesAsync"/>.</param>
/// <param name="Name">What administrators see.</param>
public sealed record MetadataSubProviderInfo(string Slug, string Name);
