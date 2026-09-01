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
/// Execution-level coverage for the MEDIUM "add-on removal preflight only probes the base archive"
/// finding.
///
/// Removing an installed add-on deletes files it owns and then repairs what it had overwritten via
/// <c>RestoreFilesAsync</c> — which validates through <c>ValidateFilesAsync</c>, and that lists the
/// archive contents of the base game *plus every add-on manifest still on disk*. The preflight used
/// to probe only the base game's archive, so a second, surviving add-on whose archive an
/// administrator had deleted sailed straight through it: the removal mutated disk (and the database)
/// and only then did the restore throw on the missing listing, leaving the installation permanently
/// inconsistent with no source to repair it from.
///
/// The preflight now probes exactly the set the restore will query, minus the add-ons being removed
/// (whose manifests the uninstall deletes before the restore runs, so their archives are never
/// needed). These tests run the real <see cref="InstallService.Modify"/> against a real
/// <see cref="GameClient"/> over an HTTP stack that genuinely returns the failure, and assert on the
/// resulting queue state, database state, requested endpoints, and the bytes left on disk.
/// </summary>
public class InstallServiceAddonRemovalSurvivingArchiveTests : IDisposable
{
    private readonly string _installDirectory;

    public InstallServiceAddonRemovalSurvivingArchiveTests()
    {
        _installDirectory = Path.Combine(Path.GetTempPath(), $"lc-addon-survivor-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_installDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_installDirectory))
            Directory.Delete(_installDirectory, true);
    }

    private const string BaseVersion = "1.5.0";
    private const string BaseEntry = "hl.exe";
    private const string BaseEntryOriginalContent = "base game executable";

    private const string AddonAVersion = "1.0.0";
    private const string AddonAEntry = "opfor/pak0.pak";

    private const string AddonBVersion = "2.0.0";
    private const string AddonBEntry = "bshift/pak0.pak";

    private sealed record Fixture(
        InstallService InstallService,
        GameInstallationService InstallationService,
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

        return new Fixture(installService, installationService, context, handler);
    }

    private sealed record Scenario(
        Game LocalGame,
        SdkGame RemoteGame,
        GameInstallation Installation,
        Guid ArchiveId,
        Guid AddonA,
        Guid AddonB,
        string BaseManifestYaml);

    /// <summary>
    /// Seeds a pinned installation with two genuinely installed add-ons, laid out the way a real
    /// install leaves them: their own manifests, their own FileList.txt, real files — and, for the
    /// add-on that is about to be removed, a base-game file it overwrote, which is exactly what has
    /// to be restored from the base archive once it is gone.
    /// </summary>
    private async Task<Scenario> SeedInstallationWithTwoAddonsAsync(Fixture fixture)
    {
        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life", Type = GameType.MainGame };
        var addonA = Guid.NewGuid();
        var addonB = Guid.NewGuid();
        var archiveId = Guid.NewGuid();

        fixture.Context.Games!.Add(game);
        await fixture.Context.SaveChangesAsync();

        var installation = new GameInstallation
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            InstallDirectory = _installDirectory,
            ArchiveId = archiveId,
            Version = BaseVersion,
            InstalledOn = DateTime.UtcNow,
        };

        await fixture.InstallationService.AddInstallationAsync(installation);

        await ManifestHelper.WriteAsync(new ManifestGame
        {
            Id = game.Id,
            Title = "Half-Life",
            Type = GameType.MainGame,
            Version = BaseVersion,
            Addons =
            [
                new ManifestGame { Id = addonA, Title = "Opposing Force", Type = GameType.Expansion, Version = AddonAVersion },
                new ManifestGame { Id = addonB, Title = "Blue Shift", Type = GameType.Expansion, Version = AddonBVersion },
            ],
        }, _installDirectory);

        await InstallAddonOnDiskAsync(addonA, "Opposing Force", AddonAVersion, AddonAEntry, overwritesBaseEntry: true);
        await InstallAddonOnDiskAsync(addonB, "Blue Shift", AddonBVersion, AddonBEntry, overwritesBaseEntry: false);

        await fixture.InstallationService.SetAddonInstalledAsync(installation.Id, addonA, AddonAVersion);
        await fixture.InstallationService.SetAddonInstalledAsync(installation.Id, addonB, AddonBVersion);

        var remoteGame = new SdkGame
        {
            Id = game.Id,
            Title = "Half-Life",
            Type = GameType.MainGame,
            BaseGameId = Guid.Empty,
            DependentGames = [addonA, addonB],
            Redistributables = [],
        };

        fixture.Handler.MapJson(FakeApiFactory.GameRoute(game.Id), remoteGame);
        fixture.Handler.MapJson(FakeApiFactory.ScriptsRoute(game.Id), Array.Empty<object>());

        // Keeping an add-on selected re-runs the add-on install path for it. A standalone
        // expansion is not one of the overlay types InstallAddonsAsync downloads, so this keeps
        // the test focused on the removal without dragging a full archive install in with it.
        fixture.Handler.MapJson(FakeApiFactory.GameRoute(addonB), new SdkGame
        {
            Id = addonB,
            Title = "Blue Shift",
            Type = GameType.StandaloneExpansion,
            BaseGameId = game.Id,
        });

        // The base game's own pinned archive is perfectly healthy in every scenario here — the
        // whole point is that the *surviving add-on's* archive is what went missing.
        fixture.Handler.MapJson(FakeApiFactory.ManifestRoute(game.Id, archiveId), new ManifestGame
        {
            Id = game.Id,
            Title = "Half-Life",
            Type = GameType.MainGame,
            Version = BaseVersion,
            Addons =
            [
                new ManifestGame { Id = addonA, Title = "Opposing Force", Type = GameType.Expansion, Version = AddonAVersion },
                new ManifestGame { Id = addonB, Title = "Blue Shift", Type = GameType.Expansion, Version = AddonBVersion },
            ],
        });

        fixture.Handler.MapJson(FakeApiFactory.ArchiveContentsRoute(game.Id, BaseVersion), new[]
        {
            new ArchiveEntry { FullName = BaseEntry, Name = BaseEntry, Crc32 = 1234u, Length = BaseEntryOriginalContent.Length },
        });

        var baseManifestYaml = await File.ReadAllTextAsync(ManifestHelper.GetPath(_installDirectory, game.Id));

        return new Scenario(game, remoteGame, installation, archiveId, addonA, addonB, baseManifestYaml);
    }

