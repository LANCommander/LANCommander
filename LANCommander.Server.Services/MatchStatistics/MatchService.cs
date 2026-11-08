using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using LANCommander.SDK.Plugins;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services.MatchStatistics;

/// <summary>
/// The low-volume side of match reporting, done straight away: creating and ending matches, players
/// and results. Events are only validated here and handed to the queue.
/// </summary>
public sealed class MatchService(
    IDbContextFactory<DatabaseContext> contextFactory,
    IMatchEventQueue queue,
    PlayerIdentityService playerIdentityService,
    IPluginEventBus pluginEventBus,
    IFusionCache cache,
    SettingsProvider<Settings.Settings> settingsProvider,
    ILogger<MatchService> logger)
{
    public const int MaxTypeLength = 64;
    public const int MaxTeamLength = 64;
    public const int MaxStatisticKeyLength = 64;

    /// <summary>How long after a match ends an untrusted client may still send late events.</summary>
    private static readonly TimeSpan LateEventWindow = TimeSpan.FromMinutes(15);

    private static readonly TimeSpan ReporterCacheDuration = TimeSpan.FromSeconds(30);

    private Settings.Models.MatchStatisticsSettings Settings => settingsProvider.CurrentValue.Server.MatchStatistics;

    public bool Enabled => Settings.Enabled;

    /// <exception cref="MatchStatisticsDisabledException"/>
    /// <exception cref="ArgumentException">The request is missing something or has something too long.</exception>
    /// <exception cref="KeyNotFoundException">The game or server doesn't exist.</exception>
    public async Task<MatchRegistration> CreateAsync(MatchCreateRequest request, MatchReporterContext caller, CancellationToken cancellationToken = default)
    {
        EnsureEnabled();

        if (request.GameId == Guid.Empty)
            throw new ArgumentException("A game is required", nameof(request));

        if (!MatchJson.IsObjectOrEmpty(request.Settings) || !MatchJson.IsObjectOrEmpty(request.Metadata))
            throw new ArgumentException("Settings and metadata must be JSON objects", nameof(request));

        CheckLength(request.ExternalId, 191, "External id");
        CheckLength(request.Map, 255, "Map");
        CheckLength(request.Mode, 255, "Mode");
        CheckLength(request.GameVersion, 64, "Game version");
        CheckLength(request.ServerName, 255, "Server name");
        CheckLength(request.ServerAddress, 255, "Server address");
        CheckLength(request.ReporterName, 255, "Reporter name");

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        if (!await context.Games!.AnyAsync(g => g.Id == request.GameId, cancellationToken))
            throw new KeyNotFoundException($"Game {request.GameId} doesn't exist");

        if (request.ServerId is { } serverId && !await context.Servers!.AnyAsync(s => s.Id == serverId, cancellationToken))
            throw new KeyNotFoundException($"Server {serverId} doesn't exist");

        var externalId = string.IsNullOrWhiteSpace(request.ExternalId) ? null : request.ExternalId.Trim();
        var created = false;

        var match = externalId == null
            ? null
            : await context.Matches!.FirstOrDefaultAsync(m => m.GameId == request.GameId && m.ExternalId == externalId, cancellationToken);

        if (match == null)
        {
            var now = DateTime.UtcNow;

            match = new Match
            {
                GameId = request.GameId,
                ServerId = request.ServerId,
                ExternalId = externalId,
                Map = Clean(request.Map),
                Mode = Clean(request.Mode),
                GameVersion = Clean(request.GameVersion),
                ServerName = Clean(request.ServerName),
                ServerAddress = Clean(request.ServerAddress),
                StartedOn = request.StartedOn?.ToUniversalTime() ?? now,
                Settings = MatchJson.ToText(request.Settings),
                Metadata = MatchJson.ToText(request.Metadata),
                Status = MatchStatus.Open,
                CreatedOn = now,
                UpdatedOn = now,
                CreatedById = caller.UserId,
                UpdatedById = caller.UserId,
            };

            context.Matches!.Add(match);

            try
            {
                await context.SaveChangesAsync(cancellationToken);
                created = true;
            }
            catch (DbUpdateException) when (externalId != null)
            {
                // Another reporter created the same match at the same moment
                context.ChangeTracker.Clear();

                match = await context.Matches!.FirstAsync(m => m.GameId == request.GameId && m.ExternalId == externalId, cancellationToken);
            }
        }

        var reporter = await GetOrAddReporterAsync(context, match, request.ReporterKind, request.ReporterName, caller, cancellationToken);

        if (request.Participants.Count > 0)
            await UpsertParticipantsAsync(context, match, request.Participants, caller, cancellationToken);

        if (created)
        {
            logger.LogInformation("Match {MatchId} started for game {GameId}", match.Id, match.GameId);

            await pluginEventBus.PublishAsync(new MatchCreatedEvent(match.Id, match.GameId, match.ServerId, match.Map, match.Mode), cancellationToken);
        }

        return new MatchRegistration
        {
            MatchId = match.Id,
            ReporterId = reporter.Id,
            Created = created,
            LastSequence = reporter.LastSequence,
            Status = match.Status,
        };
    }

    /// <exception cref="KeyNotFoundException">The match doesn't exist.</exception>
    /// <exception cref="MatchAccessDeniedException">The caller isn't one of the match's reporters.</exception>
    public async Task UpsertParticipantsAsync(Guid matchId, IReadOnlyCollection<MatchParticipantSubmission> participants, MatchReporterContext caller, CancellationToken cancellationToken = default)
    {
        EnsureEnabled();

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var match = await GetMatchForReporterAsync(context, matchId, caller, cancellationToken);

        await UpsertParticipantsAsync(context, match, participants, caller, cancellationToken);
    }

    /// <summary>
    /// Validates a batch and queues the events that pass. Events that fail are returned as rejected and
    /// shouldn't be retried.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The match or reporter doesn't exist.</exception>
    /// <exception cref="MatchAccessDeniedException">The reporter isn't the caller's, or the match no longer takes the caller's events.</exception>
    /// <exception cref="MatchQueueFullException">There's no room for the batch; retry later.</exception>
    /// <exception cref="ArgumentException">The batch has more events than allowed.</exception>
    public async Task<MatchEventBatchResult> RecordAsync(Guid matchId, Guid reporterId, IReadOnlyList<MatchEventSubmission> events, MatchReporterContext caller, bool waitForRoom, CancellationToken cancellationToken = default)
    {
        EnsureEnabled();

        var settings = Settings;

        if (events.Count > Math.Max(1, settings.MaxEventsPerRequest))
            throw new ArgumentException($"At most {settings.MaxEventsPerRequest} events can be sent at once", nameof(events));

        var reporter = await GetReporterForEventsAsync(matchId, reporterId, caller, cancellationToken);

        var result = new MatchEventBatchResult();
        var accepted = new List<QueuedMatchEvent>(events.Count);
        var ids = new HashSet<Guid>();
        var sequences = new HashSet<long>();
        var maxPayload = Math.Max(1, settings.MaxPayloadSize) * 1024;
        var now = DateTime.UtcNow;

        foreach (var submitted in events)
        {
            var problem = Validate(submitted, maxPayload, out var code, out var payload);

            if (problem == null && (!ids.Add(submitted.Id) || !sequences.Add(submitted.Sequence)))
            {
                code = MatchEventRejectionCodes.Duplicate;
                problem = "the id or sequence appears more than once in the batch";
            }

            if (problem != null)
            {
                result.Rejected.Add(new MatchEventRejection { Id = submitted.Id, Code = code, Detail = problem });
                continue;
            }

            accepted.Add(new QueuedMatchEvent(
                submitted.Id,
                matchId,
                reporter.GameId,
                reporterId,
                submitted.Sequence,
                submitted.Type.Trim(),
                submitted.GameTimeMilliseconds,
                submitted.Tick,
                Clean(submitted.ActorKey),
                Clean(submitted.SubjectKey),
                payload,
                submitted.ClientTime?.ToUniversalTime(),
                now));
        }

        if (accepted.Count > 0)
        {
            if (waitForRoom)
                await queue.EnqueueAsync(accepted, cancellationToken);
            else if (!queue.TryEnqueue(accepted))
                throw new MatchQueueFullException();
        }

        result.Queued = accepted.Count;

        return result;
    }

    /// <summary>
    /// Ends a match with its standings and totals. Results replace any sent before; statistics replace
    /// the same key for the same player or team.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The match doesn't exist.</exception>
    /// <exception cref="MatchAccessDeniedException">
    /// The caller isn't a reporter of the match, or is an untrusted client and a trusted reporter is
    /// recording the match.
    /// </exception>
    /// <exception cref="ArgumentException">A result or statistic is invalid.</exception>
    public async Task EndAsync(Guid matchId, MatchEndRequest request, MatchReporterContext caller, CancellationToken cancellationToken = default)
    {
        EnsureEnabled();

        foreach (var result in request.Results)
        {
            if (string.IsNullOrWhiteSpace(result.PlayerKey) && string.IsNullOrWhiteSpace(result.Team))
                throw new ArgumentException("Each result needs a player key or a team", nameof(request));

            if (!MatchJson.IsObjectOrEmpty(result.Payload))
                throw new ArgumentException("A result's payload must be a JSON object", nameof(request));

            CheckLength(result.Team, MaxTeamLength, "Team");
        }

        foreach (var statistic in request.Statistics)
        {
            if (string.IsNullOrWhiteSpace(statistic.Key) || statistic.Key.Trim().Length > MaxStatisticKeyLength)
                throw new ArgumentException($"Each statistic needs a key of at most {MaxStatisticKeyLength} characters", nameof(request));

            if (string.IsNullOrWhiteSpace(statistic.PlayerKey) && string.IsNullOrWhiteSpace(statistic.Team))
                throw new ArgumentException("Each statistic needs a player key or a team", nameof(request));

            if (double.IsNaN(statistic.Value) || double.IsInfinity(statistic.Value))
                throw new ArgumentException($"Statistic {statistic.Key} isn't a finite number", nameof(request));

            CheckLength(statistic.Team, MaxTeamLength, "Team");
        }

        CheckLength(request.Reason, 64, "Reason");

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var match = await GetMatchForReporterAsync(context, matchId, caller, cancellationToken);

        if (!caller.IsTrusted && await context.MatchReporters!.AnyAsync(r => r.MatchId == matchId && r.Trust != MatchReporterTrust.Untrusted, cancellationToken))
            throw new MatchAccessDeniedException("A trusted reporter is recording this match, so only it can end it");

        // Players named only in the results or statistics are added as participants
        var mentioned = request.Results.Select(r => r.PlayerKey)
            .Concat(request.Statistics.Select(s => s.PlayerKey))
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k!.Trim())
            .Except(request.Participants.Where(p => !string.IsNullOrWhiteSpace(p.PlayerKey)).Select(p => p.PlayerKey.Trim()))
            .Select(k => new MatchParticipantSubmission { PlayerKey = k });

        var participants = await UpsertParticipantsAsync(context, match, request.Participants.Concat(mentioned).ToList(), caller, cancellationToken);

        // Removed through the change tracker rather than a bulk delete, so the replacement is one
        // SaveChanges and atomic without a user transaction (which retrying providers refuse)
        var now = DateTime.UtcNow;

        if (request.Results.Count > 0)
        {
            context.MatchResults!.RemoveRange(await context.MatchResults!.Where(r => r.MatchId == matchId).ToListAsync(cancellationToken));

            context.MatchResults!.AddRange(request.Results.Select(r => new MatchResult
            {
                MatchId = matchId,
                ParticipantId = ParticipantIdOf(participants, r.PlayerKey),
                Team = Clean(r.Team),
                Rank = r.Rank,
                Score = r.Score,
                Status = r.Status,
                TimeMilliseconds = r.TimeMilliseconds,
                Payload = MatchJson.ToText(r.Payload),
                CreatedOn = now,
                UpdatedOn = now,
                CreatedById = caller.UserId,
                UpdatedById = caller.UserId,
            }));
        }

        if (request.Statistics.Count > 0)
        {
            var existing = await context.MatchStatistics!.Where(s => s.MatchId == matchId).ToListAsync(cancellationToken);

            foreach (var submitted in request.Statistics)
            {
                var participantId = ParticipantIdOf(participants, submitted.PlayerKey);
                var team = participantId == null ? Clean(submitted.Team) : null;
                var key = submitted.Key.Trim();

                var statistic = existing.FirstOrDefault(s => s.Key == key && s.ParticipantId == participantId && s.Team == team);

                if (statistic == null)
                {
                    statistic = new MatchStatistic
                    {
                        Id = Guid.NewGuid(),
                        MatchId = matchId,
                        ParticipantId = participantId,
                        Team = team,
                        Key = key,
                    };

                    existing.Add(statistic);
                    context.MatchStatistics!.Add(statistic);
                }

                statistic.Value = submitted.Value;
            }
        }

        var abandoned = request.Abandoned || string.Equals(request.Reason, "aborted", StringComparison.OrdinalIgnoreCase);

        match.Status = abandoned ? MatchStatus.Abandoned : MatchStatus.Ended;
        match.EndedOn = request.EndedOn?.ToUniversalTime() ?? match.EndedOn ?? now;
        match.EndReason = Clean(request.Reason) ?? match.EndReason;
        match.UpdatedOn = now;
        match.UpdatedById = caller.UserId;

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Match {MatchId} {Status}", match.Id, match.Status);

        await PublishEndedAsync(context, match, cancellationToken);
    }

    /// <summary>The caller's reporter in a match, for resuming after a restart.</summary>
    /// <exception cref="KeyNotFoundException">The match or reporter doesn't exist.</exception>
    /// <exception cref="MatchAccessDeniedException">The reporter isn't the caller's.</exception>
    public async Task<MatchReporterInformation> GetReporterAsync(Guid matchId, Guid reporterId, MatchReporterContext caller, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var reporter = await context.MatchReporters!
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == reporterId && r.MatchId == matchId, cancellationToken)
            ?? throw new KeyNotFoundException($"Reporter {reporterId} isn't part of match {matchId}");

        if (!caller.IsAdministrator && !caller.Owns(reporter.UserId, reporter.PluginId))
            throw new MatchAccessDeniedException("The reporter belongs to someone else");

        return new MatchReporterInformation
        {
            Id = reporter.Id,
            Kind = reporter.Kind,
            Trust = reporter.Trust,
            Name = reporter.Name,
            UserId = reporter.UserId,
            PluginId = reporter.PluginId,
            LastSequence = reporter.LastSequence,
            GapCount = reporter.GapCount,
        };
    }

    /// <summary>Marks open matches that have stopped reporting as abandoned.</summary>
    public async Task<int> AbandonStaleAsync(TimeSpan idle, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow - idle;

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var stale = await context.Matches!
            .Where(m => m.Status == MatchStatus.Open && (m.LastEventOn ?? m.CreatedOn) < cutoff)
            .Take(500)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;

        foreach (var match in stale)
        {
            match.Status = MatchStatus.Abandoned;
            match.EndedOn ??= match.LastEventOn ?? match.CreatedOn;
            match.EndReason ??= "abandoned";
            match.UpdatedOn = now;
        }

        await context.SaveChangesAsync(cancellationToken);

        foreach (var match in stale)
        {
            logger.LogInformation("Match {MatchId} abandoned after {Minutes} minutes without events", match.Id, idle.TotalMinutes);

            await pluginEventBus.PublishAsync(new MatchEndedEvent(match.Id, match.GameId, match.ServerId, match.Status, match.EndReason, []), cancellationToken);
        }

        return stale.Count;
    }

    private void EnsureEnabled()
    {
        if (!Enabled)
            throw new MatchStatisticsDisabledException();
    }

    private static string? Validate(MatchEventSubmission submitted, int maxPayload, out string code, out string? payload)
    {
        code = MatchEventRejectionCodes.Invalid;
        payload = null;

        if (submitted.Id == Guid.Empty)
            return "id is required";

        if (submitted.Sequence < 1)
            return "sequence starts at 1";

        if (string.IsNullOrWhiteSpace(submitted.Type))
            return "type is required";

        if (submitted.Type.Trim().Length > MaxTypeLength)
            return $"type is at most {MaxTypeLength} characters";

        if (submitted.GameTimeMilliseconds < 0)
            return "gameTimeMilliseconds can't be negative";

        if (submitted.ActorKey?.Trim().Length > PlayerKeys.MaxLength || submitted.SubjectKey?.Trim().Length > PlayerKeys.MaxLength)
            return $"player keys are at most {PlayerKeys.MaxLength} characters";

        if (!MatchJson.IsObjectOrEmpty(submitted.Payload))
            return "payload must be a JSON object";

        payload = MatchJson.ToText(submitted.Payload);

        if (payload?.Length > maxPayload)
        {
            code = MatchEventRejectionCodes.PayloadTooLarge;
            payload = null;

            return $"payload is larger than {maxPayload / 1024} KB";
        }

        return null;
    }

    private sealed record CachedReporter(Guid MatchId, Guid GameId, Guid? UserId, string? PluginId, MatchStatus Status, DateTime? EndedOn);

    /// <summary>
    /// Checks the reporter is the caller's and the match still takes its events. Cached briefly, since
    /// a reporter sends a batch every few seconds.
    /// </summary>
    private async Task<CachedReporter> GetReporterForEventsAsync(Guid matchId, Guid reporterId, MatchReporterContext caller, CancellationToken cancellationToken)
    {
        var reporter = await cache.GetOrSetAsync<CachedReporter?>(
            $"Matches/Reporters/{reporterId}",
            async token =>
            {
                await using var context = await contextFactory.CreateDbContextAsync(token);

                return await context.MatchReporters!
                    .AsNoTracking()
                    .Where(r => r.Id == reporterId)
                    .Select(r => new CachedReporter(r.MatchId, r.Match!.GameId, r.UserId, r.PluginId, r.Match.Status, r.Match.EndedOn))
                    .FirstOrDefaultAsync(token);
            },
            options => options.SetDuration(ReporterCacheDuration),
            cancellationToken);

        if (reporter == null || reporter.MatchId != matchId)
            throw new KeyNotFoundException($"Reporter {reporterId} isn't part of match {matchId}");

        if (!caller.ActsForAnyReporter && !caller.Owns(reporter.UserId, reporter.PluginId))
            throw new MatchAccessDeniedException("The reporter belongs to someone else");

        // Trusted reporters may send late events whenever; clients only while the match is going
        if (!caller.IsTrusted)
        {
            var closed = reporter.Status == MatchStatus.Abandoned
                || (reporter.Status == MatchStatus.Ended && reporter.EndedOn < DateTime.UtcNow - LateEventWindow);

            if (closed)
                throw new MatchAccessDeniedException("The match has ended");
        }

        return reporter;
    }

    /// <summary>The match, if the caller reports it or may act on any match.</summary>
    internal static async Task<Match> GetMatchForReporterAsync(DatabaseContext context, Guid matchId, MatchReporterContext caller, CancellationToken cancellationToken)
    {
        var match = await context.Matches!.FirstOrDefaultAsync(m => m.Id == matchId, cancellationToken)
            ?? throw new KeyNotFoundException($"Match {matchId} doesn't exist");

        if (caller.IsAdministrator)
            return match;

        var reports = caller.PluginId != null
            ? await context.MatchReporters!.AnyAsync(r => r.MatchId == matchId && r.PluginId == caller.PluginId, cancellationToken)
            : await context.MatchReporters!.AnyAsync(r => r.MatchId == matchId && r.UserId == caller.UserId, cancellationToken);

        if (!reports)
            throw new MatchAccessDeniedException("Only the match's reporters can change it");

        return match;
    }

    private static async Task<MatchReporter> GetOrAddReporterAsync(DatabaseContext context, Match match, MatchReporterKind kind, string? name, MatchReporterContext caller, CancellationToken cancellationToken)
    {
        // A trusted kind can't be claimed by an untrusted caller
        if (caller.PluginId != null)
            kind = MatchReporterKind.Plugin;
        else if (kind == MatchReporterKind.Plugin)
            kind = MatchReporterKind.Server;

        name = Clean(name);

        var existing = await context.MatchReporters!
            .FirstOrDefaultAsync(r => r.MatchId == match.Id
                && r.UserId == caller.UserId
                && r.PluginId == caller.PluginId
                && r.Name == name, cancellationToken);

        if (existing != null)
            return existing;

        var now = DateTime.UtcNow;

        var reporter = new MatchReporter
        {
            MatchId = match.Id,
            Kind = kind,
            Trust = caller.IsTrusted
                ? MatchReporterTrust.Authoritative
                : kind == MatchReporterKind.Host ? MatchReporterTrust.SemiTrusted : MatchReporterTrust.Untrusted,
            UserId = caller.UserId,
            PluginId = caller.PluginId,
            Name = name,
            CreatedOn = now,
            UpdatedOn = now,
            CreatedById = caller.UserId,
            UpdatedById = caller.UserId,
        };

        context.MatchReporters!.Add(reporter);

        await context.SaveChangesAsync(cancellationToken);

        return reporter;
    }

    /// <summary>Adds or updates participants, returning every participant of the match by player key.</summary>
    private async Task<Dictionary<string, MatchParticipant>> UpsertParticipantsAsync(DatabaseContext context, Match match, IReadOnlyCollection<MatchParticipantSubmission> submitted, MatchReporterContext caller, CancellationToken cancellationToken)
    {
        foreach (var participant in submitted)
        {
            if (!PlayerKeys.IsValid(participant.PlayerKey, out var error))
                throw new ArgumentException(error, nameof(submitted));

            CheckLength(participant.DisplayName, 255, "Display name");
            CheckLength(participant.Team, MaxTeamLength, "Team");
        }

        var participants = (await context.MatchParticipants!
            .Where(p => p.MatchId == match.Id)
            .ToListAsync(cancellationToken))
            .ToDictionary(p => p.PlayerKey, StringComparer.Ordinal);

        if (submitted.Count == 0)
            return participants;

        var now = DateTime.UtcNow;
        var touched = new List<MatchParticipant>();

        foreach (var incoming in submitted)
        {
            var key = incoming.PlayerKey.Trim();

            if (!participants.TryGetValue(key, out var participant))
            {
                participant = new MatchParticipant
                {
                    MatchId = match.Id,
                    PlayerKey = key,
                    DisplayName = PlayerKeys.DisplayNameOf(key),
                    CreatedOn = now,
                    CreatedById = caller.UserId,
                };

                context.MatchParticipants!.Add(participant);
                participants[key] = participant;
            }

            participant.DisplayName = Clean(incoming.DisplayName) ?? participant.DisplayName;
            participant.Team = Clean(incoming.Team) ?? participant.Team;
            participant.JoinedAtMilliseconds = incoming.JoinedAtMilliseconds ?? participant.JoinedAtMilliseconds;
            participant.LeftAtMilliseconds = incoming.LeftAtMilliseconds ?? participant.LeftAtMilliseconds;
            participant.UpdatedOn = now;
            participant.UpdatedById = caller.UserId;

            touched.Add(participant);
        }

        await context.SaveChangesAsync(cancellationToken);

        // A user named by a trusted reporter wins over anything inferred
        foreach (var incoming in submitted.Where(p => p.UserId != null && caller.IsTrusted))
            await playerIdentityService.LinkReportedAsync(context, participants[incoming.PlayerKey.Trim()], incoming.UserId!.Value, cancellationToken);

        var unlinked = touched.Where(p => p.UserId == null).Select(p => p.Id).Distinct().ToList();

        if (unlinked.Count > 0)
            await playerIdentityService.ResolveAsync(unlinked, cancellationToken);

        return participants;
    }

    private async Task PublishEndedAsync(DatabaseContext context, Match match, CancellationToken cancellationToken)
    {
        var results = await context.MatchResults!
            .AsNoTracking()
            .Where(r => r.MatchId == match.Id)
            .OrderBy(r => r.Rank)
            .Select(r => new
            {
                r.ParticipantId,
                PlayerKey = r.Participant != null ? r.Participant.PlayerKey : null,
                UserId = r.Participant != null ? r.Participant.UserId : null,
                r.Team,
                r.Rank,
                r.Score,
                r.Status,
                r.TimeMilliseconds,
                r.Payload,
            })
            .ToListAsync(cancellationToken);

        var information = results.Select(r => new MatchResultInformation
        {
            ParticipantId = r.ParticipantId,
            PlayerKey = r.PlayerKey,
            UserId = r.UserId,
            Team = r.Team,
            Rank = r.Rank,
            Score = r.Score,
            Status = r.Status,
            TimeMilliseconds = r.TimeMilliseconds,
            Payload = MatchJson.ToElement(r.Payload),
        }).ToList();

        await pluginEventBus.PublishAsync(new MatchEndedEvent(match.Id, match.GameId, match.ServerId, match.Status, match.EndReason, information), cancellationToken);
    }

    private static Guid? ParticipantIdOf(Dictionary<string, MatchParticipant> participants, string? playerKey) =>
        !string.IsNullOrWhiteSpace(playerKey) && participants.TryGetValue(playerKey.Trim(), out var participant)
            ? participant.Id
            : null;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void CheckLength(string? value, int maximum, string what)
    {
        if (value?.Trim().Length > maximum)
            throw new ArgumentException($"{what} is at most {maximum} characters");
    }
}
