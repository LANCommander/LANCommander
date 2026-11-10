using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Server.Services.MatchStatistics;

/// <summary>
/// Reads recorded matches for the <c>/api/Matches</c> endpoints and for plugins. Everything returned is
/// the SDK's shapes, so the database schema stays internal.
/// </summary>
public sealed class MatchQueryService(IDbContextFactory<DatabaseContext> contextFactory)
{
    public const int MaxMatchPageSize = 500;
    public const int MaxEventPageSize = 5000;
    public const int MaxAggregateLimit = 1000;

    public async Task<MatchPage<MatchSummary>> GetMatchesAsync(MatchQuery query, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var matches = Filter(context.Matches!.AsNoTracking(), query.GameId, query.ServerId, query.Map, query.Mode, query.From, query.To);

        if (query.Status is { } status)
            matches = matches.Where(m => m.Status == status);

        if (query.UserId is { } userId)
            matches = matches.Where(m => m.Participants!.Any(p => p.UserId == userId));

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxMatchPageSize);

        var total = await matches.CountAsync(cancellationToken);

        var items = await matches
            .OrderByDescending(m => m.StartedOn ?? m.CreatedOn)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(SummaryProjection)
            .ToListAsync(cancellationToken);

        return new MatchPage<MatchSummary> { Items = items, Page = page, PageSize = pageSize, Total = total };
    }

    /// <summary>
    /// A server's latest match activity, oldest first: its recent matches starting and ending, and the
    /// last <paramref name="count"/> events written for them.
    /// </summary>
    public async Task<IReadOnlyList<MatchActivity>> GetRecentActivityAsync(Guid serverId, int count, CancellationToken cancellationToken = default)
    {
        const int recentMatches = 25;

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var matches = await context.Matches!
            .AsNoTracking()
            .Where(m => m.ServerId == serverId)
            .OrderByDescending(m => m.StartedOn ?? m.CreatedOn)
            .Take(recentMatches)
            .ToListAsync(cancellationToken);

        if (matches.Count == 0)
            return [];

        var matchIds = matches.Select(m => m.Id).ToList();

        var events = await context.MatchEvents!
            .AsNoTracking()
            .Where(e => matchIds.Contains(e.MatchId))
            .OrderByDescending(e => e.ReceivedOn)
            .ThenByDescending(e => e.Sequence)
            .Take(Math.Max(1, count))
            .ToListAsync(cancellationToken);

        // Back to written order first: events of one batch share a time, and the sort below is stable
        var activity = Enumerable.Reverse(events).Select(MatchActivity.FromEvent)
            .Concat(matches.Select(MatchActivity.Started))
            .Concat(matches.Where(m => m.Status != MatchStatus.Open).Select(MatchActivity.Ended))
            .ToList();

        // Match lines older than the oldest event shown would only pad the top
        if (events.Count >= count)
        {
            var since = events[^1].ReceivedOn;

            activity.RemoveAll(a => a.Kind != MatchActivityKind.Event && a.Timestamp < since);
        }

        return activity
            .OrderBy(a => a.Timestamp)
            .ThenBy(a => a.Kind == MatchActivityKind.Started ? 0 : a.Kind == MatchActivityKind.Event ? 1 : 2)
            .ToList();
    }

    public async Task<MatchDetail?> GetMatchAsync(Guid matchId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var summary = await context.Matches!
            .AsNoTracking()
            .Where(m => m.Id == matchId)
            .Select(SummaryProjection)
            .FirstOrDefaultAsync(cancellationToken);

        if (summary == null)
            return null;

        var json = await context.Matches!
            .AsNoTracking()
            .Where(m => m.Id == matchId)
            .Select(m => new { m.Settings, m.Metadata })
            .FirstAsync(cancellationToken);

        var detail = new MatchDetail
        {
            Settings = MatchJson.ToElement(json.Settings),
            Metadata = MatchJson.ToElement(json.Metadata),
        };

        CopySummary(summary, detail);

        detail.Reporters = await context.MatchReporters!
            .AsNoTracking()
            .Where(r => r.MatchId == matchId)
            .OrderBy(r => r.CreatedOn)
            .Select(r => new MatchReporterInformation
            {
                Id = r.Id,
                Kind = r.Kind,
                Trust = r.Trust,
                Name = r.Name,
                UserId = r.UserId,
                PluginId = r.PluginId,
                LastSequence = r.LastSequence,
                GapCount = r.GapCount,
            })
            .ToListAsync(cancellationToken);

        detail.Participants = await context.MatchParticipants!
            .AsNoTracking()
            .Where(p => p.MatchId == matchId)
            .OrderBy(p => p.Team).ThenBy(p => p.DisplayName)
            .Select(p => new MatchParticipantInformation
            {
                Id = p.Id,
                PlayerKey = p.PlayerKey,
                DisplayName = p.DisplayName,
                Team = p.Team,
                UserId = p.UserId,
                UserName = p.User != null ? (p.User.Alias ?? p.User.UserName) : null,
                LinkSource = p.LinkSource,
                JoinedAtMilliseconds = p.JoinedAtMilliseconds,
                LeftAtMilliseconds = p.LeftAtMilliseconds,
            })
            .ToListAsync(cancellationToken);

        var results = await context.MatchResults!
            .AsNoTracking()
            .Where(r => r.MatchId == matchId)
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

        detail.Results = results.Select(r => new MatchResultInformation
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

        detail.Statistics = await context.MatchStatistics!
            .AsNoTracking()
            .Where(s => s.MatchId == matchId)
            .OrderBy(s => s.Key)
            .Select(s => new MatchStatisticInformation
            {
                ParticipantId = s.ParticipantId,
                PlayerKey = s.Participant != null ? s.Participant.PlayerKey : null,
                UserId = s.Participant != null ? s.Participant.UserId : null,
                Team = s.Team ?? (s.Participant != null ? s.Participant.Team : null),
                Key = s.Key,
                Value = s.Value,
            })
            .ToListAsync(cancellationToken);

        detail.Attachments = await context.MatchAttachments!
            .AsNoTracking()
            .IgnoreAutoIncludes()
            .Where(a => a.MatchId == matchId)
            .OrderBy(a => a.CreatedOn)
            .Select(AttachmentProjection)
            .ToListAsync(cancellationToken);

        return detail;
    }

    /// <exception cref="KeyNotFoundException">The match doesn't exist.</exception>
    public async Task<MatchPage<MatchEventInformation>> GetEventsAsync(Guid matchId, MatchEventQuery query, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        if (!await context.Matches!.AnyAsync(m => m.Id == matchId, cancellationToken))
            throw new KeyNotFoundException($"Match {matchId} doesn't exist");

        var events = context.MatchEvents!.AsNoTracking().Where(e => e.MatchId == matchId);

        if (!string.IsNullOrWhiteSpace(query.Type))
            events = events.Where(e => e.Type == query.Type.Trim());

        if (!string.IsNullOrWhiteSpace(query.ActorKey))
            events = events.Where(e => e.ActorKey == query.ActorKey.Trim());

        if (!string.IsNullOrWhiteSpace(query.SubjectKey))
            events = events.Where(e => e.SubjectKey == query.SubjectKey.Trim());

        if (query.AfterGameTimeMilliseconds is { } after)
            events = events.Where(e => e.GameTimeMilliseconds > after);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, MaxEventPageSize);

        var total = await events.CountAsync(cancellationToken);

        var rows = await events
            .OrderBy(e => e.GameTimeMilliseconds)
            .ThenBy(e => e.ReceivedOn)
            .ThenBy(e => e.Sequence)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new MatchPage<MatchEventInformation>
        {
            Items = rows.Select(e => new MatchEventInformation
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
            }).ToList(),
            Page = page,
            PageSize = pageSize,
            Total = total,
        };
    }

    /// <exception cref="ArgumentException">The game or key is missing.</exception>
    public async Task<IReadOnlyList<MatchStatisticAggregate>> AggregateAsync(MatchStatisticQuery query, CancellationToken cancellationToken = default)
    {
        if (query.GameId == Guid.Empty || string.IsNullOrWhiteSpace(query.Key))
            throw new ArgumentException("A game and a statistic key are required", nameof(query));

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var key = query.Key.Trim();
        var limit = Math.Clamp(query.Limit, 1, MaxAggregateLimit);

        var matches = Filter(context.Matches!.AsNoTracking(), query.GameId, query.ServerId, query.Map, query.Mode, query.From, query.To);

        if (query.ExcludeAbandoned)
            matches = matches.Where(m => m.Status != MatchStatus.Abandoned);

        var statistics = context.MatchStatistics!
            .AsNoTracking()
            .Where(s => s.Key == key && s.ParticipantId != null && matches.Any(m => m.Id == s.MatchId));

        if (query.GroupBy == MatchStatisticGrouping.User)
        {
            var rows = await Order(Aggregate(statistics
                        .Where(s => s.Participant!.UserId != null)
                        .GroupBy(s => s.Participant!.UserId!.Value), query.Aggregation), query.Descending)
                .Take(limit)
                .ToListAsync(cancellationToken);

            var userIds = rows.Select(r => r.Key).ToList();

            var names = await context.Users!
                .AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, Name = u.Alias ?? u.UserName })
                .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);

            return rows.Select(r => new MatchStatisticAggregate
            {
                UserId = r.Key,
                UserName = names.GetValueOrDefault(r.Key),
                Value = r.Value,
                MatchCount = r.MatchCount,
            }).ToList();
        }
        else
        {
            var rows = await Order(Aggregate(statistics.GroupBy(s => s.Participant!.PlayerKey), query.Aggregation), query.Descending)
                .Take(limit)
                .ToListAsync(cancellationToken);

            var playerKeys = rows.Select(r => r.Key).ToList();

            // The most recent name and user each key was seen with
            var seen = (await context.MatchParticipants!
                .AsNoTracking()
                .Where(p => playerKeys.Contains(p.PlayerKey) && p.Match!.GameId == query.GameId)
                .Select(p => new { p.PlayerKey, p.DisplayName, p.UserId, UserName = p.User != null ? (p.User.Alias ?? p.User.UserName) : null, p.CreatedOn })
                .ToListAsync(cancellationToken))
                .GroupBy(p => p.PlayerKey)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.CreatedOn).First());

            return rows.Select(r =>
            {
                seen.TryGetValue(r.Key, out var latest);

                return new MatchStatisticAggregate
                {
                    PlayerKey = r.Key,
                    DisplayName = latest?.DisplayName,
                    UserId = latest?.UserId,
                    UserName = latest?.UserName,
                    Value = r.Value,
                    MatchCount = r.MatchCount,
                };
            }).ToList();
        }
    }

    public async Task<MatchAttachmentInformation?> GetAttachmentAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.MatchAttachments!
            .AsNoTracking()
            .IgnoreAutoIncludes()
            .Where(a => a.Id == attachmentId)
            .Select(AttachmentProjection)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private sealed class AggregateRow<TKey>
    {
        public TKey Key { get; init; } = default!;
        public double Value { get; init; }
        public int MatchCount { get; init; }
    }

    private static IQueryable<AggregateRow<TKey>> Aggregate<TKey>(IQueryable<IGrouping<TKey, MatchStatistic>> groups, MatchStatisticAggregation aggregation) =>
        aggregation switch
        {
            MatchStatisticAggregation.Average => groups.Select(g => new AggregateRow<TKey> { Key = g.Key, Value = g.Average(s => s.Value), MatchCount = g.Count() }),
            MatchStatisticAggregation.Minimum => groups.Select(g => new AggregateRow<TKey> { Key = g.Key, Value = g.Min(s => s.Value), MatchCount = g.Count() }),
            MatchStatisticAggregation.Maximum => groups.Select(g => new AggregateRow<TKey> { Key = g.Key, Value = g.Max(s => s.Value), MatchCount = g.Count() }),
            MatchStatisticAggregation.Count => groups.Select(g => new AggregateRow<TKey> { Key = g.Key, Value = g.Count(), MatchCount = g.Count() }),
            _ => groups.Select(g => new AggregateRow<TKey> { Key = g.Key, Value = g.Sum(s => s.Value), MatchCount = g.Count() }),
        };

    private static IQueryable<AggregateRow<TKey>> Order<TKey>(IQueryable<AggregateRow<TKey>> rows, bool descending) =>
        descending ? rows.OrderByDescending(r => r.Value) : rows.OrderBy(r => r.Value);

    private static IQueryable<Match> Filter(IQueryable<Match> matches, Guid? gameId, Guid? serverId, string? map, string? mode, DateTime? from, DateTime? to)
    {
        if (gameId is { } game)
            matches = matches.Where(m => m.GameId == game);

        if (serverId is { } server)
            matches = matches.Where(m => m.ServerId == server);

        if (!string.IsNullOrWhiteSpace(map))
            matches = matches.Where(m => m.Map == map.Trim());

        if (!string.IsNullOrWhiteSpace(mode))
            matches = matches.Where(m => m.Mode == mode.Trim());

        if (from is { } start)
        {
            var utc = start.ToUniversalTime();
            matches = matches.Where(m => (m.StartedOn ?? m.CreatedOn) >= utc);
        }

        if (to is { } end)
        {
            var utc = end.ToUniversalTime();
            matches = matches.Where(m => (m.StartedOn ?? m.CreatedOn) < utc);
        }

        return matches;
    }

    private static readonly System.Linq.Expressions.Expression<Func<Match, MatchSummary>> SummaryProjection = m => new MatchSummary
    {
        Id = m.Id,
        GameId = m.GameId,
        GameTitle = m.Game!.Title,
        ServerId = m.ServerId,
        ServerName = m.Server != null ? m.Server.Name : m.ServerName,
        ServerAddress = m.ServerAddress,
        ExternalId = m.ExternalId,
        Map = m.Map,
        Mode = m.Mode,
        GameVersion = m.GameVersion,
        Status = m.Status,
        StartedOn = m.StartedOn,
        EndedOn = m.EndedOn,
        EndReason = m.EndReason,
        ParticipantCount = m.Participants!.Count(),
        CreatedOn = m.CreatedOn,
    };

    private static readonly System.Linq.Expressions.Expression<Func<MatchAttachment, MatchAttachmentInformation>> AttachmentProjection = a => new MatchAttachmentInformation
    {
        Id = a.Id,
        MatchId = a.MatchId,
        ReporterId = a.ReporterId,
        Kind = a.Kind,
        FileName = a.FileName,
        ContentType = a.ContentType,
        Size = a.Size,
        Sha256 = a.Sha256,
        Status = a.Status,
        StartMilliseconds = a.StartMilliseconds,
        EndMilliseconds = a.EndMilliseconds,
        UploadedOn = a.UploadedOn,
        ExpiresOn = a.ExpiresOn,
    };

    private static void CopySummary(MatchSummary source, MatchSummary target)
    {
        target.Id = source.Id;
        target.GameId = source.GameId;
        target.GameTitle = source.GameTitle;
        target.ServerId = source.ServerId;
        target.ServerName = source.ServerName;
        target.ServerAddress = source.ServerAddress;
        target.ExternalId = source.ExternalId;
        target.Map = source.Map;
        target.Mode = source.Mode;
        target.GameVersion = source.GameVersion;
        target.Status = source.Status;
        target.StartedOn = source.StartedOn;
        target.EndedOn = source.EndedOn;
        target.EndReason = source.EndReason;
        target.ParticipantCount = source.ParticipantCount;
        target.CreatedOn = source.CreatedOn;
    }
}