    private async Task InstallAddonOnDiskAsync(Guid addonId, string title, string version, string entry, bool overwritesBaseEntry)
    {
        await ManifestHelper.WriteAsync(new ManifestGame
        {
            Id = addonId,
            Title = title,
            Type = GameType.Expansion,
            Version = version,
        }, _installDirectory);

        Directory.CreateDirectory(Path.Combine(_installDirectory, Path.GetDirectoryName(entry)!));

        await File.WriteAllTextAsync(Path.Combine(_installDirectory, entry), $"{title} data");

        var fileListEntries = new List<string> { $"{entry} | 1111" };

        if (overwritesBaseEntry)
        {
            await File.WriteAllTextAsync(Path.Combine(_installDirectory, BaseEntry), "overwritten by the addon");
            fileListEntries.Add($"{BaseEntry} | 2222");
        }

        var fileListPath = GameClient.GetMetadataFilePath(_installDirectory, addonId, "FileList.txt");

        Directory.CreateDirectory(Path.GetDirectoryName(fileListPath)!);

        await File.WriteAllLinesAsync(fileListPath, fileListEntries);
    }

    private bool AddonIsOnDisk(Guid addonId, string entry) =>
        ManifestHelper.Exists(_installDirectory, addonId)
        && File.Exists(Path.Combine(_installDirectory, entry));

