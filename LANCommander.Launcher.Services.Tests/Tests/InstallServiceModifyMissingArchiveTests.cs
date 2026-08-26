using System.Net;
using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using LANCommander.Launcher.Services.Tests.Helpers;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Services;
using Shouldly;
using Xunit;
using ManifestGame = LANCommander.SDK.Models.Manifest.Game;
using SdkGame = LANCommander.SDK.Models.Game;

namespace LANCommander.Launcher.Services.Tests.Tests;

/// <summary>
/// Execution-level coverage for modifying an installation whose pinned archive was deleted
/// server-side. Two findings meet here:
///
/// MEDIUM "modify aborts before applying anything": the manifest refresh is a server round-trip
/// that 404s once an administrator deletes the pinned archive, and it used to abort the whole
/// modify before the add-on/tool changes the user actually asked for were applied. The safe
/// semantics asserted here: keep the existing on-disk manifest exactly as-is (never rewrite it to
/// the game's current effective default, which would silently re-identify what is installed) and
/// keep going.
///
/// MEDIUM "modify after pinned archive deletion leaves disk and database inconsistent": removing an
/// installed add-on deletes files it owns and then has to restore the base-game files it had
/// overwritten — and the only safe source for those is the exact archive the installation is pinned
/// to. With that archive gone, <c>RestoreFilesAsync</c> failed *after* the uninstall had already
/// mutated the directory, with nothing left to repair it from. <see cref="InstallService.Modify"/>
/// now preflights that before any disk mutation and refuses outright, while still allowing every
/// change that needs no base-file restoration (tool-only changes, adding an add-on, removing an
/// add-on that was never installed).
///
/// These are execution-level tests, not pure routing tests: they run the real
/// <see cref="InstallService.Modify"/> against a real <see cref="GameClient"/> whose HTTP stack
/// genuinely returns the failure, and assert on the resulting queue state, database state, and the
/// bytes left on disk.
/// </summary>
public class InstallServiceModifyMissingArchiveTests : IDisposable
{
    private readonly string _installDirectory;

