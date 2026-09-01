using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Services.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Services.Tests.Tests;

/// <summary>
/// Covers Phase 4 additions to GameInstallationService: installing two versions of the same game
/// side-by-side with distinct archives/paths, looking installations up by directory/archive,
/// per-installation add-on tracking, and mirroring the selected installation onto the legacy
/// Game/GameTool fields without leaking a non-selected installation's state.
/// </summary>
public class GameInstallationServiceVersioningTests
{
    private static GameInstallation MakeInstallation(Guid gameId, string installDirectory, Guid? archiveId = null, string? version = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            InstallDirectory = installDirectory,
            ArchiveId = archiveId,
            Version = version,
            InstalledOn = DateTime.UtcNow,
        };

    [Fact]
    public async Task Installing_two_versions_of_the_same_game_creates_two_installations_with_distinct_archives_and_paths()
    {
        await using var context = InMemoryDatabaseFactory.Create();
        var service = ServiceTestFactory.CreateGameInstallationService(context);

        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life" };
        context.Games!.Add(game);
        await context.SaveChangesAsync();

        var archiveA = Guid.NewGuid();
        var archiveB = Guid.NewGuid();

        var naturalDirectory = @"C:\Games\HalfLife";
        var firstDestination = await service.GenerateInstallDirectoryAsync(game.Id, naturalDirectory, "1.0.0");
        var first = MakeInstallation(game.Id, firstDestination, archiveA, "1.0.0");
        await service.AddInstallationAsync(first);

        var secondDestination = await service.GenerateInstallDirectoryAsync(game.Id, naturalDirectory, "1.1.0");
        var second = MakeInstallation(game.Id, secondDestination, archiveB, "1.1.0");
        await service.AddInstallationAsync(second, select: false);

        // The first install keeps the natural path; the second gets a distinct, version-suffixed
        // sibling directory rather than colliding with (or overwriting) the first.
        firstDestination.ShouldBe(naturalDirectory);
        secondDestination.ShouldNotBe(firstDestination);

        var installations = await service.GetInstallationsForGameAsync(game.Id);
        installations.Count.ShouldBe(2);
        installations.ShouldContain(i => i.ArchiveId == archiveA && i.InstallDirectory == firstDestination);
        installations.ShouldContain(i => i.ArchiveId == archiveB && i.InstallDirectory == secondDestination);
    }

    [Fact]
    public async Task FindByArchiveAsync_locates_the_installation_pinned_to_that_exact_archive()
    {
        await using var context = InMemoryDatabaseFactory.Create();
        var service = ServiceTestFactory.CreateGameInstallationService(context);

        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life" };
        context.Games!.Add(game);
        await context.SaveChangesAsync();

        var archiveA = Guid.NewGuid();
        var archiveB = Guid.NewGuid();

        var first = MakeInstallation(game.Id, @"C:\Games\HalfLife", archiveA, "1.0.0");
        var second = MakeInstallation(game.Id, @"C:\Games\HalfLife (1.1.0)", archiveB, "1.1.0");
        await service.AddInstallationAsync(first);
        await service.AddInstallationAsync(second, select: false);

        (await service.FindByArchiveAsync(game.Id, archiveA)).ShouldNotBeNull();
        (await service.FindByArchiveAsync(game.Id, archiveA))!.Id.ShouldBe(first.Id);
        (await service.FindByArchiveAsync(game.Id, archiveB))!.Id.ShouldBe(second.Id);
        (await service.FindByArchiveAsync(game.Id, Guid.NewGuid())).ShouldBeNull();
    }

    [Fact]
    public async Task FindByDirectoryAsync_is_case_insensitive_and_scoped_to_the_given_game()
    {
        await using var context = InMemoryDatabaseFactory.Create();
        var service = ServiceTestFactory.CreateGameInstallationService(context);

        var gameA = new Game { Id = Guid.NewGuid(), Title = "Half-Life" };
        var gameB = new Game { Id = Guid.NewGuid(), Title = "Half-Life 2" };
        context.Games!.AddRange(gameA, gameB);
        await context.SaveChangesAsync();

        var installation = MakeInstallation(gameA.Id, @"C:\Games\HalfLife");
        await service.AddInstallationAsync(installation);

        (await service.FindByDirectoryAsync(gameA.Id, @"c:\games\halflife"))!.Id.ShouldBe(installation.Id);
        (await service.FindByDirectoryAsync(gameB.Id, @"C:\Games\HalfLife")).ShouldBeNull();
        (await service.FindByDirectoryAsync(gameA.Id, null)).ShouldBeNull();
    }

