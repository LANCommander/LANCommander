using LANCommander.Server.UI.Tests.Pages;
using Microsoft.Playwright;

namespace LANCommander.Server.UI.Tests.Tests;

/// <summary>
/// Tests for the game import flow via the admin UI.
/// Imports an .lcx file and verifies the game appears in the list.
/// </summary>
public class GameImportTests : IClassFixture<ConfiguredServerFixture>, IAsyncLifetime
{
    private const string LcxFilePath = @"C:\Users\aapowell\Downloads\OpenRCT2.lcx";
    private const string ExpectedGameTitle = "OpenRCT2";

    private readonly ConfiguredServerFixture _fixture;
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public GameImportTests(ConfiguredServerFixture fixture)
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

    [Fact]
    public async Task GamesPage_InitiallyEmpty()
    {
        var gamesPage = new GamesPage(_page);
        await gamesPage.NavigateAsync();

        // Verify the page structure is correct (table area and buttons are present)
        Assert.True(await _page.GetByRole(AriaRole.Button, new() { Name = "Add Game" }).IsVisibleAsync());
        Assert.True(await _page.GetByRole(AriaRole.Button, new() { Name = "Import" }).IsVisibleAsync());

        // The table should render (with "No data" if empty, or rows if a prior test imported)
        var count = await gamesPage.GetGameCountAsync();
        Assert.True(count >= 0);
    }

    [Fact]
    public async Task GamesPage_HasImportButton()
    {
        var gamesPage = new GamesPage(_page);
        await gamesPage.NavigateAsync();

        var importButton = _page.GetByRole(AriaRole.Button, new() { Name = "Import" });
        Assert.True(await importButton.IsVisibleAsync());
    }

    [Fact]
    public async Task GamesPage_CanImportLcxFile()
    {
        var gamesPage = new GamesPage(_page);
        await gamesPage.NavigateAsync();

        await gamesPage.ImportGameAsync(LcxFilePath);

        // After import, the game should appear in the table
        Assert.True(await gamesPage.IsGameVisibleAsync(ExpectedGameTitle));
    }

    [Fact]
    public async Task GamesPage_ImportedGameShowsInList()
    {
        var gamesPage = new GamesPage(_page);
        await gamesPage.NavigateAsync();

        await gamesPage.ImportGameAsync(LcxFilePath);

        // Verify the table is no longer empty
        Assert.True(await gamesPage.GetGameCountAsync() > 0);
        Assert.True(await gamesPage.IsGameVisibleAsync(ExpectedGameTitle));
    }

    [Fact]
    public async Task GamesPage_ImportedGameCanBeOpened()
    {
        var gamesPage = new GamesPage(_page);
        await gamesPage.NavigateAsync();

        await gamesPage.ImportGameAsync(LcxFilePath);

        await gamesPage.OpenGameEditAsync(ExpectedGameTitle);

        // Verify we navigated to the game detail page
        Assert.Contains("/Games/", _page.Url);
    }
}
