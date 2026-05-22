using Microsoft.Playwright;

namespace LANCommander.Server.UI.Tests.Pages;

/// <summary>
/// Page object for the login page at /Login.
/// </summary>
public class LoginPage
{
    private readonly IPage _page;

    public LoginPage(IPage page)
    {
        _page = page;
    }

    public async Task NavigateAsync()
    {
        await _page.GotoAsync("/Login");
    }

    public async Task<bool> IsDisplayedAsync()
    {
        return await _page.GetByRole(AriaRole.Textbox, new() { Name = "User Name" }).IsVisibleAsync();
    }

    public async Task LoginAsync(string username, string password)
    {
        await _page.GetByRole(AriaRole.Textbox, new() { Name = "User Name" }).FillAsync(username);
        await _page.GetByRole(AriaRole.Textbox, new() { Name = "Password" }).FillAsync(password);
        await _page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();
    }

    public async Task<string?> GetErrorMessageAsync()
    {
        var errorLocator = _page.GetByText("Invalid login attempt.");
        if (await errorLocator.IsVisibleAsync())
            return await errorLocator.TextContentAsync();
        return null;
    }

    public async Task<bool> HasRegisterLinkAsync()
    {
        return await _page.GetByRole(AriaRole.Link, new() { Name = "Register" }).IsVisibleAsync();
    }
}
