using LANCommander.Launcher.Data.Models;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using Shouldly;
using Xunit;
using ManifestGame = LANCommander.SDK.Models.Manifest.Game;

namespace LANCommander.Launcher.Services.Tests.Tests;

/// <summary>
/// Covers the small pure decision helpers <see cref="InstallService"/> extracted out of
/// <see cref="InstallService.Add"/>/<see cref="InstallService.Modify"/> so they can be exercised
/// directly, without any network dependency:
///
/// - <see cref="InstallService.ResolveExactDestination"/> (HIGH): a side-by-side installation's
///   own directory is very often a collision-safe sibling like "Title (version)", not the natural
///   "&lt;parent&gt;/&lt;Title&gt;" path — Add() must recognize the caller supplying that same
///   parent (or no directory at all) as "no relocation intended" and preserve the exact directory,
///   only treating a genuinely different parent as a move.
/// - <see cref="InstallService.IsExplicitArchiveChange"/> (HIGH): a migrated installation's
///   ArchiveId can be null (unknown archive identity) — that must never look "different" from
///   whatever archive got resolved by default; only an explicitly-requested archive id that
///   actually differs counts as an update.
/// - <see cref="InstallService.ResolveAddonSelectionDiff"/> / <see cref="InstallService.ResolveToolsToUninstall"/>
///   (HIGH): Modify() must distinguish a null selection ("not supplied", preserve everything) from
///   an explicit empty array ("none selected", remove everything).
/// - <see cref="InstallService.RequiresResolvableArchive"/> (MEDIUM): only a fresh install or an
///   explicit version request needs a resolvable archive target; a modify/move of an existing
///   pinned installation must not be aborted just because its pinned archive was deleted
///   server-side.
/// - <see cref="InstallService.ResolveDestinationOwnership"/> (CRITICAL): only a fresh/side-by-side
///   install owns its destination directory. Every other branch of Add() resolves to a directory
///   that already holds an installation, which a canceled or failed download must never delete.
/// - <see cref="InstallService.ResolveRemovableInstalledAddons"/> (MEDIUM): only add-ons genuinely
///   installed on disk can delete/overwrite base game files, so only they make a modify require
///   base-file restoration.
/// </summary>
public class InstallServiceRoutingTests
{
    private static GameInstallation MakeInstallation(string installDirectory, Guid? archiveId = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            GameId = Guid.NewGuid(),
            InstallDirectory = installDirectory,
            ArchiveId = archiveId,
            InstalledOn = DateTime.UtcNow,
        };

    // ── ResolveExactDestination ────────────────────────────────────────────────

    [Fact]
    public void ResolveExactDestination_NoTargetInstallation_ReturnsNaturalDestination()
    {
        var result = InstallService.ResolveExactDestination(
            installDirectory: @"D:\Games",
            naturalDestination: @"D:\Games\Half-Life",
            targetInstallation: null);

        result.ShouldBe(@"D:\Games\Half-Life");
    }

    [Fact]
    public void ResolveExactDestination_BlankInstallDirectory_PreservesTheInstallationsExactDirectory()
    {
        // Legacy/back-compat callers (CLI, no directory argument at all) — must never move the
        // installation just because a natural destination was computed from an empty hint.
        var installation = MakeInstallation(@"D:\Games\Half-Life (1.1.0)");

        var result = InstallService.ResolveExactDestination(
            installDirectory: "",
            naturalDestination: @"D:\Games\Half-Life",
            targetInstallation: installation);

        result.ShouldBe(installation.InstallDirectory);
    }

    [Fact]
    public void ResolveExactDestination_NaturalDestinationMatchesInstallation_ReturnsItVerbatim()
    {
        // The common single-install case: no version suffix needed at all.
        var installation = MakeInstallation(@"D:\Games\Half-Life");

        var result = InstallService.ResolveExactDestination(
            installDirectory: @"D:\Games",
            naturalDestination: @"D:\Games\Half-Life",
            targetInstallation: installation);

        result.ShouldBe(@"D:\Games\Half-Life");
    }

