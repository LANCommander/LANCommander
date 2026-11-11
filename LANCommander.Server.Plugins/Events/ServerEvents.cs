namespace LANCommander.Server.Plugins;

// Raised by the server through LANCommander.SDK.Plugins.IPluginEventBus. Subscribe in
// IPlugin.InitializeAsync. Handlers run on the request that caused the change and are awaited, so
// hand slow work (webhooks, chat messages) off to a background task or IRecurringJobScheduler. A
// throwing handler is logged and doesn't affect the change or other handlers. Changes a plugin makes
// through the record providers raise these events too.

/// <summary>Raised after a game or add-on is added, by hand, by an import or through <see cref="IGameProvider"/>.</summary>
public sealed record GameCreatedEvent(Guid GameId, string? Title);

/// <summary>Raised after a game or add-on is saved.</summary>
public sealed record GameUpdatedEvent(Guid GameId, string? Title);

/// <summary>Raised after a game or add-on is deleted along with its archives and media.</summary>
public sealed record GameDeletedEvent(Guid GameId, string? Title);

/// <summary>
/// Raised after a game version is added, by an administrator, a plugin, or an upload that needed a
/// version for its archive. <paramref name="Version"/> can be empty for a placeholder version that an
/// archive fills in later, which raises <see cref="GameVersionUpdatedEvent"/>.
/// </summary>
public sealed record GameVersionCreatedEvent(Guid VersionId, Guid GameId, string? Version, bool Published);

/// <summary>Raised after a game version is saved, or an archive is linked to it.</summary>
public sealed record GameVersionUpdatedEvent(Guid VersionId, Guid GameId, string? Version);

/// <summary>
/// Raised when a game version is published or unpublished. Launchers are offered a game's newest
/// published version. Adding a version after an unpublished one publishes the earlier one, which
/// raises this too.
/// </summary>
public sealed record GameVersionPublishedChangedEvent(Guid VersionId, Guid GameId, string? Version, bool Published);

/// <summary>Raised after a game version is deleted.</summary>
public sealed record GameVersionDeletedEvent(Guid VersionId, Guid GameId, string? Version);

/// <summary>Raised after a redistributable is added, by hand, by an import or through <see cref="IRedistributableProvider"/>.</summary>
public sealed record RedistributableCreatedEvent(Guid RedistributableId, string? Name);

/// <summary>Raised after a redistributable is saved.</summary>
public sealed record RedistributableUpdatedEvent(Guid RedistributableId, string? Name);

/// <summary>Raised after a redistributable is deleted.</summary>
public sealed record RedistributableDeletedEvent(Guid RedistributableId, string? Name);

/// <summary>Raised after a tool is added, by hand, by an import or through <see cref="IToolProvider"/>.</summary>
public sealed record ToolCreatedEvent(Guid ToolId, string? Name);

/// <summary>Raised after a tool is saved.</summary>
public sealed record ToolUpdatedEvent(Guid ToolId, string? Name);

/// <summary>Raised after a tool is deleted.</summary>
public sealed record ToolDeletedEvent(Guid ToolId, string? Name);

/// <summary>
/// Raised after a game server is added, by hand, by an import or through <see cref="IServerProvider"/>.
/// Whether it is running is reported by <see cref="ServerStatusChangedEvent"/>.
/// </summary>
public sealed record ServerCreatedEvent(Guid ServerId, Guid? GameId, string? Name);

/// <summary>Raised after a game server's settings are saved.</summary>
public sealed record ServerUpdatedEvent(Guid ServerId, Guid? GameId, string? Name);

/// <summary>Raised after a game server is deleted.</summary>
public sealed record ServerDeletedEvent(Guid ServerId, Guid? GameId, string? Name);

/// <summary>
/// Raised after a media item is added: a game's artwork, a user's avatar or an image on a page. Its
/// file is written first when one is uploaded or downloaded with it.
/// </summary>
/// <param name="GameId">The game the media belongs to, if any.</param>
/// <param name="UserId">The user the media belongs to, such as for an avatar, if any.</param>
public sealed record MediaCreatedEvent(Guid MediaId, Guid? GameId, Guid? UserId, LANCommander.SDK.Enums.MediaType Type);

