using Bunit;
using LANCommander.Server.UI.Pages.Games.Edit;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// bUnit component tests for the game edit "General" page. These replace the flaky
/// Playwright equivalents in <c>GameEditTests</c> that asserted component behaviour
/// (tab list, form fields, action buttons). Rendering happens in-process and synchronously,
/// so there are no SignalR circuit races.
///
/// Tests that assert real routing/URL navigation (e.g. clicking a tab changes the address)
/// remain in the Playwright smoke layer — bUnit renders a single component without a router.
/// </summary>
[Collection("BUnit")]
public class GameEditComponentTests : BUnitTestContext
{
    public GameEditComponentTests(BUnitServerFixture fixture) : base(fixture)
    {
    }

    private IRenderedComponent<General> RenderGeneral()
        => RenderComponent<General>(parameters => parameters
            .Add(p => p.Id, Fixture.TestGameId));

    [Fact]
    public void GameEdit_LoadsSeededGameTitle()
    {
        var cut = RenderGeneral();

        // The seeded game's title is bound into the title lookup input.
        Assert.Contains(BUnitServerFixture.TestGameTitle, cut.Markup);
    }

    [Fact]
    public void GameEdit_ShowsAllExpectedTabs()
    {
        var cut = RenderGeneral();

        var menuText = cut.Markup;

        foreach (var tab in new[] { "General", "Media", "Archives", "Actions", "Keys", "Scripts" })
        {
            Assert.Contains(tab, menuText);
        }
    }

    [Fact]
    public void GameEdit_HasSaveButton()
    {
        var cut = RenderGeneral();

        Assert.Contains(
            cut.FindAll("button"),
            b => b.TextContent.Contains("Save", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GameEdit_HasExportButton()
    {
        var cut = RenderGeneral();

        Assert.Contains(
            cut.FindAll("button"),
            b => b.TextContent.Contains("Export", StringComparison.OrdinalIgnoreCase));
    }
}
