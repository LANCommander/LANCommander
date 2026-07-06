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
}
