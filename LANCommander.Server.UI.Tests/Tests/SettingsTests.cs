using LANCommander.Server.UI.Tests.Pages;
using Microsoft.Playwright;

namespace LANCommander.Server.UI.Tests.Tests;

/// <summary>
/// Tests for the Settings pages of the admin application.
/// Verifies that each settings sub-page is accessible and renders its expected content.
/// </summary>
[Collection("Server")]
public class SettingsTests : IAsyncLifetime
{
    private readonly ConfiguredServerFixture _fixture;
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public SettingsTests(ConfiguredServerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        (_context, _page) = await _fixture.CreateLoggedInPageAsync();
    }

    public async Task DisposeAsync()
    {
        if (_page != null) await _page.CloseAsync();
        if (_context != null) await _context.DisposeAsync();
    }

    [Fact]
    public async Task SettingsGeneral_ShowsFormElements()
    {
        var settings = new SettingsPage(_page);
        await settings.NavigateToGeneralAsync();

        Assert.Contains("/Settings/General", _page.Url);
        Assert.True(await _page.GetByText("Database Provider").IsVisibleAsync());
        Assert.True(await _page.GetByText("Port").First.IsVisibleAsync());
        Assert.True(await _page.GetByRole(AriaRole.Button, new() { Name = "Save" }).IsVisibleAsync());
    }

    [Fact]
    public async Task SettingsUsers_ShowsUserList()
    {
        var settings = new SettingsPage(_page);
        await settings.NavigateToUsersAsync();

        Assert.Contains("/Settings/Users", _page.Url);
        // Wait for the data table to render with user data
        await _page.Locator("table").First.WaitForAsync(new() { Timeout = 15000 });
        // The admin user created during fixture setup should appear in the table
        var adminCell = _page.Locator("table").GetByText(TestConstants.AdminUserName).First;
        await adminCell.WaitForAsync(new() { Timeout = 15000 });
        Assert.True(await adminCell.IsVisibleAsync());
    }

    [Fact]
    public async Task SettingsRoles_ShowsRoleList()
    {
        var settings = new SettingsPage(_page);
        await settings.NavigateToRolesAsync();

        Assert.Contains("/Settings/Roles", _page.Url);
        Assert.True(await _page.GetByRole(AriaRole.Button, new() { Name = "Add Role" }).IsVisibleAsync());
        // Wait for the data table to render then check for the Administrator role
        await _page.Locator("table").First.WaitForAsync(new() { Timeout = 15000 });
        var adminRole = _page.Locator("table").GetByText("Administrator");
        await adminRole.WaitForAsync(new() { Timeout = 15000 });
        Assert.True(await adminRole.IsVisibleAsync());
    }

    [Fact]
    public async Task SettingsAuthentication_IsAccessible()
    {
        var settings = new SettingsPage(_page);
        await settings.NavigateToAuthenticationAsync();

        Assert.Contains("/Settings/Authentication", _page.Url);
        Assert.True(await _page.GetByText("Authentication").First.IsVisibleAsync());
    }

    [Fact]
    public async Task SettingsArchives_IsAccessible()
    {
        var settings = new SettingsPage(_page);
        await settings.NavigateToArchivesAsync();

        Assert.Contains("/Settings/Archives", _page.Url);
        Assert.True(await _page.GetByText("Archives").First.IsVisibleAsync());
    }

    [Fact]
    public async Task SettingsMedia_IsAccessible()
    {
        var settings = new SettingsPage(_page);
        await settings.NavigateToMediaAsync();

        Assert.Contains("/Settings/Media", _page.Url);
        Assert.True(await _page.GetByText("Media").First.IsVisibleAsync());
    }

    [Fact]
    public async Task SettingsLogs_IsAccessible()
    {
        // Logs page does not exist; testing Beacon settings instead
        var settings = new SettingsPage(_page);
        await settings.NavigateToBeaconAsync();

        Assert.Contains("/Settings/Beacon", _page.Url);
        Assert.True(await _page.GetByText("Beacon").First.IsVisibleAsync());
    }

    [Fact]
    public async Task SettingsUpdates_IsAccessible()
    {
        var settings = new SettingsPage(_page);
        await settings.NavigateToUpdatesAsync();

        Assert.Contains("/Settings/Updates", _page.Url);
        Assert.True(await _page.GetByText("Updates").First.IsVisibleAsync());
    }

    [Fact]
    public async Task SettingsAppearance_IsAccessible()
    {
        var settings = new SettingsPage(_page);
        await settings.NavigateToAppearanceAsync();

        Assert.Contains("/Settings/Appearance", _page.Url);
        Assert.True(await _page.GetByText("Appearance").First.IsVisibleAsync());
    }
}
