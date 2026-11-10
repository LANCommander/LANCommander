using LANCommander.SDK.Models;
using LANCommander.SDK.Plugins;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services.MatchStatistics;

/// <summary>
/// The only writer of match events. Takes batches from the queue and writes each in one short
/// transaction: one writer suits SQLite's single write lock and keeps every reporting request free of
/// database work.
/// </summary>
/// <remarks>
/// Per batch it drops events already written (same id) or clashing with a written sequence number,
/// adds participants for player keys it hasn't seen, and advances each reporter's last sequence and
/// gap count. Follow-up work (plugin notifications, linking participants) goes to
/// <see cref="MatchNotificationDispatcher"/>.
/// </remarks>
public sealed class MatchEventWriter(
    IMatchEventSource source,
    IDbContextFactory<DatabaseContext> contextFactory,
    MatchNotificationDispatcher dispatcher,
    MatchActivityFeed activityFeed,
    SettingsProvider<Settings.Settings> settingsProvider,
    ILogger<MatchEventWriter> logger) : BackgroundService
{
    private const int MaxAttempts = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            IReadOnlyList<QueuedMatchEvent> batch;

            try
            {
                var settings = settingsProvider.CurrentValue.Server.MatchStatistics;

                batch = await source.ReadBatchAsync(
                    Math.Max(1, settings.BatchSize),
                    TimeSpan.FromMilliseconds(Math.Max(0, settings.FlushInterval)),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            if (batch.Count > 0)
                await WriteWithRetryAsync(batch, stoppingToken);
        }

        // Write what was accepted before shutting down
        var remaining = source.TakeAll();

        if (remaining.Count > 0)
        {
            logger.LogInformation("Writing {Count} queued match events before shutdown", remaining.Count);

            foreach (var chunk in remaining.Chunk(Math.Max(1, settingsProvider.CurrentValue.Server.MatchStatistics.BatchSize)))
                await WriteWithRetryAsync(chunk, CancellationToken.None);
        }
    }

    private async Task WriteWithRetryAsync(IReadOnlyList<QueuedMatchEvent> batch, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await WriteAsync(batch, cancellationToken);

                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Shutting down mid-write: the drain after the loop retries without a token
                await WriteAsync(batch, CancellationToken.None);

                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                // Usually a participant or reporter written by a lifecycle request at the same time;
                // the next attempt re-reads what exists
                logger.LogWarning(ex, "Writing {Count} match events failed, retrying (attempt {Attempt})", batch.Count, attempt);

                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Dropped {Count} match events after {Attempts} attempts. Reporters can resend after their last sequence", batch.Count, MaxAttempts);
            }
        }
    }

    /// <summary>Writes one batch. Internal so tests can drive it without the hosted loop.</summary>
    internal async Task WriteAsync(IReadOnlyList<QueuedMatchEvent> batch, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        context.ChangeTracker.AutoDetectChangesEnabled = false;

        var now = DateTime.UtcNow;

        // Repeats within the batch: keep the first of each id
        var candidates = batch
            .GroupBy(e => e.Id)
            .Select(g => g.First())
            .ToList();

        var reporterIds = candidates.Select(e => e.ReporterId).Distinct().ToList();

        var reporters = await context.MatchReporters!
            .Where(r => reporterIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        // Events for a reporter that's gone (its match was deleted while they were queued)
        candidates.RemoveAll(e => !reporters.TryGetValue(e.ReporterId, out var reporter) || reporter.MatchId != e.MatchId);

        if (candidates.Count == 0)
            return;

        var eventIds = candidates.Select(e => e.Id).ToList();

        var written = (await context.MatchEvents!
            .Where(e => eventIds.Contains(e.Id))
            .Select(e => e.Id)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var sequences = candidates.Select(e => e.Sequence).Distinct().ToList();

        var writtenSequences = (await context.MatchEvents!
            .Where(e => reporterIds.Contains(e.ReporterId) && sequences.Contains(e.Sequence))
            .Select(e => new { e.ReporterId, e.Sequence })
            .ToListAsync(cancellationToken))
            .Select(e => (e.ReporterId, e.Sequence))
            .ToHashSet();

        var fresh = new List<QueuedMatchEvent>(candidates.Count);
        var batchSequences = new HashSet<(Guid, long)>();

        foreach (var queued in candidates)
        {
            if (written.Contains(queued.Id))
                continue;

            if (writtenSequences.Contains((queued.ReporterId, queued.Sequence)) || !batchSequences.Add((queued.ReporterId, queued.Sequence)))
            {
                logger.LogWarning("Dropped match event {EventId}: reporter {ReporterId} already sent sequence {Sequence} as another event", queued.Id, queued.ReporterId, queued.Sequence);

                continue;
            }

            fresh.Add(queued);
        }

        if (fresh.Count == 0)
            return;

        var newParticipants = await AddMissingParticipantsAsync(context, fresh, now, cancellationToken);

        context.MatchEvents!.AddRange(fresh.Select(e => new MatchEvent
        {
            Id = e.Id,
            MatchId = e.MatchId,
            ReporterId = e.ReporterId,
            Sequence = e.Sequence,
            Type = e.Type,
            GameTimeMilliseconds = e.GameTimeMilliseconds,
            Tick = e.Tick,
            ActorKey = e.ActorKey,
            SubjectKey = e.SubjectKey,
            Payload = e.Payload,
            ClientTime = e.ClientTime,
            ReceivedOn = e.ReceivedOn,
        }));

        foreach (var group in fresh.GroupBy(e => e.ReporterId))
        {
            var reporter = reporters[group.Key];
            var above = group.Where(e => e.Sequence > reporter.LastSequence).Select(e => e.Sequence).ToList();

            if (above.Count == 0)
                continue;

            var highest = above.Max();

            // Numbers skipped between the last written one and the new highest
            reporter.GapCount += Math.Max(0, highest - reporter.LastSequence - above.Count);
            reporter.LastSequence = highest;
            reporter.UpdatedOn = now;
        }

        var matchIds = fresh.Select(e => e.MatchId).Distinct().ToList();

        var matches = await context.Matches!
            .Where(m => matchIds.Contains(m.Id))
            .ToListAsync(cancellationToken);

        foreach (var match in matches)
            match.LastEventOn = now;

        context.ChangeTracker.DetectChanges();

        await context.SaveChangesAsync(cancellationToken);

        Notify(fresh, newParticipants);
        PublishActivity(fresh, matches);
    }

    /// <summary>Shows the written events on the Statistics page of any server someone is watching.</summary>
    private void PublishActivity(List<QueuedMatchEvent> written, List<Match> matches)
    {
        foreach (var serverMatches in matches.Where(m => activityFeed.IsWatched(m.ServerId)).GroupBy(m => m.ServerId))
        {
            var matchIds = serverMatches.Select(m => m.Id).ToHashSet();

            activityFeed.Publish(serverMatches.Key, written
                .Where(e => matchIds.Contains(e.MatchId))
                .OrderBy(e => e.ReceivedOn)
                .ThenBy(e => e.Sequence)
                .Select(MatchActivity.FromEvent)
                .ToList());
        }
    }

    private static async Task<List<Guid>> AddMissingParticipantsAsync(DatabaseContext context, List<QueuedMatchEvent> events, DateTime now, CancellationToken cancellationToken)
    {
        var referenced = events
            .SelectMany(e => new[] { (e.MatchId, Key: e.ActorKey), (e.MatchId, Key: e.SubjectKey) })
            .Where(r => !string.IsNullOrEmpty(r.Key))
            .Select(r => (r.MatchId, Key: r.Key!))
            .Distinct()
            .ToList();

        if (referenced.Count == 0)
            return [];

        var matchIds = referenced.Select(r => r.MatchId).Distinct().ToList();
        var keys = referenced.Select(r => r.Key).Distinct().ToList();

        var existing = (await context.MatchParticipants!
            .Where(p => matchIds.Contains(p.MatchId) && keys.Contains(p.PlayerKey))
            .Select(p => new { p.MatchId, p.PlayerKey })
            .ToListAsync(cancellationToken))
            .Select(p => (p.MatchId, p.PlayerKey))
            .ToHashSet();

        var added = new List<Guid>();

        foreach (var (matchId, key) in referenced)
        {
            if (existing.Contains((matchId, key)))
                continue;

            var participant = new MatchParticipant
            {
                Id = Guid.NewGuid(),
                MatchId = matchId,
                PlayerKey = key,
                DisplayName = PlayerKeys.DisplayNameOf(key),
                CreatedOn = now,
                UpdatedOn = now,
            };

            context.MatchParticipants!.Add(participant);
            added.Add(participant.Id);
        }

        return added;
    }

    private void Notify(List<QueuedMatchEvent> written, List<Guid> newParticipants)
    {
        if (newParticipants.Count > 0)
        {
            dispatcher.Post($"linking {newParticipants.Count} match participants", (services, cancellationToken) =>
                services.GetRequiredService<PlayerIdentityService>().ResolveAsync(newParticipants, cancellationToken));
        }

        foreach (var group in written.GroupBy(e => (e.MatchId, e.GameId)))
        {
            var (matchId, gameId) = group.Key;
            var events = group.ToList();

            // Payloads are parsed back into JSON here, off the writer
            dispatcher.Post($"{events.Count} events recorded for match {matchId}", (services, cancellationToken) =>
                services.GetRequiredService<IPluginEventBus>().PublishAsync(
                    new MatchEventsRecordedEvent(matchId, gameId, events
                        .OrderBy(e => e.GameTimeMilliseconds)
                        .ThenBy(e => e.Sequence)
                        .Select(ToInformation)
                        .ToList()),
                    cancellationToken));
        }
    }

    private static MatchEventInformation ToInformation(QueuedMatchEvent e) => new()
    {
        Id = e.Id,
        MatchId = e.MatchId,
        ReporterId = e.ReporterId,
        Sequence = e.Sequence,
        Type = e.Type,
        GameTimeMilliseconds = e.GameTimeMilliseconds,
        Tick = e.Tick,
        ActorKey = e.ActorKey,
        SubjectKey = e.SubjectKey,
        Payload = MatchJson.ToElement(e.Payload),
        ClientTime = e.ClientTime,
        ReceivedOn = e.ReceivedOn,
    };
}
