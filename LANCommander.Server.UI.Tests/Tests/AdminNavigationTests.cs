using LANCommander.Server.UI.Tests.Pages;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace LANCommander.Server.UI.Tests.Tests;

/// <summary>
/// Tests for navigating around key parts of the admin application.
/// These tests verify that the main admin pages are accessible and render correctly
/// after logging in as an administrator.
/// Uses the shared "Server" collection fixture so the server starts once for the whole collection.
/// </summary>
[Collection("Server")]
public class AdminNavigationTests : IAsyncLifetime
{
    private readonly ConfiguredServerFixture _fixture;
    private readonly ITestOutputHelper _output;
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public AdminNavigationTests(ConfiguredServerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    public async Task InitializeAsync()
    {
        (_context, _page) = await _fixture.CreateLoggedInPageAsync();
    }

    public async Task DisposeAsync()
    {
        await ScreenshotHelper.CaptureAsync(_page, _output);
        if (_page != null) await _page.CloseAsync();
        if (_context != null) await _context.DisposeAsync();
    }

    [Fact]
    public async Task Dashboard_ShowsOverviewWithCharts()
    {
        var dashboard = new AdminDashboardPage(_page);
        Assert.True(await dashboard.IsDisplayedAsync());

        // Dashboard should show playtime charts
        await Assertions.Expect(_page.GetByText("Top 10 Total Playtime (By Player)")).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByText("Top 10 Total Playtime (By Game)")).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByText("Top Average Session Length (By Game)")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Navigation_SidebarShowsExpectedMenuItems()
    {
        var dashboard = new AdminDashboardPage(_page);
        var menuItems = await dashboard.GetMainMenuItemsAsync();

        Assert.Contains(menuItems, m => m.Contains("Dashboards"));
        Assert.Contains(menuItems, m => m.Contains("Games"));
        Assert.Contains(menuItems, m => m.Contains("Redistributables"));
        Assert.Contains(menuItems, m => m.Contains("Tools"));
        Assert.Contains(menuItems, m => m.Contains("Servers"));
        Assert.Contains(menuItems, m => m.Contains("Issues"));
        Assert.Contains(menuItems, m => m.Contains("Files"));
        Assert.Contains(menuItems, m => m.Contains("Settings"));
    }

    [Fact]
    public async Task GamesPage_ShowsEmptyTable()
    {
        var dashboard = new AdminDashboardPage(_page);
        await dashboard.NavigateToGamesAsync();

        Assert.Contains("/Games", _page.Url);
        await Assertions.Expect(_page.GetByText("Games").First).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Add Game" })).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Import" })).ToBeVisibleAsync();
        // Empty table should show "No data"
        await Assertions.Expect(_page.GetByText("No data")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task RedistributablesPage_IsAccessible()
    {
        var dashboard = new AdminDashboardPage(_page);
        await dashboard.NavigateToRedistributablesAsync();

        Assert.Contains("/Redistributables", _page.Url);
        await Assertions.Expect(_page.GetByText("Redistributables").First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ToolsPage_IsAccessible()
    {
        var dashboard = new AdminDashboardPage(_page);
        await dashboard.NavigateToToolsAsync();

        Assert.Contains("/Tools", _page.Url);
        await Assertions.Expect(_page.GetByText("Tools").First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ServersPage_IsAccessible()
    {
        var dashboard = new AdminDashboardPage(_page);
        await dashboard.NavigateToServersAsync();

        Assert.Contains("/Servers", _page.Url);
        await Assertions.Expect(_page.GetByText("Servers").First).ToBeVisibleAsync();
    }

    [Fact]
    public async Task IssuesPage_IsAccessible()
    {
        var dashboard = new AdminDashboardPage(_page);
        await dashboard.NavigateToIssuesAsync();

        Assert.Contains("/Issues", _page.Url);
    }

    [Fact]
    public async Task FilesPage_IsAccessible()
    {
        var dashboard = new AdminDashboardPage(_page);
        await dashboard.NavigateToFilesAsync();

        Assert.Contains("/Files", _page.Url);
    }

    [Fact]
    public async Task SettingsGeneralPage_IsAccessible()
    {
        var dashboard = new AdminDashboardPage(_page);
        await dashboard.NavigateToSettingsGeneralAsync();

        Assert.Contains("/Settings/General", _page.Url);
        // Verify settings-specific content is visible ("Use SSL" is unique to General settings)
        await Assertions.Expect(_page.GetByText("Use SSL")).ToBeVisibleAsync();
    }
}
