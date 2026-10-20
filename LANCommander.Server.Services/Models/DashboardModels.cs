namespace LANCommander.Server.Services.Models;

/// <summary>Headline numbers for the dashboard's statistic tiles.</summary>
/// <param name="Range">The window the range-based figures cover, ending now.</param>
/// <param name="PlayersOnline">Distinct users with an open play session (no end time).</param>
/// <param name="PlayersOnlineDelta">Change against the number of distinct users that were in a session at the start of the range.</param>
/// <param name="Sessions">Play sessions started within the range.</param>
/// <param name="SessionsDelta">Change against the number started in the previous range of the same length.</param>
/// <param name="ServersRunning">Game servers currently reporting Running.</param>
/// <param name="ServersTotal">All configured game servers.</param>
/// <param name="Storage">Archive storage usage.</param>
/// <param name="HostName">Machine name of the host running LANCommander.</param>
/// <param name="ServerStartTime">When the LANCommander process started, in UTC.</param>
public sealed record DashboardStats(
    TimeSpan Range,
    int PlayersOnline,
    int PlayersOnlineDelta,
    int Sessions,
    int SessionsDelta,
    int ServersRunning,
    int ServersTotal,
    DashboardStorage Storage,
    string HostName,
    DateTime ServerStartTime)
{
    /// <summary>How long the LANCommander process has been running.</summary>
    public TimeSpan ServerUptime => DateTime.UtcNow - ServerStartTime;
}

/// <summary>
/// Archive storage usage. <see cref="UsedBytes"/> is the sum of archive compressed sizes;
/// capacity and free space come from the drives backing the local archive storage locations
/// (each drive counted once) and are null if none of them could be read.
/// </summary>
public sealed record DashboardStorage(long UsedBytes, long? CapacityBytes, long? FreeBytes)
{
    /// <summary>Used archive bytes as a fraction (0–1) of capacity; null without a capacity.</summary>
    public double? UsedFraction => CapacityBytes is > 0 ? Math.Clamp((double)UsedBytes / CapacityBytes.Value, 0d, 1d) : null;
}

/// <summary>Total play time for a title within a range.</summary>
public sealed record DashboardTitlePlaytime(Guid GameId, string Title, TimeSpan Playtime);

/// <summary>A play session that hasn't ended yet.</summary>
/// <param name="DisplayName">Alias if set, otherwise the user name.</param>
public sealed record DashboardActiveSession(
    Guid SessionId,
    Guid UserId,
    string UserName,
    string? Alias,
    string DisplayName,
    Guid? GameId,
    string? GameTitle,
    DateTime Start)
{
    public TimeSpan Duration => DateTime.UtcNow - Start;
}