/// <summary>
/// Raised after a media item is saved, including when its file is replaced and when it is
/// reordered among the game's media.
/// </summary>
public sealed record MediaUpdatedEvent(Guid MediaId, Guid? GameId, Guid? UserId, LANCommander.SDK.Enums.MediaType Type);

/// <summary>Raised after a media item and its file are deleted, including when its game is.</summary>
public sealed record MediaDeletedEvent(Guid MediaId, Guid? GameId, Guid? UserId, LANCommander.SDK.Enums.MediaType Type);

/// <summary>
/// Raised for each game added to a user's library. Adding an add-on adds its base game too, which
/// raises its own event.
/// </summary>
public sealed record LibraryGameAddedEvent(Guid UserId, Guid GameId);

/// <summary>
/// Raised for each game removed from a user's library. Removing a game removes its hidden add-ons and
/// any add-ons the user picked, which each raise their own event.
/// </summary>
public sealed record LibraryGameRemovedEvent(Guid UserId, Guid GameId);

/// <summary>
/// Raised when what other players see of a player changes: they start or stop playing, their game
/// reports new activity, or they change their presence visibility. Games can report activity often,
/// so keep handlers quick. A player who isn't playing can be reported as not playing again, for
/// example when they change their visibility.
/// </summary>
/// <param name="Presence">
/// What everyone may see under the player's <see cref="LANCommander.SDK.Enums.PresenceVisibility"/>: no
/// activity when it is <c>GameOnly</c>. Null when the player isn't shown as playing, because they
/// stopped or their visibility is <c>Hidden</c>.
/// </param>
public sealed record PresenceChangedEvent(Guid UserId, LANCommander.SDK.Models.UserPresence? Presence);

/// <summary>
/// Raised after an uploaded archive is stored and linked to what it belongs to, and for a game, to its
/// version. Exactly one of <paramref name="GameId"/>, <paramref name="RedistributableId"/> and
/// <paramref name="ToolId"/> is set. Archives an import or packaging creates don't raise it.
/// </summary>
public sealed record ArchiveUploadedEvent(
    Guid ArchiveId,
    Guid? GameId,
    Guid? GameVersionId,
    Guid? RedistributableId,
    Guid? ToolId,
    string Version);

/// <summary>
/// Raised after a user account is created, by registration, an administrator or an external sign-in.
/// The server's default role may be added to the account just after.
/// </summary>
/// <param name="Approved">Whether the account was approved on creation; if not, <see cref="UserApprovedEvent"/> follows when it is.</param>
public sealed record UserCreatedEvent(Guid UserId, string UserName, bool Approved);

/// <summary>Raised when an administrator approves an account that wasn't approved.</summary>
public sealed record UserApprovedEvent(Guid UserId, string UserName);

/// <summary>Raised after a user account is deleted.</summary>
public sealed record UserDeletedEvent(Guid UserId, string UserName);

/// <summary>
/// Raised when a user signs in to the server: a launcher or API client getting a token, or a browser
/// sign-in, including through an external provider. Refreshing a token doesn't raise it.
/// </summary>
public sealed record UserSignedInEvent(Guid UserId, string? UserName);

/// <summary>
/// Raised when a client reports a sign-out or a browser session is signed out. Tokens already issued
/// to the user stay valid until they expire.
/// </summary>
public sealed record UserSignedOutEvent(Guid UserId, string? UserName);

/// <summary>
/// Raised after a game key is allocated to a user or a machine, by a launcher requesting one or an
/// administrator assigning it. A launcher swapping keys raises <see cref="KeyReleasedEvent"/> for the
/// key it held first.
/// </summary>
/// <param name="UserId">Set when <paramref name="Method"/> is <see cref="LANCommander.SDK.Enums.KeyAllocationMethod.UserAccount"/>.</param>
/// <param name="MacAddress">Set when <paramref name="Method"/> is <see cref="LANCommander.SDK.Enums.KeyAllocationMethod.MacAddress"/>.</param>
/// <param name="AvailableKeys">How many of the game's keys are still unallocated afterwards.</param>
public sealed record KeyAllocatedEvent(
    Guid KeyId,
    Guid GameId,
    LANCommander.SDK.Enums.KeyAllocationMethod Method,
    Guid? UserId,
    string? MacAddress,
    int AvailableKeys);

