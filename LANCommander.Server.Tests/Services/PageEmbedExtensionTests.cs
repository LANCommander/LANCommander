using LANCommander.Server.Plugins;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

/// <summary>
/// Embed kinds plugins add to pages through <see cref="IPageEmbedExtension"/>, left as the plugin's
/// component between pieces of the page's HTML.
/// </summary>
[Collection("Application")]
public class PageEmbedExtensionTests(ApplicationFixture fixture) : BaseTest(fixture)
{
    static readonly PageRenderOptions Visitor = new();
    static readonly PageRenderOptions Administrator = new() { IsAdministrator = true };

    [Fact]
    public async Task APluginsEmbedBecomesItsComponentBetweenTheHtml()
    {
        var extension = new FakeExtension("Leaderboard");
        var pageId = Guid.NewGuid();

        var page = await Render(extension).RenderAsync("Intro\n\n{{Leaderboard:weekly}}\n\nOutro", new PageRenderOptions { PageId = pageId });

        page.Segments.Count.ShouldBe(3);
        page.Segments[0].ShouldBeOfType<RenderedPageHtml>().Html.ShouldContain("Intro");
        page.Segments[2].ShouldBeOfType<RenderedPageHtml>().Html.ShouldContain("Outro");

        var component = page.Segments[1].ShouldBeOfType<RenderedPageComponent>();

        component.Extension.ShouldBeSameAs(extension);
        component.Embed.Kind.ShouldBe("leaderboard");
        component.Embed.Argument.ShouldBe("weekly");
        component.Embed.PageId.ShouldBe(pageId);
        component.Embed.IsAdministrator.ShouldBeFalse();

        page.Html.ShouldNotContain("<!--");
    }

    [Fact]
    public async Task EmbedsSideBySideKeepTheirOrder()
    {
        var page = await Render(new FakeExtension("scores")).RenderAsync("{{scores:first}}\n{{scores:second}}\n\n{{toc}}\n\n## Heading", Visitor);

        page.Segments.OfType<RenderedPageComponent>().Select(c => c.Embed.Argument).ShouldBe(["first", "second"]);
        page.Segments[0].ShouldBeOfType<RenderedPageComponent>();
        page.Html.ShouldContain("page-embed-toc");
    }

    [Fact]
    public async Task AnEmbedInsideAListIsOnlyFlaggedToAdministrators()
    {
        var extension = new FakeExtension("scores");
        var markdown = "- One\n- {{scores}}\n\n> {{scores}}";

        var visitor = await Render(extension).RenderAsync(markdown, Visitor);
        var administrator = await Render(extension).RenderAsync(markdown, Administrator);

        visitor.Segments.ShouldHaveSingleItem().ShouldBeOfType<RenderedPageHtml>().Html.ShouldNotContain("page-embed");
        administrator.Segments.ShouldHaveSingleItem().ShouldBeOfType<RenderedPageHtml>().Html.ShouldContain("outside lists and quotes");
    }

    [Fact]
    public async Task APageWithoutComponentsIsOneBlockOfHtml()
    {
        var page = await Render().RenderAsync("{{scores}}\n\nText", Administrator);

        // An unknown kind is flagged like a missing built-in embed
        page.Segments.ShouldHaveSingleItem().ShouldBeOfType<RenderedPageHtml>().Html.ShouldContain("page-embed-missing");
    }

    [Fact]
    public async Task APluginCannotReplaceABuiltInKind()
    {
        var extension = new FakeExtension("toc");

        Extensions(extension).All.ShouldBeEmpty();

        var page = await Render(extension).RenderAsync("{{toc}}\n\n## Heading", Visitor);

        page.Segments.ShouldHaveSingleItem();
        page.Html.ShouldContain("page-embed-toc");
    }

    [Fact]
    public void KindsThatCantBeWrittenOrAreTakenAreLeftOut()
    {
        var first = new FakeExtension("scores", "Scores");

        var extensions = Extensions(new FakeExtension("high-scores"), new FakeExtension(""), first, new FakeExtension("SCORES", "Other"));

        extensions.All.ShouldHaveSingleItem().ShouldBeSameAs(first);
        extensions.Find("Scores").ShouldBeSameAs(first);
    }

    [Fact]
    public void ExtensionsWhoseComponentCantTakeTheEmbedAreLeftOut()
    {
        var extensions = Extensions(
            new FakeExtension("plain") { Component = typeof(string) },
            new FakeExtension("noparameter") { Component = typeof(NoParameterComponent) },
            new FakeExtension("usable"));

        extensions.All.ShouldHaveSingleItem().Kind.ShouldBe("usable");
    }

    static PageEmbedExtensions Extensions(params IPageEmbedExtension[] extensions) =>
        new(extensions, NullLogger<PageEmbedExtensions>.Instance);

    PageRenderService Render(params IPageEmbedExtension[] extensions) =>
        new(
            NullLogger<PageRenderService>.Instance,
            GetService<PageService>(),
            GetService<GameService>(),
            GetService<ServerService>(),
            GetService<RedistributableService>(),
            GetService<ServerManager>(),
            Extensions(extensions));

    sealed class FakeExtension(string kind, string name = "Fake") : IPageEmbedExtension
    {
        public string Kind => kind;

        public string Name => name;

        public Type Component { get; init; } = typeof(EmbedComponent);
    }

    sealed class EmbedComponent : ComponentBase
    {
        [Parameter] public PageEmbed? Embed { get; set; }
    }

    sealed class NoParameterComponent : ComponentBase
    {
        public PageEmbed? Embed { get; set; }
    }
}