    private static InstallQueueGame MakeModifyItem(Guid entityId, string installDirectory, Guid[] addonIds) =>
        new(new SdkGame { Id = entityId, Title = "Half-Life" })
        {
            InstallDirectory = installDirectory,
            AddonIds = addonIds,
        };

    /// <summary>Every download endpoint that could possibly be hit in these scenarios.</summary>
    private static void ShouldHaveDownloadedNothing(Scenario scenario, RecordingHttpMessageHandler handler)
    {
        handler.Requests.ShouldNotContain(FakeApiFactory.DownloadRoute(scenario.LocalGame.Id));
        handler.Requests.ShouldNotContain(FakeApiFactory.DownloadRoute(scenario.LocalGame.Id, scenario.ArchiveId));
        handler.Requests.ShouldNotContain(FakeApiFactory.DownloadRoute(scenario.AddonA));
        handler.Requests.ShouldNotContain(FakeApiFactory.DownloadRoute(scenario.AddonB));
    }

    // ── Surviving add-on's archive is gone ───────────────────────────────────────

    [Fact]
    public async Task Modify_RemovingOneAddon_WhenASurvivingAddonsArchiveIsGone_FailsTheItem()
    {
        var fixture = CreateFixture();
        var scenario = await SeedInstallationWithTwoAddonsAsync(fixture);

        // The add-on that stays behind has had its archive deleted server-side, so the restore
        // that follows the removal could not list its contents.
        fixture.Handler.MapStatus(FakeApiFactory.ArchiveContentsRoute(scenario.AddonB, AddonBVersion), HttpStatusCode.NotFound);

        var queueItem = MakeModifyItem(scenario.LocalGame.Id, _installDirectory, addonIds: [scenario.AddonB]);

        await fixture.InstallService.Modify(queueItem, scenario.LocalGame, scenario.RemoteGame, scenario.Installation);

        queueItem.Status.ShouldBe(InstallStatus.Failed);

        // It genuinely probed the surviving add-on — that request is the whole fix.
        fixture.Handler.Requests.ShouldContain(FakeApiFactory.ArchiveContentsRoute(scenario.AddonB, AddonBVersion));
    }

    [Fact]
    public async Task Modify_RemovingOneAddon_WhenASurvivingAddonsArchiveIsGone_LeavesBothAddonsOnDisk()
    {
        var fixture = CreateFixture();
        var scenario = await SeedInstallationWithTwoAddonsAsync(fixture);

        fixture.Handler.MapStatus(FakeApiFactory.ArchiveContentsRoute(scenario.AddonB, AddonBVersion), HttpStatusCode.NotFound);

        var queueItem = MakeModifyItem(scenario.LocalGame.Id, _installDirectory, addonIds: [scenario.AddonB]);

        await fixture.InstallService.Modify(queueItem, scenario.LocalGame, scenario.RemoteGame, scenario.Installation);

        // Nothing was uninstalled: the add-on that was on its way out is untouched, and so is the
        // base-game file it had overwritten (which is what could not have been restored).
        AddonIsOnDisk(scenario.AddonA, AddonAEntry).ShouldBeTrue();
        AddonIsOnDisk(scenario.AddonB, AddonBEntry).ShouldBeTrue();

        (await File.ReadAllTextAsync(Path.Combine(_installDirectory, BaseEntry))).ShouldBe("overwritten by the addon");
    }

    [Fact]
    public async Task Modify_RemovingOneAddon_WhenASurvivingAddonsArchiveIsGone_LeavesTrackingUnchanged()
    {
        var fixture = CreateFixture();
        var scenario = await SeedInstallationWithTwoAddonsAsync(fixture);

        fixture.Handler.MapStatus(FakeApiFactory.ArchiveContentsRoute(scenario.AddonB, AddonBVersion), HttpStatusCode.NotFound);

        var queueItem = MakeModifyItem(scenario.LocalGame.Id, _installDirectory, addonIds: [scenario.AddonB]);

        await fixture.InstallService.Modify(queueItem, scenario.LocalGame, scenario.RemoteGame, scenario.Installation);

        (await fixture.InstallationService.IsAddonInstalledForInstallationAsync(scenario.Installation.Id, scenario.AddonA)).ShouldBeTrue();
        (await fixture.InstallationService.IsAddonInstalledForInstallationAsync(scenario.Installation.Id, scenario.AddonB)).ShouldBeTrue();

        var reloaded = await fixture.InstallationService.GetAsync(scenario.Installation.Id);
        reloaded!.ArchiveId.ShouldBe(scenario.ArchiveId);
        reloaded.Version.ShouldBe(BaseVersion);
    }

