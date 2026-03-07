using Microsoft.Playwright;

namespace LANCommander.Server.UI.Tests;

/// <summary>
/// Fixture for FirstTimeSetupTests that provides a fresh unconfigured server.
/// Unlike ConfiguredServerFixture, this does NOT create an admin user or set DatabaseContext.Provider,
/// so the server will redirect to /FirstTimeSetup.
/// </summary>
public class FreshServerFixture : IAsyncLifetime
{
    public PlaywrightFixture Playwright { get; private set; } = null!;
    public UITestApplicationFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Playwright = new PlaywrightFixture();
        await Playwright.InitializeAsync();

        Factory = new UITestApplicationFactory();
        _ = Factory.Services;
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await Playwright.DisposeAsync();
    }

    public async Task<(IBrowserContext Context, IPage Page)> CreatePageAsync()
    {
        var context = await Playwright.NewContextAsync(Factory.BaseAddress);
        var page = await context.NewPageAsync();
        return (context, page);
    }
}
