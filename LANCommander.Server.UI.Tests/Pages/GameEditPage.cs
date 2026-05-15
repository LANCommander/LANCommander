using Microsoft.Playwright;

namespace LANCommander.Server.UI.Tests.Pages;

/// <summary>
/// Page object for the game edit page at /Games/{id}/General and related tabs.
/// </summary>
public class GameEditPage
{
    private readonly IPage _page;
    private const int DefaultTimeout = 15000;

    public GameEditPage(IPage page)
    {
        _page = page;
    }

    /// <summary>
    /// Navigates directly to a game's edit page by its ID.
    /// </summary>
    public async Task NavigateToGameByIdAsync(Guid gameId)
    {
        var uri = new Uri(_page.Url);
        var baseUrl = $"{uri.Scheme}://{uri.Authority}";
        await _page.GotoAsync($"{baseUrl}/Games/{gameId}/General");
        await WaitForFormLoadedAsync();
    }

    /// <summary>
    /// Ensures a game is imported and navigates to its edit page.
    /// If the game already exists, it opens the edit page directly.
    /// </summary>
    public async Task NavigateAsync(string gameTitle, string lcxFilePath)
    {
        var gamesPage = new GamesPage(_page);
        await gamesPage.NavigateAsync();

        if (!await gamesPage.IsGameVisibleAsync(gameTitle, timeoutMs: 3000))
        {
            await gamesPage.ImportGameAsync(lcxFilePath);
        }

        await gamesPage.OpenGameEditAsync(gameTitle);

        // The edit page may land on /Games/{id} or /Games/{id}/General
        await WaitForFormLoadedAsync();
    }

    /// <summary>
    /// Gets the current game title from the form input.
    /// </summary>
    public async Task<string?> GetTitleAsync()
    {
        var input = GetTitleInput();
        await input.WaitForAsync(new() { Timeout = DefaultTimeout });
        return await input.InputValueAsync();
    }

    /// <summary>
    /// Sets the game title in the form input.
    /// </summary>
    public async Task SetTitleAsync(string title)
    {
        var input = GetTitleInput();
        await input.WaitForAsync(new() { Timeout = DefaultTimeout });
        await input.ClickAsync();
        await input.PressAsync("Control+a");
        await input.TypeAsync(title);
        await input.PressAsync("Tab");
    }

    /// <summary>
    /// Clicks the Save button and waits for the success notification.
    /// </summary>
    public async Task SaveAsync()
    {
        await _page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();

        // Wait for the success notification to appear
        try
        {
            await _page.WaitForSelectorAsync(".ant-notification", new() { Timeout = 10000 });
        }
        catch (TimeoutException)
        {
            // Notification may not appear in all cases; continue
        }

        await _page.WaitForTimeoutAsync(500);
    }

    /// <summary>
    /// Navigates to a specific tab by clicking the corresponding menu item in the game edit sidebar.
    /// </summary>
    public async Task NavigateToTabAsync(string tabName)
    {
        var menuItem = GetSiderMenu().GetByRole(AriaRole.Menuitem, new() { Name = tabName, Exact = true });
        await menuItem.ClickAsync();
        await _page.WaitForTimeoutAsync(1000);
    }

    /// <summary>
    /// Checks whether a tab (menu item) with the given name is visible in the game edit sidebar.
    /// </summary>
    public async Task<bool> IsTabVisibleAsync(string tabName)
    {
        var menuItem = GetSiderMenu().GetByRole(AriaRole.Menuitem, new() { Name = tabName, Exact = true });

        try
        {
            await menuItem.WaitForAsync(new() { Timeout = 5000 });
            return await menuItem.IsVisibleAsync();
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// Checks whether the Export button is visible on the page.
    /// </summary>
    public async Task<bool> IsExportButtonVisibleAsync()
    {
        return await _page.GetByRole(AriaRole.Button, new() { Name = "Export", Exact = true })
            .IsVisibleAsync();
    }

    /// <summary>
    /// Checks whether the Save button is visible on the page.
    /// </summary>
    public async Task<bool> IsSaveButtonVisibleAsync()
    {
        return await _page.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true })
            .IsVisibleAsync();
    }

    /// <summary>
    /// Gets the current page URL.
    /// </summary>
    public string GetCurrentUrl() => _page.Url;

    private ILocator GetTitleInput()
    {
        return _page.Locator(".ant-form-item")
            .Filter(new() { HasText = "Title" })
            .First
            .Locator("input")
            .First;
    }

    /// <summary>
    /// Returns the sidebar menu locator scoped to the game edit panel layout.
    /// </summary>
    private ILocator GetSiderMenu()
    {
        return _page.Locator(".panel-layout .ant-layout-sider .ant-menu");
    }

    private async Task WaitForFormLoadedAsync()
    {
        // Wait for the form to render by checking for the Title form item
        await _page.Locator(".ant-form-item").Filter(new() { HasText = "Title" }).First
            .WaitForAsync(new() { Timeout = DefaultTimeout });
    }
}
