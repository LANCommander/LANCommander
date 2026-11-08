using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using LANCommander.Server.Services.MatchStatistics;

namespace LANCommander.Server.Plugins;

/// <summary>Lets plugins record matches through the same services as the HTTP API, as trusted reporters.</summary>
internal sealed class MatchStatisticsSink(
    MatchService matchService,
    MatchAttachmentService attachmentService,
    PlayerIdentityService identityService) : IMatchStatisticsSink
{
    public Task<MatchRegistration> CreateMatchAsync(string pluginId, MatchCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
            throw new ArgumentException("The plugin's id is required", nameof(pluginId));

        return matchService.CreateAsync(request, MatchReporterContext.ForPlugin(pluginId.Trim()), cancellationToken);
    }

    public Task UpsertParticipantsAsync(Guid matchId, IEnumerable<MatchParticipantSubmission> participants, CancellationToken cancellationToken = default) =>
        matchService.UpsertParticipantsAsync(matchId, participants.ToList(), MatchReporterContext.Host, cancellationToken);

    public Task<MatchEventBatchResult> RecordAsync(Guid matchId, Guid reporterId, IReadOnlyList<MatchEventSubmission> events, CancellationToken cancellationToken = default) =>
        matchService.RecordAsync(matchId, reporterId, events, MatchReporterContext.Host, waitForRoom: true, cancellationToken);

    public Task EndMatchAsync(Guid matchId, MatchEndRequest request, CancellationToken cancellationToken = default) =>
        matchService.EndAsync(matchId, request, MatchReporterContext.Host, cancellationToken);

    public Task<MatchAttachmentInformation> AddAttachmentAsync(Guid matchId, MatchAttachmentCreateRequest request, Stream content, CancellationToken cancellationToken = default) =>
        attachmentService.AddAsync(matchId, request, content, MatchReporterContext.Host, cancellationToken);

    public Task<PlayerIdentityInformation> LinkPlayerAsync(PlayerIdentitySubmission identity, CancellationToken cancellationToken = default) =>
        identityService.AddAsync(identity, PlayerIdentitySource.Plugin, null, cancellationToken);
}
