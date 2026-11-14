using Bunit;
using LANCommander.Server.UI.Pages.Settings.Plugins.Components;

namespace LANCommander.Server.UI.Tests.Components;

[Collection("BUnit")]
public class PluginLinksMenuComponentTests(BUnitServerFixture fixture) : BUnitTestContext(fixture)
{
    private IRenderedComponent<PluginLinksMenu> RenderMenu(string? repositoryUrl, string? projectUrl)
    {
        var cut = Render<PluginLinksMenu>(p => p
            .Add(x => x.Name, "Acme Widgets")
            .Add(x => x.RepositoryUrl, repositoryUrl)
            .Add(x => x.ProjectUrl, projectUrl));

        // The popup's content renders once it has been opened
        if (cut.FindAll("button").Count > 0)
            cut.Find("button").Click();

        return cut;
    }

    private static Dictionary<string, string?> Links(IRenderedComponent<PluginLinksMenu> cut) =>
        cut.FindAll("a.lc-menu-item-content").ToDictionary(a => a.TextContent.Trim(), a => a.GetAttribute("href"));

    [Fact]
    public void OffersTheSourceCodeAndTheProjectWebsite()
    {
        var cut = RenderMenu("https://github.com/acme/widgets", "https://acme.example/widgets");

        var links = Links(cut);
        Assert.Equal("https://github.com/acme/widgets", links["Source code"]);
        Assert.Equal("https://acme.example/widgets", links["Project website"]);
        Assert.All(cut.FindAll("a.lc-menu-item-content"), a => Assert.Equal("_blank", a.GetAttribute("target")));
    }

    [Fact]
    public void LeavesOutAProjectWebsiteThatIsTheRepository()
    {
        var cut = RenderMenu("https://github.com/acme/widgets.git", "https://github.com/acme/widgets/");

        Assert.Equal(["Source code"], Links(cut).Keys);
    }

    [Fact]
    public void OffersOnlyTheProjectWebsiteWithoutARepository()
    {
        var cut = RenderMenu(null, "https://github.com/acme/widgets");

        Assert.Equal(["Project website"], Links(cut).Keys);
    }

    [Fact]
    public void IgnoresLinksThatAreNotWebPages()
    {
        var cut = RenderMenu("javascript:alert(1)", "file:///etc/passwd");

        Assert.Empty(cut.Markup.Trim());
    }
}
