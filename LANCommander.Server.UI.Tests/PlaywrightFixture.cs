using Microsoft.Playwright;

namespace LANCommander.Server.UI.Tests;

/// <summary>
/// Shared Playwright fixture that manages browser lifetime across all tests in the collection.
/// Starts the server process and initializes Playwright once per test run.
/// </summary>
public class PlaywrightFixture : IAsyncLifetime
{
    public IPlaywright Playwright { get; private set; } = null!;
    public IBrowser Browser { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
        });
    }

    public async Task DisposeAsync()
    {
        await Browser.DisposeAsync();
        Playwright.Dispose();
    }

    public async Task<IBrowserContext> NewContextAsync(string? baseUrl = null)
    {
        return await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            BaseURL = baseUrl ?? TestConstants.BaseUrl,
        });
    }
}
