using Microsoft.Playwright;

namespace LANCommander.Server.UI.Tests.Pages;

/// <summary>
/// Page object for the Roles management page at /Settings/Roles.
/// </summary>
public class RolesPage
{
    private readonly IPage _page;

    public RolesPage(IPage page)
    {
        _page = page;
    }

    /// <summary>
    /// Navigate to Settings > Roles via the sidebar menu.
    /// </summary>
    public async Task NavigateAsync()
    {
        await _page.GetByRole(AriaRole.Button, new() { Name = "Settings" }).ClickAsync();
        await _page.GetByRole(AriaRole.Link, new() { Name = "Roles", Exact = true }).ClickAsync();
        await _page.WaitForURLAsync("**/Settings/Roles", new() { Timeout = 10000 });
        await _page.WaitForSelectorAsync("text=Add Role", new() { Timeout = 10000 });
    }

    /// <summary>
    /// Click "Add Role", fill in the name, and confirm the modal.
    /// </summary>
    public async Task AddRoleAsync(string name)
    {
        await _page.GetByRole(AriaRole.Button, new() { Name = "Add Role" }).ClickAsync();

        // Wait for the modal to appear
        await _page.WaitForSelectorAsync(".ant-modal", new() { State = WaitForSelectorState.Visible, Timeout = 10000 });

        // Fill the Name input inside the modal
        var modal = _page.Locator(".ant-modal");
        await modal.GetByRole(AriaRole.Textbox).FillAsync(name);

        // Click the OK button in the modal footer
        await modal.Locator(".ant-modal-footer").GetByRole(AriaRole.Button, new() { Name = "OK" }).ClickAsync();

        // Wait for modal to close
        await _page.WaitForSelectorAsync(".ant-modal", new() { State = WaitForSelectorState.Hidden, Timeout = 10000 });
    }

    /// <summary>
    /// Check whether a role with the given name appears in the table.
    /// </summary>
    public async Task<bool> IsRoleVisibleAsync(string name)
    {
        var row = _page.Locator("table tbody tr").Filter(new() { HasText = name });
        return await row.CountAsync() > 0;
    }

    /// <summary>
    /// Delete a role by clicking its delete button and confirming the popconfirm.
    /// </summary>
    public async Task DeleteRoleAsync(string name)
    {
        var row = _page.Locator("table tbody tr").Filter(new() { HasText = name });

        // Click the close/delete button (the danger text button with close icon)
        await row.Locator("button.ant-btn-dangerous").ClickAsync();

        // Wait for the popconfirm popover to appear and click the OK button
        var okButton = _page.Locator(".ant-popover .ant-btn-primary");
        await okButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
        await okButton.ClickAsync();

        // Wait for the row to be removed from the table
        await row.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 10000 });
    }

    /// <summary>
    /// Check whether the delete button for a given role is disabled.
    /// </summary>
    public async Task<bool> IsDeleteDisabledAsync(string name)
    {
        var row = _page.Locator("table tbody tr").Filter(new() { HasText = name });

        // The close button in the row (both the real delete and disabled version use the same icon)
        var deleteButton = row.Locator("button").Filter(new() { Has = _page.Locator("[aria-label='close']") });

        if (await deleteButton.CountAsync() == 0)
            return true; // No delete button means it's effectively non-deletable

        return await deleteButton.IsDisabledAsync();
    }
}
