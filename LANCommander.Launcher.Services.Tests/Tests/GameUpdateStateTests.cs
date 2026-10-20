using LANCommander.Launcher.Models;
using LANCommander.Launcher.Services.Tests.Helpers;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Services.Tests.Tests;

/// <summary>
/// A game with automatic updates off is frozen on its installed version: it isn't offered an update,
/// and the installed version is recognised by its server id before its label.
/// </summary>
public class GameUpdateStateTests
{
    [Fact]
    public void UpdateIsAvailableWhenLatestDiffersAndGameTakesUpdates()
    {
        var game = GameFactory.Make("Game", installed: true);
        game.InstalledVersion = "1.0";
        game.LatestVersion = "2.0";

        game.IsUpdateAvailable().ShouldBeTrue();
        game.AsListItem().State.ShouldBe(ListItemState.UpdateAvailable);
    }

    [Fact]
    public void FrozenGameIsNotOfferedAnUpdate()
    {
        var game = GameFactory.Make("Game", installed: true);
        game.InstalledVersion = "1.0";
        game.LatestVersion = "2.0";
        game.AutoUpdate = false;

        game.IsUpdateAvailable().ShouldBeFalse();
        game.AsListItem().State.ShouldNotBe(ListItemState.UpdateAvailable);
    }

    [Fact]
    public void NewGamesTakeUpdatesByDefault()
    {
        GameFactory.Make("Game").AutoUpdate.ShouldBeTrue();
    }

    private static SDK.Models.GameVersion Version(string label, int sortOrder) =>
        new() { Id = Guid.NewGuid(), Version = label, SortOrder = sortOrder, ArchiveId = Guid.NewGuid() };

    [Fact]
    public void ResolveInstalledVersionPrefersId()
    {
        var v1 = Version("1.0", 0);
        var v2 = Version("2.0", 1);

        var game = GameFactory.Make("Game", installed: true);
        game.InstalledVersion = "2.0";
        game.InstalledVersionId = v1.Id;

        InstallService.ResolveInstalledVersion([v1, v2], game).ShouldBe(v1);
    }

    [Fact]
    public void ResolveInstalledVersionFallsBackToNewestMatchingLabel()
    {
        var older = Version("1.0", 0);
        var newer = Version("1.0", 1);

        var game = GameFactory.Make("Game", installed: true);
        game.InstalledVersion = "1.0";

        InstallService.ResolveInstalledVersion([older, newer], game).ShouldBe(newer);
    }
}