    [Fact]
    public void ResolveExactDestination_SameParentAsASiblingInstallation_PreservesItsExactPath()
    {
        // Regression test for the HIGH "Modify side-by-side install misclassified as Move" bug:
        // a sibling installation's own directory ("Half-Life (1.1.0)") differs from the natural
        // "<parent>/Half-Life" path purely because another installation already occupies the
        // natural path. The caller (e.g. the Modify dialog, which derives its directory argument
        // from Path.GetDirectoryName(installation.InstallDirectory)) supplying that *same* parent
        // must not be treated as "move it to the natural path" — there's no relocation intent.
        var installation = MakeInstallation(@"D:\Games\Half-Life (1.1.0)");

        var result = InstallService.ResolveExactDestination(
            installDirectory: @"D:\Games",
            naturalDestination: @"D:\Games\Half-Life",
            targetInstallation: installation);

        result.ShouldBe(@"D:\Games\Half-Life (1.1.0)");
    }

    [Fact]
    public void ResolveExactDestination_SameParent_IsCaseInsensitiveAndTrailingSlashInsensitive()
    {
        var installation = MakeInstallation(@"D:\Games\Half-Life (1.1.0)");

        var result = InstallService.ResolveExactDestination(
            installDirectory: @"d:\GAMES\",
            naturalDestination: @"d:\GAMES\Half-Life",
            targetInstallation: installation);

        result.ShouldBe(@"D:\Games\Half-Life (1.1.0)");
    }

    [Fact]
    public void ResolveExactDestination_GenuinelyDifferentParent_TreatsItAsAMoveDestination()
    {
        // Contrast case: the new guard must not be overly broad — a real relocation request
        // (a parent directory that is neither blank, nor the natural match, nor the
        // installation's own current parent) must still resolve to the natural destination so
        // Next() correctly classifies this as a Move.
        var installation = MakeInstallation(@"D:\OldGames\Half-Life (1.1.0)");

        var result = InstallService.ResolveExactDestination(
            installDirectory: @"D:\NewGames",
            naturalDestination: @"D:\NewGames\Half-Life",
            targetInstallation: installation);

        result.ShouldBe(@"D:\NewGames\Half-Life");
    }

    // ── IsExplicitArchiveChange ─────────────────────────────────────────────────

    [Fact]
    public void IsExplicitArchiveChange_NoTargetInstallation_IsFalse()
    {
        InstallService.IsExplicitArchiveChange(Guid.NewGuid(), targetInstallation: null).ShouldBeFalse();
    }

    [Fact]
    public void IsExplicitArchiveChange_NoArchiveRequested_IsFalse_EvenWithAKnownInstalledArchive()
    {
        var installation = MakeInstallation(@"D:\Games\Half-Life", archiveId: Guid.NewGuid());

        InstallService.IsExplicitArchiveChange(requestedArchiveId: null, installation).ShouldBeFalse();
    }

    [Fact]
    public void IsExplicitArchiveChange_NoArchiveRequested_IsFalse_ForAMigratedInstallationWithUnknownArchive()
    {
        // Regression test for the HIGH "migrated installation ArchiveId null gets classified as
        // update" bug: a migrated installation's ArchiveId is null (unknown), and Add() resolves
        // some default archive even when the caller never asked for a version change at all. That
        // must never count as an explicit request.
        var migratedInstallation = MakeInstallation(@"D:\Games\Half-Life", archiveId: null);

        InstallService.IsExplicitArchiveChange(requestedArchiveId: null, migratedInstallation).ShouldBeFalse();
    }

    [Fact]
    public void IsExplicitArchiveChange_ExplicitArchiveDifferentFromUnknownInstalledArchive_IsTrue()
    {
        // Contrast case: an explicit request must still work even against a migrated/unknown
        // installation — this is what ChangeVersionAsync/UpdateGameAsync use.
        var migratedInstallation = MakeInstallation(@"D:\Games\Half-Life", archiveId: null);

        InstallService.IsExplicitArchiveChange(Guid.NewGuid(), migratedInstallation).ShouldBeTrue();
    }