    public InstallServiceModifyMissingArchiveTests()
    {
        _installDirectory = Path.Combine(Path.GetTempPath(), $"lc-modify-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_installDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_installDirectory))
            Directory.Delete(_installDirectory, true);
    }

    private const string InstalledVersion = "1.5.0";
    private const string BaseEntry = "hl.exe";
    private const string BaseEntryOriginalContent = "base game executable";
    private const string AddonEntry = "opfor/pak0.pak";

    private sealed record Fixture(
        InstallService InstallService,
        GameInstallationService InstallationService,
        ToolService ToolService,
        Data.DatabaseContext Context,
        RecordingHttpMessageHandler Handler);

    private Fixture CreateFixture()
    {
        var handler = new RecordingHttpMessageHandler();
        var context = InMemoryDatabaseFactory.Create();
        var gameClient = FakeApiFactory.CreateGameClient(handler, _installDirectory);
        var toolClient = ServiceTestFactory.CreateToolClient();
        var installationService = ServiceTestFactory.CreateGameInstallationService(context);
        var toolService = ServiceTestFactory.CreateToolService(context);
        var gameService = ServiceTestFactory.CreateGameService(context, toolService, installationService, gameClient, toolClient);
        var installService = ServiceTestFactory.CreateInstallService(gameService, toolService, installationService, gameClient, toolClient);

        return new Fixture(installService, installationService, toolService, context, handler);
    }

    private async Task<string> WritePinnedManifestOnDiskAsync(Guid gameId, string version, Guid? addonId = null)
    {
        var manifest = new ManifestGame
        {
            Id = gameId,
            Title = "Half-Life",
            Type = GameType.MainGame,
            Version = version,
        };

        if (addonId.HasValue)
            manifest.Addons.Add(new ManifestGame { Id = addonId.Value, Title = "Opposing Force", Type = GameType.Expansion, Version = "1.0.0" });

        await ManifestHelper.WriteAsync(manifest, _installDirectory);

        return await File.ReadAllTextAsync(ManifestHelper.GetPath(_installDirectory, gameId));
    }

    /// <summary>
    /// Makes an add-on genuinely installed on disk the way a real install leaves it: its own
    /// manifest, its own FileList.txt, and real files — including one that overwrote a base-game
    /// file, which is precisely what has to be restored from the base game's pinned archive once
    /// the add-on is removed.
    /// </summary>
    private async Task InstallAddonOnDiskAsync(Guid addonId)
    {
        await ManifestHelper.WriteAsync(new ManifestGame
        {
            Id = addonId,
            Title = "Opposing Force",
            Type = GameType.Expansion,
            Version = "1.0.0",
        }, _installDirectory);

        Directory.CreateDirectory(Path.Combine(_installDirectory, "opfor"));

        await File.WriteAllTextAsync(Path.Combine(_installDirectory, AddonEntry), "addon data");
        await File.WriteAllTextAsync(Path.Combine(_installDirectory, BaseEntry), "overwritten by the addon");

        var fileListPath = GameClient.GetMetadataFilePath(_installDirectory, addonId, "FileList.txt");

        Directory.CreateDirectory(Path.GetDirectoryName(fileListPath)!);

        await File.WriteAllLinesAsync(fileListPath,
        [
            $"{AddonEntry} | 1111",
            $"{BaseEntry} | 2222",
        ]);
    }

    private bool AddonIsOnDisk(Guid addonId) =>
        ManifestHelper.Exists(_installDirectory, addonId)
        && File.Exists(Path.Combine(_installDirectory, AddonEntry));

    private async Task<(Game LocalGame, SdkGame RemoteGame, GameInstallation Installation)> SeedPinnedInstallationAsync(
        Fixture fixture,
        Guid deletedArchiveId,
        Guid[]? dependentGames = null)
    {
        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life", Type = GameType.MainGame };

        fixture.Context.Games!.Add(game);
        await fixture.Context.SaveChangesAsync();

        var installation = new GameInstallation
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            InstallDirectory = _installDirectory,
            ArchiveId = deletedArchiveId,
            Version = InstalledVersion,
            InstalledOn = DateTime.UtcNow,
        };

        await fixture.InstallationService.AddInstallationAsync(installation);

        var remoteGame = new SdkGame
        {
            Id = game.Id,
            Title = "Half-Life",
            Type = GameType.MainGame,
            BaseGameId = Guid.Empty,
            DependentGames = dependentGames ?? [],
            Redistributables = [],
        };

        // The pinned archive is gone: the server answers the archive-scoped manifest request with
        // 404 (game exists, that archive does not) exactly as it does after an archive deletion.
        fixture.Handler.MapStatus(FakeApiFactory.ManifestRoute(game.Id, deletedArchiveId), HttpStatusCode.NotFound);
        fixture.Handler.MapJson(FakeApiFactory.ScriptsRoute(game.Id), Array.Empty<object>());
        fixture.Handler.MapJson(FakeApiFactory.GameRoute(game.Id), remoteGame);

        // ...and so is its contents listing, which is the only thing that could say which base-game
        // files an add-on removal has to put back.
        fixture.Handler.MapStatus(FakeApiFactory.ArchiveContentsRoute(game.Id, InstalledVersion), HttpStatusCode.NotFound);

        return (game, remoteGame, installation);
    }

    private static InstallQueueGame MakeModifyItem(Guid entityId, string installDirectory, Guid[]? addonIds = null, Guid[]? toolIds = null) =>
        new(new SdkGame { Id = entityId, Title = "Half-Life" })
        {
            InstallDirectory = installDirectory,
            AddonIds = addonIds!,
            ToolIds = toolIds!,
        };

    // ── Manifest preservation (no add-on removal involved) ───────────────────────

