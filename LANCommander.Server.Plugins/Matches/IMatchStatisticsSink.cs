using LANCommander.SDK.Models;

namespace LANCommander.Server.Plugins;

/// <summary>
/// Records multiplayer matches from inside the server, without going through HTTP: a plugin that
/// parses a game server's log, polls RCON or receives a game's own reports writes through this.
/// Provided by the server; inject it into a scoped plugin service.
/// </summary>
/// <remarks>
/// The plugin is the match's reporter and is trusted: player user ids it supplies are kept as given.
/// Events go through the same queue as reported ones, so <see cref="RecordAsync"/> returns before they
/// are written; subscribe to <see cref="MatchEventsRecordedEvent"/> to see them land.
/// </remarks>
public interface IMatchStatisticsSink
{
    /// <summary>
    /// Starts a match, or returns the one already recorded for the game under
    /// <see cref="MatchCreateRequest.ExternalId"/>, with the plugin registered as a reporter.
    /// </summary>
    /// <param name="pluginId">The calling plugin's <c>IPlugin.Id</c>, recorded as the reporter.</param>
    /// <exception cref="KeyNotFoundException">The game or server does not exist.</exception>
    /// <exception cref="InvalidOperationException">Match statistics are turned off.</exception>
    Task<MatchRegistration> CreateMatchAsync(string pluginId, MatchCreateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Adds players to a match, or updates the name, team and times of ones already in it.</summary>
    /// <exception cref="KeyNotFoundException">The match does not exist.</exception>
    Task UpsertParticipantsAsync(Guid matchId, IEnumerable<MatchParticipantSubmission> participants, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queues events to be written. Waits while the queue is full rather than failing. Events with an
    /// id already written are dropped, so retrying is safe.
    /// </summary>
    /// <param name="reporterId">The <see cref="MatchRegistration.ReporterId"/> from <see cref="CreateMatchAsync"/>.</param>
    /// <exception cref="KeyNotFoundException">The match or reporter does not exist.</exception>
    Task<MatchEventBatchResult> RecordAsync(Guid matchId, Guid reporterId, IReadOnlyList<MatchEventSubmission> events, CancellationToken cancellationToken = default);

    /// <summary>Ends a match with its standings and totals, replacing any sent before.</summary>
    /// <exception cref="KeyNotFoundException">The match does not exist.</exception>
    Task EndMatchAsync(Guid matchId, MatchEndRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a file with a match. Reads <paramref name="content"/> from its current position to the end
    /// without disposing it.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The match does not exist.</exception>
    /// <exception cref="InvalidDataException">The content doesn't match the declared size or hash.</exception>
    Task<MatchAttachmentInformation> AddAttachmentAsync(Guid matchId, MatchAttachmentCreateRequest request, Stream content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Links an in-game id to a user, so participants with that player key are linked to them, including
    /// ones already recorded.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The user or game does not exist.</exception>
    /// <exception cref="InvalidOperationException">The id is already linked to another user.</exception>
    Task<PlayerIdentityInformation> LinkPlayerAsync(PlayerIdentitySubmission identity, CancellationToken cancellationToken = default);
}
