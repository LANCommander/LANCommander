using System.Text.RegularExpressions;
using LANCommander.SDK.Events;
using LANCommander.Server.Services.Events;
using LANCommander.Server.UI.Tests.Pages;
using Microsoft.Extensions.DependencyInjection;
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

        // Dashboard should show the range chart, the sessions panel and the all-time playtime charts
        await Assertions.Expect(_page.GetByText("Playtime by title", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByText("Active sessions", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByText("Playtime by player", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByText("Share of playtime", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByText("Average session length", new() { Exact = true })).ToBeVisibleAsync();
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

    [Theory]
    [InlineData(1440)]
    [InlineData(390)]
    public async Task Webhooks_UseDedicatedPagesAndResponsiveEditor(int width)
    {
        await _page.SetViewportSizeAsync(width, 900);
        using var scope = _fixture.Factory.RealServices.CreateScope();
        var webhooks = scope.ServiceProvider.GetRequiredService<WebhookService>();
        var name = $"Navigation webhook ({width})";
        var id = await webhooks.SaveAsync(null, new WebhookEdit
        {
            Name = name,
            Endpoint = "http://127.0.0.1:1/events",
            EventTypes = [ServerEventTypes.GameCreated],
        });
        try
        {
            await _page.GotoAsync("/Settings/Integrations/Webhooks");
            await _page.GetByRole(AriaRole.Button, new() { Name = $"Actions for {name}", Exact = true }).ClickAsync();
            await _page.GetByRole(AriaRole.Link, new() { Name = "Edit", Exact = true }).ClickAsync();
            await Assertions.Expect(_page).ToHaveURLAsync(new Regex($"/Webhooks/{id}/General$"));
            await Assertions.Expect(_page.GetByLabel("Name")).ToHaveValueAsync(name);
            await Assertions.Expect(_page.Locator(".lc-tags-input")).ToContainTextAsync(ServerEventTypes.GameCreated);

            var pair = _page.Locator(".webhook-delivery-settings");
            var columns = await pair.EvaluateAsync<string>("el => getComputedStyle(el).gridTemplateColumns");
            Assert.Equal(width > 900 ? 2 : 1, columns.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
            var timeout = await pair.Locator("input").First.BoundingBoxAsync();
            Assert.NotNull(timeout);
            Assert.InRange(timeout.Width, 40, 448);
            await ScreenshotHelper.CaptureAsync(_page, _output);

            await _page.GetByRole(AriaRole.Link, new() { Name = "Webhooks", Exact = true }).ClickAsync();
            await _page.GetByRole(AriaRole.Button, new() { Name = $"Actions for {name}", Exact = true }).ClickAsync();
            await _page.GetByRole(AriaRole.Button, new() { Name = "Run test", Exact = true }).ClickAsync();
            await Assertions.Expect(_page).ToHaveURLAsync(new Regex($"/Webhooks/{id}/Deliveries\\?delivery="));
            await Assertions.Expect(_page.Locator("pre code")).ToContainTextAsync(ServerEventTypes.Test);
            await Assertions.Expect(_page.Locator("form")).ToHaveCountAsync(0);
            await _page.GetByRole(AriaRole.Link, new() { Name = "General", Exact = true }).ClickAsync();
            await Assertions.Expect(_page).ToHaveURLAsync(new Regex($"/Webhooks/{id}/General$"));
        }
        finally
        {
            await webhooks.DeleteAsync(id);
        }
    }

    [Fact]
    public async Task GamesPage_ListsTheSeededGame()
    {
        var dashboard = new AdminDashboardPage(_page);
        await dashboard.NavigateToGamesAsync();

        Assert.Contains("/Games", _page.Url);
        await Assertions.Expect(_page.GetByText("Games").First).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Add Game" })).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByRole(AriaRole.Button, new() { Name = "Import" })).ToBeVisibleAsync();
        // The fixture seeds one game
        await Assertions.Expect(_page.Locator("td").GetByText(ConfiguredServerFixture.TestGameTitle, new() { Exact = true })).ToBeVisibleAsync();
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