    [Fact]
    public void IsExplicitArchiveChange_ExplicitArchiveMatchesTheInstalledArchive_IsFalse()
    {
        var archiveId = Guid.NewGuid();
        var installation = MakeInstallation(@"D:\Games\Half-Life", archiveId);

        InstallService.IsExplicitArchiveChange(archiveId, installation).ShouldBeFalse();
    }

    [Fact]
    public void IsExplicitArchiveChange_ExplicitArchiveDiffersFromTheInstalledArchive_IsTrue()
    {
        var installation = MakeInstallation(@"D:\Games\Half-Life", archiveId: Guid.NewGuid());

        InstallService.IsExplicitArchiveChange(Guid.NewGuid(), installation).ShouldBeTrue();
    }

    // ── RequiresResolvableArchive ────────────────────────────────────────────────

    [Fact]
    public void RequiresResolvableArchive_FreshInstall_IsTrue()
    {
        // No existing installation to modify/move — this request has to actually download
        // something, so its archive target must resolve.
        InstallService.RequiresResolvableArchive(requestedArchiveId: null, targetInstallation: null).ShouldBeTrue();
        InstallService.RequiresResolvableArchive(Guid.NewGuid(), targetInstallation: null).ShouldBeTrue();
    }

    [Fact]
    public void RequiresResolvableArchive_ExplicitArchiveRequestAgainstAnInstallation_IsTrue()
    {
        // An explicit version choice must fail loudly if that version no longer exists rather
        // than quietly installing something else.
        var installation = MakeInstallation(@"D:\Games\Half-Life", archiveId: Guid.NewGuid());

        InstallService.RequiresResolvableArchive(Guid.NewGuid(), installation).ShouldBeTrue();
    }

    [Fact]
    public void RequiresResolvableArchive_ModifyOrMoveOfAPinnedInstallation_IsFalse()
    {
        // Regression guard for the MEDIUM "archive resolve failures abort install/modify/move"
        // finding: a modify (addon/tool selection) or move carries the installation's own pinned
        // archive along purely as metadata and never re-downloads it, so it must not require that
        // archive to still exist server-side.
        var pinned = MakeInstallation(@"D:\Games\Half-Life (1.1.0)", archiveId: Guid.NewGuid());

        InstallService.RequiresResolvableArchive(requestedArchiveId: null, pinned).ShouldBeFalse();
    }

    [Fact]
    public void RequiresResolvableArchive_ModifyOfAMigratedInstallationWithUnknownArchive_IsFalse()
    {
        var migrated = MakeInstallation(@"D:\Games\Half-Life", archiveId: null);

        InstallService.RequiresResolvableArchive(requestedArchiveId: null, migrated).ShouldBeFalse();
    }

    // ── ResolveDestinationOwnership ──────────────────────────────────────────────

    [Fact]
    public void ResolveDestinationOwnership_FreshSideBySideInstall_OwnsItsDestination()
    {
        // The only branch of Add() that generates a directory specifically for this install — it
        // may clean itself up after a canceled/failed download.
        InstallService.ResolveDestinationOwnership(
            targetInstallation: null,
            exactInstallDirectory: false,
            isOverlayInstallType: false)
            .ShouldBe(InstallDestinationOwnership.Fresh);
    }

    [Fact]
    public void ResolveDestinationOwnership_InPlaceUpdateOrModify_NeverOwnsTheDestination()
    {
        // Regression guard for the CRITICAL "a canceled/failed download deletes the whole existing
        // installation" finding: this destination is an installation that already exists on disk.
        var installation = MakeInstallation(@"D:\Games\Half-Life (1.1.0)", archiveId: Guid.NewGuid());

        InstallService.ResolveDestinationOwnership(
            installation,
            exactInstallDirectory: false,
            isOverlayInstallType: false)
            .ShouldBe(InstallDestinationOwnership.ExistingInstallation);
    }

    [Fact]
    public void ResolveDestinationOwnership_LegacyExactDirectoryUpdate_NeverOwnsTheDestination()
    {
        // A legacy pre-migration install has no GameInstallation row, so the caller supplies its
        // existing folder verbatim. That folder is still full of the user's installed game.
        InstallService.ResolveDestinationOwnership(
            targetInstallation: null,
            exactInstallDirectory: true,
            isOverlayInstallType: false)
            .ShouldBe(InstallDestinationOwnership.ExistingInstallation);
    }

