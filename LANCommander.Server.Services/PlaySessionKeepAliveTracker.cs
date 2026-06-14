using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services;

/// <summary>
/// Tracks the last time each active play session was heard from via the RPC keepalive. Backed by
/// <see cref="IFusionCache"/> so it works across instances when a distributed (e.g. Redis) backend
/// is configured. The sweep consults this to end sessions that have gone stale, ending them at the
/// last known-alive time so recorded playtime isn't inflated.
/// </summary>
public sealed class PlaySessionKeepAliveTracker(
    IFusionCache cache,
    SettingsProvider<Settings.Settings> settingsProvider)
{
    private static string GetCacheKey(Guid gameId, Guid userId) => $"PlaySessions/KeepAlive/{gameId}/{userId}";

    // Keep entries alive well past the staleness timeout so the sweep can still read the last
    // keepalive time when ending a stale session, rather than losing it to expiration.
    private TimeSpan EntryDuration =>
        TimeSpan.FromSeconds(Math.Max(1, settingsProvider.CurrentValue.Server.GameServers.KeepAliveTimeout) * 3);

    /// <summary>Records that the session for this game/user was heard from just now.</summary>
    public async Task TouchAsync(Guid gameId, Guid userId) =>
        await cache.SetAsync(GetCacheKey(gameId, userId), DateTime.UtcNow, EntryDuration);

    /// <summary>
    /// Returns the last-seen time for the session, seeding it with the current time if we have no
    /// record yet (e.g. a session that predates a restart with no distributed backend). Seeding
    /// grants a fresh grace window rather than immediately reaping a session we haven't observed yet.
    /// </summary>
    public async Task<DateTime> GetOrSeedAsync(Guid gameId, Guid userId)
    {
        var cacheKey = GetCacheKey(gameId, userId);

        var lastSeen = await cache.TryGetAsync<DateTime>(cacheKey);

        if (lastSeen.HasValue)
            return lastSeen.Value;

        var now = DateTime.UtcNow;

        await cache.SetAsync(cacheKey, now, EntryDuration);

        return now;
    }

    /// <summary>Stops tracking the session (it ended normally or was swept).</summary>
    public async Task RemoveAsync(Guid gameId, Guid userId) =>
        await cache.RemoveAsync(GetCacheKey(gameId, userId));
}
