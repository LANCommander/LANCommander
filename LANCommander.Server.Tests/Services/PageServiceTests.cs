using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

[Collection("Application")]
public class PageServiceTests(ApplicationFixture fixture) : BaseTest(fixture)
{
    [Fact]
    public void GetCacheKeyNormalizesRoute()
    {
        // Full route and bare sub-route must resolve to the same normalized key so the
        // public render path and the service invalidate/read the same cache entry.
        PageService.GetCacheKey("Pages/Parent/Child").ShouldBe("Page|parent/child");
        PageService.GetCacheKey("/Pages/Parent/Child/").ShouldBe("Page|parent/child");
        PageService.GetCacheKey("parent/child").ShouldBe("Page|parent/child");
    }

    [Fact]
    public async Task UniqueSlugIsNotSuffixed()
    {
        var pageService = GetService<PageService>();
        // Already a valid PascalCase route slug so it survives ToRouteSlug unchanged.
        var slug = "Unique" + Guid.NewGuid().ToString("N");

        var page = await pageService.AddAsync(new Page { Title = "Unique", Slug = slug, Contents = "" });

        page.Slug.ShouldBe(slug);
        page.Route.ShouldBe($"Pages/{slug}");
    }

    [Fact]
    public async Task DuplicateSiblingSlugGetsSuffix()
    {
        var pageService = GetService<PageService>();
        var slug = "Dup" + Guid.NewGuid().ToString("N");

        var first = await pageService.AddAsync(new Page { Title = "First", Slug = slug, Contents = "" });
        var second = await pageService.AddAsync(new Page { Title = "Second", Slug = slug, Contents = "" });

        first.Slug.ShouldBe(slug);
        second.Slug.ShouldBe($"{slug}-1");
    }

    [Fact]
    public async Task ReSavingPageKeepsItsOwnSlug()
    {
        var pageService = GetService<PageService>();
        var slug = "Resave" + Guid.NewGuid().ToString("N");

        var page = await pageService.AddAsync(new Page { Title = "Original", Slug = slug, Contents = "" });

        page = await pageService.GetAsync(page.Id);
        page.Title = "Renamed";

        var updated = await pageService.UpdateAsync(page);

        // The page must not collide with itself and gain a spurious "-1" suffix.
        updated.Slug.ShouldBe(slug);
    }

    [Fact]
    public async Task SlugIsGeneratedInPascalCase()
    {
        var pageService = GetService<PageService>();
        var unique = Guid.NewGuid().ToString("N");

        // An empty slug falls back to a PascalCase conversion of the title. The unique
        // suffix is kept inside the last word so its casing is preserved.
        var page = await pageService.AddAsync(new Page { Title = $"Getting Started{unique}", Slug = "", Contents = "" });

        page.Slug.ShouldBe($"GettingStarted{unique}");
        page.Route.ShouldBe($"Pages/GettingStarted{unique}");
    }

    [Fact]
    public async Task RenamingParentUpdatesDescendantRoutes()
    {
        var pageService = GetService<PageService>();
        var suffix = Guid.NewGuid().ToString("N");

        var parent = await pageService.AddAsync(new Page { Title = "Parent", Slug = "Parent" + suffix, Contents = "" });

        parent = await pageService.GetAsync(parent.Id);
        var child = await pageService.AddAsync(new Page { Title = "Child", Slug = "Child" + suffix, Contents = "", Parent = parent });

        child = await pageService.GetAsync(child.Id);
        var grandchild = await pageService.AddAsync(new Page { Title = "Grandchild", Slug = "Grandchild" + suffix, Contents = "", Parent = child });

        // Rename the top-level parent's slug; every descendant route must be reprefixed.
        parent = await pageService.GetAsync(parent.Id);
        parent.Slug = "Renamed" + suffix;
        await pageService.UpdateAsync(parent);

        var updatedChild = await pageService.GetAsync(child.Id);
        var updatedGrandchild = await pageService.GetAsync(grandchild.Id);

        updatedChild.Route.ShouldBe($"Pages/Renamed{suffix}/Child{suffix}");
        updatedGrandchild.Route.ShouldBe($"Pages/Renamed{suffix}/Child{suffix}/Grandchild{suffix}");
    }

