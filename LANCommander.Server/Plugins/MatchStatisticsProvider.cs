using LANCommander.SDK.Models;
using LANCommander.Server.Services.MatchStatistics;

namespace LANCommander.Server.Plugins;

/// <summary>Gives plugins the recorded matches in the same shapes as the <c>/api/Matches</c> endpoints.</summary>
internal sealed class MatchStatisticsProvider(
    MatchQueryService queryService,
    MatchAttachmentService attachmentService,
    PlayerIdentityService identityService) : IMatchStatisticsProvider
{
    public Task<MatchPage<MatchSummary>> GetMatchesAsync(MatchQuery query, CancellationToken cancellationToken = default) =>
        queryService.GetMatchesAsync(query, cancellationToken);

    public Task<MatchDetail?> GetMatchAsync(Guid matchId, CancellationToken cancellationToken = default) =>
        queryService.GetMatchAsync(matchId, cancellationToken);

    public Task<MatchPage<MatchEventInformation>> GetEventsAsync(Guid matchId, MatchEventQuery query, CancellationToken cancellationToken = default) =>
        queryService.GetEventsAsync(matchId, query, cancellationToken);

    public Task<IReadOnlyList<MatchStatisticAggregate>> AggregateAsync(MatchStatisticQuery query, CancellationToken cancellationToken = default) =>
        queryService.AggregateAsync(query, cancellationToken);

    public Task<IReadOnlyList<PlayerIdentityInformation>> GetPlayerIdentitiesAsync(Guid userId, CancellationToken cancellationToken = default) =>
        identityService.GetForUserAsync(userId, cancellationToken);

    public async Task<Stream?> OpenAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default) =>
        (await attachmentService.OpenAsync(attachmentId, cancellationToken))?.Content;
}
