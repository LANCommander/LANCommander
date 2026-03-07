using LANCommander.Server.UI.Tests.Pages;
using Microsoft.Playwright;

namespace LANCommander.Server.UI.Tests.Tests;

/// <summary>
/// Tests for the game edit page, covering tab navigation, form fields, and persistence.
/// </summary>
[Collection("Server")]
public class GameEditTests : IAsyncLifetime
{
    private static readonly string LcxFilePath = Path.Combine(AppContext.BaseDirectory, "TestData", "OpenRCT2.lcx");
    private const string ExpectedGameTitle = "OpenRCT2";

    private readonly ConfiguredServerFixture _fixture;
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public GameEditTests(ConfiguredServerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        (_context, _page) = await _fixture.CreateLoggedInPageAsync();
    }

    public async Task DisposeAsync()
    {
        if (_page != null) await _page.CloseAsync();
        if (_context != null) await _context.DisposeAsync();
    }

    private async Task<GameEditPage> NavigateToGameEditAsync()
    {
        var editPage = new GameEditPage(_page);
        await editPage.NavigateAsync(ExpectedGameTitle, LcxFilePath);
        return editPage;
    }

    [Fact]
    public async Task GameEdit_ShowsGeneralTab()
    {
        var editPage = await NavigateToGameEditAsync();

        var title = await editPage.GetTitleAsync();
        Assert.False(string.IsNullOrWhiteSpace(title), "Title input should have a value");
        Assert.Contains("/Games/", editPage.GetCurrentUrl());
    }

    [Fact]
    public async Task GameEdit_CanModifyTitle()
    {
        var editPage = await NavigateToGameEditAsync();
        const string modifiedTitle = "OpenRCT2 Modified";

        await editPage.SetTitleAsync(modifiedTitle);
        await editPage.SaveAsync();

        // Reload the page to verify persistence
        await _page.ReloadAsync();
        await _page.WaitForSelectorAsync(".ant-form-item", new() { Timeout = 15000 });

        var editPageAfterReload = new GameEditPage(_page);
        var titleAfterReload = await editPageAfterReload.GetTitleAsync();
        Assert.Equal(modifiedTitle, titleAfterReload);

        // Restore original title for other tests
        await editPageAfterReload.SetTitleAsync(ExpectedGameTitle);
        await editPageAfterReload.SaveAsync();
    }

    [Fact]
    public async Task GameEdit_ShowsAllExpectedTabs()
    {
        var editPage = await NavigateToGameEditAsync();

        var expectedTabs = new[] { "General", "Archives", "Media", "Scripts", "Actions", "Keys" };

        foreach (var tab in expectedTabs)
        {
            Assert.True(await editPage.IsTabVisibleAsync(tab), $"Tab '{tab}' should be visible");
        }
    }

    [Fact]
    public async Task GameEdit_CanNavigateToMediaTab()
    {
        var editPage = await NavigateToGameEditAsync();

        await editPage.NavigateToTabAsync("Media");

        Assert.Contains("/Media", editPage.GetCurrentUrl());
    }

    [Fact]
    public async Task GameEdit_CanNavigateToScriptsTab()
    {
        var editPage = await NavigateToGameEditAsync();

        await editPage.NavigateToTabAsync("Scripts");

        Assert.Contains("/Scripts", editPage.GetCurrentUrl());
    }

    [Fact]
    public async Task GameEdit_HasExportButton()
    {
        var editPage = await NavigateToGameEditAsync();

        Assert.True(await editPage.IsExportButtonVisibleAsync(), "Export button should be visible");
    }

    [Fact]
    public async Task GameEdit_HasSaveButton()
    {
        var editPage = await NavigateToGameEditAsync();

        Assert.True(await editPage.IsSaveButtonVisibleAsync(), "Save button should be visible");
    }
}
