using Bunit;
using LANCommander.Server.Services;
using LANCommander.Server.UI.Pages.Dashboard;
using Microsoft.Extensions.DependencyInjection;
using DashboardIndex = LANCommander.Server.UI.Pages.Dashboard.Index;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// bUnit tests for the dashboard: the statistic tiles, the range switch and the active sessions
/// panel, against the real <see cref="DashboardService"/> and the fixture's SQLite database.
/// </summary>
[Collection("BUnit")]
public class DashboardComponentTests : BUnitTestContext
{
    public DashboardComponentTests(BUnitServerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public void Dashboard_ShowsStatisticTilesAndHostSubtitle()
    {
        var cut = Render<DashboardIndex>();

        cut.WaitForAssertion(() =>
        {
            var titles = cut.FindAll(".lc-statistic-title").Select(t => t.TextContent.Trim()).ToList();

            Assert.Equal(["Players online", "Sessions this week", "Game servers", "Archive storage"], titles);
        });

        var subtitle = cut.Find(".lc-page-header-subtitle").TextContent;

        Assert.StartsWith(Environment.MachineName.ToLowerInvariant(), subtitle);
        Assert.Contains(" · up ", subtitle);
    }

    [Fact]
    public void Dashboard_RangeSwitch_RetitlesTilesAndChart()
    {
        var cut = Render<DashboardIndex>();

        cut.WaitForAssertion(() => Assert.Contains("last 7 days", cut.Find(".dashboard-playtime .lc-card-subtitle").TextContent));

        cut.FindAll(".lc-segmented-option").Single(o => o.TextContent.Trim() == "24h").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("last 24 hours", cut.Find(".dashboard-playtime .lc-card-subtitle").TextContent);
            Assert.Contains(cut.FindAll(".lc-statistic-title"), t => t.TextContent.Trim() == "Sessions today");
        });
    }

    [Fact]
    public async Task Dashboard_ListsOpenSessions()
    {
        using var scope = Fixture.Factory.RealServices.CreateScope();
        var playSessions = scope.ServiceProvider.GetRequiredService<PlaySessionService>();
        var users = scope.ServiceProvider.GetRequiredService<UserService>();

        var admin = await users.GetAsync(TestConstants.AdminUserName);

        await playSessions.StartSessionAsync(Fixture.TestGameId, admin.Id);

        try
        {
            var cut = Render<DashboardIndex>();

            cut.WaitForAssertion(() =>
            {
                var row = cut.FindAll(".dashboard-session")
                    .Single(r => r.QuerySelector(".dashboard-session-game")!.TextContent == BUnitServerFixture.TestGameTitle);

                Assert.Equal("0m", row.QuerySelector(".dashboard-session-duration")!.TextContent);
                Assert.NotNull(row.QuerySelector(".lc-avatar-small"));
            });

            // The test user is an administrator, so the panel links to the full list
            Assert.Equal("/Settings/Tools/ActiveSessions", cut.Find(".dashboard-sessions .lc-card-footer a").GetAttribute("href"));
        }
        finally
        {
            var session = await playSessions.FirstOrDefaultAsync(ps => ps.GameId == Fixture.TestGameId && ps.UserId == admin.Id && ps.End == null);

            if (session != null)
                await playSessions.DeleteAsync(session);
        }
    }
}

public class DashboardFormatTests
{
    [Theory]
    [InlineData(0, "0m")]
    [InlineData(46, "46m")]
    [InlineData(68, "1h 08m")]
    [InlineData(72, "1h 12m")]
    [InlineData(26 * 60 + 5, "26h 05m")]
    public void Duration_WritesHoursAndPaddedMinutes(int minutes, string expected) =>
        Assert.Equal(expected, DashboardFormat.Duration(TimeSpan.FromMinutes(minutes)));

    [Fact]
    public void Uptime_SwitchesToDaysAfterADay()
    {
        Assert.Equal("6d 14h", DashboardFormat.Uptime(new TimeSpan(6, 14, 37, 0)));
        Assert.Equal("3h 05m", DashboardFormat.Uptime(new TimeSpan(3, 5, 0)));
    }

    [Fact]
    public void HoursPlayed_UsesMinutesUnderAnHour()
    {
        Assert.Equal("41 h played", DashboardFormat.HoursPlayed(TimeSpan.FromHours(41.7)));
        Assert.Equal("25 m played", DashboardFormat.HoursPlayed(TimeSpan.FromMinutes(25)));
    }

    [Fact]
    public void Delta_IsSigned()
    {
        Assert.Equal("+5 vs 7d ago", DashboardFormat.Delta(5, "7d ago"));
        Assert.Equal("−2 vs 24h ago", DashboardFormat.Delta(-2, "24h ago"));
        Assert.Equal("no change vs 30d ago", DashboardFormat.Delta(0, "30d ago"));
    }

    [Fact]
    public void Storage_SplitsFigureFromUnitAndCapacity()
    {
        Assert.Equal(("1.24", "TB of 4 TB"), DashboardFormat.Storage(1_240_000_000_000, 4_000_000_000_000));
        Assert.Equal(("512", "GB used"), DashboardFormat.Storage(512_000_000_000, null));
        Assert.Equal(("0", "B of 4 TB"), DashboardFormat.Storage(0, 4_000_000_000_000));
    }

    [Fact]
    public void Abbreviate_KeepsShortLabels()
    {
        Assert.Equal("Quake III", DashboardFormat.Abbreviate("Quake III"));
        Assert.Equal("Natural Sel…", DashboardFormat.Abbreviate("Natural Selection 2"));
    }
}