    [Fact]
    public void ResolveDestinationOwnership_OverlayAddonSharingItsBaseGamesDirectory_NeverOwnsTheDestination()
    {
        // Expansions/mods deliberately extract into the base game's directory — deleting it on a
        // failed add-on download would take the base game and every sibling add-on with it.
        InstallService.ResolveDestinationOwnership(
            targetInstallation: null,
            exactInstallDirectory: false,
            isOverlayInstallType: true)
            .ShouldBe(InstallDestinationOwnership.ExistingInstallation);
    }

    // ── ResolveRemovableInstalledAddons ──────────────────────────────────────────

    [Fact]
    public void ResolveRemovableInstalledAddons_OnlyCountsAddonsWithAManifestOnDisk()
    {
        // ResolveAddonSelectionDiff lists every *available* addon that isn't selected, including
        // ones that were never installed. Only genuinely installed ones delete files (and therefore
        // need base-game files restored), so only they may make Modify() refuse.
        var installDirectory = Path.Combine(Path.GetTempPath(), $"lc-removable-addons-{Guid.NewGuid()}");
        Directory.CreateDirectory(installDirectory);

        try
        {
            var installedAddonId = Guid.NewGuid();
            var neverInstalledAddonId = Guid.NewGuid();

            ManifestHelper.Write(
                new ManifestGame { Id = installedAddonId, Title = "Opposing Force", Type = GameType.Expansion, Version = "1.0.0" },
                installDirectory);

            var removable = InstallService.ResolveRemovableInstalledAddons(
                installDirectory,
                new[] { installedAddonId, neverInstalledAddonId });

            removable.ShouldBe(new[] { installedAddonId });
        }
        finally
        {
            Directory.Delete(installDirectory, true);
        }
    }

    [Fact]
    public void ResolveRemovableInstalledAddons_NullOrBlankInputs_ReturnNothing()
    {
        InstallService.ResolveRemovableInstalledAddons(null, new[] { Guid.NewGuid() }).ShouldBeEmpty();
        InstallService.ResolveRemovableInstalledAddons("   ", new[] { Guid.NewGuid() }).ShouldBeEmpty();
        InstallService.ResolveRemovableInstalledAddons(Path.GetTempPath(), null).ShouldBeEmpty();
    }

    // ── ResolveAddonSelectionDiff ────────────────────────────────────────────────
    [Fact]
    public void ResolveAddonSelectionDiff_NullSelection_TouchesNothing()
    {
        var allAddons = new[] { Guid.NewGuid(), Guid.NewGuid() };

        var (remove, add) = InstallService.ResolveAddonSelectionDiff(allAddons, selectedAddonIds: null);

        remove.ShouldBeEmpty();
        add.ShouldBeEmpty();
    }

    [Fact]
    public void ResolveAddonSelectionDiff_ExplicitEmptySelection_RemovesEveryAddon()
    {
        var addonA = Guid.NewGuid();
        var addonB = Guid.NewGuid();

        var (remove, add) = InstallService.ResolveAddonSelectionDiff(new[] { addonA, addonB }, selectedAddonIds: Array.Empty<Guid>());

        remove.ShouldBe(new[] { addonA, addonB }, ignoreOrder: true);
        add.ShouldBeEmpty();
    }

    [Fact]
    public void ResolveAddonSelectionDiff_ExplicitSubset_ComputesTheCorrectAddRemoveSplit()
    {
        var keep = Guid.NewGuid();
        var remove1 = Guid.NewGuid();

        var (remove, add) = InstallService.ResolveAddonSelectionDiff(new[] { keep, remove1 }, selectedAddonIds: new[] { keep });

        remove.ShouldBe(new[] { remove1 });
        add.ShouldBe(new[] { keep });
    }

    // ── ResolveToolsToUninstall ──────────────────────────────────────────────────

