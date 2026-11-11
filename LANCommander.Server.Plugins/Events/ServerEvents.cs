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
