using LANCommander.Server.UI.Tests.Pages;
using Microsoft.Playwright;

namespace LANCommander.Server.UI.Tests.Tests;

/// <summary>
/// Tests for the Settings > Users page and user registration flow.
/// </summary>
public class UserManagementTests : IClassFixture<ConfiguredServerFixture>, IAsyncLifetime
{
    private readonly ConfiguredServerFixture _fixture;
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public UserManagementTests(ConfiguredServerFixture fixture)
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
    public async Task UsersPage_ShowsAdminUser()
    {
        var usersPage = new UsersPage(_page);
        await usersPage.NavigateAsync();

        Assert.True(await usersPage.IsUserVisibleAsync(TestConstants.AdminUserName));
    }

    [Fact]
    public async Task UsersPage_CanSearchUsers()
    {
        var usersPage = new UsersPage(_page);
        await usersPage.NavigateAsync();

        await usersPage.SearchUsersAsync(TestConstants.AdminUserName);

        Assert.True(await usersPage.IsUserVisibleAsync(TestConstants.AdminUserName));
        // After searching for "admin", the admin user must be in the results
        var count = await usersPage.GetUserCountAsync();
        Assert.True(count >= 1, $"Expected at least 1 user matching 'admin', got {count}");
    }

    [Fact]
    public async Task UsersPage_ShowsUserRoles()
    {
        var usersPage = new UsersPage(_page);
        await usersPage.NavigateAsync();

        var roles = await usersPage.GetUserRolesTextAsync(TestConstants.AdminUserName);
        Assert.Contains("Administrator", roles);
    }

    [Fact]
    public async Task Register_NewUser_AppearsInUserList()
    {
        var testUserName = $"testuser_{Guid.NewGuid().ToString()[..8]}";

        // Register a new user via the public Register page using a separate anonymous context
        var (anonContext, anonPage) = await _fixture.CreateAnonymousPageAsync();
        try
        {
            await anonPage.GotoAsync("/Register");
            await anonPage.WaitForSelectorAsync("#login-submit", new() { Timeout = 10000 });

            await anonPage.Locator("input[name='Model.UserName']").FillAsync(testUserName);
            await anonPage.Locator("input[name='Model.Password']").FillAsync(TestConstants.AdminPassword);
            await anonPage.Locator("input[name='Model.PasswordConfirmation']").FillAsync(TestConstants.AdminPassword);
            await anonPage.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

            // Wait for registration to complete (redirects to home)
            await anonPage.WaitForURLAsync("**/", new() { Timeout = 10000 });
        }
        finally
        {
            await anonPage.CloseAsync();
            await anonContext.DisposeAsync();
        }

        // Navigate to users page as admin and verify the new user appears
        var usersPage = new UsersPage(_page);
        await usersPage.NavigateAsync();

        Assert.True(await usersPage.IsUserVisibleAsync(testUserName));
    }

    [Fact]
    public async Task UsersPage_CanDeleteUser()
    {
        var testUserName = $"deluser_{Guid.NewGuid().ToString()[..8]}";

        // Create a user via the Register page
        var (anonContext, anonPage) = await _fixture.CreateAnonymousPageAsync();
        try
        {
            await anonPage.GotoAsync("/Register");
            await anonPage.WaitForSelectorAsync("#login-submit", new() { Timeout = 10000 });

            await anonPage.Locator("input[name='Model.UserName']").FillAsync(testUserName);
            await anonPage.Locator("input[name='Model.Password']").FillAsync(TestConstants.AdminPassword);
            await anonPage.Locator("input[name='Model.PasswordConfirmation']").FillAsync(TestConstants.AdminPassword);
            await anonPage.GetByRole(AriaRole.Button, new() { Name = "Register" }).ClickAsync();

            await anonPage.WaitForURLAsync("**/", new() { Timeout = 10000 });
        }
        finally
        {
            await anonPage.CloseAsync();
            await anonContext.DisposeAsync();
        }

        // Navigate to users page as admin
        var usersPage = new UsersPage(_page);
        await usersPage.NavigateAsync();

        // Verify user exists before deletion
        Assert.True(await usersPage.IsUserVisibleAsync(testUserName));

        // Delete the user
        await usersPage.DeleteUserAsync(testUserName);

        // Verify user is gone
        Assert.False(await usersPage.IsUserVisibleAsync(testUserName));
    }
}
