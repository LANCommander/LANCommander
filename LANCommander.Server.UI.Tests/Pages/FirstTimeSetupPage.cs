using Microsoft.Playwright;

namespace LANCommander.Server.UI.Tests.Pages;

/// <summary>
/// Page object for the First Time Setup wizard at /FirstTimeSetup.
/// </summary>
public class FirstTimeSetupPage
{
    private readonly IPage _page;

    public FirstTimeSetupPage(IPage page)
    {
        _page = page;
    }

    public async Task NavigateAsync()
    {
        await _page.GotoAsync("/FirstTimeSetup");
        // Wait for Blazor to render the page
        await _page.WaitForSelectorAsync("text=First Time Setup", new() { Timeout = 10000 });
    }

    public async Task<bool> IsDisplayedAsync()
    {
        return await _page.GetByText("First Time Setup").IsVisibleAsync();
    }

    // Step 1: Database
    public async Task SelectDatabaseProviderAsync(string provider)
    {
        await _page.GetByRole(AriaRole.Combobox).ClickAsync();
        await _page.GetByRole(AriaRole.Option, new() { Name = provider }).ClickAsync();
    }

    public async Task ClickConnectAsync()
    {
        await _page.GetByRole(AriaRole.Button, new() { Name = "Connect" }).ClickAsync();
    }

    public async Task CompleteDatabaseStepAsync(string provider = "SQLite")
    {
        await SelectDatabaseProviderAsync(provider);
        await ClickConnectAsync();
        // Wait for navigation to paths step
        await _page.WaitForURLAsync("**/FirstTimeSetup/Paths", new() { Timeout = 30000 });
    }

    // Step 2: Paths
    public async Task CompletePathsStepAsync()
    {
        await _page.GetByRole(AriaRole.Button, new() { Name = "Next" }).ClickAsync();
        await _page.WaitForURLAsync("**/FirstTimeSetup/Metadata", new() { Timeout = 10000 });
    }

    // Step 3: Metadata
    public async Task CompleteMetadataStepAsync()
    {
        await _page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await _page.WaitForURLAsync("**/FirstTimeSetup/Administrator", new() { Timeout = 10000 });
    }

    // Step 4: Administrator
    public async Task CreateAdministratorAsync(string username, string password)
    {
        // AntDesign doesn't use standard label/for associations, so use role-based selection
        // Username is the first textbox on the Administrator step
        await _page.WaitForSelectorAsync("text=To get started", new() { Timeout = 10000 });
        await _page.GetByRole(AriaRole.Textbox).First.FillAsync(username);
        await _page.Locator("input[name='context.Password']").FillAsync(password);
        await _page.Locator("input[name='context.PasswordConfirm']").FillAsync(password);
        await _page.GetByRole(AriaRole.Button, new() { Name = "Create" }).ClickAsync();
    }

    /// <summary>
    /// Completes the entire first-time setup wizard from start to finish.
    /// </summary>
    public async Task CompleteFullSetupAsync(
        string adminUsername = "admin",
        string adminPassword = "Password1234!",
        string databaseProvider = "SQLite")
    {
        await CompleteDatabaseStepAsync(databaseProvider);
        await CompletePathsStepAsync();
        await CompleteMetadataStepAsync();
        await CreateAdministratorAsync(adminUsername, adminPassword);

        // Wait for the success message
        await _page.WaitForSelectorAsync("text=Setup completed", new() { Timeout = 15000 });

        // Wait for redirect to login page (may already have happened)
        await _page.WaitForSelectorAsync("text=User Name", new() { Timeout = 15000 });
    }
}
