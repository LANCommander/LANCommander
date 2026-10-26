namespace LANCommander.Server.Plugins;

/// <summary>
/// A version of a game. Each version has its own archive, actions, scripts, save paths, option
/// schema and redistributables; the latest one is what an edit to the game changes.
/// </summary>
public sealed record GameVersionInfo
{
    public Guid Id { get; init; }
    public Guid GameId { get; init; }

    /// <summary>The version number, e.g. <c>1.2.0</c>.</summary>
    public string Version { get; init; } = "";

    public string? Changelog { get; init; }

    /// <summary>Position among the game's versions; the highest is the latest.</summary>
    public int SortOrder { get; init; }

    /// <summary>Whether launchers are offered the version.</summary>
    public bool Published { get; init; }

    /// <summary>The options the version's install offers, as an option schema, or null.</summary>
    public string? OptionSchema { get; init; }

    /// <summary>The version's archive, if it has one. Set by the server; ignored when writing.</summary>
    public Guid? ArchiveId { get; init; }

    /// <summary>Set by the server; ignored when writing.</summary>
    public DateTime CreatedOn { get; init; }
}

/// <summary>A redistributable required by a game version, with that version's option values as JSON.</summary>
public sealed record GameVersionRedistributableInfo(RedistributableInfo Redistributable, string? Options);

/// <summary>
/// Lists and manages the versions of games. Provided by the server; inject it into a scoped plugin
/// service or page. A version's archive arrives through <see cref="IPackageImporter"/>.
/// </summary>
public interface IGameVersionProvider
{
    /// <summary>A game's versions, oldest first.</summary>
    Task<IReadOnlyList<GameVersionInfo>> GetAsync(Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>A game's latest version, or null when it has none yet.</summary>
    Task<GameVersionInfo?> GetLatestAsync(Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The latest version, creating an empty initial version only when the game has none.
    /// Does not create a new version or publish a draft when a version already exists.
    /// </summary>
    /// <exception cref="KeyNotFoundException">There is no such game.</exception>
    Task<GameVersionInfo> GetOrCreateLatestAsync(Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>The version with the given id, or null.</summary>
    Task<GameVersionInfo?> GetVersionAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a version after the game's latest, carrying over the latest's actions, scripts, save
    /// paths, option schema and redistributables. A draft latest version is published by this.
    /// </summary>
    /// <exception cref="KeyNotFoundException">There is no such game.</exception>
    Task<GameVersionInfo> CreateAsync(Guid gameId, string version, string? changelog = null, CancellationToken cancellationToken = default);

    /// <summary>Writes a version's number, changelog, sort order, published state and option schema.</summary>
    /// <exception cref="KeyNotFoundException">There is no such version.</exception>
    Task<GameVersionInfo> UpdateAsync(GameVersionInfo version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a version along with its archive, file included, and its actions, scripts and save
    /// paths. Does nothing when there is no such version.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>A version's redistributables and their per-version option values.</summary>
    /// <exception cref="KeyNotFoundException">There is no such version.</exception>
    Task<IReadOnlyList<GameVersionRedistributableInfo>> GetRedistributablesAsync(Guid versionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces a version's redistributables, preserving options for those kept. An empty list
    /// unlinks them all. The game's own list is mirrored according to the server's publishing rules.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The version or a requested redistributable does not exist.</exception>
    Task SetRedistributablesAsync(Guid versionId, IEnumerable<Guid> redistributableIds, CancellationToken cancellationToken = default);

    /// <summary>Sets option values as JSON for a redistributable already linked to the version.</summary>
    /// <exception cref="KeyNotFoundException">The version or its redistributable link does not exist.</exception>
    Task SetRedistributableOptionsAsync(Guid versionId, Guid redistributableId, string? optionsJson, CancellationToken cancellationToken = default);
}