    [Fact]
    public async Task CircularReferenceIsRejected()
    {
        var pageService = GetService<PageService>();
        var suffix = Guid.NewGuid().ToString("N");

        var a = await pageService.AddAsync(new Page { Title = "A", Slug = "A" + suffix, Contents = "" });

        a = await pageService.GetAsync(a.Id);
        var b = await pageService.AddAsync(new Page { Title = "B", Slug = "B" + suffix, Contents = "", Parent = a });

        b = await pageService.GetAsync(b.Id);
        var c = await pageService.AddAsync(new Page { Title = "C", Slug = "C" + suffix, Contents = "", Parent = b });

        c = await pageService.GetAsync(c.Id);

        // Attempt to reparent A beneath its own grandchild C, forming A -> C -> B -> A.
        var reparentedA = await pageService.GetAsync(a.Id);
        reparentedA.Parent = c;

        await Should.ThrowAsync<Exception>(async () => await pageService.UpdateAsync(reparentedA));
    }

    [Fact]
    public async Task AddingWithOnlyParentIdNestsTheRoute()
    {
        var pageService = GetService<PageService>();
        var suffix = Guid.NewGuid().ToString("N");

        var parent = await pageService.AddAsync(new Page { Title = "Parent", Slug = "Parent" + suffix, Contents = "" });
        var child = await pageService.AddAsync(new Page { Title = "Child", Slug = "Child" + suffix, Contents = "", ParentId = parent.Id });

        child.Route.ShouldBe($"Pages/Parent{suffix}/Child{suffix}");
        (await pageService.GetAsync(child.Id)).ParentId.ShouldBe(parent.Id);
    }

    [Fact]
    public async Task RootSlugCannotShadowTheEditor()
    {
        var pageService = GetService<PageService>();

        var page = await pageService.AddAsync(new Page { Title = "Edit", Slug = "", Contents = "" });

        // /Pages/Edit is the editor, so a root page there could never be reached
        page.Slug.ShouldNotBe("Edit");
        page.Route.ShouldNotBe("Pages/Edit");

        await pageService.DeleteAsync(page);
    }

    [Fact]
    public async Task SlugsDifferingOnlyByCaseCollide()
    {
        var pageService = GetService<PageService>();
        var suffix = Guid.NewGuid().ToString("N");

        var parent = await pageService.AddAsync(new Page { Title = "Parent", Slug = "Parent" + suffix, Contents = "" });
        await pageService.AddAsync(new Page { Title = "Rules", Slug = "HouseRules", Contents = "", ParentId = parent.Id });
        var second = await pageService.AddAsync(new Page { Title = "Rules", Slug = "Houserules", Contents = "", ParentId = parent.Id });

        second.Slug.ShouldBe("Houserules-1");
    }

    [Fact]
    public async Task NewPagesAreAppendedAfterTheirSiblings()
    {
        var pageService = GetService<PageService>();
        var suffix = Guid.NewGuid().ToString("N");

        var parent = await pageService.AddAsync(new Page { Title = "Parent", Slug = "Parent" + suffix, Contents = "" });
        var first = await pageService.AddAsync(new Page { Title = "First", Slug = "First", Contents = "", ParentId = parent.Id });
        var second = await pageService.AddAsync(new Page { Title = "Second", Slug = "Second", Contents = "", ParentId = parent.Id });

        (await pageService.GetAsync(second.Id)).SortOrder.ShouldBeGreaterThan((await pageService.GetAsync(first.Id)).SortOrder);
    }

    [Fact]
    public async Task MovingAPageToTheRootClearsItsParent()
    {
        var pageService = GetService<PageService>();
        var suffix = Guid.NewGuid().ToString("N");

        var parent = await pageService.AddAsync(new Page { Title = "Parent", Slug = "Parent" + suffix, Contents = "" });
        var child = await pageService.AddAsync(new Page { Title = "Child", Slug = "Child" + suffix, Contents = "", ParentId = parent.Id });

        await pageService.UpdateOrderAsync(Guid.Empty, [child.Id]);

        var moved = await pageService.GetAsync(child.Id);

        moved.ParentId.ShouldBeNull();
        moved.Route.ShouldBe($"Pages/Child{suffix}");
    }

