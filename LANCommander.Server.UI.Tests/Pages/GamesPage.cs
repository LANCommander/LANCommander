using Microsoft.Playwright;

namespace LANCommander.Server.UI.Tests.Pages;

/// <summary>
/// Page object for the Games list page and the import dialog flow.
/// </summary>
public class GamesPage
{
    private readonly IPage _page;

    public GamesPage(IPage page)
    {
        _page = page;
    }

    /// <summary>
    /// Navigate to the Games page and wait for it to render.
    /// </summary>
    public async Task NavigateAsync()
    {
        await _page.GetByRole(AriaRole.Link, new() { Name = "Games" }).ClickAsync();
        await _page.WaitForURLAsync("**/Games", new() { Timeout = 10000 });
        // Wait for the page toolbar to render (Add Game button is always present)
        await _page.WaitForSelectorAsync("text=Add Game", new() { Timeout = 10000 });
    }

    /// <summary>
    /// Returns the number of games displayed in the table, or 0 if the empty state is shown.
    /// </summary>
    public async Task<int> GetGameCountAsync()
    {
        var noData = _page.GetByText("No data");
        if (await noData.IsVisibleAsync())
            return 0;

        // Game rows are table body rows; each row has a title cell
        var rows = _page.Locator(".ant-table-tbody tr.ant-table-row");
        return await rows.CountAsync();
    }

    /// <summary>
    /// Checks whether a game with the given title is visible in the table.
    /// Waits up to the given timeout for the element to appear.
    /// </summary>
    public async Task<bool> IsGameVisibleAsync(string title, int timeoutMs = 10000)
    {
        try
        {
            await _page.GetByRole(AriaRole.Cell, new() { Name = title, Exact = true })
                .WaitForAsync(new() { Timeout = timeoutMs });
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// Runs the full import flow: open dialog, upload file, select all items, import, close.
    /// </summary>
    public async Task ImportGameAsync(string filePath)
    {
        // Stage 1 – Open the import dialog
        await _page.GetByRole(AriaRole.Button, new() { Name = "Import" }).ClickAsync();

        // The modal renders inside .ant-modal-wrap
        var modal = _page.Locator(".ant-modal-wrap");

        // Wait for the modal with file input to be attached (it's hidden via opacity: 0)
        await modal.Locator("input[type='file']").WaitForAsync(new()
        {
            State = WaitForSelectorState.Attached,
            Timeout = 15000
        });

        // Upload the file via the hidden input
        await modal.Locator("input[type='file']").SetInputFilesAsync(filePath);

        // Click the "Upload" button to start the chunk upload
        await modal.GetByRole(AriaRole.Button, new() { Name = "Upload", Exact = true }).ClickAsync();

        // Stage 2 – Wait for the record-selection tree to appear (has checkboxes)
        await modal.Locator(".ant-tree").WaitForAsync(new() { Timeout = 60000 });

        // Select all tree checkboxes that aren't already checked
        var uncheckedBoxes = modal.Locator(".ant-tree-checkbox:not(.ant-tree-checkbox-checked)");
        var count = await uncheckedBoxes.CountAsync();
        for (var i = 0; i < count; i++)
        {
            var first = modal.Locator(".ant-tree-checkbox:not(.ant-tree-checkbox-checked)").First;
            if (await first.CountAsync() == 0)
                break;
            await first.ClickAsync();
        }

        // Click the Import button inside the modal to start the import
        await modal.GetByRole(AriaRole.Button, new() { Name = "Import", Exact = true }).ClickAsync();

        // Stage 3/4 – Wait for the "Close" button which appears on completion
        await modal.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true })
            .WaitForAsync(new() { Timeout = 60000 });

        // Close the dialog
        await modal.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();

        // Wait for the modal to animate out and the table to reload
        await _page.WaitForTimeoutAsync(2000);
    }

    /// <summary>
    /// Click on a game row to open its edit page.
    /// </summary>
    public async Task OpenGameEditAsync(string title)
    {
        await _page.GetByRole(AriaRole.Cell, new() { Name = title, Exact = true }).ClickAsync();
        await _page.WaitForURLAsync("**/Games/*/Edit/**", new() { Timeout = 15000 });
    }
}
