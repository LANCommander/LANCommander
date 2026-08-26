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
/// Execution-level regression coverage for the CRITICAL "a canceled or failed in-place update
/// deletes the whole existing installation" finding.
///
/// <see cref="InstallService.Update"/> installs the target archive as a full snapshot straight into
/// the installation's *existing* directory. The SDK's extraction cleanup used to answer any
/// cancel/HTTP/corrupt-archive failure with a recursive delete of that destination, so a dropped
/// connection or one bad archive destroyed a working installation — saves, add-ons, tools and all —
/// and left the <see cref="GameInstallation"/> record pointing at nothing.
///
/// <see cref="InstallService.Update"/> now declares
/// <see cref="InstallDestinationOwnership.ExistingInstallation"/> on the plan item it executes, and
/// these tests assert the end-to-end consequence: real service, real
/// <see cref="GameClient"/> over a recording HTTP stack that genuinely fails, and real bytes on
/// disk that must still be there afterwards.
///
/// The pure counterpart — which branch of <c>Add()</c> owns its destination at all — lives in
/// <see cref="InstallServiceRoutingTests"/>.
/// </summary>
public class InstallServiceUpdateDestinationSafetyTests : IDisposable
{
    private readonly string _installDirectory;

    public InstallServiceUpdateDestinationSafetyTests()
    {
        _installDirectory = Path.Combine(Path.GetTempPath(), $"lc-update-safety-{Guid.NewGuid()}");
        Directory.CreateDirectory(_installDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_installDirectory))
            Directory.Delete(_installDirectory, true);
    }

    private const string InstalledVersion = "1.0.0";
    private const string SentinelName = "saves/campaign.sav";
    private const string SentinelContent = "the user's save game";

    private sealed record Fixture(
        InstallService InstallService,
        GameInstallationService InstallationService,
        Data.DatabaseContext Context,
        RecordingHttpMessageHandler Handler);

    private Fixture CreateFixture()
    {
        var handler = new RecordingHttpMessageHandler();
        var context = InMemoryDatabaseFactory.Create();
        // One attempt: these tests assert what a failed download leaves on disk, and retrying a
        // deterministic failure ten times only makes them slow.
        var gameClient = FakeApiFactory.CreateGameClient(handler, _installDirectory, maxInstallAttempts: 1);
        var toolClient = ServiceTestFactory.CreateToolClient();
        var installationService = ServiceTestFactory.CreateGameInstallationService(context);
        var toolService = ServiceTestFactory.CreateToolService(context);
        var gameService = ServiceTestFactory.CreateGameService(context, toolService, installationService, gameClient, toolClient);
        var installService = ServiceTestFactory.CreateInstallService(gameService, toolService, installationService, gameClient, toolClient);

        return new Fixture(installService, installationService, context, handler);
    }

    private async Task<(Game LocalGame, SdkGame RemoteGame, GameInstallation Installation)> SeedInstalledGameAsync(
        Fixture fixture,
        Guid installedArchiveId)
    {
        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life", Type = GameType.MainGame };

        fixture.Context.Games!.Add(game);
        await fixture.Context.SaveChangesAsync();

        var installation = new GameInstallation
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            InstallDirectory = _installDirectory,
            ArchiveId = installedArchiveId,
            Version = InstalledVersion,
            InstalledOn = DateTime.UtcNow,
        };

        await fixture.InstallationService.AddInstallationAsync(installation);

        // A real, populated installation: manifest plus the user's own files.
        await ManifestHelper.WriteAsync(new ManifestGame
        {
            Id = game.Id,
            Title = "Half-Life",
            Type = GameType.MainGame,
            Version = InstalledVersion,
        }, _installDirectory);

        Directory.CreateDirectory(Path.Combine(_installDirectory, "saves"));
        await File.WriteAllTextAsync(Path.Combine(_installDirectory, SentinelName), SentinelContent);

        var remoteGame = new SdkGame
        {
            Id = game.Id,
            Title = "Half-Life",
            Type = GameType.MainGame,
            BaseGameId = Guid.Empty,
            DependentGames = [],
            Redistributables = [],
        };

        fixture.Handler.MapJson(FakeApiFactory.GameRoute(game.Id), remoteGame);
        fixture.Handler.MapJson(FakeApiFactory.ScriptsRoute(game.Id), Array.Empty<object>());

        return (game, remoteGame, installation);
    }

    private static InstallQueueGame MakeUpdateItem(Guid entityId, Guid targetArchiveId, string installDirectory) =>
        new(new SdkGame { Id = entityId, Title = "Half-Life" })
        {
            InstallDirectory = installDirectory,
            ArchiveId = targetArchiveId,
            ArchiveVersion = "2.0.0",
            Version = "2.0.0",
            IsUpdate = true,
            Tasks =
            [
                new SDK.Models.InstallTaskDefinition
                {
                    Type = InstallTaskType.DownloadAndExtract,
                    Title = "Download Half-Life",
                    Order = 0,
                    TargetId = entityId,
                    TargetName = "Half-Life",
                    IsCritical = true,
                    ReportsProgress = true,
                },
                new SDK.Models.InstallTaskDefinition
                {
                    Type = InstallTaskType.WriteManifest,
                    Title = "Write manifest",
                    Order = 1,
                    TargetId = entityId,
                    TargetName = "Half-Life",
                    IsCritical = true,
                },
            ],
        };

    private void AssertInstallationSurvived(Guid gameId)
    {
        Directory.Exists(_installDirectory).ShouldBeTrue();
        File.ReadAllText(Path.Combine(_installDirectory, SentinelName)).ShouldBe(SentinelContent);
        ManifestHelper.Exists(_installDirectory, gameId).ShouldBeTrue();
    }

    [Fact]
    public async Task Update_WhenTheDownloadFails_LeavesTheExistingInstallationOnDisk()
    {
        var fixture = CreateFixture();
        var installedArchiveId = Guid.NewGuid();
        var targetArchiveId = Guid.NewGuid();
        var (localGame, remoteGame, installation) = await SeedInstalledGameAsync(fixture, installedArchiveId);

        fixture.Handler.Map(FakeApiFactory.DownloadRoute(localGame.Id, targetArchiveId),
            _ => new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent(string.Empty) });

        var queueItem = MakeUpdateItem(localGame.Id, targetArchiveId, _installDirectory);

        await fixture.InstallService.Update(queueItem, localGame, remoteGame, installation);

        queueItem.Status.ShouldBe(InstallStatus.Failed);

        AssertInstallationSurvived(localGame.Id);
    }

    [Fact]
    public async Task Update_WhenTheArchiveIsCorrupt_LeavesTheExistingInstallationOnDisk()
    {
        var fixture = CreateFixture();
        var installedArchiveId = Guid.NewGuid();
        var targetArchiveId = Guid.NewGuid();
        var (localGame, remoteGame, installation) = await SeedInstalledGameAsync(fixture, installedArchiveId);

        fixture.Handler.Map(FakeApiFactory.DownloadRoute(localGame.Id, targetArchiveId),
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent("this is not an archive"u8.ToArray()) });

        var queueItem = MakeUpdateItem(localGame.Id, targetArchiveId, _installDirectory);

        await fixture.InstallService.Update(queueItem, localGame, remoteGame, installation);

        queueItem.Status.ShouldBe(InstallStatus.Failed);

        AssertInstallationSurvived(localGame.Id);
    }

    [Fact]
    public async Task Update_WhenTheUpdateIsCanceled_LeavesTheExistingInstallationOnDisk()
    {
        var fixture = CreateFixture();
        var installedArchiveId = Guid.NewGuid();
        var targetArchiveId = Guid.NewGuid();
        var (localGame, remoteGame, installation) = await SeedInstalledGameAsync(fixture, installedArchiveId);

        var queueItem = MakeUpdateItem(localGame.Id, targetArchiveId, _installDirectory);

        // Cancel exactly when the archive download is requested — the flow a user hitting "cancel"
        // mid-update produces.
        fixture.Handler.Map(FakeApiFactory.DownloadRoute(localGame.Id, targetArchiveId), _ =>
        {
            queueItem.CancellationToken.Cancel();

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(FakeApiFactory.CreateZip(("hl.exe", "new version"))),
            };
        });

        await fixture.InstallService.Update(queueItem, localGame, remoteGame, installation);

        AssertInstallationSurvived(localGame.Id);
    }

    [Fact]
    public async Task Update_WhenTheDownloadFails_NeverPersistsTheNewArchiveIdentity()
    {
        var fixture = CreateFixture();
        var installedArchiveId = Guid.NewGuid();
        var targetArchiveId = Guid.NewGuid();
        var (localGame, remoteGame, installation) = await SeedInstalledGameAsync(fixture, installedArchiveId);

        fixture.Handler.Map(FakeApiFactory.DownloadRoute(localGame.Id, targetArchiveId),
            _ => new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent(string.Empty) });

        var queueItem = MakeUpdateItem(localGame.Id, targetArchiveId, _installDirectory);

        await fixture.InstallService.Update(queueItem, localGame, remoteGame, installation);

        var reloaded = await fixture.InstallationService.GetAsync(installation.Id);

        reloaded!.ArchiveId.ShouldBe(installedArchiveId);
        reloaded.Version.ShouldBe(InstalledVersion);
    }
}
