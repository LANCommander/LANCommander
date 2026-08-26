using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using LANCommander.Launcher.Services.Tests.Helpers;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Exceptions;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Services.Tests.Tests;

/// <summary>
/// Regression coverage for the CRITICAL "Update() can persist new archive/version metadata
/// without ever installing anything" bug: <c>ChangeVersionAsync(inPlace: true)</c> used to build a
/// manually-constructed <see cref="SDK.Models.InstallPlanItem"/> with an empty <see cref="SDK.Models.InstallTaskDefinition"/>
/// list, so <c>ExecuteInstallPlanItemAsync</c>'s task loop simply iterated zero times — no
/// download, no extraction, no manifest write — and yet <see cref="InstallService.Update"/> still
/// went on to overwrite the installation's <c>ArchiveId</c>/<c>Version</c> as if the transition had
/// actually happened. <see cref="InstallService.Update"/> must now refuse outright whenever the
/// plan it's about to execute has no executable download/write-manifest tasks, instead of ever
/// reaching that state — asserted here directly (without any network dependency, since the guard
/// fires before <c>GameClient.ExecuteInstallPlanItemAsync</c> is ever called).
/// </summary>
public class InstallServiceUpdateGuardTests
{
    private static InstallService CreateInstallService(out GameInstallationService installationService, out Data.DatabaseContext context)
    {
        context = InMemoryDatabaseFactory.Create();
        installationService = ServiceTestFactory.CreateGameInstallationService(context);
        var toolService = ServiceTestFactory.CreateToolService(context);
        var gameService = ServiceTestFactory.CreateGameService(context, toolService, installationService);

        return ServiceTestFactory.CreateInstallService(gameService, toolService, installationService);
    }

    private static InstallQueueGame MakeQueueItem(Guid entityId, Guid archiveId, string version, string installDirectory, List<SDK.Models.InstallTaskDefinition> tasks)
    {
        var sdkGame = new SDK.Models.Game { Id = entityId, Title = "Half-Life" };

        return new InstallQueueGame(sdkGame)
        {
            ArchiveId = archiveId,
            ArchiveVersion = version,
            Version = version,
            InstallDirectory = installDirectory,
            Tasks = tasks,
        };
    }

    private async Task<(Game LocalGame, SDK.Models.Game RemoteGame, GameInstallation Installation)> SeedAsync(GameInstallationService installationService, Data.DatabaseContext context, Guid fromArchiveId)
    {
        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life" };
        context.Games!.Add(game);
        await context.SaveChangesAsync();

        var installation = new GameInstallation
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            InstallDirectory = @"C:\Games\HalfLife",
            ArchiveId = fromArchiveId,
            Version = "1.0.0",
            InstalledOn = DateTime.UtcNow,
        };
        await installationService.AddInstallationAsync(installation);

        var remoteGame = new SDK.Models.Game { Id = game.Id, Title = "Half-Life", Type = GameType.MainGame, BaseGameId = Guid.Empty, DependentGames = [] };

        return (game, remoteGame, installation);
    }

    [Fact]
    public async Task Update_WithNoTasksAtAll_RefusesToPersistAndFailsTheItem()
    {
        var installService = CreateInstallService(out var installationService, out var context);
        var fromArchiveId = Guid.NewGuid();
        var toArchiveId = Guid.NewGuid();
        var (localGame, remoteGame, installation) = await SeedAsync(installationService, context, fromArchiveId);

        var queueItem = MakeQueueItem(localGame.Id, toArchiveId, "2.0.0", installation.InstallDirectory, tasks: new List<SDK.Models.InstallTaskDefinition>());

        await installService.Update(queueItem, localGame, remoteGame, installation);

        queueItem.Status.ShouldBe(InstallStatus.Failed);

        // The installation record must be completely untouched — neither the in-memory instance
        // nor what's actually persisted may show the new archive/version.
        installation.ArchiveId.ShouldBe(fromArchiveId);
        installation.Version.ShouldBe("1.0.0");

        var reloaded = await installationService.GetAsync(installation.Id);
        reloaded!.ArchiveId.ShouldBe(fromArchiveId);
        reloaded.Version.ShouldBe("1.0.0");
    }

    [Fact]
    public async Task Update_WithTasksMissingDownloadAndExtract_RefusesToPersist()
    {
        var installService = CreateInstallService(out var installationService, out var context);
        var fromArchiveId = Guid.NewGuid();
        var toArchiveId = Guid.NewGuid();
        var (localGame, remoteGame, installation) = await SeedAsync(installationService, context, fromArchiveId);

        // Has a WriteManifest task but no DownloadAndExtract — still insufficient to actually
        // install anything, so this must be refused exactly like a fully-empty task list.
        var tasks = new List<SDK.Models.InstallTaskDefinition>
        {
            new() { Type = InstallTaskType.WriteManifest, Title = "Write manifest", IsCritical = true },
        };
        var queueItem = MakeQueueItem(localGame.Id, toArchiveId, "2.0.0", installation.InstallDirectory, tasks);

        await installService.Update(queueItem, localGame, remoteGame, installation);

        queueItem.Status.ShouldBe(InstallStatus.Failed);
        installation.ArchiveId.ShouldBe(fromArchiveId);
        installation.Version.ShouldBe("1.0.0");
    }

    [Fact]
    public async Task Update_WithTasksMissingWriteManifest_RefusesToPersist()
    {
        var installService = CreateInstallService(out var installationService, out var context);
        var fromArchiveId = Guid.NewGuid();
        var toArchiveId = Guid.NewGuid();
        var (localGame, remoteGame, installation) = await SeedAsync(installationService, context, fromArchiveId);

        // Has a DownloadAndExtract task but no WriteManifest — a fresh install's plan always
        // includes both, so a plan missing either one is never a legitimate/complete transition.
        var tasks = new List<SDK.Models.InstallTaskDefinition>
        {
            new() { Type = InstallTaskType.DownloadAndExtract, Title = "Download", IsCritical = true },
        };
        var queueItem = MakeQueueItem(localGame.Id, toArchiveId, "2.0.0", installation.InstallDirectory, tasks);

        await installService.Update(queueItem, localGame, remoteGame, installation);

        queueItem.Status.ShouldBe(InstallStatus.Failed);
        installation.ArchiveId.ShouldBe(fromArchiveId);
        installation.Version.ShouldBe("1.0.0");
    }

    [Fact]
    public async Task Update_WithNullTasks_RefusesToPersist()
    {
        var installService = CreateInstallService(out var installationService, out var context);
        var fromArchiveId = Guid.NewGuid();
        var toArchiveId = Guid.NewGuid();
        var (localGame, remoteGame, installation) = await SeedAsync(installationService, context, fromArchiveId);

        var queueItem = MakeQueueItem(localGame.Id, toArchiveId, "2.0.0", installation.InstallDirectory, tasks: null!);

        await installService.Update(queueItem, localGame, remoteGame, installation);

        queueItem.Status.ShouldBe(InstallStatus.Failed);
        installation.ArchiveId.ShouldBe(fromArchiveId);
    }
}