    [Fact]
    public async Task CountsEveryDescendant()
    {
        var pageService = GetService<PageService>();
        var suffix = Guid.NewGuid().ToString("N");

        var parent = await pageService.AddAsync(new Page { Title = "Parent", Slug = "Parent" + suffix, Contents = "" });
        var child = await pageService.AddAsync(new Page { Title = "Child", Slug = "Child", Contents = "", ParentId = parent.Id });
        await pageService.AddAsync(new Page { Title = "Grandchild", Slug = "Grandchild", Contents = "", ParentId = child.Id });
        await pageService.AddAsync(new Page { Title = "Sibling", Slug = "Sibling", Contents = "", ParentId = parent.Id });

        (await pageService.CountDescendantsAsync(parent.Id)).ShouldBe(3);
        (await pageService.CountDescendantsAsync(child.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task RemovingAnImageFromAPageDeletesIt()
    {
        var pageService = GetService<PageService>();
        var mediaService = GetService<MediaService>();

        var image = await AddPageImageAsync();
        var page = await pageService.AddAsync(new Page { Title = "Image", Slug = "Image" + Guid.NewGuid().ToString("N"), Contents = ImageMarkdown(image) });

        page = await pageService.GetAsync(page.Id);
        page.Contents = "No more image";
        await pageService.UpdateAsync(page);

        (await mediaService.GetAsync(image.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task AnImageAnotherPageStillUsesIsKept()
    {
        var pageService = GetService<PageService>();
        var mediaService = GetService<MediaService>();

        var image = await AddPageImageAsync();
        var page = await pageService.AddAsync(new Page { Title = "Image", Slug = "Image" + Guid.NewGuid().ToString("N"), Contents = ImageMarkdown(image) });
        await pageService.AddAsync(new Page { Title = "Other", Slug = "Other" + Guid.NewGuid().ToString("N"), Contents = ImageMarkdown(image) });

        page = await pageService.GetAsync(page.Id);
        page.Contents = "";
        await pageService.UpdateAsync(page);

        (await mediaService.GetAsync(image.Id)).ShouldNotBeNull();
    }

    [Fact]
    public async Task DeletingAPageDeletesItsAndItsChildrensImages()
    {
        var pageService = GetService<PageService>();
        var mediaService = GetService<MediaService>();
        var suffix = Guid.NewGuid().ToString("N");

        var parentImage = await AddPageImageAsync();
        var childImage = await AddPageImageAsync();

        var parent = await pageService.AddAsync(new Page { Title = "Parent", Slug = "Parent" + suffix, Contents = ImageMarkdown(parentImage) });
        var child = await pageService.AddAsync(new Page { Title = "Child", Slug = "Child", Contents = ImageMarkdown(childImage), ParentId = parent.Id });

        await pageService.DeleteAsync(await pageService.GetAsync(parent.Id));

        (await pageService.GetAsync(child.Id)).ShouldBeNull();
        (await mediaService.GetAsync(parentImage.Id)).ShouldBeNull();
        (await mediaService.GetAsync(childImage.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task LinkingGamesLeavesTheGamesIntact()
    {
        var pageService = GetService<PageService>();
        var gameService = GetService<GameService>();

        var game = await gameService.AddAsync(new Game { Title = "Linked " + Guid.NewGuid().ToString("N") });
        var page = await pageService.AddAsync(new Page { Title = "Links", Slug = "Links" + Guid.NewGuid().ToString("N"), Contents = "" });

        // As the editor saves: the page with its links loaded, given freshly loaded games
        page = await pageService.Include(p => p.Parent, p => p.Games, p => p.Redistributables, p => p.Servers).FirstOrDefaultAsync(p => p.Id == page.Id);
        page.Games = await gameService.GetAsync(g => g.Id == game.Id);
        await pageService.UpdateAsync(page);

        var linked = await pageService.Include(p => p.Games).FirstOrDefaultAsync(p => p.Id == page.Id);

        linked.Games.Select(g => g.Id).ShouldBe([game.Id]);
        (await gameService.GetAsync(game.Id)).Title.ShouldBe(game.Title);

        // Unlinking everything clears the links rather than being skipped as "not loaded"
        linked.Games = [];
        await pageService.UpdateAsync(linked);

        (await pageService.Include(p => p.Games).FirstOrDefaultAsync(p => p.Id == page.Id)).Games.ShouldBeEmpty();
    }

    [Fact]
    public async Task SearchFindsPagesContainingEveryWord()
    {
        var pageService = GetService<PageService>();
        var word = "Zq" + Guid.NewGuid().ToString("N");

        var parent = await pageService.AddAsync(new Page { Title = "Event", Slug = "Event" + word, Contents = "" });
        var inText = await pageService.AddAsync(new Page { Title = "Schedule", Slug = "", Contents = $"Doors open at noon. The **{word}** tournament starts later.", ParentId = parent.Id });
        var inTitle = await pageService.AddAsync(new Page { Title = $"{word} Rules", Slug = "", Contents = "Be nice." });
        await pageService.AddAsync(new Page { Title = "Unrelated", Slug = "Unrelated" + word.ToLower() + "x", Contents = "Nothing here." });

        var results = await pageService.SearchAsync(word.ToLower());

        // Title matches rank above text matches, and the slug isn't searched
        results.Select(r => r.Id).ShouldBe([inTitle.Id, inText.Id]);

        var textResult = results[1];

        textResult.Snippet.ShouldContain($"The {word} tournament");
        textResult.Path.ShouldBe(["Event"]);

        (await pageService.SearchAsync($"{word} noon")).Select(r => r.Id).ShouldBe([inText.Id]);
        (await pageService.SearchAsync($"{word} absent")).ShouldBeEmpty();
        (await pageService.SearchAsync("   ")).ShouldBeEmpty();
    }

    [Fact]
    public void SnippetsAreCutAtWords()
    {
        var text = String.Join(' ', Enumerable.Repeat("filler", 60)) + " needle " + String.Join(' ', Enumerable.Repeat("after", 60));

        var snippet = PageService.GetSnippet(text, ["needle"]);

        snippet.ShouldStartWith("…filler");
        snippet.ShouldEndWith("after…");
        snippet.ShouldContain("needle");
    }

    [Fact]
    public async Task DuplicatingCopiesContentsAndLinks()
    {
        var pageService = GetService<PageService>();
        var game = await GetService<GameService>().AddAsync(new Game { Title = "Duplicated " + Guid.NewGuid().ToString("N") });
        var suffix = Guid.NewGuid().ToString("N");

        var parent = await pageService.AddAsync(new Page { Title = "Parent", Slug = "Parent" + suffix, Contents = "" });
        var original = await pageService.AddAsync(new Page { Title = "Original", Slug = "", Contents = "Copied text", ParentId = parent.Id, Games = [game] });

        var copy = await pageService.DuplicateAsync(original.Id);

        copy = await pageService.Include(p => p.Games).FirstOrDefaultAsync(p => p.Id == copy.Id);

        copy.Title.ShouldBe("Original (Copy)");
        copy.Contents.ShouldBe("Copied text");
        copy.ParentId.ShouldBe(parent.Id);
        copy.Route.ShouldBe($"Pages/Parent{suffix}/OriginalCopy");
        copy.Games.Select(g => g.Id).ShouldBe([game.Id]);
    }

    [Fact]
    public void FindsMediaReferencesInContents()
    {
        var id = Guid.NewGuid();

        var contents = $"![a](/api/Media/{id}/Download?fileId={Guid.NewGuid()}) and ![b](/api/Media/{id.ToString().ToUpper()}/Thumbnail)";

        PageService.GetMediaReferences(contents).ShouldBe([id]);
        PageService.GetMediaReferences(null).ShouldBeEmpty();
    }

    static string ImageMarkdown(Media media) => $"![image](/api/Media/{media.Id}/Download?fileId={media.FileId})";

    async Task<Media> AddPageImageAsync()
    {
        await EnsureStorageLocationsExistAsync();

        var mediaService = GetService<MediaService>();

        return await mediaService.AddAsync(new Media
        {
            Type = LANCommander.SDK.Enums.MediaType.PageImage,
            Crc32 = string.Empty,
            FileId = Guid.NewGuid(),
            StorageLocation = await mediaService.GetDefaultStorageLocationAsync(),
        });
    }
}
