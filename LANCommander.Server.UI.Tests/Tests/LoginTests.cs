using LANCommander.Server.UI.Tests.Pages;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace LANCommander.Server.UI.Tests.Tests;

/// <summary>
/// Tests for the login flow against a server that has already been configured.
/// These tests assume the server is running with a known admin user.
/// Uses IClassFixture to start the server once for all tests in this class.
/// </summary>
[Collection("Server")]
public class LoginTests : IAsyncLifetime
{
    private readonly ConfiguredServerFixture _fixture;
    private readonly ITestOutputHelper _output;
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public LoginTests(ConfiguredServerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    public async Task InitializeAsync()
    {
        (_context, _page) = await _fixture.CreateAnonymousPageAsync();
    }

    public async Task DisposeAsync()
    {
        await ScreenshotHelper.CaptureIfFailedAsync(_page, _output);
        if (_page != null) await _page.CloseAsync();
        if (_context != null) await _context.DisposeAsync();
    }

    [Fact]
    public async Task UnauthenticatedUser_RedirectsToLogin()
    {
        await _page.GotoAsync("/");

        Assert.Contains("/Login", _page.Url);

        var loginPage = new LoginPage(_page);
        Assert.True(await loginPage.IsDisplayedAsync());
    }

    [Fact]
    public async Task LoginPage_ShowsExpectedElements()
    {
        var loginPage = new LoginPage(_page);
        await loginPage.NavigateAsync();

        Assert.True(await loginPage.IsDisplayedAsync());
        Assert.True(await loginPage.HasRegisterLinkAsync());
        Assert.True(await _page.GetByRole(AriaRole.Button, new() { Name = "Login" }).IsVisibleAsync());
    }

    [Fact]
    public async Task Login_WithValidCredentials_RedirectsToDashboard()
    {
        var loginPage = new LoginPage(_page);
        await loginPage.NavigateAsync();

        await loginPage.LoginAsync(TestConstants.AdminUserName, TestConstants.AdminPassword);

        // Should redirect to dashboard
        await _page.WaitForSelectorAsync("text=Dashboard", new() { Timeout = 15000 });

        var dashboard = new AdminDashboardPage(_page);
        Assert.True(await dashboard.IsDisplayedAsync());
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ShowsError()
    {
        var loginPage = new LoginPage(_page);
        await loginPage.NavigateAsync();

        await loginPage.LoginAsync("admin", "WrongPassword123!");

        // Should stay on login page with error message
        await _page.WaitForSelectorAsync("text=Invalid login attempt.", new() { Timeout = 5000 });

        var error = await loginPage.GetErrorMessageAsync();
        Assert.NotNull(error);
        Assert.Contains("Invalid login attempt", error);
    }

    [Fact]
    public async Task Login_WithEmptyCredentials_StaysOnLoginPage()
    {
        var loginPage = new LoginPage(_page);
        await loginPage.NavigateAsync();

        await loginPage.LoginAsync("", "");

        // Should remain on login page
        Assert.Contains("/Login", _page.Url);
    }
}