    [Fact]
    public async Task Modify_RemovingOneAddon_WhenASurvivingAddonsArchiveIsGone_RefusesBeforeAnyMutationOrDownload()
    {
        // "Before all disk/DB mutations" includes the metadata refresh: no manifest may be
        // rewritten and no archive may be fetched.
        var fixture = CreateFixture();
        var scenario = await SeedInstallationWithTwoAddonsAsync(fixture);

        fixture.Handler.MapStatus(FakeApiFactory.ArchiveContentsRoute(scenario.AddonB, AddonBVersion), HttpStatusCode.NotFound);

        var queueItem = MakeModifyItem(scenario.LocalGame.Id, _installDirectory, addonIds: [scenario.AddonB]);

        await fixture.InstallService.Modify(queueItem, scenario.LocalGame, scenario.RemoteGame, scenario.Installation);

        (await File.ReadAllTextAsync(ManifestHelper.GetPath(_installDirectory, scenario.LocalGame.Id))).ShouldBe(scenario.BaseManifestYaml);

        fixture.Handler.Requests.ShouldNotContain(FakeApiFactory.ManifestRoute(scenario.LocalGame.Id, scenario.ArchiveId));
        fixture.Handler.Requests.ShouldNotContain(FakeApiFactory.ManifestRoute(scenario.LocalGame.Id));

        ShouldHaveDownloadedNothing(scenario, fixture.Handler);
    }

    [Fact]
    public async Task Modify_RemovingOneAddon_WhenASurvivingAddonsContentsFailForAnUnrelatedReason_FailsWithoutMutating()
    {
        // Only 400/404 is classified as "that exact archive is gone". Anything else — a server
        // error here — propagates instead of being read as a deleted archive, and still must never
        // proceed into a removal it cannot repair.
        var fixture = CreateFixture();
        var scenario = await SeedInstallationWithTwoAddonsAsync(fixture);

        fixture.Handler.Map(FakeApiFactory.ArchiveContentsRoute(scenario.AddonB, AddonBVersion),
            _ => new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent(string.Empty) });

        var queueItem = MakeModifyItem(scenario.LocalGame.Id, _installDirectory, addonIds: [scenario.AddonB]);

        await fixture.InstallService.Modify(queueItem, scenario.LocalGame, scenario.RemoteGame, scenario.Installation);

        queueItem.Status.ShouldBe(InstallStatus.Failed);

        AddonIsOnDisk(scenario.AddonA, AddonAEntry).ShouldBeTrue();
        AddonIsOnDisk(scenario.AddonB, AddonBEntry).ShouldBeTrue();

        (await fixture.InstallationService.IsAddonInstalledForInstallationAsync(scenario.Installation.Id, scenario.AddonA)).ShouldBeTrue();
        (await fixture.InstallationService.IsAddonInstalledForInstallationAsync(scenario.Installation.Id, scenario.AddonB)).ShouldBeTrue();

