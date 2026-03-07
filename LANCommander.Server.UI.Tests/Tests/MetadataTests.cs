using LANCommander.Server.Services;
using LANCommander.Server.UI.Tests.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace LANCommander.Server.UI.Tests.Tests;

/// <summary>
/// Tests for metadata management pages (Tags, Genres, Platforms).
/// Verifies CRUD operations through the admin UI using a shared page object.
/// </summary>
public class MetadataTests : IClassFixture<ConfiguredServerFixture>, IAsyncLifetime
{
    private readonly ConfiguredServerFixture _fixture;
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public MetadataTests(ConfiguredServerFixture fixture)
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
        Assert.True(await _page.GetByText("No data").IsVisibleAsync());
    }

    [Fact]
    public async Task TagsPage_CanAddTag()
    {
        var tagsPage = new MetadataPage(_page, "Tags");
        await tagsPage.NavigateAsync();

        await tagsPage.AddItemAsync("Action");

        Assert.True(await tagsPage.IsItemVisibleAsync("Action"));
    }

    [Fact]
    public async Task TagsPage_CanEditTag()
    {
        var tagsPage = new MetadataPage(_page, "Tags");
        await tagsPage.NavigateAsync();

        await tagsPage.AddItemAsync("Puzzle");
        Assert.True(await tagsPage.IsItemVisibleAsync("Puzzle"));

        await tagsPage.EditItemAsync("Puzzle", "Puzzle Games");
        Assert.True(await tagsPage.IsItemVisibleAsync("Puzzle Games"));
    }

    [Fact]
    public async Task TagsPage_CanDeleteTag()
    {
        var tagsPage = new MetadataPage(_page, "Tags");
        await tagsPage.NavigateAsync();

        await tagsPage.AddItemAsync("Temporary");
        Assert.True(await tagsPage.IsItemVisibleAsync("Temporary"));

        await tagsPage.DeleteItemAsync("Temporary");
        Assert.False(await tagsPage.IsItemVisibleAsync("Temporary"));
    }

    // --- Genres ---

    [Fact]
    public async Task GenresPage_CanAddGenre()
    {
        var genresPage = new MetadataPage(_page, "Genres");
        await genresPage.NavigateAsync();

        await genresPage.AddItemAsync("RPG");

        Assert.True(await genresPage.IsItemVisibleAsync("RPG"));
    }

    [Fact]
    public async Task GenresPage_CanDeleteGenre()
    {
        var genresPage = new MetadataPage(_page, "Genres");
        await genresPage.NavigateAsync();

        await genresPage.AddItemAsync("Strategy");
        Assert.True(await genresPage.IsItemVisibleAsync("Strategy"));

        await genresPage.DeleteItemAsync("Strategy");
        Assert.False(await genresPage.IsItemVisibleAsync("Strategy"));
    }

    // --- Platforms ---

    [Fact]
    public async Task PlatformsPage_CanAddPlatform()
    {
        var platformsPage = new MetadataPage(_page, "Platforms");
        await platformsPage.NavigateAsync();

        await platformsPage.AddItemAsync("Windows");

        Assert.True(await platformsPage.IsItemVisibleAsync("Windows"));
    }

    [Fact]
    public async Task PlatformsPage_CanDeletePlatform()
    {
        var platformsPage = new MetadataPage(_page, "Platforms");
        await platformsPage.NavigateAsync();

        await platformsPage.AddItemAsync("Linux");
        Assert.True(await platformsPage.IsItemVisibleAsync("Linux"));

        await platformsPage.DeleteItemAsync("Linux");
        Assert.False(await platformsPage.IsItemVisibleAsync("Linux"));
    }
}
