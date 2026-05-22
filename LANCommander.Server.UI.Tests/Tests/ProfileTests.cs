using LANCommander.Server.UI.Tests.Pages;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace LANCommander.Server.UI.Tests.Tests;

/// <summary>
/// Tests for the user profile and change password pages.
/// Uses IClassFixture to share the server instance across all tests in this class.
/// </summary>
[Collection("Server")]
public class ProfileTests : IAsyncLifetime
{
    private readonly ConfiguredServerFixture _fixture;
    private readonly ITestOutputHelper _output;
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public ProfileTests(ConfiguredServerFixture fixture, ITestOutputHelper output)
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
    public async Task ProfilePage_ShowsCurrentUsername()
    {
        var profilePage = new ProfilePage(_page);
        await profilePage.NavigateAsync();

        var username = await profilePage.GetUsernameAsync();

        Assert.Equal(TestConstants.AdminUserName, username);
    }

    [Fact]
    public async Task ProfilePage_ShowsFormElements()
    {
        var profilePage = new ProfilePage(_page);
        await profilePage.NavigateAsync();

        Assert.True(await profilePage.HasFieldAsync("Username"), "Username field should be visible");
        Assert.True(await profilePage.HasFieldAsync("Alias"), "Alias field should be visible");
        Assert.True(await profilePage.HasFieldAsync("Email Address"), "Email Address field should be visible");
        Assert.True(
            await _page.GetByRole(AriaRole.Button, new() { Name = "Save" }).IsVisibleAsync(),
            "Save button should be visible");
    }

    [Fact]
    public async Task ProfilePage_CanUpdateAlias()
    {
        var profilePage = new ProfilePage(_page);
        await profilePage.NavigateAsync();

        await profilePage.SetAliasAsync("Test Admin");
        await profilePage.SaveAsync();

        // Saving triggers a logout redirect; wait for the login page
        await _page.WaitForURLAsync("**/Login**", new() { Timeout = 15000 });

        // Re-login and navigate back to profile to verify persistence
        var loginPage = new LoginPage(_page);
        await loginPage.LoginAsync(TestConstants.AdminUserName, TestConstants.AdminPassword);
        await _page.WaitForSelectorAsync("text=Dashboard", new() { Timeout = 15000 });

        await profilePage.NavigateAsync();

        var alias = await profilePage.GetAliasAsync();
        Assert.Equal("Test Admin", alias);
    }

    [Fact]
    public async Task ChangePassword_PageShowsFormElements()
    {
        var profilePage = new ProfilePage(_page);
        await profilePage.NavigateToChangePasswordAsync();

        Assert.True(await profilePage.HasPasswordFieldAsync("Current Password"), "Current Password field should be visible");
        Assert.True(await profilePage.HasPasswordFieldAsync("New Password"), "New Password field should be visible");
        Assert.True(await profilePage.HasPasswordFieldAsync("Confirm Password"), "Confirm Password field should be visible");
        Assert.True(
            await _page.GetByRole(AriaRole.Button, new() { Name = "Change" }).IsVisibleAsync(),
            "Change button should be visible");
    }

    [Fact]
    public async Task ChangePassword_CanChangePassword()
    {
        const string newPassword = "NewPassword123!";

        // Step 1: Change the password
        var profilePage = new ProfilePage(_page);
        await profilePage.NavigateToChangePasswordAsync();
        await profilePage.ChangePasswordAsync(TestConstants.AdminPassword, newPassword);

        // Wait for success message
        await _page.WaitForSelectorAsync("text=Password changed!", new() { Timeout = 10000 });

        // Step 2: Verify login works with the new password in a fresh browser context
        var (anonContext, anonPage) = await _fixture.CreateAnonymousPageAsync();
        try
        {
            var loginPage = new LoginPage(anonPage);
            await loginPage.NavigateAsync();
            await loginPage.LoginAsync(TestConstants.AdminUserName, newPassword);
            await anonPage.WaitForSelectorAsync("text=Dashboard", new() { Timeout = 15000 });

            Assert.Contains("/", anonPage.Url);
        }
        finally
        {
            await anonPage.CloseAsync();
            await anonContext.DisposeAsync();
        }

        // Step 3: Change the password back to the original so other tests aren't affected
        await profilePage.NavigateToChangePasswordAsync();
        await profilePage.ChangePasswordAsync(newPassword, TestConstants.AdminPassword);
        await _page.WaitForSelectorAsync("text=Password changed!", new() { Timeout = 10000 });
    }
}
