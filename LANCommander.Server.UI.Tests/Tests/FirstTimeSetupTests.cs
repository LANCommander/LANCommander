using LANCommander.Server.UI.Tests.Pages;
using Microsoft.Playwright;

namespace LANCommander.Server.UI.Tests.Tests;

/// <summary>
/// Tests for the first-time setup wizard when the server has no existing configuration.
/// Each test gets a fresh server instance via WebApplicationFactory with an empty in-memory database.
/// </summary>
public class FirstTimeSetupTests : IAsyncLifetime
{
    private PlaywrightFixture _playwright = null!;
    private UITestApplicationFactory _factory = null!;
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public async Task InitializeAsync()
    {
        _playwright = new PlaywrightFixture();
        await _playwright.InitializeAsync();

        _factory = new UITestApplicationFactory();
        // Trigger the factory to start the Kestrel server
        _ = _factory.Services;

        _context = await _playwright.NewContextAsync(_factory.BaseAddress);
        _page = await _context.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        if (_page != null) await _page.CloseAsync();
        if (_context != null) await _context.DisposeAsync();
        await _factory.DisposeAsync();
        await _playwright.DisposeAsync();
    }

    [Fact]
    public async Task FreshServer_RedirectsToFirstTimeSetup()
    {
        await _page.GotoAsync("/");

        // A fresh server should show the First Time Setup page
        await _page.WaitForSelectorAsync("text=First Time Setup", new() { Timeout = 10000 });

        var setupPage = new FirstTimeSetupPage(_page);
        Assert.True(await setupPage.IsDisplayedAsync());
    }

    [Fact]
    public async Task FirstTimeSetup_ShowsFourSteps()
    {
        var setupPage = new FirstTimeSetupPage(_page);
        await setupPage.NavigateAsync();

        // Verify all 4 steps are visible in the wizard
        Assert.True(await _page.GetByText("Database").First.IsVisibleAsync());
        Assert.True(await _page.GetByText("Paths").IsVisibleAsync());
        // "Metadata" may be truncated in UI to "Metad" but the text node still exists
        Assert.True(await _page.Locator("text=/Metad/").First.IsVisibleAsync());
        Assert.True(await _page.GetByText("Administrator").IsVisibleAsync());
    }

    [Fact]
    public async Task FirstTimeSetup_DatabaseStep_ShowsProviderOptions()
    {
        var setupPage = new FirstTimeSetupPage(_page);
        await setupPage.NavigateAsync();

        // Open the database provider dropdown
        await _page.GetByRole(AriaRole.Combobox).ClickAsync();

        // Wait for the dropdown listbox to appear
        await _page.WaitForSelectorAsync("[role='listbox']", new() { Timeout = 5000 });

        // Verify all expected providers are shown
        Assert.True(await _page.GetByRole(AriaRole.Option, new() { Name = "SQLite" }).IsVisibleAsync());
        Assert.True(await _page.GetByRole(AriaRole.Option, new() { Name = "MySQL" }).IsVisibleAsync());
        Assert.True(await _page.GetByRole(AriaRole.Option, new() { Name = "PostgreSQL" }).IsVisibleAsync());
    }

    [Fact(Skip = "Requires real database and file I/O - not supported with in-memory WebApplicationFactory")]
    public async Task FirstTimeSetup_CompleteWizardAndLogin()
    {
        var setupPage = new FirstTimeSetupPage(_page);
        await setupPage.NavigateAsync();

        await setupPage.CompleteFullSetupAsync(
            adminUsername: TestConstants.AdminUserName,
            adminPassword: TestConstants.AdminPassword);

        // After setup, we should be on the login page
        Assert.Contains("/Login", _page.Url);

        var loginPage = new LoginPage(_page);
        Assert.True(await loginPage.IsDisplayedAsync());

        // Now log in with the admin credentials that were just created
        await loginPage.LoginAsync(TestConstants.AdminUserName, TestConstants.AdminPassword);

        // Wait for the Blazor app to render the dashboard
        await _page.WaitForSelectorAsync("text=Dashboard", new() { Timeout = 15000 });

        var dashboard = new AdminDashboardPage(_page);
        Assert.True(await dashboard.IsDisplayedAsync());
    }
}
