using Bunit;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.UI.Pages.Games.Components;
using LANCommander.Server.UI.Pages.Games.Edit;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// The game editor's header as drawn: "Games › {title}", the game's title and status, and the
/// "Unsaved changes" state that Discard clears. Nothing here saves, so the seeded game is untouched.
/// </summary>
[Collection("BUnit")]
public class GameEditHeaderTests : BUnitTestContext
{
    public GameEditHeaderTests(BUnitServerFixture fixture) : base(fixture)
    {
    }

    private IRenderedComponent<General> RenderGeneral()
    {
        var cut = Render<General>(parameters => parameters.Add(p => p.Id, Fixture.TestGameId));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".game-general-form")), TimeSpan.FromSeconds(10));

        return cut;
    }

    private static AngleSharp.Dom.IElement InputFor(IRenderedComponent<General> cut, string label)
    {
        var id = cut.FindAll("label.lc-form-item-label")
            .First(l => l.TextContent.Trim() == label)
            .GetAttribute("for");

        return cut.Find($"#{id}");
    }

    [Fact]
    public void Header_ShowsBreadcrumbTitleAndStatus()
    {
        var cut = RenderGeneral();

        var crumbs = cut.FindAll(".lc-page-header-breadcrumb li").Select(li => li.TextContent.Trim()).ToList();
        Assert.Equal(["Games", BUnitServerFixture.TestGameTitle], crumbs);

        Assert.Equal(BUnitServerFixture.TestGameTitle, cut.Find(".lc-page-header-title").TextContent.Trim());
        Assert.Equal("Published", cut.Find(".lc-page-header-tags .lc-tag").TextContent.Trim());
    }

    [Fact]
    public void Header_ShowsUnsavedChanges_UntilDiscarded()
    {
        var cut = RenderGeneral();

        Assert.Empty(cut.FindAll(".game-edit-unsaved"));
        Assert.True(cut.FindAll("button").Single(b => b.TextContent.Trim() == "Discard").HasAttribute("disabled"));

        InputFor(cut, "Sort title").Change("Changed sort title");

        cut.WaitForAssertion(() => Assert.Contains("Unsaved changes", cut.Find(".game-edit-unsaved").TextContent));

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Discard").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Empty(cut.FindAll(".game-edit-unsaved"));
            Assert.NotEqual("Changed sort title", InputFor(cut, "Sort title").GetAttribute("value"));
        }, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void MainGame_LocksInstallToAndShowInLibrary()
    {
        var cut = RenderGeneral();

        var installTo = cut.FindAll(".lc-form-item")
            .Single(i => i.QuerySelector("label.lc-form-item-label")?.TextContent.Trim() == "Install to");
        Assert.NotNull(installTo.QuerySelector(".rz-dropdown.rz-state-disabled"));

        var showInLibrary = cut.FindAll(".game-general-switch")
            .Single(s => s.TextContent.Contains("Show in library"));
        Assert.NotNull(showInLibrary.QuerySelector(".rz-switch.rz-state-disabled"));

        // The main game's own folder can still be renamed
        Assert.False(InputFor(cut, "Directory name").HasAttribute("disabled"));
    }

    [Fact]
    public void Header_ShowsHidden_WhenShowInDepotIsTurnedOff()
    {
        var cut = RenderGeneral();

        cut.Find(".game-general-switch.lc-switch-on .rz-switch").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal("Hidden", cut.Find(".lc-page-header-tags .lc-tag").TextContent.Trim());
            Assert.NotEmpty(cut.FindAll(".game-edit-unsaved"));
        });
    }
}

/// <summary>The texts of the editor's header and Preview, and the snapshot behind "Unsaved changes".</summary>
public class GameEditFormatTests
{
    [Theory]
    [InlineData("338.4", 4_700_000_000L, "v338.4 · 4.70 GB")]
    [InlineData("v1.2", null, "v1.2")]
    [InlineData(null, 512_000_000L, "512.00 MB")]
    [InlineData("", 0L, null)]
    public void Summary_JoinsVersionAndSize(string? version, long? size, string? expected)
    {
        Assert.Equal(expected, GameEditFormat.Summary(version, size)?.Replace(',', '.'));
    }

    [Theory]
    [InlineData(6, 1, "Media · 6 screenshots, 1 video")]
    [InlineData(1, 0, "Media · 1 screenshot")]
    [InlineData(0, 2, "Media · 2 videos")]
    [InlineData(0, 0, "Media")]
    public void MediaKicker_CountsEachKind(int screenshots, int videos, string expected)
    {
        Assert.Equal(expected, GameEditFormat.MediaKicker(screenshots, videos));
    }

    [Theory]
    [InlineData(1, "1 redistributable")]
    [InlineData(2, "2 redistributables")]
    [InlineData(0, "0 redistributables")]
    public void Count_Pluralises(int count, string expected)
    {
        Assert.Equal(expected, GameEditFormat.Count(count, "redistributable"));
    }

    private static Game NewGame() => new()
    {
        Title = "Arena Blitz",
        Description = "Line one\r\nLine two",
        Genres = [new Genre { Id = Guid.NewGuid(), Name = "Shooter" }, new Genre { Id = Guid.NewGuid(), Name = "Strategy" }],
        ExternalIds = [new GameExternalId { Provider = "Steam", ExternalId = "4920" }],
    };

    [Fact]
    public void Snapshot_IgnoresLineEndingsEmptyTextAndTaxonomyOrder()
    {
        var game = NewGame();
        var saved = GameEditSnapshot.Of(game);

        game.Description = "Line one\nLine two";
        game.Notes = "";
        game.Genres = game.Genres!.Reverse().ToList();

        Assert.Equal(saved, GameEditSnapshot.Of(game));
    }

    [Fact]
    public void Snapshot_SeesEditedFields()
    {
        var game = NewGame();
        var saved = GameEditSnapshot.Of(game);

        game.Published = false;
        Assert.NotEqual(saved, GameEditSnapshot.Of(game));
        game.Published = true;

        game.Genres!.Remove(game.Genres.First());
        Assert.NotEqual(saved, GameEditSnapshot.Of(game));

        game = NewGame();
        saved = GameEditSnapshot.Of(game);
        game.ExternalIds!.First().ExternalId = "4921";
        Assert.NotEqual(saved, GameEditSnapshot.Of(game));

        game = NewGame();
        saved = GameEditSnapshot.Of(game);
        game.Type = GameType.Mod;
        Assert.NotEqual(saved, GameEditSnapshot.Of(game));

        game = NewGame();
        saved = GameEditSnapshot.Of(game);
        game.InstallTo = GameInstallLocation.SubDirectory;
        Assert.NotEqual(saved, GameEditSnapshot.Of(game));

        game = NewGame();
        saved = GameEditSnapshot.Of(game);
        game.ShowInLibrary = false;
        Assert.NotEqual(saved, GameEditSnapshot.Of(game));

        game = NewGame();
        saved = GameEditSnapshot.Of(game);
        game.DirectoryName = "arena";
        Assert.NotEqual(saved, GameEditSnapshot.Of(game));
    }
}