    [Fact]
    public void ResolveToolsToUninstall_NullSelection_UninstallsNothing()
    {
        var installedTools = new[] { Guid.NewGuid(), Guid.NewGuid() };

        var toUninstall = InstallService.ResolveToolsToUninstall(installedTools, selectedToolIds: null);

        toUninstall.ShouldBeEmpty();
    }

    [Fact]
    public void ResolveToolsToUninstall_ExplicitEmptySelection_UninstallsEveryInstalledTool()
    {
        var toolA = Guid.NewGuid();
        var toolB = Guid.NewGuid();

        var toUninstall = InstallService.ResolveToolsToUninstall(new[] { toolA, toolB }, selectedToolIds: Array.Empty<Guid>());

        toUninstall.ShouldBe(new[] { toolA, toolB }, ignoreOrder: true);
    }

    [Fact]
    public void ResolveToolsToUninstall_ExplicitSubset_KeepsOnlySelectedTools()
    {
        var keep = Guid.NewGuid();
        var remove = Guid.NewGuid();

        var toUninstall = InstallService.ResolveToolsToUninstall(new[] { keep, remove }, selectedToolIds: new[] { keep });

        toUninstall.ShouldBe(new[] { remove });
    }

    // ── HasExecutableInstallTasks ────────────────────────────────────────────────

    [Fact]
    public void HasExecutableInstallTasks_NullTaskList_IsFalse()
    {
        InstallService.HasExecutableInstallTasks(null).ShouldBeFalse();
    }

    [Fact]
    public void HasExecutableInstallTasks_EmptyTaskList_IsFalse()
    {
        // Regression test for the CRITICAL "in-place ChangeVersionAsync builds a manual
        // InstallPlanItem with empty Tasks" bug: Update() must never treat an empty task list as
        // sufficient to persist a new ArchiveId/Version.
        InstallService.HasExecutableInstallTasks(new List<SDK.Models.InstallTaskDefinition>()).ShouldBeFalse();
    }

    [Fact]
    public void HasExecutableInstallTasks_MissingDownloadAndExtract_IsFalse()
    {
        var tasks = new List<SDK.Models.InstallTaskDefinition>
        {
            new() { Type = SDK.Enums.InstallTaskType.WriteManifest },
            new() { Type = SDK.Enums.InstallTaskType.WriteScripts },
        };

        InstallService.HasExecutableInstallTasks(tasks).ShouldBeFalse();
    }

    [Fact]
    public void HasExecutableInstallTasks_MissingWriteManifest_IsFalse()
    {
        var tasks = new List<SDK.Models.InstallTaskDefinition>
        {
            new() { Type = SDK.Enums.InstallTaskType.DownloadAndExtract },
            new() { Type = SDK.Enums.InstallTaskType.WriteScripts },
        };

        InstallService.HasExecutableInstallTasks(tasks).ShouldBeFalse();
    }

    [Fact]
    public void HasExecutableInstallTasks_HasBothCriticalTasks_IsTrue()
    {
        var tasks = new List<SDK.Models.InstallTaskDefinition>
        {
            new() { Type = SDK.Enums.InstallTaskType.VerifyFiles },
            new() { Type = SDK.Enums.InstallTaskType.DownloadAndExtract },
            new() { Type = SDK.Enums.InstallTaskType.WriteManifest },
            new() { Type = SDK.Enums.InstallTaskType.WriteScripts },
        };

        InstallService.HasExecutableInstallTasks(tasks).ShouldBeTrue();
    }

    [Fact]
    public void HasExecutableInstallTasks_MatchesWhatGameClientBuildGameInstallTasksProduces()
    {
        // Ties the guard directly to the exact task list ChangeVersionAsync(inPlace: true) now
        // builds via GameClient.BuildGameInstallTasks — proving the in-place plan really does
        // satisfy Update()'s own guard rather than the two just happening to agree by coincidence.
        var game = new SDK.Models.Game { Id = Guid.NewGuid(), Title = "Half-Life" };

        var tasks = SDK.Services.GameClient.BuildGameInstallTasks(game, Guid.NewGuid(), "1.0.0");

        InstallService.HasExecutableInstallTasks(tasks).ShouldBeTrue();
    }
}
