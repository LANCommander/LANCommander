using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Settings.Enums;
using LANCommander.Server.UI.Tests.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace LANCommander.Server.UI.Tests;

/// <summary>
/// Shared fixture that starts the server via WebApplicationFactory, programmatically creates
/// the admin user, and makes it available for all tests in a class.
/// Used via IClassFixture&lt;ConfiguredServerFixture&gt;.
/// </summary>
public class ConfiguredServerFixture : IAsyncLifetime
{
    public PlaywrightFixture Playwright { get; private set; } = null!;
    public UITestApplicationFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        Playwright = new PlaywrightFixture();
        await Playwright.InitializeAsync();

        Factory = new UITestApplicationFactory();
        // Trigger the factory to start the Kestrel server
        _ = Factory.Services;

        // Create the admin user via the service layer (before setting Provider
        // so OnConfiguring doesn't try to add a conflicting SQLite provider)
        using var scope = Factory.RealServices.CreateScope();
        var roleService = scope.ServiceProvider.GetRequiredService<RoleService>();
        var userService = scope.ServiceProvider.GetRequiredService<UserService>();

        await roleService.AddAsync(new Role { Name = RoleService.AdministratorRoleName });
        var user = await userService.AddAsync(new User { UserName = TestConstants.AdminUserName });
        await userService.ChangePassword(user.UserName, TestConstants.AdminPassword);
        await userService.AddToRoleAsync(user.UserName, RoleService.AdministratorRoleName);

        // Now set the database provider so the server doesn't redirect to /FirstTimeSetup
        DatabaseContext.Provider = DatabaseProvider.SQLite;
    }

    public async Task DisposeAsync()
    {
        // Reset the static provider so other tests can use a fresh state
        DatabaseContext.Provider = DatabaseProvider.Unknown;

        await Factory.DisposeAsync();
        await Playwright.DisposeAsync();
    }

    /// <summary>
    /// Creates a new browser context and page, already logged in as admin.
    /// </summary>
    public async Task<(IBrowserContext Context, IPage Page)> CreateLoggedInPageAsync()
    {
        var context = await Playwright.NewContextAsync(Factory.BaseAddress);
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
        var context = await Playwright.NewContextAsync(Factory.BaseAddress);
        var page = await context.NewPageAsync();
        return (context, page);
    }
}
