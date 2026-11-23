using Bunit;
using LANCommander.Server.Plugins;
using LANCommander.Server.Services.Pages;
using LANCommander.Server.UI.Pages.Pages.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// A rendered page as shown: its HTML with plugins' embeds as their components in between, and a
/// component that fails hidden from visitors without taking the page with it.
/// </summary>
[Collection("BUnit")]
public class RenderedPageViewTests(BUnitServerFixture fixture) : BUnitTestContext(fixture)
{
    private sealed class FakeExtension(Type component) : IPageEmbedExtension
    {
        public string Kind => "scores";
        public string Name => "Scores";
        public Type Component => component;
    }

    public sealed class ScoresEmbed : ComponentBase
    {
        [Parameter] public PageEmbed? Embed { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "p");
            builder.AddAttribute(1, "class", "scores");
            builder.AddContent(2, $"Scores for {Embed?.Argument}");
            builder.CloseElement();
        }
    }

    public sealed class BrokenEmbed : ComponentBase
    {
        [Parameter] public PageEmbed? Embed { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder) =>
            throw new InvalidOperationException("Scores are unavailable");
    }

    static RenderedPage Page(Type component, bool isAdministrator) => new([
        new RenderedPageHtml("<p class=\"before\">Before</p>"),
        new RenderedPageComponent(new FakeExtension(component), new PageEmbed { Kind = "scores", Argument = "weekly", IsAdministrator = isAdministrator }),
        new RenderedPageHtml("<p class=\"after\">After</p>"),
    ]);

    [Fact]
    public void APluginsComponentIsShownBetweenThePagesHtml()
    {
        var cut = Render<RenderedPageView>(p => p.Add(x => x.Page, Page(typeof(ScoresEmbed), false)));

        Assert.Equal("Scores for weekly", cut.Find(".page-embed-plugin-scores .scores").TextContent);

        var markup = cut.Markup;

        Assert.True(markup.IndexOf("Before") < markup.IndexOf("Scores for weekly"));
        Assert.True(markup.IndexOf("Scores for weekly") < markup.IndexOf("After"));
    }

    [Fact]
    public void AFailingComponentShowsNothingToVisitors()
    {
        var cut = Render<RenderedPageView>(p => p.Add(x => x.Page, Page(typeof(BrokenEmbed), false)));

        Assert.Equal("After", cut.Find(".after").TextContent);
        Assert.Empty(cut.Find(".page-embed-plugin-scores").TextContent.Trim());
        Assert.DoesNotContain("Scores are unavailable", cut.Markup);
    }

    [Fact]
    public void AFailingComponentShowsItsErrorToAdministrators()
    {
        var cut = Render<RenderedPageView>(p => p.Add(x => x.Page, Page(typeof(BrokenEmbed), true)));

        Assert.Equal("After", cut.Find(".after").TextContent);
        Assert.Contains("Scores could not be displayed", cut.Markup);
        Assert.Contains("Scores are unavailable", cut.Markup);
    }
}