        ShouldHaveDownloadedNothing(scenario, fixture.Handler);
    }

    // ── Passing counterpart ──────────────────────────────────────────────────────

    [Fact]
    public async Task Modify_RemovingOneAddon_WhenEverySurvivingArchiveIsAvailable_AppliesTheRemovalAndRestoresBaseFiles()
    {
        var fixture = CreateFixture();
        var scenario = await SeedInstallationWithTwoAddonsAsync(fixture);

        fixture.Handler.MapJson(FakeApiFactory.ArchiveContentsRoute(scenario.AddonB, AddonBVersion), new[]
        {
            new ArchiveEntry { FullName = AddonBEntry, Name = "pak0.pak", Crc32 = 4321u, Length = 5 },
        });

        // The removed add-on's own archive is gone as well, and that must not matter at all: its
        // manifest is deleted by the uninstall, so nothing ever asks the server about it.
        fixture.Handler.MapStatus(FakeApiFactory.ArchiveContentsRoute(scenario.AddonA, AddonAVersion), HttpStatusCode.NotFound);

        fixture.Handler.Map(FakeApiFactory.DownloadRoute(scenario.LocalGame.Id, scenario.ArchiveId),
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(FakeApiFactory.CreateZip((BaseEntry, BaseEntryOriginalContent))),
            });

        var queueItem = MakeModifyItem(scenario.LocalGame.Id, _installDirectory, addonIds: [scenario.AddonB]);

        await fixture.InstallService.Modify(queueItem, scenario.LocalGame, scenario.RemoteGame, scenario.Installation);

        queueItem.Status.ShouldBe(InstallStatus.Complete);

        // A is gone, B survives untouched, and the base file A had overwritten came back out of the
        // installation's own pinned archive (never the game's current effective default).
        AddonIsOnDisk(scenario.AddonA, AddonAEntry).ShouldBeFalse();
        AddonIsOnDisk(scenario.AddonB, AddonBEntry).ShouldBeTrue();

        (await fixture.InstallationService.IsAddonInstalledForInstallationAsync(scenario.Installation.Id, scenario.AddonA)).ShouldBeFalse();
        (await fixture.InstallationService.IsAddonInstalledForInstallationAsync(scenario.Installation.Id, scenario.AddonB)).ShouldBeTrue();

        (await File.ReadAllTextAsync(Path.Combine(_installDirectory, BaseEntry))).ShouldBe(BaseEntryOriginalContent);

        fixture.Handler.Requests.ShouldContain(FakeApiFactory.DownloadRoute(scenario.LocalGame.Id, scenario.ArchiveId));
        fixture.Handler.Requests.ShouldNotContain(FakeApiFactory.DownloadRoute(scenario.LocalGame.Id));
    }

    [Fact]
    public async Task Modify_RemovingOneAddon_NeverProbesTheArchiveOfTheAddonBeingRemoved()
    {
        // The exclusion, asserted directly: probing the removed add-on's archive would refuse
        // removals that are the only way to get rid of a broken add-on in the first place.
        var fixture = CreateFixture();
        var scenario = await SeedInstallationWithTwoAddonsAsync(fixture);

        fixture.Handler.MapJson(FakeApiFactory.ArchiveContentsRoute(scenario.AddonB, AddonBVersion), new[]
        {
            new ArchiveEntry { FullName = AddonBEntry, Name = "pak0.pak", Crc32 = 4321u, Length = 5 },
        });

        fixture.Handler.MapStatus(FakeApiFactory.ArchiveContentsRoute(scenario.AddonA, AddonAVersion), HttpStatusCode.NotFound);

        fixture.Handler.Map(FakeApiFactory.DownloadRoute(scenario.LocalGame.Id, scenario.ArchiveId),
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(FakeApiFactory.CreateZip((BaseEntry, BaseEntryOriginalContent))),
            });

        var queueItem = MakeModifyItem(scenario.LocalGame.Id, _installDirectory, addonIds: [scenario.AddonB]);

        await fixture.InstallService.Modify(queueItem, scenario.LocalGame, scenario.RemoteGame, scenario.Installation);

        queueItem.Status.ShouldBe(InstallStatus.Complete);
        fixture.Handler.Requests.ShouldNotContain(FakeApiFactory.ArchiveContentsRoute(scenario.AddonA, AddonAVersion));
    }
}
