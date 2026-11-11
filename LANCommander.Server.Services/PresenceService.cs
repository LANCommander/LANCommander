using LANCommander.SDK.Enums;
using LANCommander.SDK.Plugins;
using LANCommander.Server.Data;
using LANCommander.Server.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;
using PresenceActivity = LANCommander.SDK.Models.PresenceActivity;
using UserPresence = LANCommander.SDK.Models.UserPresence;

namespace LANCommander.Server.Services;

/// <summary>
/// Sends presence changes to connected clients. The server's SignalR layer implements it, so
/// this project needs no hub of its own.
/// </summary>
public interface IPresenceBroadcaster
{
    /// <summary>Tells everyone a player's presence changed.</summary>
    Task ChangedAsync(UserPresence presence);

    /// <summary>Tells one user about a presence change, after what everyone was told.</summary>
    Task ChangedAsync(Guid recipientId, UserPresence presence);

    /// <summary>Tells everyone a player is no longer shown as playing.</summary>
    Task EndedAsync(Guid userId);
}

/// <summary>
/// Who is playing what. An active play session says a player is in a game, so a game without
/// the Interposer still shows; the activity the game reports through the launcher adds to it.
/// Everyone signed in can see everyone else, as far as each player's
/// <see cref="PresenceVisibility"/> allows. A player always sees their own presence in full.
/// </summary>
/// <remarks>
/// Activity lives in the cache, never the database: it means nothing once the session ends.
/// Visibility is a user custom field, so it lasts and needs no schema of its own.
/// </remarks>
public sealed class PresenceService(
    IFusionCache cache,
    IDbContextFactory<DatabaseContext> contextFactory,
    IServiceScopeFactory scopeFactory,
    IPresenceBroadcaster broadcaster,
    IPluginEventBus eventBus,
    ILogger<PresenceService> logger) : IHostedService
{
    public const string VisibilityFieldName = "PresenceVisibility";

    internal const int MaxTextLength = 128;
    internal const int MaxUrlLength = 1024;

    // Long enough to outlast any session; the session's end removes it sooner.
    private static readonly TimeSpan ActivityDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan VisibilityDuration = TimeSpan.FromHours(1);

    private readonly List<IDisposable> _subscriptions = new();

    private static string ActivityKey(Guid userId) => $"Presence/Activity/{userId}";
    private static string VisibilityKey(Guid userId) => $"Presence/Visibility/{userId}";

    private sealed record CachedActivity(Guid GameId, PresenceActivity Activity);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscriptions.Add(eventBus.Subscribe<PlaySessionStartedEvent>((e, _) => OnSessionChangedAsync(e.UserId)));
        _subscriptions.Add(eventBus.Subscribe<PlaySessionEndedEvent>((e, _) => OnSessionEndedAsync(e.UserId)));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var subscription in _subscriptions)
            subscription.Dispose();

        _subscriptions.Clear();

        return Task.CompletedTask;
    }

    /// <summary>Everyone in a game that <paramref name="viewerId"/> may see, themselves included.</summary>
    public async Task<IReadOnlyList<UserPresence>> GetAllAsync(Guid viewerId)
    {
        var sessions = await GetActiveSessionsAsync(null);
        var result = new List<UserPresence>();

        foreach (var session in sessions)
        {
            var presence = await ForViewerAsync(session, viewerId);

            if (presence != null)
                result.Add(presence);
        }

        return result;
    }

    /// <summary>
    /// Records what a player is doing in their game and tells everyone. Ignored unless the player
    /// has an active session in that game: a report can't make anyone look like they are playing.
    /// </summary>
    public async Task UpdateActivityAsync(Guid userId, Guid gameId, PresenceActivity? activity)
    {
        var session = (await GetActiveSessionsAsync(userId)).FirstOrDefault(s => s.GameId == gameId);

        if (session == null)
        {
            logger.LogDebug("Ignoring presence from {UserId} for {GameId}: no active session", userId, gameId);
            return;
        }

        activity = Sanitize(activity);

        if (activity == null)
            await cache.RemoveAsync(ActivityKey(userId));
        else
            await cache.SetAsync(ActivityKey(userId), new CachedActivity(gameId, activity), ActivityDuration);

        await PublishAsync(WithActivity(session, activity));
    }

    public async Task<PresenceVisibility> GetVisibilityAsync(Guid userId)
    {
        return await cache.GetOrSetAsync<PresenceVisibility>(VisibilityKey(userId), async _ =>
        {
            using var scope = scopeFactory.CreateScope();

            var customFields = scope.ServiceProvider.GetRequiredService<UserCustomFieldService>();
            var field = await customFields.GetAsync(userId, VisibilityFieldName);

            return Enum.TryParse<PresenceVisibility>(field?.Value, out var visibility)
                ? visibility
                : PresenceVisibility.Visible;
        }, VisibilityDuration);
    }

    /// <summary>Sets a player's visibility, and shows or hides what they are playing at once.</summary>
    public async Task SetVisibilityAsync(Guid userId, PresenceVisibility visibility)
    {
        using (var scope = scopeFactory.CreateScope())
        {
            var customFields = scope.ServiceProvider.GetRequiredService<UserCustomFieldService>();

            // The default is no field at all
            if (visibility == PresenceVisibility.Visible)
                await customFields.DeleteAsync(userId, VisibilityFieldName);
            else
                await customFields.UpdateAsync(userId, VisibilityFieldName, visibility.ToString());
        }

        await cache.SetAsync(VisibilityKey(userId), visibility, VisibilityDuration);

        await OnSessionChangedAsync(userId);
    }

    private async Task OnSessionChangedAsync(Guid userId)
    {
        try
        {
            var session = (await GetActiveSessionsAsync(userId)).FirstOrDefault();

            if (session == null)
            {
                await broadcaster.EndedAsync(userId);
                await eventBus.PublishAsync(new PresenceChangedEvent(userId, null));
                return;
            }

            await PublishAsync(session);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not publish presence for {UserId}", userId);
        }
    }

    private async Task OnSessionEndedAsync(Guid userId)
    {
        try
        {
            await cache.RemoveAsync(ActivityKey(userId));

            // Starting a game ends the one before it, so look for one still running
            await OnSessionChangedAsync(userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not publish the end of {UserId}'s presence", userId);
        }
    }

    // Everyone gets what the player's visibility allows; then the player, who may see more.
    // SignalR keeps each connection's messages in order, so the player's own copy lands last.
    private async Task PublishAsync(UserPresence presence)
    {
        var visibility = await GetVisibilityAsync(presence.UserId);
        var shared = Restrict(presence, visibility);

        if (shared == null)
            await broadcaster.EndedAsync(presence.UserId);
        else
            await broadcaster.ChangedAsync(shared);

        if (visibility != PresenceVisibility.Visible)
            await broadcaster.ChangedAsync(presence.UserId, presence);

        await eventBus.PublishAsync(new PresenceChangedEvent(presence.UserId, shared));
    }

    private async Task<UserPresence?> ForViewerAsync(UserPresence presence, Guid viewerId)
    {
        if (presence.UserId == viewerId)
            return presence;

        return Restrict(presence, await GetVisibilityAsync(presence.UserId));
    }

    internal static UserPresence? Restrict(UserPresence presence, PresenceVisibility visibility) => visibility switch
    {
        PresenceVisibility.Hidden => null,
        PresenceVisibility.GameOnly => WithActivity(presence, null),
        _ => presence,
    };

    private static UserPresence WithActivity(UserPresence presence, PresenceActivity? activity) => new()
    {
        UserId = presence.UserId,
        UserName = presence.UserName,
        GameId = presence.GameId,
        GameTitle = presence.GameTitle,
        IconMediaId = presence.IconMediaId,
        StartedOn = presence.StartedOn,
        Activity = activity,
    };

    /// <summary>
    /// The active session of one user, or of everyone, with the activity each has reported. A
    /// session for a game since deleted has nothing to show.
    /// </summary>
    private async Task<IReadOnlyList<UserPresence>> GetActiveSessionsAsync(Guid? userId)
    {
        await using var context = await contextFactory.CreateDbContextAsync();

        var query = context.PlaySessions!
            .AsNoTracking()
            .Where(ps => ps.End == null && ps.GameId != null);

        if (userId.HasValue)
            query = query.Where(ps => ps.UserId == userId.Value);

        var rows = await query
            .Select(ps => new
            {
                ps.UserId,
                GameId = ps.GameId!.Value,
                ps.Start,
                ps.CreatedOn,
                ps.User!.UserName,
                ps.User.Alias,
                ps.Game!.Title,
                IconMediaId = ps.Game.Media!
                    .Where(m => m.Type == MediaType.Icon)
                    .Select(m => (Guid?)m.Id)
                    .FirstOrDefault(),
            })
            .ToListAsync();

        var sessions = new List<UserPresence>();

        // One per player: the newest, should a stale one linger
        foreach (var row in rows.GroupBy(r => r.UserId).Select(g => g.OrderByDescending(r => r.Start ?? r.CreatedOn).First()))
        {
            var cached = await cache.TryGetAsync<CachedActivity>(ActivityKey(row.UserId));

            sessions.Add(new UserPresence
            {
                UserId = row.UserId,
                UserName = String.IsNullOrWhiteSpace(row.Alias) ? row.UserName : row.Alias,
                GameId = row.GameId,
                GameTitle = row.Title,
                IconMediaId = row.IconMediaId,
                StartedOn = DateTime.SpecifyKind(row.Start ?? row.CreatedOn, DateTimeKind.Utc),
                Activity = cached.HasValue && cached.Value.GameId == row.GameId ? cached.Value.Activity : null,
            });
        }

        return sessions;
    }

    /// <summary>
    /// What a client reported, trimmed to what is safe to pass on: bounded text, and images
    /// only as <c>http(s)</c> URLs. Null when nothing is left.
    /// </summary>
    internal static PresenceActivity? Sanitize(PresenceActivity? activity)
    {
        if (activity == null)
            return null;

        var clean = new PresenceActivity
        {
            Details = Text(activity.Details),
            State = Text(activity.State),
            StartsOn = activity.StartsOn,
            EndsOn = activity.EndsOn,
            PartySize = Math.Clamp(activity.PartySize, 0, 9999),
            PartyMax = Math.Clamp(activity.PartyMax, 0, 9999),
            LargeImageUrl = Url(activity.LargeImageUrl),
            LargeImageText = Text(activity.LargeImageText),
            SmallImageUrl = Url(activity.SmallImageUrl),
            SmallImageText = Text(activity.SmallImageText),
        };

        if (clean.PartyMax > 0 && clean.PartySize > clean.PartyMax)
            clean.PartySize = clean.PartyMax;

        return clean.HasContent ? clean : null;

        static string? Text(string? value)
        {
            value = value?.Trim();

            if (String.IsNullOrEmpty(value))
                return null;

            return value.Length > MaxTextLength ? value[..MaxTextLength] : value;
        }

        static string? Url(string? value)
        {
            value = value?.Trim();

            if (String.IsNullOrEmpty(value) || value.Length > MaxUrlLength)
                return null;

            return Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
                ? uri.ToString()
                : null;
        }
    }
}
