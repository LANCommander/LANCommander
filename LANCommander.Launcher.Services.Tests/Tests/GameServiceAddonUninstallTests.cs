using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Services.Tests.Helpers;
using LANCommander.SDK.Enums;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Services.Tests.Tests;

/// <summary>
/// Regression tests for the MEDIUM "uninstalling an installation leaves add-on legacy Game fields
/// installed" finding.
///
/// Deleting a <see cref="GameInstallation"/> cascades its <see cref="GameInstallationAddon"/>
/// tracking rows away. <c>GameInstallationService.SyncAddonMirrorsAsync</c> deliberately refuses to
/// clear an add-on's legacy Game.Installed/InstallDirectory mirror when it can find no tracking row
/// for that add-on anywhere (it cannot tell genuinely unmigrated legacy state from a real "not
/// installed" fact), so after the cascade the just-uninstalled add-ons were left permanently
/// reporting installed. <c>GameService.UninstallAsync</c> now captures the tracked add-ons before
/// the delete and clears exactly those that no surviving installation still tracks.
///
/// These run on real SQLite rather than EF InMemory precisely because the bug depends on a
/// database-enforced cascade of rows that are not in the change tracker — EF InMemory would not
/// perform that cascade at all, so the regression could not be reproduced there.
/// </summary>
public class GameServiceAddonUninstallTests
{
    private static GameInstallation MakeInstallation(Guid gameId, string installDirectory) =>
        new()
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            InstallDirectory = installDirectory,
            InstalledOn = DateTime.UtcNow,
        };

    private static Game MakeAddon(Guid baseGameId, string title, string installDirectory) =>
        new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            Type = GameType.Expansion,
            BaseGameId = baseGameId,
            Installed = true,
            InstallDirectory = installDirectory,
            InstalledVersion = "1.0",
            InstalledOn = DateTime.UtcNow,
        };

    [Fact]
    public async Task UninstallAsync_clears_addon_legacy_state_when_its_last_tracking_row_is_deleted()
    {
        await using var db = SqliteTestDatabase.Create();

        var context = db.Context;
        var installationService = ServiceTestFactory.CreateGameInstallationService(context);
        var toolService = ServiceTestFactory.CreateToolService(context);
        var gameService = ServiceTestFactory.CreateGameService(context, toolService, installationService);

        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life", Installed = true };
        context.Games!.Add(game);
        await context.SaveChangesAsync();

        var installation = MakeInstallation(game.Id, @"C:\Games\HalfLife");
        await installationService.AddInstallationAsync(installation);

        var addon = MakeAddon(game.Id, "Opposing Force", installation.InstallDirectory);
        context.Games!.Add(addon);
        await context.SaveChangesAsync();

        await installationService.SetAddonInstalledAsync(installation.Id, addon.Id, "1.0");

        await gameService.UninstallAsync(game, installation);

        // The cascade really happened — this is the precondition the bug depended on.
        (await context.Set<GameInstallationAddon>().CountAsync()).ShouldBe(0);

        var reloadedAddon = await context.Games!.FindAsync(addon.Id);

        reloadedAddon!.Installed.ShouldBeFalse("the add-on's files went with the installation, so its legacy mirror must not keep claiming it is installed");
        reloadedAddon.InstallDirectory.ShouldBeNull();
        reloadedAddon.InstalledVersion.ShouldBeNull();
        reloadedAddon.InstalledOn.ShouldBeNull();
    }

    [Fact]
    public async Task UninstallAsync_preserves_addon_legacy_state_when_the_addon_survives_on_another_installation()
    {
        await using var db = SqliteTestDatabase.Create();

        var context = db.Context;
        var installationService = ServiceTestFactory.CreateGameInstallationService(context);
        var toolService = ServiceTestFactory.CreateToolService(context);
        var gameService = ServiceTestFactory.CreateGameService(context, toolService, installationService);

        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life", Installed = true };
        context.Games!.Add(game);
        await context.SaveChangesAsync();

        var installationA = MakeInstallation(game.Id, @"C:\Games\HalfLife (1.0.0)");
        var installationB = MakeInstallation(game.Id, @"C:\Games\HalfLife (1.1.0)");
        await installationService.AddInstallationAsync(installationA);
        await installationService.AddInstallationAsync(installationB, select: true);

        var addon = MakeAddon(game.Id, "Opposing Force", installationB.InstallDirectory);
        context.Games!.Add(addon);
        await context.SaveChangesAsync();

        // The same add-on is installed into both side-by-side installations.
        await installationService.SetAddonInstalledAsync(installationA.Id, addon.Id, "1.0");
        await installationService.SetAddonInstalledAsync(installationB.Id, addon.Id, "1.0");

        await gameService.UninstallAsync(game, installationA);

        // Only installation A's tracking row went away with it.
        (await context.Set<GameInstallationAddon>().CountAsync()).ShouldBe(1);
        (await installationService.IsAddonInstalledForInstallationAsync(installationB.Id, addon.Id)).ShouldBeTrue();

        var reloadedAddon = await context.Games!.FindAsync(addon.Id);

        reloadedAddon!.Installed.ShouldBeTrue("the add-on is still installed on the surviving installation");
        reloadedAddon.InstallDirectory.ShouldBe(installationB.InstallDirectory);
    }

    [Fact]
    public async Task UninstallAsync_leaves_untracked_legacy_addon_state_alone()
    {
        // An add-on carrying genuinely unmigrated legacy state — installed according to its Game
        // fields but never associated with any installation of this game — has no connection to
        // the installation being removed, so uninstalling must not touch it. (This is the same
        // conservative rule SyncAddonMirrorsAsync already applies; the new clearing pass must not
        // widen it.)
        await using var db = SqliteTestDatabase.Create();

        var context = db.Context;
        var installationService = ServiceTestFactory.CreateGameInstallationService(context);
        var toolService = ServiceTestFactory.CreateToolService(context);
        var gameService = ServiceTestFactory.CreateGameService(context, toolService, installationService);

        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life", Installed = true };
        context.Games!.Add(game);
        await context.SaveChangesAsync();

        var installation = MakeInstallation(game.Id, @"C:\Games\HalfLife");
        await installationService.AddInstallationAsync(installation);

        var untrackedAddon = MakeAddon(game.Id, "Blue Shift", @"C:\Legacy\HalfLife");
        context.Games!.Add(untrackedAddon);
        await context.SaveChangesAsync();

        await gameService.UninstallAsync(game, installation);

        var reloadedAddon = await context.Games!.FindAsync(untrackedAddon.Id);

        reloadedAddon!.Installed.ShouldBeTrue("legacy add-on state that was never connected to this installation must be preserved");
        reloadedAddon.InstallDirectory.ShouldBe(@"C:\Legacy\HalfLife");
    }

    [Fact]
    public async Task UninstallAsync_clears_only_the_addons_that_lost_their_last_tracking_row()
    {
        await using var db = SqliteTestDatabase.Create();

        var context = db.Context;
        var installationService = ServiceTestFactory.CreateGameInstallationService(context);
        var toolService = ServiceTestFactory.CreateToolService(context);
        var gameService = ServiceTestFactory.CreateGameService(context, toolService, installationService);

        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life", Installed = true };
        context.Games!.Add(game);
        await context.SaveChangesAsync();

        var installationA = MakeInstallation(game.Id, @"C:\Games\HalfLife (1.0.0)");
        var installationB = MakeInstallation(game.Id, @"C:\Games\HalfLife (1.1.0)");
        await installationService.AddInstallationAsync(installationA, select: true);
        await installationService.AddInstallationAsync(installationB, select: false);

        var sharedAddon = MakeAddon(game.Id, "Opposing Force", installationA.InstallDirectory);
        var onlyOnAAddon = MakeAddon(game.Id, "Blue Shift", installationA.InstallDirectory);
        context.Games!.AddRange(sharedAddon, onlyOnAAddon);
        await context.SaveChangesAsync();

        await installationService.SetAddonInstalledAsync(installationA.Id, sharedAddon.Id, "1.0");
        await installationService.SetAddonInstalledAsync(installationB.Id, sharedAddon.Id, "1.0");
        await installationService.SetAddonInstalledAsync(installationA.Id, onlyOnAAddon.Id, "1.0");

        await gameService.UninstallAsync(game, installationA);

        var reloadedShared = await context.Games!.FindAsync(sharedAddon.Id);
        var reloadedOnlyOnA = await context.Games!.FindAsync(onlyOnAAddon.Id);

        reloadedShared!.Installed.ShouldBeTrue();
        reloadedShared.InstallDirectory.ShouldBe(installationB.InstallDirectory);

        reloadedOnlyOnA!.Installed.ShouldBeFalse();
        reloadedOnlyOnA.InstallDirectory.ShouldBeNull();
    }

    [Fact]
    public async Task ClearOrphanedAddonLegacyStateAsync_ignores_addons_that_are_still_tracked_elsewhere()
    {
        // Direct coverage of the reusable service method, independent of the uninstall flow.
        await using var db = SqliteTestDatabase.Create();

        var context = db.Context;
        var installationService = ServiceTestFactory.CreateGameInstallationService(context);

        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life", Installed = true };
        context.Games!.Add(game);
        await context.SaveChangesAsync();

        var installation = MakeInstallation(game.Id, @"C:\Games\HalfLife");
        await installationService.AddInstallationAsync(installation);

        var trackedAddon = MakeAddon(game.Id, "Opposing Force", installation.InstallDirectory);
        var orphanedAddon = MakeAddon(game.Id, "Blue Shift", installation.InstallDirectory);
        context.Games!.AddRange(trackedAddon, orphanedAddon);
        await context.SaveChangesAsync();

        await installationService.SetAddonInstalledAsync(installation.Id, trackedAddon.Id, "1.0");

        await installationService.ClearOrphanedAddonLegacyStateAsync(game.Id, [trackedAddon.Id, orphanedAddon.Id]);

        (await context.Games!.FindAsync(trackedAddon.Id))!.Installed.ShouldBeTrue();
        (await context.Games!.FindAsync(orphanedAddon.Id))!.Installed.ShouldBeFalse();
    }

    [Fact]
    public async Task GetTrackedAddonIdsForInstallationAsync_returns_tracking_rows_regardless_of_installed_flag()
    {
        await using var db = SqliteTestDatabase.Create();

        var context = db.Context;
        var installationService = ServiceTestFactory.CreateGameInstallationService(context);

        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life", Installed = true };
        context.Games!.Add(game);
        await context.SaveChangesAsync();

        var installation = MakeInstallation(game.Id, @"C:\Games\HalfLife");
        await installationService.AddInstallationAsync(installation);

        var addon = MakeAddon(game.Id, "Opposing Force", installation.InstallDirectory);
        context.Games!.Add(addon);
        await context.SaveChangesAsync();

        await installationService.SetAddonInstalledAsync(installation.Id, addon.Id, "1.0");
        await installationService.SetAddonUninstalledAsync(installation.Id, addon.Id);

        var tracked = await installationService.GetTrackedAddonIdsForInstallationAsync(installation.Id);

        tracked.ShouldBe([addon.Id]);
    }
}
