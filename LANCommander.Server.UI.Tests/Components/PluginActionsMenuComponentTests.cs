using Bunit;
using LANCommander.Server.UI.Pages.Settings.Plugins.Components;
using Microsoft.AspNetCore.Components;

namespace LANCommander.Server.UI.Tests.Components;

[Collection("BUnit")]
public class PluginActionsMenuComponentTests(BUnitServerFixture fixture) : BUnitTestContext(fixture)
{
    private IRenderedComponent<PluginActionsMenu> RenderMenu(
        string? repositoryUrl = null,
        string? projectUrl = null,
        string? settingsHref = null,
        Action? onUninstall = null)
    {
        var cut = Render<PluginActionsMenu>(p =>
        {
            p.Add(x => x.Name, "Acme Widgets")
             .Add(x => x.RepositoryUrl, repositoryUrl)
             .Add(x => x.ProjectUrl, projectUrl)
             .Add(x => x.SettingsHref, settingsHref);

            if (onUninstall != null)
                p.Add(x => x.OnUninstall, EventCallback.Factory.Create(this, onUninstall));
        });

        // The popup's content renders once it has been opened
        if (cut.FindAll("button").Count > 0)
            cut.Find("button").Click();

        return cut;
    }

    private static Dictionary<string, string?> Links(IRenderedComponent<PluginActionsMenu> cut) =>
        cut.FindAll("a.lc-menu-item-content").ToDictionary(a => a.TextContent.Trim(), a => a.GetAttribute("href"));

    private static List<string> Items(IRenderedComponent<PluginActionsMenu> cut) =>
        cut.FindAll(".lc-menu-item-text").Select(i => i.TextContent.Trim()).ToList();

    [Fact]
    public void OffersTheSourceCodeAndTheProjectWebsite()
    {
        var cut = RenderMenu("https://github.com/acme/widgets", "https://acme.example/widgets");

        var links = Links(cut);
        Assert.Equal("https://github.com/acme/widgets", links["Source code"]);
        Assert.Equal("https://acme.example/widgets", links["Project website"]);
        Assert.All(cut.FindAll("a.lc-menu-item-content[target]"), a => Assert.Equal("_blank", a.GetAttribute("target")));
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

    [Fact]
    public void OffersThePluginsSettingsFirstInTheSameTab()
    {
        var cut = RenderMenu("https://github.com/acme/widgets", settingsHref: "/Plugins/Widgets/Settings", onUninstall: () => { });

        Assert.Equal(["Settings", "Source code", "Uninstall"], Items(cut));

        var settings = cut.FindAll("a.lc-menu-item-content").Single(a => a.TextContent.Trim() == "Settings");
        Assert.Equal("/Plugins/Widgets/Settings", settings.GetAttribute("href"));
        Assert.Null(settings.GetAttribute("target"));
    }

    [Fact]
    public void UninstallingHandsOffToThePage()
    {
        var uninstalled = false;
        var cut = RenderMenu(onUninstall: () => uninstalled = true);

        var item = cut.FindAll(".lc-menu-item").Single(i => i.TextContent.Trim() == "Uninstall");
        Assert.Contains("lc-menu-item-danger", item.ClassList);

        item.QuerySelector("button")!.Click();

        Assert.True(uninstalled);
    }

    [Fact]
    public void OffersNoUninstallWithoutAHandler()
    {
        var cut = RenderMenu("https://github.com/acme/widgets");

        Assert.DoesNotContain("Uninstall", Items(cut));
    }
}
