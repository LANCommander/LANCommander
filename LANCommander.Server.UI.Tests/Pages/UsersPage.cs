using Microsoft.Playwright;

namespace LANCommander.Server.UI.Tests.Pages;

/// <summary>
/// Page object for the Settings > Users page at /Settings/Users.
/// </summary>
public class UsersPage
{
    private readonly IPage _page;

    public UsersPage(IPage page)
    {
        _page = page;
    }

    /// <summary>
    /// Navigates to the Users page via the Settings sidebar menu and waits for the table to load.
    /// </summary>
    public async Task NavigateAsync()
    {
        await _page.GetByRole(AriaRole.Button, new() { Name = "Settings" }).ClickAsync();
        await _page.GetByRole(AriaRole.Link, new() { Name = "Users", Exact = true }).ClickAsync();
        await _page.WaitForURLAsync("**/Settings/Users", new() { Timeout = 10000 });
        // Wait for table data rows to render (Blazor SSR + async data load)
        await WaitForTableDataAsync();
    }

    /// <summary>
    /// Returns the number of data rows in the users table.
    /// </summary>
    public async Task<int> GetUserCountAsync()
    {
        return await _page.Locator("tr.ant-table-row").CountAsync();
    }

    /// <summary>
    /// Searches for users by typing into the DataTable search input and waiting for results.
    /// </summary>
    public async Task SearchUsersAsync(string query)
    {
        var searchInput = _page.GetByPlaceholder("Search");
        await searchInput.FillAsync(query);
        // Press Enter to ensure the search triggers via Blazor's event pipeline
        await searchInput.PressAsync("Enter");
        // Wait for the server-side search to complete and table to re-render
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
        await _page.WaitForTimeoutAsync(1000);
    }

    /// <summary>
    /// Checks whether a given username appears in a table row.
    /// </summary>
    public async Task<bool> IsUserVisibleAsync(string username)
    {
        return await _page.Locator("tr.ant-table-row", new() { HasTextString = username }).CountAsync() > 0;
    }

    /// <summary>
    /// Gets the text content of the Roles column for a given user.
    /// </summary>
    public async Task<string> GetUserRolesTextAsync(string username)
    {
        var row = _page.Locator("tr.ant-table-row", new() { HasTextString = username });
        // Roles are rendered as <span class="ant-tag"> inside the row
        var tags = row.Locator(".ant-tag");
        var count = await tags.CountAsync();
        var roles = new List<string>();
        for (int i = 0; i < count; i++)
        {
            var text = await tags.Nth(i).TextContentAsync();
            if (!string.IsNullOrWhiteSpace(text))
                roles.Add(text.Trim());
        }
        return string.Join(", ", roles);
    }

    /// <summary>
    /// Deletes a user by clicking the delete button on their row and confirming the popconfirm.
    /// </summary>
    public async Task DeleteUserAsync(string username)
    {
        var row = _page.Locator("tr.ant-table-row", new() { HasTextString = username });
        // Click the danger button (delete icon) in the row - AntDesign renders it as ant-btn-dangerous
        await row.Locator("button.ant-btn-dangerous").ClickAsync();
        // Wait for the Popconfirm overlay to appear and click OK
        var okButton = _page.GetByRole(AriaRole.Button, new() { Name = "OK" });
        await okButton.WaitForAsync(new() { Timeout = 5000 });
        await okButton.ClickAsync();
        // Wait for the deletion round-trip and table re-render
        await _page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 });
        await _page.WaitForTimeoutAsync(1000);
    }

    private async Task WaitForTableDataAsync()
    {
        await _page.Locator("tr.ant-table-row").First.WaitForAsync(new() { Timeout = 15000 });
    }
}
