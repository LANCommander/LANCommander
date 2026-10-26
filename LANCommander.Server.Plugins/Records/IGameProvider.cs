using LANCommander.SDK.Enums;

namespace LANCommander.Server.Plugins;

/// <summary>
/// A game or add-on on the server: its own fields only. Its links, children and files are managed
/// through the methods of <see cref="IGameProvider"/> and the other providers.
/// </summary>
/// <remarks>
/// To change a game, read it, change what is needed with <c>with { … }</c> and pass it to
/// <see cref="IGameProvider.UpdateAsync"/>. Every field is written, so start from a fresh read.
/// </remarks>
public sealed record GameInfo
{
    /// <summary>Empty when adding a game, to have the server choose one.</summary>
    public Guid Id { get; init; }

    public string Title { get; init; } = "";
    public string? SortTitle { get; init; }
    public string? DirectoryName { get; init; }
    public string? Description { get; init; }
    public string? Notes { get; init; }
    public DateTime? ReleasedOn { get; init; }

    /// <summary>A main game, or one of the kinds of add-on.</summary>
    public GameType Type { get; init; } = GameType.MainGame;

    /// <summary>For an add-on, the game it adds to.</summary>
    public Guid? BaseGameId { get; init; }

    public GameInstallLocation InstallTo { get; init; } = GameInstallLocation.OwnDirectory;
    public bool ShowInLibrary { get; init; } = true;
    public bool Singleplayer { get; init; }

    /// <summary>Whether launchers are offered the game.</summary>
    public bool Published { get; init; } = true;

    public KeyAllocationMethod KeyAllocationMethod { get; init; } = KeyAllocationMethod.UserAccount;
    public string? ValidKeyRegex { get; init; }

    /// <summary>The game's engine, an <see cref="TaxonomyKind.Engine"/> record.</summary>
    public Guid? EngineId { get; init; }

    /// <summary>Set by the server; ignored when writing.</summary>
    public DateTime CreatedOn { get; init; }

    /// <summary>Set by the server; ignored when writing.</summary>
    public DateTime UpdatedOn { get; init; }
}

/// <summary>The kinds of record a game is linked to, other than its engine.</summary>
public enum GameLinkKind
{
    /// <summary><see cref="TaxonomyKind.Category"/> records.</summary>
    Categories,
    /// <summary><see cref="TaxonomyKind.Collection"/> records.</summary>
    Collections,
    /// <summary><see cref="TaxonomyKind.Company"/> records.</summary>
    Developers,
    /// <summary><see cref="TaxonomyKind.Genre"/> records.</summary>
    Genres,
    /// <summary><see cref="TaxonomyKind.Platform"/> records.</summary>
    Platforms,
    /// <summary><see cref="TaxonomyKind.Company"/> records.</summary>
    Publishers,
    /// <summary><see cref="TaxonomyKind.Tag"/> records.</summary>
    Tags,
    /// <summary>Redistributables, which the game's latest version installs alongside it.</summary>
    Redistributables,
    /// <summary>Tools.</summary>
    Tools,
}

/// <summary>An id a game goes by in another service, such as a store.</summary>
public sealed record GameExternalIdInfo
{
    public Guid Id { get; init; }
    public Guid GameId { get; init; }

    /// <summary>The service, e.g. <c>Steam</c> or <c>GOG</c>.</summary>
    public string Provider { get; init; } = "";

    public string ExternalId { get; init; } = "";
}

/// <summary>A named value an administrator or plugin records against a game.</summary>
public sealed record GameCustomFieldInfo
{
    public Guid Id { get; init; }
    public Guid GameId { get; init; }
    public string Name { get; init; } = "";
    public string Value { get; init; } = "";
}

