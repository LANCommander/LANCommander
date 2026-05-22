using Microsoft.Playwright;

namespace LANCommander.Server.UI.Tests.Pages;

/// <summary>
/// Page object for the user profile page at /Profile and the change password page at /Profile/ChangePassword.
/// </summary>
public class ProfilePage
{
    private readonly IPage _page;

    public ProfilePage(IPage page)
    {
        _page = page;
    }

    public async Task NavigateAsync()
    {
        await _page.GotoAsync("/Profile");
        await _page.WaitForSelectorAsync("text=Profile", new() { Timeout = 10000 });
    }

    public async Task<string?> GetUsernameAsync()
    {
        var input = _page.Locator(".ant-form-item")
            .Filter(new() { HasText = "Username" })
            .Locator("input");
        return await input.InputValueAsync();
    }

    public async Task<bool> HasFieldAsync(string label)
    {
        return await _page.Locator(".ant-form-item")
            .Filter(new() { HasText = label })
            .IsVisibleAsync();
    }

    public async Task SetAliasAsync(string alias)
    {
        var input = _page.Locator(".ant-form-item")
            .Filter(new() { HasText = "Alias" })
            .Locator("input");
        await input.ClickAsync();
        await input.PressAsync("Control+a");
        await input.TypeAsync(alias);
        // Blur to ensure change event fires
        await input.PressAsync("Tab");
    }

    public async Task<string?> GetAliasAsync()
    {
        var input = _page.Locator(".ant-form-item")
            .Filter(new() { HasText = "Alias" })
            .Locator("input");
        return await input.InputValueAsync();
    }

    /// <summary>
    /// Clicks the Save button. Note: saving the profile triggers a redirect to /Logout?force=true.
    /// </summary>
    public async Task SaveAsync()
    {
        await _page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
    }

    public async Task NavigateToChangePasswordAsync()
    {
        await _page.GotoAsync("/Profile/ChangePassword");
        await _page.WaitForSelectorAsync("text=Change Password", new() { Timeout = 10000 });
    }

    public async Task<bool> HasPasswordFieldAsync(string label)
    {
        return await _page.Locator(".ant-form-item")
            .Filter(new() { HasText = label })
            .IsVisibleAsync();
    }

    /// <summary>
    /// Fills the change password form and submits it.
    /// </summary>
    public async Task ChangePasswordAsync(string currentPassword, string newPassword)
    {
        // Fill Current Password if visible
        var currentPasswordField = _page.Locator(".ant-form-item")
            .Filter(new() { HasText = "Current Password" })
            .Locator("input");

        if (await currentPasswordField.IsVisibleAsync())
            await currentPasswordField.FillAsync(currentPassword);

        // Fill New Password
        await _page.Locator(".ant-form-item")
            .Filter(new() { HasText = "New Password" })
            .Locator("input")
            .FillAsync(newPassword);

        // Fill Confirm Password
        await _page.Locator(".ant-form-item")
            .Filter(new() { HasText = "Confirm Password" })
            .Locator("input")
            .FillAsync(newPassword);

        // Click Change button
        await _page.GetByRole(AriaRole.Button, new() { Name = "Change" }).ClickAsync();
    }
}
