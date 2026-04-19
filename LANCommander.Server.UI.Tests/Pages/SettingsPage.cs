using Microsoft.Playwright;

namespace LANCommander.Server.UI.Tests.Pages;

/// <summary>
/// Page object for navigating to and interacting with Settings sub-pages.
/// </summary>
public class SettingsPage
{
    private readonly IPage _page;
    private const int DefaultTimeout = 15000;

    public SettingsPage(IPage page)
    {
        _page = page;
    }

    /// <summary>
    /// Expands the Settings submenu in the sidebar if it's not already open.
    /// </summary>
    private async Task ExpandSettingsMenuAsync()
    {
        await _page.GetByRole(AriaRole.Button, new() { Name = "Settings" }).ClickAsync();
    }

    /// <summary>
    /// Navigates to a settings sub-page by clicking the Settings menu button, then the sub-item link.
    /// Waits for the URL to match and the page header to render.
    /// </summary>
    private async Task NavigateToSettingsSubPageAsync(string linkName, string urlSegment, string? waitForText = null)
    {
        await ExpandSettingsMenuAsync();
        await _page.GetByRole(AriaRole.Link, new() { Name = linkName, Exact = true }).ClickAsync();
        await _page.WaitForURLAsync($"**/Settings/{urlSegment}", new() { Timeout = DefaultTimeout });

        if (waitForText != null)
            await _page.WaitForSelectorAsync($"text={waitForText}", new() { Timeout = DefaultTimeout });
    }

    public async Task NavigateToGeneralAsync()
    {
        await NavigateToSettingsSubPageAsync("General", "General", "Database Provider");
    }

    public async Task NavigateToUsersAsync()
    {
        await NavigateToSettingsSubPageAsync("Users", "Users", "Username");
    }

    public async Task NavigateToRolesAsync()
    {
        await NavigateToSettingsSubPageAsync("Roles", "Roles", "Add Role");
    }

    public async Task NavigateToAuthenticationAsync()
    {
        await NavigateToSettingsSubPageAsync("Authentication", "Authentication", "Authentication");
    }

    public async Task NavigateToArchivesAsync()
    {
        await NavigateToSettingsSubPageAsync("Archives", "Archives", "Archives");
    }

    public async Task NavigateToMediaAsync()
    {
        await NavigateToSettingsSubPageAsync("Media", "Media", "Media");
    }

    public async Task NavigateToUpdatesAsync()
    {
        await NavigateToSettingsSubPageAsync("Updates", "Updates", "Updates");
    }

    public async Task NavigateToAppearanceAsync()
    {
        await NavigateToSettingsSubPageAsync("Appearance", "Appearance", "Appearance");
    }

    public async Task NavigateToBeaconAsync()
    {
        await NavigateToSettingsSubPageAsync("Beacon", "Beacon", "Beacon");
    }
}