/// <summary>
/// Lists and manages the server's games and add-ons, and their links, external ids and custom
/// fields. Provided by the server; inject it into a scoped plugin service or page.
/// </summary>
/// <remarks>
/// A game's archives arrive through <see cref="IPackageImporter"/>. Its versions, actions, scripts,
/// save paths, multiplayer modes, keys and media have providers of their own.
/// </remarks>
public interface IGameProvider
{
    /// <summary>Every game and add-on, ordered by title.</summary>
    Task<IReadOnlyList<GameInfo>> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>The game with the given id, or null.</summary>
    Task<GameInfo?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The add-ons of a game, ordered by title.</summary>
    Task<IReadOnlyList<GameInfo>> GetAddonsAsync(Guid baseGameId, CancellationToken cancellationToken = default);

    /// <summary>The game recorded under an id in another service, or null.</summary>
    /// <param name="provider">The service, compared case-insensitively.</param>
    /// <param name="externalId">The id in that service.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    Task<GameInfo?> FindByExternalIdAsync(string provider, string externalId, CancellationToken cancellationToken = default);

    /// <summary>Adds a game.</summary>
    /// <exception cref="KeyNotFoundException">The base game or engine does not exist.</exception>
    Task<GameInfo> AddAsync(GameInfo game, CancellationToken cancellationToken = default);

    /// <summary>Writes every field of a game.</summary>
    /// <exception cref="KeyNotFoundException">The game, its base game or its engine does not exist.</exception>
    Task<GameInfo> UpdateAsync(GameInfo game, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a game along with its archives and media, files included. Does nothing when there is
    /// no such game.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>The ids of the records a game is linked to.</summary>
    /// <exception cref="KeyNotFoundException">There is no such game.</exception>
    Task<IReadOnlyList<Guid>> GetLinksAsync(Guid gameId, GameLinkKind kind, CancellationToken cancellationToken = default);

    /// <summary>Replaces the records a game is linked to. An empty list unlinks them all.</summary>
    /// <exception cref="KeyNotFoundException">The game, or one of the records, does not exist.</exception>
    Task SetLinksAsync(Guid gameId, GameLinkKind kind, IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>The options a game's latest version passes to a redistributable, as JSON, or null.</summary>
    Task<string?> GetRedistributableOptionsAsync(Guid gameId, Guid redistributableId, CancellationToken cancellationToken = default);

    /// <summary>Sets the options a game's latest version passes to a redistributable, as JSON.</summary>
    /// <exception cref="KeyNotFoundException">There is no such game.</exception>
    Task SetRedistributableOptionsAsync(Guid gameId, Guid redistributableId, string? optionsJson, CancellationToken cancellationToken = default);

    /// <exception cref="KeyNotFoundException">There is no such game.</exception>
    Task<IReadOnlyList<GameExternalIdInfo>> GetExternalIdsAsync(Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>Records the id a game goes by in a service, replacing any it had there.</summary>
    /// <exception cref="KeyNotFoundException">There is no such game.</exception>
    Task<GameExternalIdInfo> SetExternalIdAsync(Guid gameId, string provider, string externalId, CancellationToken cancellationToken = default);

    /// <summary>Forgets the id a game goes by in a service. Does nothing when it has none there.</summary>
    Task RemoveExternalIdAsync(Guid gameId, string provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every game's custom fields, including the owning game id. Use this for bulk identity or
    /// provenance checks without making a separate request for each game.
    /// </summary>
    Task<IReadOnlyList<GameCustomFieldInfo>> GetCustomFieldsAsync(CancellationToken cancellationToken = default);

    /// <exception cref="KeyNotFoundException">There is no such game.</exception>
    Task<IReadOnlyList<GameCustomFieldInfo>> GetCustomFieldsAsync(Guid gameId, CancellationToken cancellationToken = default);

    /// <summary>Sets a custom field, adding it when the game has none by that name.</summary>
    /// <exception cref="KeyNotFoundException">There is no such game.</exception>
    Task<GameCustomFieldInfo> SetCustomFieldAsync(Guid gameId, string name, string value, CancellationToken cancellationToken = default);

    /// <summary>Removes a custom field. Does nothing when the game has none by that name.</summary>
    Task RemoveCustomFieldAsync(Guid gameId, string name, CancellationToken cancellationToken = default);
}
