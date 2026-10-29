using Bunit;
using LANCommander.Server.Plugins;
using LANCommander.Server.UI.Shared;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// bUnit coverage for the sidebar profile footer. Social links and the version used to live inside
/// the profile dropdown's (lazy) overlay; they must now render always-visible at the foot of the
/// rail, so they appear without the dropdown being opened.
/// </summary>
[Collection("BUnit")]
public class SidebarProfileComponentTests : BUnitTestContext
{
    public SidebarProfileComponentTests(BUnitServerFixture fixture) : base(fixture)
    {
    }

    [Fact]
    public void SocialLinksAndVersionAreVisibleWithoutOpeningTheDropdown()
    {
        var cut = Render<SidebarProfile>();

        // The dropdown overlay is a lazy Radzen popup, so anything only rendered inside it would be
        // absent here. These being present proves they are pinned to the rail, not in the menu.
        var social = cut.Find(".lc-social");
        Assert.NotEmpty(social.QuerySelectorAll("a"));

        var version = cut.Find(".lc-sidebar-version");
        Assert.Contains("LANCommander v", version.TextContent);

        // And they are siblings of the profile dropdown, not descendants of it
        Assert.Empty(cut.FindAll(".lc-sidebar-profile .lc-social"));
        Assert.Empty(cut.FindAll(".lc-sidebar-profile .lc-sidebar-version"));
    }

    [Fact]
    public void PluginProfileEntriesAreListedInTheProfileMenu()
    {
        Services.AddSingleton<IServerNavigationExtension>(new Entry("pin", "Game PIN", "/Profile/AccessTokens"));
        Services.AddSingleton<IServerNavigationExtension>(new Entry("last", "Last Entry", null));

        var cut = Render<SidebarProfile>();

        cut.Find(".lc-sidebar-profile-button").Click();

        cut.WaitForAssertion(() =>
        {
            var labels = cut.FindAll(".lc-menu-item-text").Select(item => item.TextContent.Trim()).ToList();

            Assert.Equal(["Access Tokens", "Game PIN", "Last Entry", "Logout"], labels[^4..]);
        });
    }

    private sealed class Entry(string id, string label, string? after) : IServerNavigationExtension
    {
        public string Id => id;
        public string Label => label;
        public string Href => $"/Plugins/{id}";
        public string? Section => ServerNavigationSection.Profile;
        public string? After => after;
        public PluginAccessPolicy Access => PluginAccessPolicy.AuthenticatedUser;
    }
}
