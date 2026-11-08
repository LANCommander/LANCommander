using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using LANCommander.SDK.Plugins;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services.MatchStatistics;

/// <summary>
/// Links match participants to LANCommander users. A participant is linked, in order, by a user a
/// trusted reporter named, by a known player identity (a Steam ID, a name used in a game), or by the
/// user having been in the game, on the match's server, under the participant's name. The last
/// records the name as an inferred identity so later matches link directly.
/// </summary>
public sealed class PlayerIdentityService(
    IDbContextFactory<DatabaseContext> contextFactory,
    IPluginEventBus pluginEventBus,
    ILogger<PlayerIdentityService> logger)
{
    public async Task<IReadOnlyList<PlayerIdentityInformation>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var identities = await context.PlayerIdentities!
            .AsNoTracking()
            .Where(i => i.UserId == userId)
            .OrderBy(i => i.Kind).ThenBy(i => i.Value)
            .ToListAsync(cancellationToken);

        return identities.Select(ToInformation).ToList();
    }

    /// <summary>
    /// Links an in-game id to a user and links participants already recorded under it.
    /// Adding an id the user already has returns it.
    /// </summary>
    /// <exception cref="ArgumentException">The player key isn't valid.</exception>
    /// <exception cref="KeyNotFoundException">The user or game doesn't exist.</exception>
    /// <exception cref="InvalidOperationException">The id is already linked to another user.</exception>
    public async Task<PlayerIdentityInformation> AddAsync(PlayerIdentitySubmission submission, PlayerIdentitySource source, Guid? createdById, CancellationToken cancellationToken = default)
    {
        if (!PlayerKeys.IsValid(submission.PlayerKey, out var error))
            throw new ArgumentException(error, nameof(submission));

        var (kind, value) = PlayerKeys.Normalize(submission.PlayerKey);

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        if (!await context.Users!.AnyAsync(u => u.Id == submission.UserId, cancellationToken))
            throw new KeyNotFoundException($"User {submission.UserId} doesn't exist");

        if (submission.GameId is { } gameId && !await context.Games!.AnyAsync(g => g.Id == gameId, cancellationToken))
            throw new KeyNotFoundException($"Game {gameId} doesn't exist");

        // Checked here as well as by the unique index, which doesn't stop duplicates where GameId is null
        var existing = await context.PlayerIdentities!
            .FirstOrDefaultAsync(i => i.GameId == submission.GameId && i.Kind == kind && i.Value == value, cancellationToken);

        if (existing != null)
        {
            if (existing.UserId != submission.UserId)
                throw new InvalidOperationException($"{PlayerKeys.Format(kind, value)} is already linked to another user");

            return ToInformation(existing);
        }

        var now = DateTime.UtcNow;

        var identity = new PlayerIdentity
        {
            UserId = submission.UserId,
            GameId = submission.GameId,
            Kind = kind,
            Value = value,
            Source = source,
            CreatedOn = now,
            UpdatedOn = now,
            CreatedById = createdById,
            UpdatedById = createdById,
        };

        context.PlayerIdentities!.Add(identity);

        await context.SaveChangesAsync(cancellationToken);

        await LinkRecordedAsync(context, identity, cancellationToken);

        return ToInformation(identity);
    }

    /// <summary>Removes an identity. Participants already linked through it stay linked.</summary>
    public async Task<bool> DeleteAsync(Guid identityId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var identity = await context.PlayerIdentities!.FirstOrDefaultAsync(i => i.Id == identityId, cancellationToken);

        if (identity == null)
            return false;

        context.PlayerIdentities!.Remove(identity);

        await context.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <summary>Tries to link each of the participants that isn't linked yet.</summary>
    public async Task ResolveAsync(IReadOnlyCollection<Guid> participantIds, CancellationToken cancellationToken = default)
    {
        if (participantIds.Count == 0)
            return;

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var ids = participantIds.ToList();

        var participants = await context.MatchParticipants!
            .Include(p => p.Match)
            .Where(p => ids.Contains(p.Id) && p.UserId == null)
            .ToListAsync(cancellationToken);

        foreach (var byMatch in participants.GroupBy(p => p.MatchId))
        {
            var match = byMatch.First().Match!;
            List<SessionCandidate>? sessions = null;

            foreach (var participant in byMatch)
            {
                var (kind, value) = PlayerKeys.Normalize(participant.PlayerKey);

                var identity = await context.PlayerIdentities!
                    .AsNoTracking()
                    .Where(i => i.Kind == kind && i.Value == value && (i.GameId == match.GameId || i.GameId == null))
                    .OrderBy(i => i.GameId == null)
                    .FirstOrDefaultAsync(cancellationToken);

                if (identity != null)
                {
                    await LinkAsync(context, participant, identity.UserId, MatchParticipantLinkSource.Identity, cancellationToken);
                    continue;
                }

                var name = participant.DisplayName?.Trim();

                if (string.IsNullOrEmpty(name) && kind == PlayerKeys.NameKind)
                    name = value;

                if (string.IsNullOrEmpty(name))
                    continue;

                sessions ??= await GetSessionsAsync(context, match, cancellationToken);

                var userId = PickSessionUser(sessions, match.ServerId, name);

                if (userId == null)
                    continue;

                await LinkAsync(context, participant, userId.Value, MatchParticipantLinkSource.Session, cancellationToken);
                await InferIdentityAsync(context, userId.Value, match.GameId, name, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Tries again to link the unlinked participants of a game's open matches, after a player's
    /// session learns which server they joined.
    /// </summary>
    public async Task ResolveOpenMatchesAsync(Guid gameId, Guid? serverId, CancellationToken cancellationToken = default)
    {
        List<Guid> ids;

        await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
        {
            ids = await context.MatchParticipants!
                .Where(p => p.UserId == null
                    && p.Match!.GameId == gameId
                    && p.Match.Status == MatchStatus.Open
                    && (serverId == null || p.Match.ServerId == serverId || p.Match.ServerId == null))
                .Select(p => p.Id)
                .Take(1000)
                .ToListAsync(cancellationToken);
        }

        await ResolveAsync(ids, cancellationToken);
    }

    /// <summary>
    /// Tries again to link participants added since <paramref name="since"/>, in case a session or
    /// identity they needed arrived after them, or their linking was skipped under load.
    /// </summary>
    public async Task ResolveRecentAsync(DateTime since, CancellationToken cancellationToken = default)
    {
        List<Guid> ids;

        await using (var context = await contextFactory.CreateDbContextAsync(cancellationToken))
        {
            ids = await context.MatchParticipants!
                .Where(p => p.UserId == null && p.CreatedOn >= since)
                .OrderByDescending(p => p.CreatedOn)
                .Select(p => p.Id)
                .Take(2000)
                .ToListAsync(cancellationToken);
        }

        await ResolveAsync(ids, cancellationToken);
    }

    /// <summary>Links a participant to the user a trusted reporter named, if the user exists.</summary>
    internal async Task<bool> LinkReportedAsync(DatabaseContext context, MatchParticipant participant, Guid userId, CancellationToken cancellationToken)
    {
        if (participant.UserId == userId)
            return true;

        if (!await context.Users!.AnyAsync(u => u.Id == userId, cancellationToken))
        {
            logger.LogDebug("Ignored reported user {UserId} for participant {PlayerKey}: no such user", userId, participant.PlayerKey);
            return false;
        }

        await LinkAsync(context, participant, userId, MatchParticipantLinkSource.Reported, cancellationToken);

        return true;
    }

    private sealed record SessionCandidate(Guid UserId, Guid? ServerId, string Alias);

    private static async Task<List<SessionCandidate>> GetSessionsAsync(DatabaseContext context, Match match, CancellationToken cancellationToken)
    {
        // Sessions that overlap the match, with some slack for launchers that report late
        var start = (match.StartedOn ?? match.CreatedOn).AddMinutes(-5);
        var end = match.EndedOn ?? DateTime.UtcNow;

        return await context.PlaySessions!
            .AsNoTracking()
            .Where(ps => ps.GameId == match.GameId
                && ps.Alias != null
                && ps.Start <= end
                && (ps.End == null || ps.End >= start))
            .Select(ps => new SessionCandidate(ps.UserId, ps.ServerId, ps.Alias!))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// The one user who played under <paramref name="name"/> during the match. Sessions known to be on
    /// the match's server are preferred; sessions known to be on another server never count. Two
    /// users with the name means no link.
    /// </summary>
    internal static Guid? PickSessionUser(IEnumerable<(Guid UserId, Guid? ServerId, string Alias)> sessions, Guid? matchServerId, string name)
    {
        var named = sessions
            .Where(s => string.Equals(s.Alias.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matchServerId != null)
        {
            var onServer = named.Where(s => s.ServerId == matchServerId).ToList();

            named = onServer.Count > 0 ? onServer : named.Where(s => s.ServerId == null).ToList();
        }

        var users = named.Select(s => s.UserId).Distinct().ToList();

        return users.Count == 1 ? users[0] : null;
    }

    private static Guid? PickSessionUser(List<SessionCandidate> sessions, Guid? matchServerId, string name) =>
        PickSessionUser(sessions.Select(s => (s.UserId, s.ServerId, s.Alias)), matchServerId, name);

    private async Task InferIdentityAsync(DatabaseContext context, Guid userId, Guid gameId, string name, CancellationToken cancellationToken)
    {
        var value = name.Trim().ToLowerInvariant();

        if (value.Length > PlayerKeys.MaxValueLength)
            return;

        var taken = await context.PlayerIdentities!
            .AnyAsync(i => i.GameId == gameId && i.Kind == PlayerKeys.NameKind && i.Value == value, cancellationToken);

        if (taken)
            return;

        var now = DateTime.UtcNow;

        context.PlayerIdentities!.Add(new PlayerIdentity
        {
            UserId = userId,
            GameId = gameId,
            Kind = PlayerKeys.NameKind,
            Value = value,
            Source = PlayerIdentitySource.Inferred,
            CreatedOn = now,
            UpdatedOn = now,
        });

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Another link inferred the same name at the same time
            logger.LogDebug(ex, "Didn't record inferred identity {Name} for game {GameId}", value, gameId);
            context.ChangeTracker.Clear();
        }
    }

    /// <summary>Links participants recorded before the identity was added.</summary>
    private async Task LinkRecordedAsync(DatabaseContext context, PlayerIdentity identity, CancellationToken cancellationToken)
    {
        var key = PlayerKeys.Format(identity.Kind, identity.Value);

        IQueryable<MatchParticipant> query = context.MatchParticipants!
            .Where(p => p.UserId == null);

        if (identity.GameId is { } gameId)
            query = query.Where(p => p.Match!.GameId == gameId);

        query = identity.Kind == PlayerKeys.NameKind
            // Names are matched in any case, with or without the kind
            ? query.Where(p => p.PlayerKey.ToLower() == key || p.PlayerKey.ToLower() == identity.Value)
            : query.Where(p => p.PlayerKey == key);

        var participants = await query.Take(10_000).ToListAsync(cancellationToken);

        foreach (var participant in participants)
            await LinkAsync(context, participant, identity.UserId, MatchParticipantLinkSource.Identity, cancellationToken);
    }

    private async Task LinkAsync(DatabaseContext context, MatchParticipant participant, Guid userId, MatchParticipantLinkSource source, CancellationToken cancellationToken)
    {
        participant.UserId = userId;
        participant.LinkSource = source;
        participant.UpdatedOn = DateTime.UtcNow;

        context.ChangeTracker.DetectChanges();

        await context.SaveChangesAsync(cancellationToken);

        await pluginEventBus.PublishAsync(new MatchParticipantLinkedEvent(participant.MatchId, participant.Id, participant.PlayerKey, userId, source), cancellationToken);
    }

    private static PlayerIdentityInformation ToInformation(PlayerIdentity identity) => new()
    {
        Id = identity.Id,
        UserId = identity.UserId,
        GameId = identity.GameId,
        PlayerKey = PlayerKeys.Format(identity.Kind, identity.Value),
        Source = identity.Source,
        CreatedOn = identity.CreatedOn,
    };
}
