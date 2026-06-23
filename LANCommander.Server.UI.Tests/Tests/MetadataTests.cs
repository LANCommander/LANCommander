using LANCommander.Server.Services;
using LANCommander.Server.UI.Tests.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Xunit.Abstractions;

namespace LANCommander.Server.UI.Tests.Tests;

/// <summary>
/// Tests for metadata management pages (Tags, Genres, Platforms).
/// Verifies CRUD operations through the admin UI using a shared page object.
/// </summary>
[Collection("Server")]
public class MetadataTests : IAsyncLifetime
{
    private readonly ConfiguredServerFixture _fixture;
    private readonly ITestOutputHelper _output;
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public MetadataTests(ConfiguredServerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    public async Task InitializeAsync()
    {
        (_context, _page) = await _fixture.CreateLoggedInPageAsync();
    }

    public async Task DisposeAsync()
    {
        await ScreenshotHelper.CaptureAsync(_page, _output);
        if (_page != null) await _page.CloseAsync();
        if (_context != null) await _context.DisposeAsync();
    }

    // --- Tags ---

    [Fact]
    public async Task TagsPage_ShowsEmptyState()
    {
        // Clear any tags left by other tests so the empty state is visible
        using (var scope = _fixture.Factory.RealServices.CreateScope())
        {
            var tagService = scope.ServiceProvider.GetRequiredService<TagService>();
            var existing = await tagService.GetAsync();
            foreach (var tag in existing)
                await tagService.DeleteAsync(tag);
        }

        var tagsPage = new MetadataPage(_page, "Tags");
        await tagsPage.NavigateAsync();

        Assert.Equal(0, await tagsPage.GetItemCountAsync());
        await Assertions.Expect(_page.GetByText("No data")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task TagsPage_CanAddTag()
    {
        var tagsPage = new MetadataPage(_page, "Tags");
        await tagsPage.NavigateAsync();

        await tagsPage.AddItemAsync("Action");

        await Assertions.Expect(tagsPage.Item("Action")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task TagsPage_CanEditTag()
    {
        var tagsPage = new MetadataPage(_page, "Tags");
        await tagsPage.NavigateAsync();

        await tagsPage.AddItemAsync("Puzzle");
        await Assertions.Expect(tagsPage.Item("Puzzle")).ToBeVisibleAsync();

        await tagsPage.EditItemAsync("Puzzle", "Puzzle Games");
        await Assertions.Expect(tagsPage.Item("Puzzle Games")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task TagsPage_CanDeleteTag()
    {
        var tagsPage = new MetadataPage(_page, "Tags");
        await tagsPage.NavigateAsync();

        await tagsPage.AddItemAsync("Temporary");
        await Assertions.Expect(tagsPage.Item("Temporary")).ToBeVisibleAsync();

        await tagsPage.DeleteItemAsync("Temporary");
        await Assertions.Expect(tagsPage.Item("Temporary")).ToBeHiddenAsync();
    }

    // --- Genres ---

    [Fact]
    public async Task GenresPage_CanAddGenre()
    {
        var genresPage = new MetadataPage(_page, "Genres");
        await genresPage.NavigateAsync();

        await genresPage.AddItemAsync("RPG");

        await Assertions.Expect(genresPage.Item("RPG")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task GenresPage_CanDeleteGenre()
    {
        var genresPage = new MetadataPage(_page, "Genres");
        await genresPage.NavigateAsync();

        await genresPage.AddItemAsync("Strategy");
        await Assertions.Expect(genresPage.Item("Strategy")).ToBeVisibleAsync();

        await genresPage.DeleteItemAsync("Strategy");
        await Assertions.Expect(genresPage.Item("Strategy")).ToBeHiddenAsync();
    }

    // --- Platforms ---

    [Fact]
    public async Task PlatformsPage_CanAddPlatform()
    {
        var platformsPage = new MetadataPage(_page, "Platforms");
        await platformsPage.NavigateAsync();

        await platformsPage.AddItemAsync("Windows");

        await Assertions.Expect(platformsPage.Item("Windows")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task PlatformsPage_CanDeletePlatform()
    {
        var platformsPage = new MetadataPage(_page, "Platforms");
        await platformsPage.NavigateAsync();

        await platformsPage.AddItemAsync("Linux");
        await Assertions.Expect(platformsPage.Item("Linux")).ToBeVisibleAsync();

        await platformsPage.DeleteItemAsync("Linux");
        await Assertions.Expect(platformsPage.Item("Linux")).ToBeHiddenAsync();
    }
}