    [Fact]
    public async Task Modify_WhenThePinnedArchiveWasDeleted_CompletesInsteadOfFailing()
    {
        var fixture = CreateFixture();
        var deletedArchiveId = Guid.NewGuid();
        var (localGame, remoteGame, installation) = await SeedPinnedInstallationAsync(fixture, deletedArchiveId);
        await WritePinnedManifestOnDiskAsync(localGame.Id, InstalledVersion);

        var queueItem = MakeModifyItem(localGame.Id, _installDirectory);

        await fixture.InstallService.Modify(queueItem, localGame, remoteGame, installation);

        queueItem.Status.ShouldBe(InstallStatus.Complete);
    }

    [Fact]
    public async Task Modify_WhenThePinnedArchiveWasDeleted_PreservesTheExistingOnDiskManifestVerbatim()
    {
        // The manifest on disk is the only remaining record of what is actually installed once the
        // archive is gone — it must be neither rewritten to the game's current default nor
        // otherwise re-identified.
        var fixture = CreateFixture();
        var deletedArchiveId = Guid.NewGuid();
        var (localGame, remoteGame, installation) = await SeedPinnedInstallationAsync(fixture, deletedArchiveId);
        var originalManifestYaml = await WritePinnedManifestOnDiskAsync(localGame.Id, InstalledVersion);

        var queueItem = MakeModifyItem(localGame.Id, _installDirectory);

        await fixture.InstallService.Modify(queueItem, localGame, remoteGame, installation);

        var manifestOnDisk = await File.ReadAllTextAsync(ManifestHelper.GetPath(_installDirectory, localGame.Id));
        manifestOnDisk.ShouldBe(originalManifestYaml);

        var reloadedManifest = await ManifestHelper.ReadAsync<ManifestGame>(_installDirectory, localGame.Id);
        reloadedManifest!.Version.ShouldBe(InstalledVersion);
    }

    [Fact]
    public async Task Modify_WhenThePinnedArchiveWasDeleted_NeverFallsBackToTheEffectiveDefaultManifest()
    {
        // Requesting the un-scoped manifest endpoint would hand back the game's *current* default
        // and silently repoint this installation at a version it does not have on disk.
        var fixture = CreateFixture();
        var deletedArchiveId = Guid.NewGuid();
        var (localGame, remoteGame, installation) = await SeedPinnedInstallationAsync(fixture, deletedArchiveId);
        await WritePinnedManifestOnDiskAsync(localGame.Id, InstalledVersion);

        var queueItem = MakeModifyItem(localGame.Id, _installDirectory);

        await fixture.InstallService.Modify(queueItem, localGame, remoteGame, installation);

        fixture.Handler.Requests.ShouldContain(FakeApiFactory.ManifestRoute(localGame.Id, deletedArchiveId));
        fixture.Handler.Requests.ShouldNotContain(FakeApiFactory.ManifestRoute(localGame.Id));
    }

    [Fact]
    public async Task Modify_WhenThePinnedArchiveWasDeleted_LeavesTheInstallationsPinnedIdentityUntouched()
    {
        var fixture = CreateFixture();
        var deletedArchiveId = Guid.NewGuid();
        var (localGame, remoteGame, installation) = await SeedPinnedInstallationAsync(fixture, deletedArchiveId);
        await WritePinnedManifestOnDiskAsync(localGame.Id, InstalledVersion);

        var queueItem = MakeModifyItem(localGame.Id, _installDirectory);

        await fixture.InstallService.Modify(queueItem, localGame, remoteGame, installation);

        var reloaded = await fixture.InstallationService.GetAsync(installation.Id);

        reloaded!.ArchiveId.ShouldBe(deletedArchiveId);
        reloaded.Version.ShouldBe(InstalledVersion);
    }

