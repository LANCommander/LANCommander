using LANCommander.Server.Services;
using LANCommander.Server.Services.Models;
using Shouldly;
using DataArchive = LANCommander.Server.Data.Models.Archive;
using DataPlaySession = LANCommander.Server.Data.Models.PlaySession;
using DataUser = LANCommander.Server.Data.Models.User;

namespace LANCommander.Server.Tests.Services;

/// <summary>
/// The in-memory database is shared across the run, so these tests compare aggregates before and
/// after seeding their own rows instead of asserting absolute totals.
/// </summary>
[Collection("Application")]
public class DashboardServiceTests(ApplicationFixture fixture) : DalTest(fixture)
{
    private static readonly TimeSpan Day = TimeSpan.FromHours(24);

    private async Task<DataUser> AddUserAsync(string? alias = null) =>
        await GetService<UserService>().AddAsync(
            new DataUser { UserName = Unique("player"), Alias = alias },
            bypassPasswordPolicy: true,
            password: "Test1234!");

    private Task<DataPlaySession> AddSessionAsync(Guid userId, Guid gameId, DateTime start, DateTime? end) =>
        GetService<PlaySessionService>().AddAsync(new DataPlaySession
        {
            UserId = userId,
            GameId = gameId,
            Start = start,
            End = end,
        });

    [Fact]
    public async Task StatsReflectSeededSessionsServersAndArchives()
    {
        var dashboard = GetService<DashboardService>();
        var before = await dashboard.GetStatsAsync(Day);

        var now = DateTime.UtcNow;
        var game = await AddGameAsync();
        var online = await AddUserAsync();
        var offline = await AddUserAsync();

        // Online now, started within the range
        await AddSessionAsync(online.Id, game.Id, now.AddMinutes(-5), null);
        // Finished within the range
        await AddSessionAsync(offline.Id, game.Id, now.AddHours(-2), now.AddHours(-1));
        // Started in the previous range
        await AddSessionAsync(offline.Id, game.Id, now.AddHours(-30), now.AddHours(-29));

        await AddServerAsync();

        var storageLocation = await GetStorageLocationAsync();

        await GetService<ArchiveService>().AddAsync(new DataArchive
        {
            GameId = game.Id,
            ObjectKey = Unique("object"),
            Version = "1.0",
            StorageLocationId = storageLocation.Id,
            CompressedSize = 123_456,
        });

        var after = await dashboard.GetStatsAsync(Day);

        after.Range.ShouldBe(Day);
        (after.PlayersOnline - before.PlayersOnline).ShouldBe(1);
        // The online player wasn't in a session when the range began
        (after.PlayersOnlineDelta - before.PlayersOnlineDelta).ShouldBe(1);
        (after.Sessions - before.Sessions).ShouldBe(2);
        (after.SessionsDelta - before.SessionsDelta).ShouldBe(1);
        (after.ServersTotal - before.ServersTotal).ShouldBe(1);
        after.ServersRunning.ShouldBeLessThanOrEqualTo(after.ServersTotal);
        (after.Storage.UsedBytes - before.Storage.UsedBytes).ShouldBe(123_456);
        after.HostName.ShouldBe(Environment.MachineName);
        after.ServerStartTime.ShouldBeLessThanOrEqualTo(DateTime.UtcNow);
        after.ServerUptime.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public async Task PlaytimeByTitleClipsSessionsToTheRange()
    {
        var now = DateTime.UtcNow;
        var game = await AddGameAsync();
        var user = await AddUserAsync();

        // 1h inside the range
        await AddSessionAsync(user.Id, game.Id, now.AddHours(-3), now.AddHours(-2));
        // Straddles the range start: only the last hour counts
        await AddSessionAsync(user.Id, game.Id, now.AddHours(-25), now.AddHours(-23));
        // Entirely before the range
        await AddSessionAsync(user.Id, game.Id, now.AddHours(-40), now.AddHours(-30));
        // Still running: counts up to now (~10 minutes)
        await AddSessionAsync(user.Id, game.Id, now.AddMinutes(-10), null);

        var playtime = await GetService<DashboardService>().GetPlaytimeByTitleAsync(Day, top: int.MaxValue);

        var entry = playtime.SingleOrDefault(p => p.GameId == game.Id);

        entry.ShouldNotBeNull();
        entry.Title.ShouldBe(game.Title);
        entry.Playtime.TotalMinutes.ShouldBe(130, tolerance: 1);

        playtime.Select(p => p.Playtime).ShouldBeInOrder(SortDirection.Descending);
    }

    [Fact]
    public async Task PlaytimeByTitleHonoursTop()
    {
        var playtime = await GetService<DashboardService>().GetPlaytimeByTitleAsync(TimeSpan.FromDays(3650), top: 1);

        playtime.Count.ShouldBeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task ActiveSessionsListOpenSessionsWithNames()
    {
        var now = DateTime.UtcNow;
        var game = await AddGameAsync();
        var aliased = await AddUserAsync(alias: "Gordon");
        var plain = await AddUserAsync();

        var open = await AddSessionAsync(aliased.Id, game.Id, now.AddMinutes(-20), null);
        var openPlain = await AddSessionAsync(plain.Id, game.Id, now.AddMinutes(-3), null);
        var closed = await AddSessionAsync(plain.Id, game.Id, now.AddHours(-2), now.AddHours(-1));

        var sessions = await GetService<DashboardService>().GetActiveSessionsAsync();

        sessions.ShouldNotContain(s => s.SessionId == closed.Id);

        var aliasedSession = sessions.Single(s => s.SessionId == open.Id);

        aliasedSession.UserId.ShouldBe(aliased.Id);
        aliasedSession.UserName.ShouldBe(aliased.UserName);
        aliasedSession.DisplayName.ShouldBe("Gordon");
        aliasedSession.GameId.ShouldBe(game.Id);
        aliasedSession.GameTitle.ShouldBe(game.Title);
        aliasedSession.Duration.TotalMinutes.ShouldBe(20, tolerance: 1);

        sessions.Single(s => s.SessionId == openPlain.Id).DisplayName.ShouldBe(plain.UserName);

        // Most recently started first
        sessions.Select(s => s.Start).ShouldBeInOrder(SortDirection.Descending);
    }

    [Theory]
    [InlineData(-3, -2, 1)]    // inside
    [InlineData(-25, -23, 1)]  // straddles the start
    [InlineData(-40, -30, 0)]  // before
    [InlineData(-30, 2, 24)]   // covers the whole range, clipped at both ends
    public void ClipDurationKeepsOnlyTheOverlap(int startHours, int endHours, int expectedHours)
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        DashboardService
            .ClipDuration(now.AddHours(startHours), now.AddHours(endHours), now - Day, now)
            .ShouldBe(TimeSpan.FromHours(expectedHours));
    }

    [Fact]
    public void StorageUsedFractionNeedsCapacity()
    {
        new DashboardStorage(50, 200, 150).UsedFraction.ShouldBe(0.25);
        new DashboardStorage(50, null, null).UsedFraction.ShouldBeNull();
    }

    [Fact]
    public void DriveCapacityCountsEachDriveOnce()
    {
        var temp = Path.GetTempPath();

        var single = DashboardService.GetDriveCapacity([temp]);
        var duplicated = DashboardService.GetDriveCapacity([temp, Path.Combine(temp, "a"), Path.Combine(temp, "b")]);

        single.Capacity.ShouldNotBeNull();
        duplicated.Capacity.ShouldBe(single.Capacity);
    }
}