/// <summary>Raised after a game key is released. The previous holder is given when there was one.</summary>
/// <param name="AvailableKeys">How many of the game's keys are unallocated afterwards.</param>
public sealed record KeyReleasedEvent(Guid KeyId, Guid GameId, Guid? UserId, string? MacAddress, int AvailableKeys);

/// <summary>Raised after a cloud save is uploaded and its file written.</summary>
public sealed record SaveUploadedEvent(Guid SaveId, Guid GameId, Guid UserId, long Size, LANCommander.SDK.Enums.RuntimePlatform Platforms);

/// <summary>
/// Raised after a cloud save and its file are deleted: by its owner, an administrator, or the server
/// making room under a user's save count or storage limits.
/// </summary>
public sealed record SaveDeletedEvent(Guid SaveId, Guid? GameId, Guid UserId);

/// <summary>Raised when a player starts a game.</summary>
public sealed record PlaySessionStartedEvent(Guid SessionId, Guid GameId, Guid UserId, DateTime Start);

/// <summary>Raised when a play session ends. Times are UTC.</summary>
/// <param name="TimedOut">
/// The launcher stopped reporting in, so the server ended the session at the last time it heard from it.
/// </param>
public sealed record PlaySessionEndedEvent(Guid SessionId, Guid GameId, Guid UserId, DateTime Start, DateTime End, bool TimedOut);

/// <summary>Raised after a user reports an issue with a game.</summary>
public sealed record IssueCreatedEvent(Guid IssueId, Guid GameId, Guid? ReportedByUserId, string Description);

/// <summary>Raised after an issue is marked resolved.</summary>
public sealed record IssueResolvedEvent(Guid IssueId, Guid GameId, Guid ResolvedByUserId);

/// <summary>Raised after a multiplayer match is first recorded, by a reporter or through <see cref="IMatchStatisticsSink"/>.</summary>
public sealed record MatchCreatedEvent(Guid MatchId, Guid GameId, Guid? ServerId, string? Map, string? Mode);

/// <summary>
/// Raised after a match is ended with its results, or abandoned. Raised again if a reporter sends the
/// results again, so handlers should replace rather than add.
/// </summary>
/// <param name="Results">The final standings, best first. Empty for an abandoned match.</param>
public sealed record MatchEndedEvent(
    Guid MatchId,
    Guid GameId,
    Guid? ServerId,
    LANCommander.SDK.Enums.MatchStatus Status,
    string? Reason,
    IReadOnlyList<LANCommander.SDK.Models.MatchResultInformation> Results);

/// <summary>
/// Raised after a batch of a match's events is written. Batches are raised in order for each match,
/// off the request that reported them; a slow handler delays later notifications but not writing.
/// </summary>
public sealed record MatchEventsRecordedEvent(
    Guid MatchId,
    Guid GameId,
    IReadOnlyList<LANCommander.SDK.Models.MatchEventInformation> Events);

/// <summary>Raised when a match participant is linked to a LANCommander user.</summary>
public sealed record MatchParticipantLinkedEvent(
    Guid MatchId,
    Guid ParticipantId,
    string PlayerKey,
    Guid UserId,
    LANCommander.SDK.Enums.MatchParticipantLinkSource LinkSource);

/// <summary>
/// Raised when a game server's status changes, whichever engine runs it, including one a plugin adds.
/// </summary>
/// <param name="Error">Why the server failed, when <paramref name="Status"/> is <see cref="ServerEngineStatus.Error"/>.</param>
public sealed record ServerStatusChangedEvent(Guid ServerId, string? ServerName, ServerEngineStatus Status, string? Error);
