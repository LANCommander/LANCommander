using Microsoft.Playwright;

namespace LANCommander.Server.UI.Tests.Pages;

/// <summary>
/// Reusable page object for metadata management pages (Tags, Genres, Platforms).
/// All metadata pages share the same layout: a DataTable with Add/Edit/Delete actions
/// and a modal dialog for creating/editing items.
/// </summary>
public class MetadataPage
{
    private readonly IPage _page;
    private readonly string _metadataType;
    private readonly string _singularType;

    /// <param name="page">Playwright page instance.</param>
    /// <param name="metadataType">Plural type name used in the URL, e.g. "Tags", "Genres", "Platforms".</param>
    public MetadataPage(IPage page, string metadataType)
    {
        _page = page;
        _metadataType = metadataType;
        _singularType = metadataType.TrimEnd('s');
    }

    public async Task NavigateAsync()
    {
        await _page.GotoAsync($"/Metadata/{_metadataType}");
        await _page.WaitForSelectorAsync($"text={_metadataType}", new() { Timeout = 10000 });
    }

    public async Task AddItemAsync(string name)
    {
        await _page.GetByRole(AriaRole.Button, new() { Name = $"Add {_singularType}" }).ClickAsync();

        // Wait for the modal to appear
        await _page.WaitForSelectorAsync($"text=New {_singularType}", new() { Timeout = 5000 });

        var nameInput = _page.Locator(".ant-modal").GetByRole(AriaRole.Textbox);
        await nameInput.FillAsync(name);

        await _page.Locator(".ant-modal").GetByRole(AriaRole.Button, new() { Name = "OK" }).ClickAsync();

        // Wait for modal to close and table to refresh
        await _page.WaitForSelectorAsync(".ant-modal", new() { State = WaitForSelectorState.Hidden, Timeout = 5000 });
    }

    public async Task<bool> IsItemVisibleAsync(string name)
    {
        // Look for the name text within the table body
        var cell = _page.Locator(".ant-table-tbody").GetByText(name, new() { Exact = true });
        return await cell.IsVisibleAsync();
    }

    public async Task EditItemAsync(string oldName, string newName)
    {
        // Find the row containing the old name and click its Edit button
        var row = _page.Locator(".ant-table-tbody tr").Filter(new() { HasText = oldName });
        await row.GetByRole(AriaRole.Button, new() { Name = "Edit" }).ClickAsync();

        // Wait for the edit modal to appear
        await _page.WaitForSelectorAsync($"text=Edit {_singularType}", new() { Timeout = 5000 });

        var nameInput = _page.Locator(".ant-modal").GetByRole(AriaRole.Textbox);
        await nameInput.ClearAsync();
        await nameInput.FillAsync(newName);

        await _page.Locator(".ant-modal").GetByRole(AriaRole.Button, new() { Name = "OK" }).ClickAsync();

        // Wait for modal to close and table to refresh
        await _page.WaitForSelectorAsync(".ant-modal", new() { State = WaitForSelectorState.Hidden, Timeout = 5000 });
    }

    public async Task DeleteItemAsync(string name)
    {
        // Find the row containing the name and click its delete (close icon) button
        var row = _page.Locator(".ant-table-tbody tr").Filter(new() { HasText = name });
        await row.Locator("button.ant-btn-dangerous").ClickAsync();

        // Wait for popconfirm to appear and click OK to confirm deletion
        await _page.WaitForSelectorAsync(".ant-popover", new() { Timeout = 5000 });
        await _page.Locator(".ant-popover").GetByRole(AriaRole.Button, new() { Name = "OK" }).ClickAsync();

        // Wait for popconfirm to close
        await _page.WaitForSelectorAsync(".ant-popover", new() { State = WaitForSelectorState.Hidden, Timeout = 5000 });
    }

    public async Task<int> GetItemCountAsync()
    {
        var noData = _page.GetByText("No data");
        if (await noData.IsVisibleAsync())
            return 0;

        return await _page.Locator(".ant-table-tbody tr").CountAsync();
    }
}
