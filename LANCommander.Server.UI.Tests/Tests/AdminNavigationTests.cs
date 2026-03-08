using LANCommander.Server.UI.Tests.Pages;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace LANCommander.Server.UI.Tests.Tests;

/// <summary>
/// Tests for navigating around key parts of the admin application.
/// These tests verify that the main admin pages are accessible and render correctly
/// after logging in as an administrator.
/// Uses IClassFixture to start the server once for all tests in this class.
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
        await ScreenshotHelper.CaptureIfFailedAsync(_page, _output);
        if (_page != null) await _page.CloseAsync();
        if (_context != null) await _context.DisposeAsync();
    }

    [Fact]
    public async Task Dashboard_ShowsOverviewWithCharts()
    {
        var dashboard = new AdminDashboardPage(_page);
        Assert.True(await dashboard.IsDisplayedAsync());

        // Dashboard should show playtime charts
        Assert.True(await _page.GetByText("Top 10 Total Playtime (By Player)").IsVisibleAsync());
        Assert.True(await _page.GetByText("Top 10 Total Playtime (By Game)").IsVisibleAsync());
        Assert.True(await _page.GetByText("Top Average Session Length (By Game)").IsVisibleAsync());
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
        Assert.True(await _page.GetByText("Games").First.IsVisibleAsync());
        Assert.True(await _page.GetByRole(AriaRole.Button, new() { Name = "Add Game" }).IsVisibleAsync());
        Assert.True(await _page.GetByRole(AriaRole.Button, new() { Name = "Import" }).IsVisibleAsync());
        // Empty table should show "No data"
        Assert.True(await _page.GetByText("No data").IsVisibleAsync());
    }

    [Fact]
    public async Task RedistributablesPage_IsAccessible()
    {
        var dashboard = new AdminDashboardPage(_page);
        await dashboard.NavigateToRedistributablesAsync();

        Assert.Contains("/Redistributables", _page.Url);
        Assert.True(await _page.GetByText("Redistributables").First.IsVisibleAsync());
    }

    [Fact]
    public async Task ToolsPage_IsAccessible()
    {
        var dashboard = new AdminDashboardPage(_page);
        await dashboard.NavigateToToolsAsync();

        Assert.Contains("/Tools", _page.Url);
        Assert.True(await _page.GetByText("Tools").First.IsVisibleAsync());
    }

    [Fact]
    public async Task ServersPage_IsAccessible()
    {
        var dashboard = new AdminDashboardPage(_page);
        await dashboard.NavigateToServersAsync();

        Assert.Contains("/Servers", _page.Url);
        Assert.True(await _page.GetByText("Servers").First.IsVisibleAsync());
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
        // Verify settings-specific content is visible (Database Provider is unique to General settings)
        Assert.True(await _page.GetByText("Database Provider").IsVisibleAsync());
    }

    [Fact]
    public async Task SettingsMenu_ShowsAllExpectedSubItems()
    {
        // Open the Settings submenu
        await _page.GetByRole(AriaRole.Button, new() { Name = "Settings" }).ClickAsync();

        // Verify key settings sub-items are visible
        var expectedSettings = new[] {
            "General", "Users", "Roles", "Authentication",
            "Archives", "Media", "Logs", "Updates"
        };

        foreach (var setting in expectedSettings)
        {
            Assert.True(
                await _page.GetByRole(AriaRole.Link, new() { Name = setting, Exact = true }).IsVisibleAsync(),
                $"Settings menu should contain '{setting}'");
        }
    }
}
