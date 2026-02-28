using LANCommander.Server.UI.Tests.Pages;
using Microsoft.Playwright;

namespace LANCommander.Server.UI.Tests;

/// <summary>
/// Shared fixture that starts a fresh server, completes first-time setup, and makes it
/// available for all tests in a class. Used via IClassFixture&lt;ConfiguredServerFixture&gt;
/// to avoid starting/stopping the server for every single test method.
/// </summary>
public class ConfiguredServerFixture : IAsyncLifetime
{
    public PlaywrightFixture Playwright { get; private set; } = null!;
    public ServerManager ServerManager { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Playwright = new PlaywrightFixture();
        await Playwright.InitializeAsync();

        ServerManager = new ServerManager();
        ServerManager.EnsureCleanState();
        await ServerManager.StartAsync();

        // Complete first-time setup to create the admin user
        var context = await Playwright.NewContextAsync();
        var page = await context.NewPageAsync();

        var setupPage = new FirstTimeSetupPage(page);
        await setupPage.NavigateAsync();
        await setupPage.CompleteFullSetupAsync(
            adminUsername: TestConstants.AdminUserName,
            adminPassword: TestConstants.AdminPassword);

        await page.CloseAsync();
        await context.DisposeAsync();
    }

    public async Task DisposeAsync()
    {
        await ServerManager.DisposeAsync();
        await Playwright.DisposeAsync();
    }

    /// <summary>
    /// Creates a new browser context and page, already logged in as admin.
    /// </summary>
    public async Task<(IBrowserContext Context, IPage Page)> CreateLoggedInPageAsync()
    {
        var context = await Playwright.NewContextAsync();
        var page = await context.NewPageAsync();

        var loginPage = new LoginPage(page);
        await loginPage.NavigateAsync();
        await loginPage.LoginAsync(TestConstants.AdminUserName, TestConstants.AdminPassword);
        await page.WaitForSelectorAsync("text=Dashboard", new() { Timeout = 15000 });

        return (context, page);
    }

    /// <summary>
    /// Creates a new browser context and page (not logged in).
    /// </summary>
    public async Task<(IBrowserContext Context, IPage Page)> CreateAnonymousPageAsync()
    {
        var context = await Playwright.NewContextAsync();
        var page = await context.NewPageAsync();
        return (context, page);
    }
}