    [Fact]
    public async Task An_existing_installations_pinned_archive_is_unaffected_by_a_new_installation_appearing()
    {
        // Simulates the "pinned default change has no effect" guarantee: an installation already
        // pinned to archive A must keep its own ArchiveId/Version when the game gets another
        // installation (representing, e.g., the admin changing the default or a newer archive
        // being uploaded and someone installing it side-by-side).
        await using var context = InMemoryDatabaseFactory.Create();
        var service = ServiceTestFactory.CreateGameInstallationService(context);

        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life" };
        context.Games!.Add(game);
        await context.SaveChangesAsync();

        var archiveA = Guid.NewGuid();
        var archiveB = Guid.NewGuid();

        var pinned = MakeInstallation(game.Id, @"C:\Games\HalfLife", archiveA, "1.0.0");
        await service.AddInstallationAsync(pinned);

        var additional = MakeInstallation(game.Id, @"C:\Games\HalfLife (2.0.0)", archiveB, "2.0.0");
        await service.AddInstallationAsync(additional, select: false);

        var reloadedPinned = await service.GetAsync(pinned.Id);
        reloadedPinned!.ArchiveId.ShouldBe(archiveA);
        reloadedPinned.Version.ShouldBe("1.0.0");
        reloadedPinned.InstallDirectory.ShouldBe(@"C:\Games\HalfLife");
    }

    [Fact]
    public async Task Removing_one_installation_keeps_the_others_state_completely_intact()
    {
        await using var context = InMemoryDatabaseFactory.Create();
        var service = ServiceTestFactory.CreateGameInstallationService(context);

        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life" };
        context.Games!.Add(game);
        await context.SaveChangesAsync();

        var archiveA = Guid.NewGuid();
        var archiveB = Guid.NewGuid();
        var first = MakeInstallation(game.Id, @"C:\Games\HalfLife", archiveA, "1.0.0");
        var second = MakeInstallation(game.Id, @"C:\Games\HalfLife (2.0.0)", archiveB, "2.0.0");
        await service.AddInstallationAsync(first);
        await service.AddInstallationAsync(second, select: false);

        await service.DeleteInstallationAsync(second.Id);

        var remaining = await service.GetInstallationsForGameAsync(game.Id);
        remaining.Count.ShouldBe(1);

        var survivor = remaining.Single();
        survivor.Id.ShouldBe(first.Id);
        survivor.ArchiveId.ShouldBe(archiveA);
        survivor.Version.ShouldBe("1.0.0");
        survivor.InstallDirectory.ShouldBe(@"C:\Games\HalfLife");
        survivor.IsSelected.ShouldBeTrue();
    }

    [Fact]
    public async Task SyncLegacyMirrorsAsync_mirrors_only_the_selected_installation_onto_the_game()
    {
        await using var context = InMemoryDatabaseFactory.Create();
        var service = ServiceTestFactory.CreateGameInstallationService(context);

        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life" };
        context.Games!.Add(game);
        await context.SaveChangesAsync();

        var archiveA = Guid.NewGuid();
        var archiveB = Guid.NewGuid();
        var first = MakeInstallation(game.Id, @"C:\Games\HalfLife", archiveA, "1.0.0");
        var second = MakeInstallation(game.Id, @"C:\Games\HalfLife (2.0.0)", archiveB, "2.0.0");
        await service.AddInstallationAsync(first);
        await service.AddInstallationAsync(second, select: true);

        await service.SyncLegacyMirrorsAsync(game.Id);

        var reloadedGame = await context.Games!.FindAsync(game.Id);
        reloadedGame!.Installed.ShouldBeTrue();
        reloadedGame.InstallDirectory.ShouldBe(second.InstallDirectory);
        reloadedGame.InstalledVersion.ShouldBe("2.0.0");

        // Switch selection back to the first installation and re-sync — the mirror must follow
        // the newly selected installation, not the previously selected one.
        await service.SelectInstallationAsync(first.Id);
        await service.SyncLegacyMirrorsAsync(game.Id);

        reloadedGame = await context.Games!.FindAsync(game.Id);
        reloadedGame!.InstallDirectory.ShouldBe(first.InstallDirectory);
        reloadedGame.InstalledVersion.ShouldBe("1.0.0");
    }

