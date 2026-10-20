using LANCommander.Server.Data;
using LANCommander.Server.Settings.Enums;
using LANCommander.Server.UI.Fixtures.Data;
using LANCommander.Server.UI.Tests.Pages;
using Microsoft.Playwright;

namespace LANCommander.Server.UI.Tests.Visual;

/// <summary>
/// A server seeded with <see cref="FixtureDatabaseSeeder"/> and a browser whose every setting that
/// affects rendering is pinned: viewport, device scale, locale, time zone, colour scheme and clock.
/// Shared by the "Visual" collection.
/// </summary>
public class VisualServerFixture : IAsyncLifetime
{
    public const int ViewportWidth = 1440;
    public const int ViewportHeight = 900;

    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private string _storageRoot = null!;
    private string? _adminStorageState;

    public VisualTestApplicationFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Factory = new VisualTestApplicationFactory();
        _ = Factory.Services;

        // A fixed folder, because settings pages show storage paths and captures must not change between runs
        _storageRoot = Path.Combine(Path.GetTempPath(), "LANCommander_Visual");

        if (Directory.Exists(_storageRoot))
            Directory.Delete(_storageRoot, recursive: true);

        await new FixtureDatabaseSeeder(Factory.RealServices).SeedAsync(_storageRoot);

        // Set after seeding so the server treats itself as configured (no /FirstTimeSetup redirect)
        DatabaseContext.Provider = DatabaseProvider.SQLite;

        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            // Font hinting and subpixel positioning vary by GPU and platform; keep text rasterization plain
            Args = ["--font-render-hinting=none", "--disable-lcd-text", "--force-color-profile=srgb"],
        });
    }

    public async Task DisposeAsync()
    {
        DatabaseContext.Provider = DatabaseProvider.Unknown;

        await _browser.DisposeAsync();
        _playwright.Dispose();
        await Factory.DisposeAsync();

        try { Directory.Delete(_storageRoot, recursive: true); } catch { }
    }

    /// <summary>A page with pinned rendering settings, not logged in.</summary>
    public Task<IPage> NewPageAsync() => NewPageAsync(storageState: null);

    /// <summary>A page with pinned rendering settings, logged in as the fixture administrator.</summary>
    public async Task<IPage> NewAdminPageAsync()
    {
        if (_adminStorageState == null)
        {
            var loginPage = await NewPageAsync(storageState: null);

            var login = new LoginPage(loginPage);
            await login.NavigateAsync();
            await login.LoginAsync(FixtureData.AdminUserName, FixtureData.AdminPassword);
            await loginPage.WaitForURLAsync(url => !url.Contains("/Login", StringComparison.OrdinalIgnoreCase),
                new() { Timeout = 15000 });

            _adminStorageState = await loginPage.Context.StorageStateAsync();
            await loginPage.Context.DisposeAsync();
        }

        return await NewPageAsync(_adminStorageState);
    }

    private async Task<IPage> NewPageAsync(string? storageState)
    {
        var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = Factory.BaseAddress,
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = ViewportWidth, Height = ViewportHeight },
            DeviceScaleFactor = 1,
            Locale = "en-US",
            TimezoneId = "UTC",
            ColorScheme = ColorScheme.Dark,
            ReducedMotion = ReducedMotion.Reduce,
            StorageState = storageState,
        });

        var page = await context.NewPageAsync();

        await page.Clock.SetFixedTimeAsync(FixtureIds.Epoch);

        return page;
    }
}

[CollectionDefinition("Visual")]
public class VisualCollection : ICollectionFixture<VisualServerFixture>
{
}
