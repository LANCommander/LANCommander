using Microsoft.Playwright;

namespace LANCommander.Server.UI.Tests.Pages;

/// <summary>
/// Page object for the admin dashboard and navigation.
/// </summary>
public class AdminDashboardPage
{
    private readonly IPage _page;

    public AdminDashboardPage(IPage page)
    {
        _page = page;
    }

    public async Task<bool> IsDisplayedAsync()
    {
        return await _page.GetByText("Dashboard").First.IsVisibleAsync();
    }

    public async Task<string> GetPageTitleAsync()
    {
        return await _page.TitleAsync();
    }

    // Navigation helpers
    public async Task NavigateToGamesAsync()
    {
        await _page.GetByRole(AriaRole.Link, new() { Name = "Games" }).ClickAsync();
        await _page.WaitForURLAsync("**/Games", new() { Timeout = 10000 });
        // Wait for Blazor to render the page content
        await _page.WaitForSelectorAsync("text=Add Game", new() { Timeout = 10000 });
    }

    public async Task NavigateToSettingsGeneralAsync()
    {
        await _page.GetByRole(AriaRole.Button, new() { Name = "Settings" }).ClickAsync();
        await _page.GetByRole(AriaRole.Link, new() { Name = "General" }).ClickAsync();
        await _page.WaitForURLAsync("**/Settings/General", new() { Timeout = 10000 });
        // Wait for Blazor to render the settings content
        await _page.WaitForSelectorAsync("text=Database Provider", new() { Timeout = 10000 });
    }

    public async Task NavigateToRedistributablesAsync()
    {
        await _page.GetByRole(AriaRole.Link, new() { Name = "Redistributables" }).ClickAsync();
        await _page.WaitForURLAsync("**/Redistributables", new() { Timeout = 10000 });
    }

    public async Task NavigateToServersAsync()
    {
        await _page.GetByRole(AriaRole.Link, new() { Name = "Servers", Exact = true }).ClickAsync();
        await _page.WaitForURLAsync("**/Servers", new() { Timeout = 10000 });
    }

    public async Task NavigateToIssuesAsync()
    {
        await _page.GetByRole(AriaRole.Link, new() { Name = "Issues" }).ClickAsync();
        await _page.WaitForURLAsync("**/Issues", new() { Timeout = 10000 });
    }

    public async Task NavigateToFilesAsync()
    {
        await _page.GetByRole(AriaRole.Link, new() { Name = "Files" }).ClickAsync();
        await _page.WaitForURLAsync("**/Files", new() { Timeout = 10000 });
    }

    public async Task NavigateToToolsAsync()
    {
        await _page.GetByRole(AriaRole.Link, new() { Name = "Tools", Exact = true }).ClickAsync();
        await _page.WaitForURLAsync("**/Tools", new() { Timeout = 10000 });
    }

    /// <summary>
    /// Gets the visible menu items from the sidebar navigation.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetMainMenuItemsAsync()
    {
        // Wait for sidebar menu items to render
        await _page.GetByRole(AriaRole.Complementary)
            .Locator("[role='menuitem']")
            .First
            .WaitForAsync(new() { Timeout = 10000 });

        var menuItems = _page.GetByRole(AriaRole.Complementary).Locator("[role='menuitem']");
        var count = await menuItems.CountAsync();
        var items = new List<string>();

        for (int i = 0; i < count; i++)
        {
            var text = await menuItems.Nth(i).TextContentAsync();
            if (!string.IsNullOrWhiteSpace(text))
                items.Add(text.Trim());
        }

        return items;
    }
}