    [Fact]
    public async Task Modify_WhenTheManifestRequestFailsForAnUnrelatedReason_StillFailsTheItem()
    {
        // The skip is deliberately narrow: only "that exact archive is gone" (404/400) is tolerated.
        // A server error, auth failure, or anything else must not be quietly swallowed.
        var fixture = CreateFixture();
        var deletedArchiveId = Guid.NewGuid();
        var (localGame, remoteGame, installation) = await SeedPinnedInstallationAsync(fixture, deletedArchiveId);
        await WritePinnedManifestOnDiskAsync(localGame.Id, InstalledVersion);

        fixture.Handler.Map(FakeApiFactory.ManifestRoute(localGame.Id, deletedArchiveId),
            _ => new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent(string.Empty) });

        var queueItem = MakeModifyItem(localGame.Id, _installDirectory);

        await fixture.InstallService.Modify(queueItem, localGame, remoteGame, installation);

        queueItem.Status.ShouldBe(InstallStatus.Failed);
    }

    [Fact]
    public async Task Modify_WithAnAvailableArchive_StillRefreshesTheManifestFromTheServer()
    {
        // Contrast case: the normal path must be entirely unaffected — the archive-scoped manifest
        // is fetched and written over the on-disk one.
        var fixture = CreateFixture();
        var archiveId = Guid.NewGuid();
        var (localGame, remoteGame, installation) = await SeedPinnedInstallationAsync(fixture, archiveId);
        await WritePinnedManifestOnDiskAsync(localGame.Id, InstalledVersion);

        fixture.Handler.MapJson(FakeApiFactory.ManifestRoute(localGame.Id, archiveId), new ManifestGame
        {
            Id = localGame.Id,
            Title = "Half-Life",
            Type = GameType.MainGame,
            Version = "1.6.0",
        });

        var queueItem = MakeModifyItem(localGame.Id, _installDirectory);

        await fixture.InstallService.Modify(queueItem, localGame, remoteGame, installation);

        queueItem.Status.ShouldBe(InstallStatus.Complete);

        var reloadedManifest = await ManifestHelper.ReadAsync<ManifestGame>(_installDirectory, localGame.Id);
        reloadedManifest!.Version.ShouldBe("1.6.0");
    }

    // ── Removing a genuinely installed add-on with the pinned archive gone ───────

    [Fact]
    public async Task Modify_RemovingAnInstalledAddon_WhenThePinnedArchiveIsGone_FailsTheItem()
    {
        var fixture = CreateFixture();
        var deletedArchiveId = Guid.NewGuid();
        var addonId = Guid.NewGuid();
        var (localGame, remoteGame, installation) = await SeedPinnedInstallationAsync(fixture, deletedArchiveId, dependentGames: [addonId]);
        await WritePinnedManifestOnDiskAsync(localGame.Id, InstalledVersion, addonId);
        await InstallAddonOnDiskAsync(addonId);
        await fixture.InstallationService.SetAddonInstalledAsync(installation.Id, addonId, "1.0.0");

        // Explicit empty selection == "remove every add-on".
        var queueItem = MakeModifyItem(localGame.Id, _installDirectory, addonIds: []);

        await fixture.InstallService.Modify(queueItem, localGame, remoteGame, installation);

        queueItem.Status.ShouldBe(InstallStatus.Failed);
    }

    [Fact]
    public async Task Modify_RemovingAnInstalledAddon_WhenThePinnedArchiveIsGone_LeavesTheAddonFilesOnDisk()
    {
        // The heart of the finding: without this preflight the uninstall ran first, deleted the
        // add-on's files (including the base-game file it had overwritten), and only then failed on
        // the missing archive contents — with no remaining source to restore anything from.
        var fixture = CreateFixture();
        var deletedArchiveId = Guid.NewGuid();
        var addonId = Guid.NewGuid();
        var (localGame, remoteGame, installation) = await SeedPinnedInstallationAsync(fixture, deletedArchiveId, dependentGames: [addonId]);
        await WritePinnedManifestOnDiskAsync(localGame.Id, InstalledVersion, addonId);
        await InstallAddonOnDiskAsync(addonId);
        await fixture.InstallationService.SetAddonInstalledAsync(installation.Id, addonId, "1.0.0");

        var queueItem = MakeModifyItem(localGame.Id, _installDirectory, addonIds: []);

        await fixture.InstallService.Modify(queueItem, localGame, remoteGame, installation);

        AddonIsOnDisk(addonId).ShouldBeTrue();
        File.Exists(Path.Combine(_installDirectory, BaseEntry)).ShouldBeTrue();
    }

    [Fact]
    public async Task Modify_RemovingAnInstalledAddon_WhenThePinnedArchiveIsGone_LeavesTrackingUnchanged()
    {
        var fixture = CreateFixture();
        var deletedArchiveId = Guid.NewGuid();
        var addonId = Guid.NewGuid();
        var (localGame, remoteGame, installation) = await SeedPinnedInstallationAsync(fixture, deletedArchiveId, dependentGames: [addonId]);
        await WritePinnedManifestOnDiskAsync(localGame.Id, InstalledVersion, addonId);
        await InstallAddonOnDiskAsync(addonId);
        await fixture.InstallationService.SetAddonInstalledAsync(installation.Id, addonId, "1.0.0");

        var queueItem = MakeModifyItem(localGame.Id, _installDirectory, addonIds: []);

        await fixture.InstallService.Modify(queueItem, localGame, remoteGame, installation);

        (await fixture.InstallationService.IsAddonInstalledForInstallationAsync(installation.Id, addonId)).ShouldBeTrue();

        var reloaded = await fixture.InstallationService.GetAsync(installation.Id);
        reloaded!.ArchiveId.ShouldBe(deletedArchiveId);
        reloaded.Version.ShouldBe(InstalledVersion);
    }

    [Fact]
    public async Task Modify_RemovingAnInstalledAddon_WhenThePinnedArchiveIsGone_RefusesBeforeAnyMetadataIsRewritten()
    {
        // "Before disk mutation" includes the metadata refresh: the manifest on disk must be
        // exactly as it was, and no archive download may be attempted either.
        var fixture = CreateFixture();
        var deletedArchiveId = Guid.NewGuid();
        var addonId = Guid.NewGuid();
        var (localGame, remoteGame, installation) = await SeedPinnedInstallationAsync(fixture, deletedArchiveId, dependentGames: [addonId]);
        var originalManifestYaml = await WritePinnedManifestOnDiskAsync(localGame.Id, InstalledVersion, addonId);
        await InstallAddonOnDiskAsync(addonId);
        await fixture.InstallationService.SetAddonInstalledAsync(installation.Id, addonId, "1.0.0");

        var queueItem = MakeModifyItem(localGame.Id, _installDirectory, addonIds: []);

        await fixture.InstallService.Modify(queueItem, localGame, remoteGame, installation);

        (await File.ReadAllTextAsync(ManifestHelper.GetPath(_installDirectory, localGame.Id))).ShouldBe(originalManifestYaml);

        fixture.Handler.Requests.ShouldNotContain(FakeApiFactory.ManifestRoute(localGame.Id, deletedArchiveId));
        fixture.Handler.Requests.ShouldNotContain(FakeApiFactory.ManifestRoute(localGame.Id));
        fixture.Handler.Requests.ShouldNotContain(FakeApiFactory.DownloadRoute(localGame.Id));
        fixture.Handler.Requests.ShouldNotContain(FakeApiFactory.DownloadRoute(localGame.Id, deletedArchiveId));
    }

    [Fact]
    public async Task Modify_RemovingAnInstalledAddon_WhenTheContentsRequestFailsForAnUnrelatedReason_FailsWithoutMutating()
    {
        // Only 400/404 is classified as "that exact archive is gone". A server error must fail the
        // item too (never silently proceed into a removal it cannot repair), and must equally leave
        // disk and tracking untouched.
        var fixture = CreateFixture();
        var deletedArchiveId = Guid.NewGuid();
        var addonId = Guid.NewGuid();
        var (localGame, remoteGame, installation) = await SeedPinnedInstallationAsync(fixture, deletedArchiveId, dependentGames: [addonId]);
        await WritePinnedManifestOnDiskAsync(localGame.Id, InstalledVersion, addonId);
        await InstallAddonOnDiskAsync(addonId);
        await fixture.InstallationService.SetAddonInstalledAsync(installation.Id, addonId, "1.0.0");

        fixture.Handler.Map(FakeApiFactory.ArchiveContentsRoute(localGame.Id, InstalledVersion),
            _ => new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent(string.Empty) });

        var queueItem = MakeModifyItem(localGame.Id, _installDirectory, addonIds: []);

        await fixture.InstallService.Modify(queueItem, localGame, remoteGame, installation);

        queueItem.Status.ShouldBe(InstallStatus.Failed);
        AddonIsOnDisk(addonId).ShouldBeTrue();
        (await fixture.InstallationService.IsAddonInstalledForInstallationAsync(installation.Id, addonId)).ShouldBeTrue();
    }

    // ── Changes that need no base-file restoration are still allowed ─────────────

    [Fact]
    public async Task Modify_WhenThePinnedArchiveWasDeleted_StillAppliesToolChanges()
    {
        var fixture = CreateFixture();
        var deletedArchiveId = Guid.NewGuid();
        var toolId = Guid.NewGuid();
        var (localGame, remoteGame, installation) = await SeedPinnedInstallationAsync(fixture, deletedArchiveId);
        await WritePinnedManifestOnDiskAsync(localGame.Id, InstalledVersion);

        fixture.Context.Set<Tool>().Add(new Tool { Id = toolId, Name = "Mod Loader" });
        await fixture.Context.SaveChangesAsync();

        await fixture.ToolService.SetToolInstalledForInstallationAsync(installation.Id, localGame.Id, toolId, _installDirectory, "1.0.0");
        (await fixture.ToolService.IsToolInstalledForInstallationAsync(installation.Id, toolId)).ShouldBeTrue();

        // Explicit empty selection == "uninstall every tool". No add-on selection at all, so no
        // base-game file can be removed and nothing has to be restored.
        var queueItem = MakeModifyItem(localGame.Id, _installDirectory, toolIds: []);

        await fixture.InstallService.Modify(queueItem, localGame, remoteGame, installation);

        queueItem.Status.ShouldBe(InstallStatus.Complete);
        (await fixture.ToolService.IsToolInstalledForInstallationAsync(installation.Id, toolId)).ShouldBeFalse();
    }

    [Fact]
    public async Task Modify_RemovingAnAddonThatWasNeverInstalled_StillSucceedsWithThePinnedArchiveGone()
    {
        // ResolveAddonSelectionDiff lists every *available* add-on that isn't selected, including
        // ones that were never installed. Uninstalling those is a no-op, so it must not be refused.
        var fixture = CreateFixture();
        var deletedArchiveId = Guid.NewGuid();
        var addonId = Guid.NewGuid();
        var (localGame, remoteGame, installation) = await SeedPinnedInstallationAsync(fixture, deletedArchiveId, dependentGames: [addonId]);
        await WritePinnedManifestOnDiskAsync(localGame.Id, InstalledVersion, addonId);

        var queueItem = MakeModifyItem(localGame.Id, _installDirectory, addonIds: []);

        await fixture.InstallService.Modify(queueItem, localGame, remoteGame, installation);

        queueItem.Status.ShouldBe(InstallStatus.Complete);
    }

    [Fact]
    public async Task Modify_AddingAnAddon_StillSucceedsWithThePinnedArchiveGone()
    {
        // Adding an add-on installs from that add-on's *own* archive and removes no base files, so
        // it needs no restoration source and must not be blocked by the missing base archive.
        var fixture = CreateFixture();
        var deletedArchiveId = Guid.NewGuid();
        var addonId = Guid.NewGuid();
        var (localGame, remoteGame, installation) = await SeedPinnedInstallationAsync(fixture, deletedArchiveId, dependentGames: [addonId]);
        await WritePinnedManifestOnDiskAsync(localGame.Id, InstalledVersion);

        // A standalone expansion is not one of the overlay types InstallAddonsAsync downloads, so
        // this exercises the routing decision without dragging a full archive install in with it.
        fixture.Handler.MapJson(FakeApiFactory.GameRoute(addonId), new SdkGame
        {
            Id = addonId,
            Title = "Opposing Force",
            Type = GameType.StandaloneExpansion,
            BaseGameId = localGame.Id,
        });

        var queueItem = MakeModifyItem(localGame.Id, _installDirectory, addonIds: [addonId]);

        await fixture.InstallService.Modify(queueItem, localGame, remoteGame, installation);

        queueItem.Status.ShouldBe(InstallStatus.Complete);
        (await fixture.InstallationService.IsAddonInstalledForInstallationAsync(installation.Id, addonId)).ShouldBeTrue();
    }

    // ── Removal still works normally when the archive is available ───────────────

    [Fact]
    public async Task Modify_RemovingAnInstalledAddon_WhenTheArchiveIsAvailable_AppliesTheRemovalAndRestoresBaseFiles()
    {
        var fixture = CreateFixture();
        var archiveId = Guid.NewGuid();
        var addonId = Guid.NewGuid();
        var (localGame, remoteGame, installation) = await SeedPinnedInstallationAsync(fixture, archiveId, dependentGames: [addonId]);
        await WritePinnedManifestOnDiskAsync(localGame.Id, InstalledVersion, addonId);
        await InstallAddonOnDiskAsync(addonId);
        await fixture.InstallationService.SetAddonInstalledAsync(installation.Id, addonId, "1.0.0");

        fixture.Handler.MapJson(FakeApiFactory.ManifestRoute(localGame.Id, archiveId), new ManifestGame
        {
            Id = localGame.Id,
            Title = "Half-Life",
            Type = GameType.MainGame,
            Version = InstalledVersion,
            Addons = [new ManifestGame { Id = addonId, Title = "Opposing Force", Type = GameType.Expansion, Version = "1.0.0" }],
        });

        // The pinned archive still lists (and can still serve) the base file the add-on overwrote.
        fixture.Handler.MapJson(FakeApiFactory.ArchiveContentsRoute(localGame.Id, InstalledVersion), new[]
        {
            new ArchiveEntry { FullName = BaseEntry, Name = BaseEntry, Crc32 = 1234u, Length = BaseEntryOriginalContent.Length },
        });

        fixture.Handler.Map(FakeApiFactory.DownloadRoute(localGame.Id, archiveId),
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(FakeApiFactory.CreateZip((BaseEntry, BaseEntryOriginalContent))),
            });

        var queueItem = MakeModifyItem(localGame.Id, _installDirectory, addonIds: []);

        await fixture.InstallService.Modify(queueItem, localGame, remoteGame, installation);

        queueItem.Status.ShouldBe(InstallStatus.Complete);

        // The add-on is gone, and the base file it had overwritten came back from the pinned
        // archive (never from the game's current effective default).
        AddonIsOnDisk(addonId).ShouldBeFalse();
        (await fixture.InstallationService.IsAddonInstalledForInstallationAsync(installation.Id, addonId)).ShouldBeFalse();

        (await File.ReadAllTextAsync(Path.Combine(_installDirectory, BaseEntry))).ShouldBe(BaseEntryOriginalContent);

        fixture.Handler.Requests.ShouldContain(FakeApiFactory.DownloadRoute(localGame.Id, archiveId));
        fixture.Handler.Requests.ShouldNotContain(FakeApiFactory.DownloadRoute(localGame.Id));
    }
}
