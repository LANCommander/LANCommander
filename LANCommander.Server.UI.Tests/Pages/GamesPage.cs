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
        // Wait a moment for table rendering
        await _page.WaitForTimeoutAsync(500);

        var noData = _page.GetByText("No data");
        if (await noData.IsVisibleAsync())
            return 0;

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

        // Wait for the file input to be attached (it's hidden via opacity: 0)
        var fileInput = modal.Locator("input[type='file']");
        await fileInput.WaitForAsync(new()
        {
            State = WaitForSelectorState.Attached,
            Timeout = 15000
        });

        // Wait for Blazor's InputFile JS interop to register the change handler.
        // On CI runners (especially headless Linux), there's a race condition where
        // SetInputFilesAsync dispatches the change event before Blazor's handler is ready.
        await _page.WaitForTimeoutAsync(2000);

        // Set the files on the input element
        await fileInput.SetInputFilesAsync(filePath);

        // Try to click the Upload button. If it's still disabled (Blazor didn't process
        // the change event), retry by dispatching the change event manually via JS.
        var uploadBtn = modal.GetByRole(AriaRole.Button, new() { Name = "Upload", Exact = true });
        try
        {
            await uploadBtn.ClickAsync(new() { Timeout = 5000 });
        }
        catch (TimeoutException)
        {
            // Re-dispatch the change event for Blazor's InputFile component
            await fileInput.EvaluateAsync("el => el.dispatchEvent(new Event('change', { bubbles: true }))");
            await _page.WaitForTimeoutAsync(1000);

            // If still disabled, try setting files again with a fresh dispatch
            if (await uploadBtn.IsDisabledAsync())
            {
                await fileInput.SetInputFilesAsync(filePath);
                await _page.WaitForTimeoutAsync(1000);
            }

            await uploadBtn.ClickAsync(new() { Timeout = 15000 });
        }

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

        // Wait for the modal to animate out, then navigate to Games to ensure fresh table
        await _page.WaitForTimeoutAsync(1000);
        await _page.GotoAsync(_page.Url.Split('?')[0]);
        await _page.WaitForSelectorAsync("text=Add Game", new() { Timeout = 10000 });
    }

    /// <summary>
    /// Click the Edit link for a game to open its detail/edit page.
    /// </summary>
    public async Task OpenGameEditAsync(string title)
    {
        // Find the table row containing the game title, then click its Edit link
        var row = _page.Locator("tr.ant-table-row", new() { HasText = title });
        await row.GetByRole(AriaRole.Link, new() { Name = "Edit" }).ClickAsync();
        await _page.WaitForURLAsync("**/Games/*", new() { Timeout = 15000 });
    }
}
