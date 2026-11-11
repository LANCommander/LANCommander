using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Pages;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

[Collection("Application")]
public class PageRenderServiceTests(ApplicationFixture fixture) : BaseTest(fixture)
{
    static readonly PageRenderOptions Visitor = new();
    static readonly PageRenderOptions Administrator = new() { IsAdministrator = true };

    [Fact]
    public async Task RawHtmlIsNotRendered()
    {
        var html = await GetService<PageRenderService>().RenderAsync("Hello <script>alert(1)</script>");

        html.ShouldNotContain("<script>");
    }

    [Fact]
    public async Task GameEmbedShowsTheGameEncoded()
    {
        var game = await GetService<GameService>().AddAsync(new Game
        {
            Title = "<b>Bold</b> " + Guid.NewGuid().ToString("N"),
            Description = "A game & more",
        });

        var html = await GetService<PageRenderService>().RenderAsync($"Intro\n\n{{{{game:{game.Id}}}}}\n\nOutro", Visitor);

        html.ShouldContain("page-embed-game");
        html.ShouldContain("&lt;b&gt;Bold&lt;/b&gt;");
        html.ShouldContain("A game &amp; more");
        html.ShouldNotContain("<b>Bold</b>");

        // Edit links are only for administrators
        html.ShouldNotContain($"/Games/{game.Id}");
        (await GetService<PageRenderService>().RenderAsync($"{{{{game:{game.Id}}}}}", Administrator)).ShouldContain($"/Games/{game.Id}");
    }

    [Fact]
    public async Task MissingEmbedIsOnlyShownToAdministrators()
    {
        var markdown = $"{{{{game:{Guid.NewGuid()}}}}}";

        var visitor = await GetService<PageRenderService>().RenderAsync(markdown, Visitor);
        var administrator = await GetService<PageRenderService>().RenderAsync(markdown, Administrator);

        visitor.ShouldNotContain("page-embed");
        visitor.ShouldNotContain("{{");
        administrator.ShouldContain("page-embed-missing");
    }

    [Fact]
    public async Task EmbedSyntaxInCodeIsLeftAlone()
    {
        var html = await GetService<PageRenderService>().RenderAsync("```\n{{toc}}\n```\n\n    {{toc}}\n\nInline {{toc}} text", Administrator);

        html.ShouldNotContain("page-embed");
        html.ShouldContain("{{toc}}");
    }

    [Fact]
    public async Task TableOfContentsLinksToHeadings()
    {
        var html = await GetService<PageRenderService>().RenderAsync("{{toc}}\n\n## Getting Started\n\n### Seating\n\n## Rules", Visitor);

        html.ShouldContain("page-embed-toc");
        html.ShouldContain("href=\"#getting-started\"");
        html.ShouldContain(">Seating<");
        html.ShouldContain("page-embed-toc-level-2");
    }

    [Fact]
    public async Task ChildrenListsThePagesBeneath()
    {
        var pageService = GetService<PageService>();
        var suffix = Guid.NewGuid().ToString("N");

        var parent = await pageService.AddAsync(new Page { Title = "Parent", Slug = "Parent" + suffix, Contents = "{{children}}" });
        await pageService.AddAsync(new Page { Title = "First Child", Slug = "", Contents = "", ParentId = parent.Id });
        await pageService.AddAsync(new Page { Title = "Second Child", Slug = "", Contents = "", ParentId = parent.Id });

        var html = await GetService<PageRenderService>().RenderAsync(parent.Contents, new PageRenderOptions { PageId = parent.Id });

        html.ShouldContain($"href=\"/Pages/Parent{suffix}/FirstChild\"");
        html.IndexOf("First Child").ShouldBeLessThan(html.IndexOf("Second Child"));
    }

    [Fact]
    public async Task PageEmbedLinksToThePage()
    {
        var page = await GetService<PageService>().AddAsync(new Page { Title = "Linked", Slug = "Linked" + Guid.NewGuid().ToString("N"), Contents = "" });

        var html = await GetService<PageRenderService>().RenderAsync($"{{{{page:{page.Id}}}}}", Visitor);

        html.ShouldContain($"href=\"/{page.Route}\"");
        html.ShouldContain(">Linked<");
    }

    [Fact]
    public async Task ServerEmbedShowsItsAddress()
    {
        var server = await GetService<ServerService>().AddAsync(new LANCommander.Server.Data.Models.Server
        {
            Name = "Arena " + Guid.NewGuid().ToString("N"),
            Host = "192.168.1.20",
            Port = 27015,
        });

        var html = await GetService<PageRenderService>().RenderAsync($"{{{{server:{server.Id}}}}}", Visitor);

        html.ShouldContain("page-embed-server");
        html.ShouldContain("192.168.1.20:27015");
        html.ShouldContain("page-embed-status");
    }

    [Fact]
    public void PlainTextLeavesOutSyntaxAndEmbeds()
    {
        var text = PageMarkdown.ToPlainText("# Title\n\n{{toc}}\n\nSome **bold** text");

        text.ShouldBe("Title Some bold text");
    }
}
