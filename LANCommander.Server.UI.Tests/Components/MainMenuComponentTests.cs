using AngleSharp.Dom;
using Bunit;
using LANCommander.Server.Plugins;
using LANCommander.Server.UI.Shared;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// bUnit tests for the sidebar navigation: plugin entries placed among the built-in entries written
/// in its markup, through <see cref="Controls.IMenuInsertions"/>.
/// </summary>
[Collection("BUnit")]
public class MainMenuComponentTests : BUnitTestContext
{
    public MainMenuComponentTests(BUnitServerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public void MainMenu_WithoutPlugins_ShowsOnlyBuiltInEntries()
    {
        var cut = Render<MainMenu>();

        Assert.Equal(
            ["Dashboards", "-", "Games", "Metadata", "Redistributables", "Tools", "Servers", "Scripting", "-", "Pages", "Issues", "Files", "Settings"],
            Labels(cut.Find(".lc-menu-list")));
    }

    [Fact]
    public void MainMenu_PlacesPluginEntriesAtTheirAnchors()
    {
        Add(new Entry("sync", "Role Sync", ServerNavigationSection.Settings, "/Settings/Users"));
        Add(new Entry("first", "First Setting", ServerNavigationSection.Settings, ServerNavigationAnchor.Start));
        Add(new Entry("catalog", "Catalog", ServerNavigationSection.Main, ServerNavigationSection.Metadata));
        Add(new Entry("moby", "MobyGames", ServerNavigationSection.Integrations, "/Settings/Integrations/IGDB"));
        Add(new Entry("webhooks", "Webhooks", ServerNavigationSection.Settings));
        Add(new Entry("gone", "Lost Anchor", ServerNavigationSection.Settings, "/Settings/Gone"));
        Add(new Entry("loose", "Loose"));

        var cut = Render<MainMenu>();
        var root = Labels(cut.Find(".lc-menu-list"));
        var settings = Labels(Group(cut, "Settings"));

        Assert.Equal(["Games", "Metadata", "Catalog", "Redistributables"], root[2..6]);
        Assert.Equal(["Settings", "-", "Loose"], root[^3..]);
        Assert.Equal(["First Setting", "General", "Database", "Users", "Role Sync", "Roles"], settings[..6]);
        Assert.Equal(["API", "Lost Anchor", "Webhooks"], settings[^3..]);
        Assert.Equal(["HQ", "IGDB", "MobyGames", "PCGamingWiki"], Labels(Group(cut, "Integrations"))[..4]);
    }

    void Add(IServerNavigationExtension entry) => Services.AddSingleton(entry);

    static IElement Group(IRenderedComponent<MainMenu> cut, string title) =>
        cut.FindAll(".lc-submenu")
            .Single(group => group.QuerySelector(".lc-submenu-title")!.TextContent.Trim() == title)
            .QuerySelector(".lc-submenu-list")!;

    /// <summary>The labels of a list's own entries, with dividers as "-".</summary>
    static List<string> Labels(IElement list) =>
        list.Children
            .Select(item => item.ClassList.Contains("lc-menu-divider")
                ? "-"
                : item.QuerySelector(".lc-menu-item-text")!.TextContent.Trim())
            .ToList();

    private sealed class Entry(string id, string label, string? section = null, string? after = null) : IServerNavigationExtension
    {
        public string Id => id;
        public string Label => label;
        public string Href => $"/Plugins/{id}";
        public string? Section => section;
        public string? After => after;
    }
}
