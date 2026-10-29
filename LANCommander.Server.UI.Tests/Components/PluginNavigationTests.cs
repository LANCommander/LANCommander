using LANCommander.Server.Plugins;
using LANCommander.Server.UI;
using Microsoft.Extensions.Logging.Abstractions;

namespace LANCommander.Server.UI.Tests.Components;

public class PluginNavigationTests
{
    [Fact]
    public void EntriesFollowTheirAnchorInTheirSection()
    {
        var navigation = new PluginNavigation(
        [
            new Entry("sync", "Role Sync", ServerNavigationSection.Settings, "/Settings/Users"),
            new Entry("other", "Elsewhere", ServerNavigationSection.Metadata, "/Settings/Users"),
        ]);

        Assert.Equal(["Role Sync"], Labels(navigation.After(ServerNavigationSection.Settings, "/settings/users")));
        Assert.Empty(navigation.After(ServerNavigationSection.Settings, "/Settings/Roles"));
    }

    [Fact]
    public void MainSectionIsTheTopLevel()
    {
        var navigation = new PluginNavigation(
        [
            new Entry("catalog", "Catalog", ServerNavigationSection.Main, ServerNavigationSection.Metadata),
        ]);

        Assert.Equal(["Catalog"], Labels(navigation.After(null, ServerNavigationSection.Metadata)));
        Assert.Empty(navigation.After(ServerNavigationSection.Metadata, ServerNavigationSection.Metadata));
    }

    [Fact]
    public void EntriesAtTheSameSpotAreOrderedThenSortedByLabel()
    {
        var navigation = new PluginNavigation(
        [
            new Entry("b", "Beta", ServerNavigationSection.Scripting, "/Scripting/Snippets", order: 1),
            new Entry("a", "Alpha", ServerNavigationSection.Scripting, "/Scripting/Snippets", order: 1),
            new Entry("z", "Zeta", ServerNavigationSection.Scripting, "/Scripting/Snippets", order: 0),
        ]);

        Assert.Equal(["Zeta", "Alpha", "Beta"], Labels(navigation.After(ServerNavigationSection.Scripting, "/Scripting/Snippets")));
    }

    [Fact]
    public void StartEntriesArePlacedFirstOnly()
    {
        var navigation = new PluginNavigation(
        [
            new Entry("first", "First", ServerNavigationSection.Settings, ServerNavigationAnchor.Start),
        ]);

        Assert.Equal(["First"], Labels(navigation.Start(ServerNavigationSection.Settings)));
        Assert.Empty(navigation.End(ServerNavigationSection.Settings, Anchors()));
    }

    [Fact]
    public void EntriesWithoutAnAnchorOrWithAMissingOneArePlacedLast()
    {
        var navigation = new PluginNavigation(
        [
            new Entry("last", "Last", ServerNavigationSection.Settings),
            new Entry("missing", "Missing", ServerNavigationSection.Settings, "/Settings/Gone"),
            new Entry("present", "Present", ServerNavigationSection.Settings, "/Settings/Users"),
            new Entry("plugin", "Chained", ServerNavigationSection.Settings, "/Plugins/last"),
        ]);

        Assert.Equal(
            ["Chained", "Last", "Missing"],
            Labels(navigation.End(ServerNavigationSection.Settings, Anchors("/Settings/Users", "/Plugins/last"))));
        Assert.Empty(navigation.After(ServerNavigationSection.Settings, "/Plugins/last"));
    }

    [Fact]
    public void EntriesWithoutAKnownSectionGoToThePluginArea()
    {
        var navigation = new PluginNavigation(
        [
            new Entry("none", "None"),
            new Entry("unknown", "Unknown", "Nowhere"),
            new Entry("placed", "Placed", ServerNavigationSection.Settings),
        ]);

        Assert.Equal(["None", "Unknown"], Labels(navigation.PluginArea));
        Assert.Empty(navigation.End(null, Anchors()));
    }

    [Fact]
    public void ProfileEntriesGoToTheProfileMenuOnly()
    {
        var navigation = new PluginNavigation(
        [
            new Entry("pin", "PIN", ServerNavigationSection.Profile, "/Profile/AccessTokens"),
            new Entry("last", "Last", ServerNavigationSection.Profile),
        ]);

        Assert.Empty(navigation.PluginArea);
        Assert.Empty(navigation.End(null, Anchors()));
        Assert.Equal(["PIN"], Labels(navigation.After(ServerNavigationSection.Profile, "/Profile/AccessTokens")));
        Assert.Equal(["Last"], Labels(navigation.End(ServerNavigationSection.Profile, Anchors("/Profile/AccessTokens"))));
    }

    [Fact]
    public void ResolvingDropsInvalidAndDuplicateEntries()
    {
        var entries = PluginNavigation.Resolve(
        [
            new Entry("valid", "Valid"),
            new Entry("outside", "Outside", href: "/Games"),
            new Entry("", "No Id"),
            new Entry("same", "First", href: "/Plugins/First"),
            new Entry("same", "Second", href: "/Plugins/Second"),
            new Entry("route-a", "Route A", href: "/Plugins/Shared"),
            new Entry("route-b", "Route B", href: "/Plugins/Shared"),
            new Entry("unknown", "Unknown Section", "Nowhere"),
        ], NullLogger.Instance);

        Assert.Equal(["Valid", "Unknown Section"], Labels(entries));
    }

    static List<string> Labels(IEnumerable<IServerNavigationExtension> entries) =>
        entries.Select(entry => entry.Label).ToList();

    static HashSet<string> Anchors(params string[] anchors) => new(anchors, StringComparer.OrdinalIgnoreCase);

    private sealed class Entry(
        string id,
        string label,
        string? section = null,
        string? after = null,
        int order = 0,
        string? href = null) : IServerNavigationExtension
    {
        public string Id => id;
        public string Label => label;
        public string Href => href ?? $"/Plugins/{id}";
        public string? Section => section;
        public string? After => after;
        public int Order => order;
    }
}
