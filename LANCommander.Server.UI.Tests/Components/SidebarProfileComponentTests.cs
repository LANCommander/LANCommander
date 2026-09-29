using Bunit;
using LANCommander.Server.UI.Shared;
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
}
