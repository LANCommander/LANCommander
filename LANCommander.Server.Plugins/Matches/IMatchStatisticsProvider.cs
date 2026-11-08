using LANCommander.SDK.Models;

namespace LANCommander.Server.Plugins;

/// <summary>
/// Reads the multiplayer matches the server has recorded: for a leaderboard, a tournament bracket or
/// anything else built on match results. Provided by the server; inject it into a scoped plugin
/// service or page. Returns the same shapes as the <c>/api/Matches</c> endpoints.
/// Like the record providers, this is a trusted host service, not an authorization boundary.
/// </summary>
public interface IMatchStatisticsProvider
{
    /// <summary>Matches, newest first.</summary>
    Task<MatchPage<MatchSummary>> GetMatchesAsync(MatchQuery query, CancellationToken cancellationToken = default);

    /// <summary>A match with its reporters, participants, results, statistics and attachments, or null.</summary>
    Task<MatchDetail?> GetMatchAsync(Guid matchId, CancellationToken cancellationToken = default);

    /// <summary>A match's events in game-time order.</summary>
    /// <exception cref="KeyNotFoundException">The match does not exist.</exception>
    Task<MatchPage<MatchEventInformation>> GetEventsAsync(Guid matchId, MatchEventQuery query, CancellationToken cancellationToken = default);

    /// <summary>One statistic aggregated across a game's matches, best first: a leaderboard.</summary>
    Task<IReadOnlyList<MatchStatisticAggregate>> AggregateAsync(MatchStatisticQuery query, CancellationToken cancellationToken = default);

    /// <summary>The in-game ids linked to a user.</summary>
    Task<IReadOnlyList<PlayerIdentityInformation>> GetPlayerIdentitiesAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens an uploaded attachment's file. The caller owns and must dispose the stream. Null when the
    /// attachment doesn't exist or its upload hasn't completed.
    /// </summary>
    Task<Stream?> OpenAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default);
}
