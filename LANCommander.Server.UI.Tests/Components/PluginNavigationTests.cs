using System.Security.Claims;
using LANCommander.Server.Plugins;
using LANCommander.Server.UI;
using Microsoft.Extensions.Logging.Abstractions;

namespace LANCommander.Server.UI.Tests.Components;

public class PluginNavigationTests
{
    [Fact]
    public void EntriesFollowTheirAnchorInTheirSection()
    {
        var navigation = Navigation(
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
        var navigation = Navigation(
        [
            new Entry("catalog", "Catalog", ServerNavigationSection.Main, ServerNavigationSection.Metadata),
        ]);

        Assert.Equal(["Catalog"], Labels(navigation.After(null, ServerNavigationSection.Metadata)));
        Assert.Empty(navigation.After(ServerNavigationSection.Metadata, ServerNavigationSection.Metadata));
    }

    [Fact]
    public void EntriesAtTheSameSpotAreOrderedThenSortedByLabel()
    {
        var navigation = Navigation(
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
        var navigation = Navigation(
        [
            new Entry("first", "First", ServerNavigationSection.Settings, ServerNavigationAnchor.Start),
        ]);

        Assert.Equal(["First"], Labels(navigation.Start(ServerNavigationSection.Settings)));
        Assert.Empty(navigation.End(ServerNavigationSection.Settings, Anchors()));
    }

    [Fact]
    public void EntriesWithoutAnAnchorOrWithAMissingOneArePlacedLast()
    {
        var navigation = Navigation(
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
        var navigation = Navigation(
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
        var navigation = Navigation(
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
        var (links, _) = PluginNavigation.Resolve(
        [
            new Entry("valid", "Valid"),
            new Entry("outside", "Outside", href: "/Games"),
            new Entry("", "No Id"),
            new Entry("same", "First", href: "/Plugins/First"),
            new Entry("same", "Second", href: "/Plugins/Second"),
            new Entry("route-a", "Route A", href: "/Plugins/Shared"),
            new Entry("route-b", "Route B", href: "/Plugins/Shared"),
            new Entry("unknown", "Unknown Section", "Nowhere"),
        ], [], NullLogger.Instance);

        Assert.Equal(["Valid", "Unknown Section"], links.Select(link => link.Label));
    }

    [Fact]
    public void ResolvingDropsInvalidDuplicateAndCyclicGroups()
    {
        var (links, groups) = PluginNavigation.Resolve(
        [
            new Entry("shared", "Link Sharing An Id"),
        ],
        [
            Group("tools", "Tools"),
            Group(ServerNavigationSection.Settings, "Built-In Name"),
            Group("profile", "In Profile", ServerNavigationSection.Profile),
            Group("shared", "Group Sharing An Id"),
            Group("a", "A", "b"),
            Group("b", "B", "a"),
            Group("below-cycle", "Below Cycle", "a"),
        ], NullLogger.Instance);

        Assert.Empty(links);
        Assert.Equal(["Tools", "Below Cycle"], groups.Select(group => group.Label));
    }

    [Fact]
    public void GroupsHoldTheEntriesThatNameThem()
    {
        var navigation = Build(
        [
            new Entry("catalog", "Catalog", "tools"),
            new Entry("first", "First", "tools", ServerNavigationAnchor.Start),
            new Entry("deep", "Deep", "nested"),
        ],
        [
            Group("tools", "Tools", ServerNavigationSection.Main, ServerNavigationSection.Metadata),
            Group("nested", "Nested", "tools"),
        ]);

        Assert.Equal(["Tools"], Labels(navigation.After(null, ServerNavigationSection.Metadata)));
        Assert.Equal(["First"], Labels(navigation.Start("tools")));
        Assert.Equal(["Catalog", "Nested"], Labels(navigation.End("tools", Anchors())));
        Assert.Equal(["Deep"], Labels(navigation.End("nested", Anchors())));
        Assert.Empty(navigation.PluginArea);
    }

    [Fact]
    public void EntriesDoNotFollowPluginGroups()
    {
        var navigation = Build(
            [new Entry("inside", "Inside", "tools"), new Entry("after", "After", ServerNavigationSection.Main, "tools")],
            [Group("tools", "Tools", ServerNavigationSection.Main)]);

        Assert.Empty(navigation.After(null, "tools"));
        Assert.Equal(["After", "Tools"], Labels(navigation.End(null, Anchors("tools"))));
    }

    [Fact]
    public void GroupsWithoutVisibleEntriesAreHidden()
    {
        var navigation = Build(
        [
            new Entry("hidden", "Hidden", "admin", access: PluginAccessPolicy.AuthenticatedUser),
            new Entry("restricted", "Restricted", "open"),
            new Entry("shown", "Shown", "open", access: PluginAccessPolicy.AuthenticatedUser),
        ],
        [
            Group("admin", "Admin Group"),
            Group("empty", "Empty", access: PluginAccessPolicy.AuthenticatedUser),
            Group("open", "Open Group", access: PluginAccessPolicy.AuthenticatedUser),
        ],
        new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "player")], "Test")));

        Assert.Equal(["Open Group"], Labels(navigation.PluginArea));
        Assert.Equal(["Shown"], Labels(navigation.End("open", Anchors())));
        Assert.Empty(navigation.End("admin", Anchors()));
    }

    [Fact]
    public void EntriesInAMissingGroupGoToThePluginArea()
    {
        var navigation = Build([new Entry("orphan", "Orphan", "missing")], []);

        Assert.Equal(["Orphan"], Labels(navigation.PluginArea));
    }

    static PluginNavigation Navigation(IEnumerable<IServerNavigationExtension> entries) =>
        new(entries.Select(PluginNavigationEntry.From));

    static PluginNavigation Build(
        IEnumerable<IServerNavigationExtension> links,
        IEnumerable<IServerNavigationGroupExtension> groups,
        ClaimsPrincipal? user = null) =>
        PluginNavigation.Build(links, groups, user ?? Administrator, null, NullLogger.Instance);

    static readonly ClaimsPrincipal Administrator = new(new ClaimsIdentity(
        [new Claim(ClaimTypes.Name, "admin"), new Claim(ClaimTypes.Role, LANCommander.SDK.Roles.Administrator)], "Test"));

    static ServerNavigationGroup Group(
        string id,
        string label,
        string? section = null,
        string? after = null,
        PluginAccessPolicy? access = null) =>
        new() { Id = id, Label = label, Section = section, After = after, Access = access ?? PluginAccessPolicy.Administrator };

    static List<string> Labels(IEnumerable<PluginNavigationEntry> entries) =>
        entries.Select(entry => entry.Label).ToList();

    static HashSet<string> Anchors(params string[] anchors) => new(anchors, StringComparer.OrdinalIgnoreCase);

    private sealed class Entry(
        string id,
        string label,
        string? section = null,
        string? after = null,
        int order = 0,
        string? href = null,
        PluginAccessPolicy? access = null) : IServerNavigationExtension
    {
        public string Id => id;
        public string Label => label;
        public string Href => href ?? $"/Plugins/{id}";
        public string? Section => section;
        public string? After => after;
        public int Order => order;
        public PluginAccessPolicy Access => access ?? PluginAccessPolicy.Administrator;
    }
}
