using System.Diagnostics;
using LANCommander.SDK;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Enums;
using LANCommander.Server.Services.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services;

/// <summary>
/// Aggregates for the admin dashboard: players and sessions, game servers, archive storage and
/// per-title playtime. All times are UTC (play sessions are stored in UTC).
/// </summary>
public sealed class DashboardService(
    ILogger<DashboardService> logger,
    IDbContextFactory<DatabaseContext> contextFactory,
    ServerManager serverManager)
{
    private static readonly Lazy<DateTime> ProcessStartTime = new(() =>
    {
        try
        {
            using var process = Process.GetCurrentProcess();

            return process.StartTime.ToUniversalTime();
        }
        catch
        {
            return DateTime.UtcNow;
        }
    });

    /// <summary>When the LANCommander server process started, in UTC.</summary>
    public static DateTime ServerStartTime => ProcessStartTime.Value;

    /// <summary>How long the LANCommander server process has been running.</summary>
    public static TimeSpan ServerUptime => DateTime.UtcNow - ServerStartTime;

    public async Task<DashboardStats> GetStatsAsync(TimeSpan range, CancellationToken cancellationToken = default)
    {
        if (range <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(range), "The range must be positive");

        var now = DateTime.UtcNow;
        var rangeStart = now - range;
        var previousRangeStart = rangeStart - range;

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var sessions = context.Set<PlaySession>().AsNoTracking();

        var playersOnline = await sessions
            .Where(ps => ps.Start != null && ps.End == null)
            .Select(ps => ps.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        // Players who were mid-session at the start of the range
        var playersAtRangeStart = await sessions
            .Where(ps => ps.Start != null && ps.Start <= rangeStart && (ps.End == null || ps.End > rangeStart))
            .Select(ps => ps.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        var sessionsInRange = await sessions
            .CountAsync(ps => ps.Start != null && ps.Start >= rangeStart && ps.Start <= now, cancellationToken);

        var sessionsInPreviousRange = await sessions
            .CountAsync(ps => ps.Start != null && ps.Start >= previousRangeStart && ps.Start < rangeStart, cancellationToken);

        var serverIds = await context.Set<Data.Models.Server>()
            .AsNoTracking()
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var serversRunning = 0;

        foreach (var serverId in serverIds)
        {
            try
            {
                if (await serverManager.GetStatusAsync(serverId) == ServerProcessStatus.Running)
                    serversRunning++;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Could not get the status of server {ServerId}", serverId);
            }
        }

        var storage = await GetStorageAsync(context, cancellationToken);

        return new DashboardStats(
            range,
            playersOnline,
            playersOnline - playersAtRangeStart,
            sessionsInRange,
            sessionsInRange - sessionsInPreviousRange,
            serversRunning,
            serverIds.Count,
            storage,
            Environment.MachineName,
            ServerStartTime);
    }

    /// <summary>
    /// Total play time per title within the range, longest first. Sessions straddling the range
    /// start are clipped to it; sessions still open count up to now.
    /// </summary>
    public async Task<IReadOnlyList<DashboardTitlePlaytime>> GetPlaytimeByTitleAsync(TimeSpan range, int top = 10, CancellationToken cancellationToken = default)
    {
        if (range <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(range), "The range must be positive");

        var now = DateTime.UtcNow;
        var rangeStart = now - range;

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var sessions = await context.Set<PlaySession>()
            .AsNoTracking()
            .Where(ps => ps.GameId != null && ps.Start != null && ps.Start < now && (ps.End == null || ps.End > rangeStart))
            .Select(ps => new { GameId = ps.GameId!.Value, Start = ps.Start!.Value, ps.End })
            .ToListAsync(cancellationToken);

        var totals = sessions
            .GroupBy(s => s.GameId)
            .Select(g => new
            {
                GameId = g.Key,
                Ticks = g.Sum(s => ClipDuration(s.Start, s.End ?? now, rangeStart, now).Ticks),
            })
            .Where(t => t.Ticks > 0)
            .OrderByDescending(t => t.Ticks)
            .Take(Math.Max(0, top))
            .ToList();

        var gameIds = totals.Select(t => t.GameId).ToList();

        var titles = await context.Set<Game>()
            .AsNoTracking()
            .Where(g => gameIds.Contains(g.Id))
            .Select(g => new { g.Id, g.Title })
            .ToDictionaryAsync(g => g.Id, g => g.Title, cancellationToken);

        return totals
            .Select(t => new DashboardTitlePlaytime(
                t.GameId,
                titles.TryGetValue(t.GameId, out var title) ? title : "Unknown game",
                TimeSpan.FromTicks(t.Ticks)))
            .ToList();
    }

    /// <summary>Play sessions that haven't ended, most recently started first.</summary>
    public async Task<IReadOnlyList<DashboardActiveSession>> GetActiveSessionsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var sessions = await context.Set<PlaySession>()
            .AsNoTracking()
            .Where(ps => ps.Start != null && ps.End == null)
            .OrderByDescending(ps => ps.Start)
            .Select(ps => new
            {
                ps.Id,
                ps.UserId,
                ps.GameId,
                Start = ps.Start!.Value,
            })
            .ToListAsync(cancellationToken);

        var userIds = sessions.Select(s => s.UserId).Distinct().ToList();
        var gameIds = sessions.Where(s => s.GameId.HasValue).Select(s => s.GameId!.Value).Distinct().ToList();

        // Loaded separately rather than through navigations so sessions whose user or game has
        // gone missing still show up
        var users = await context.Set<User>()
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.UserName, u.Alias })
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var games = await context.Set<Game>()
            .AsNoTracking()
            .Where(g => gameIds.Contains(g.Id))
            .Select(g => new { g.Id, g.Title })
            .ToDictionaryAsync(g => g.Id, g => g.Title, cancellationToken);

        return sessions
            .Select(s =>
            {
                users.TryGetValue(s.UserId, out var user);

                var userName = user?.UserName ?? "Unknown user";
                var alias = user?.Alias;

                string? gameTitle = null;

                if (s.GameId.HasValue)
                    games.TryGetValue(s.GameId.Value, out gameTitle);

                return new DashboardActiveSession(
                    s.Id,
                    s.UserId,
                    userName,
                    alias,
                    String.IsNullOrWhiteSpace(alias) ? userName : alias,
                    s.GameId,
                    gameTitle,
                    s.Start);
            })
            .ToList();
    }

    /// <summary>The part of [start, end) that falls within [rangeStart, rangeEnd).</summary>
    public static TimeSpan ClipDuration(DateTime start, DateTime end, DateTime rangeStart, DateTime rangeEnd)
    {
        var clippedStart = start > rangeStart ? start : rangeStart;
        var clippedEnd = end < rangeEnd ? end : rangeEnd;

        return clippedEnd > clippedStart ? clippedEnd - clippedStart : TimeSpan.Zero;
    }

    private async Task<DashboardStorage> GetStorageAsync(DatabaseContext context, CancellationToken cancellationToken)
    {
        var used = await context.Set<Archive>()
            .AsNoTracking()
            .SumAsync(a => (long?)a.CompressedSize, cancellationToken) ?? 0;

        var locationPaths = await context.Set<StorageLocation>()
            .AsNoTracking()
            .Where(sl => sl.Type == StorageLocationType.Archive)
            .Select(sl => sl.Path)
            .ToListAsync(cancellationToken);

        var (capacity, free) = GetDriveCapacity(locationPaths, logger);

        return new DashboardStorage(used, capacity, free);
    }

    /// <summary>
    /// Total and free bytes across the drives that hold the given storage location paths, each
    /// drive counted once. A path maps to the mounted drive with the longest matching root, so
    /// this also works for Linux mount points. Null when no drive could be read.
    /// </summary>
    public static (long? Capacity, long? Free) GetDriveCapacity(IEnumerable<string> storageLocationPaths, ILogger? logger = null)
    {
        DriveInfo[] drives;

        try
        {
            drives = DriveInfo.GetDrives();
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Could not enumerate drives");

            return (null, null);
        }

        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var seen = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        long? capacity = null;
        long? free = null;

        foreach (var storageLocationPath in storageLocationPaths.Where(p => !String.IsNullOrWhiteSpace(p)))
        {
            try
            {
                var fullPath = Path.GetFullPath(AppPaths.ResolveStorageLocationPath(storageLocationPath));

                var drive = drives
                    .Where(d => fullPath.StartsWith(d.RootDirectory.FullName, comparison))
                    .OrderByDescending(d => d.RootDirectory.FullName.Length)
                    .FirstOrDefault();

                if (drive == null || !seen.Add(drive.Name) || !drive.IsReady)
                    continue;

                capacity = (capacity ?? 0) + drive.TotalSize;
                free = (free ?? 0) + drive.AvailableFreeSpace;
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "Could not read drive capacity for storage location {Path}", storageLocationPath);
            }
        }

        return (capacity, free);
    }
}