    [Fact]
    public async Task SyncLegacyMirrorsAsync_clears_the_game_when_no_installation_remains_selected()
    {
        await using var context = InMemoryDatabaseFactory.Create();
        var service = ServiceTestFactory.CreateGameInstallationService(context);

        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life" };
        context.Games!.Add(game);
        await context.SaveChangesAsync();

        var installation = MakeInstallation(game.Id, @"C:\Games\HalfLife", Guid.NewGuid(), "1.0.0");
        await service.AddInstallationAsync(installation);
        await service.DeleteInstallationAsync(installation.Id);

        await service.SyncLegacyMirrorsAsync(game.Id);

        var reloadedGame = await context.Games!.FindAsync(game.Id);
        reloadedGame!.Installed.ShouldBeFalse();
        reloadedGame.InstallDirectory.ShouldBeNull();
        reloadedGame.InstalledVersion.ShouldBeNull();
    }

    [Fact]
    public async Task Addon_install_state_is_isolated_per_base_installation()
    {
        await using var context = InMemoryDatabaseFactory.Create();
        var service = ServiceTestFactory.CreateGameInstallationService(context);

        var baseGame = new Game { Id = Guid.NewGuid(), Title = "Half-Life" };
        var addon = new Game { Id = Guid.NewGuid(), Title = "Opposing Force", BaseGameId = baseGame.Id };
        context.Games!.AddRange(baseGame, addon);
        await context.SaveChangesAsync();

        var installationA = MakeInstallation(baseGame.Id, @"C:\Games\HalfLife", Guid.NewGuid(), "1.0.0");
        var installationB = MakeInstallation(baseGame.Id, @"C:\Games\HalfLife (1.1.0)", Guid.NewGuid(), "1.1.0");
        await service.AddInstallationAsync(installationA);
        await service.AddInstallationAsync(installationB, select: false);

        // The add-on is installed for installation A only.
        await service.SetAddonInstalledAsync(installationA.Id, addon.Id, "1.0");

        (await service.IsAddonInstalledForInstallationAsync(installationA.Id, addon.Id)).ShouldBeTrue();
        (await service.IsAddonInstalledForInstallationAsync(installationB.Id, addon.Id)).ShouldBeFalse();

        var installedForA = await service.GetInstalledAddonsForInstallationAsync(installationA.Id);
        installedForA.ShouldContain(a => a.AddonGameId == addon.Id);

        var installedForB = await service.GetInstalledAddonsForInstallationAsync(installationB.Id);
        installedForB.ShouldBeEmpty();

        // Uninstalling from installation A must not be observable from installation B (it was
        // never installed there to begin with — this just guards against a shared/global flag).
        await service.SetAddonUninstalledAsync(installationA.Id, addon.Id);
        (await service.IsAddonInstalledForInstallationAsync(installationA.Id, addon.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task SyncLegacyMirrorsAsync_mirrors_addon_state_only_from_the_selected_installation()
    {
        await using var context = InMemoryDatabaseFactory.Create();
        var service = ServiceTestFactory.CreateGameInstallationService(context);

        var baseGame = new Game { Id = Guid.NewGuid(), Title = "Half-Life" };
        var addon = new Game { Id = Guid.NewGuid(), Title = "Opposing Force", BaseGameId = baseGame.Id };
        context.Games!.AddRange(baseGame, addon);
        await context.SaveChangesAsync();

        var installationA = MakeInstallation(baseGame.Id, @"C:\Games\HalfLife", Guid.NewGuid(), "1.0.0");
        var installationB = MakeInstallation(baseGame.Id, @"C:\Games\HalfLife (1.1.0)", Guid.NewGuid(), "1.1.0");
        await service.AddInstallationAsync(installationA);
        await service.AddInstallationAsync(installationB, select: true);

        // Addon installed only for the non-selected installation A.
        await service.SetAddonInstalledAsync(installationA.Id, addon.Id, "1.0");

        await service.SyncLegacyMirrorsAsync(baseGame.Id);

        var reloadedAddon = await context.Games!.FindAsync(addon.Id);
        reloadedAddon!.Installed.ShouldBeFalse("the addon is only installed for a non-selected installation");

        // Now install it for the selected installation (B) instead and re-sync.
        await service.SetAddonInstalledAsync(installationB.Id, addon.Id, "1.0");
        await service.SyncLegacyMirrorsAsync(baseGame.Id);

        reloadedAddon = await context.Games!.FindAsync(addon.Id);
        reloadedAddon!.Installed.ShouldBeTrue();
        reloadedAddon.InstallDirectory.ShouldBe(installationB.InstallDirectory);
    }

    [Fact]
    public async Task SyncLegacyMirrorsAsync_preserves_legacy_installed_addon_state_when_unmigrated()
    {
        // Simulates legacy/unmigrated data: an addon Game row whose legacy Installed flag was
        // set true under the old single-install model, but no GameInstallationAddon association
        // has ever been created for it (e.g. the AddGameInstallations migration's add-on
        // backfill hasn't covered it, or ran before this fix). SyncLegacyMirrorsAsync gets
        // triggered by something unrelated (any installation change for this game), and must not
        // wipe out that legacy installed state just because it has no explicit association yet.
        await using var context = InMemoryDatabaseFactory.Create();
        var service = ServiceTestFactory.CreateGameInstallationService(context);

        var baseGame = new Game { Id = Guid.NewGuid(), Title = "Half-Life" };
        var addon = new Game
        {
            Id = Guid.NewGuid(),
            Title = "Opposing Force",
            BaseGameId = baseGame.Id,
            // Legacy pre-migration state: installed directly via the single-install fields,
            // never through GameInstallationAddon.
            Installed = true,
            InstallDirectory = @"C:\Games\HalfLife",
            InstalledVersion = "1.0",
        };
        context.Games!.AddRange(baseGame, addon);
        await context.SaveChangesAsync();

        var installation = MakeInstallation(baseGame.Id, @"C:\Games\HalfLife", Guid.NewGuid(), "1.0.0");
        await service.AddInstallationAsync(installation);

        // No GameInstallationAddon row exists anywhere for this game — the defensive guard must
        // kick in here rather than treating "no association" as "explicitly uninstalled".
        await service.SyncLegacyMirrorsAsync(baseGame.Id);

        var reloadedAddon = await context.Games!.FindAsync(addon.Id);
        reloadedAddon!.Installed.ShouldBeTrue("legacy installed state must survive when no association has ever been recorded");
        reloadedAddon.InstallDirectory.ShouldBe(@"C:\Games\HalfLife");
        reloadedAddon.InstalledVersion.ShouldBe("1.0");
    }

    [Fact]
    public async Task SyncLegacyMirrorsAsync_clearing_is_scoped_per_addon_not_gamewide()
    {
        // Contrast with the legacy/unmigrated case above: an addon's *own* association history
        // is what matters, not whether the game has any tracking activity at all. Establishing
        // tracking for a different addon must not cause an unrelated, never-tracked legacy addon
        // to be treated as "explicitly not installed" — but once that *same* addon has an
        // association recorded (even for a sibling installation), its absence on the selected
        // installation becomes authoritative, exactly like the existing
        // SyncLegacyMirrorsAsync_mirrors_addon_state_only_from_the_selected_installation case.
        await using var context = InMemoryDatabaseFactory.Create();
        var service = ServiceTestFactory.CreateGameInstallationService(context);

        var baseGame = new Game { Id = Guid.NewGuid(), Title = "Half-Life" };
        var trackedAddon = new Game { Id = Guid.NewGuid(), Title = "Opposing Force", BaseGameId = baseGame.Id };
        var untrackedLegacyAddon = new Game
        {
            Id = Guid.NewGuid(),
            Title = "Blue Shift",
            BaseGameId = baseGame.Id,
            Installed = true,
            InstallDirectory = @"C:\Games\HalfLife",
            InstalledVersion = "1.0",
        };
        context.Games!.AddRange(baseGame, trackedAddon, untrackedLegacyAddon);
        await context.SaveChangesAsync();

        var installationA = MakeInstallation(baseGame.Id, @"C:\Games\HalfLife", Guid.NewGuid(), "1.0.0");
        var installationB = MakeInstallation(baseGame.Id, @"C:\Games\HalfLife (1.1.0)", Guid.NewGuid(), "1.1.0");
        await service.AddInstallationAsync(installationA);
        await service.AddInstallationAsync(installationB, select: true);

        // trackedAddon has an association recorded against installation A only — establishing
        // that per-installation tracking is active for *that addon*, even though B (the selected
        // installation) has no row for it. untrackedLegacyAddon never gets any row at all.
        await service.SetAddonInstalledAsync(installationA.Id, trackedAddon.Id, "1.0");

        await service.SyncLegacyMirrorsAsync(baseGame.Id);

        var reloadedTracked = await context.Games!.FindAsync(trackedAddon.Id);
        reloadedTracked!.Installed.ShouldBeFalse("tracked elsewhere but absent on the selected installation — absence here is authoritative");

        var reloadedUntracked = await context.Games!.FindAsync(untrackedLegacyAddon.Id);
        reloadedUntracked!.Installed.ShouldBeTrue("never tracked anywhere for this game — its legacy installed state must be preserved");
        reloadedUntracked.InstallDirectory.ShouldBe(@"C:\Games\HalfLife");
    }
}
