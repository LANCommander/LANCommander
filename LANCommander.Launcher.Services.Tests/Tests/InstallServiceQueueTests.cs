using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using LANCommander.Launcher.Services.Tests.Helpers;
using LANCommander.SDK.Enums;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Services.Tests.Tests;

/// <summary>
/// Covers InstallService queue-isolation behavior (two side-by-side installs of the same game
/// must never dedupe/cancel/overwrite each other's queue entries) and Move targeting exactly one
/// installation's directory.
/// </summary>
public class InstallServiceQueueTests
{
    private static InstallQueueGame MakeGameQueueItem(Guid entityId, Guid archiveId, string version, string installDirectory)
    {
        var sdkGame = new SDK.Models.Game { Id = entityId, Title = "Half-Life" };
        var item = new InstallQueueGame(sdkGame)
        {
            ArchiveId = archiveId,
            ArchiveVersion = version,
            Version = version,
            InstallDirectory = installDirectory,
        };

        return item;
    }

    private static InstallService CreateInstallService(out GameInstallationService installationService, out Data.DatabaseContext context)
    {
        context = InMemoryDatabaseFactory.Create();
        installationService = ServiceTestFactory.CreateGameInstallationService(context);
        var toolService = ServiceTestFactory.CreateToolService(context);
        var gameService = ServiceTestFactory.CreateGameService(context, toolService, installationService);

        return ServiceTestFactory.CreateInstallService(gameService, toolService, installationService);
    }

    [Fact]
    public void Two_versions_of_the_same_game_can_be_queued_at_once_without_deduping()
    {
        var installService = CreateInstallService(out _, out _);

        var gameId = Guid.NewGuid();
        var itemV1 = MakeGameQueueItem(gameId, Guid.NewGuid(), "1.0.0", @"C:\Games\HalfLife");
        var itemV2 = MakeGameQueueItem(gameId, Guid.NewGuid(), "1.1.0", @"C:\Games\HalfLife (1.1.0)");

        installService.Queue.Add(itemV1);
        installService.Queue.Add(itemV2);

        installService.Queue.Count.ShouldBe(2);
        installService.Queue.Select(i => i.Id).Distinct().Count().ShouldBe(2);
        installService.Queue.ShouldAllBe(i => i.EntityId == gameId);
    }

    [Fact]
    public void CancelInstallAsync_cancels_only_the_targeted_versions_queue_item()
    {
        var installService = CreateInstallService(out _, out _);

        var gameId = Guid.NewGuid();
        var itemV1 = MakeGameQueueItem(gameId, Guid.NewGuid(), "1.0.0", @"C:\Games\HalfLife");
        var itemV2 = MakeGameQueueItem(gameId, Guid.NewGuid(), "1.1.0", @"C:\Games\HalfLife (1.1.0)");
        itemV1.Status = InstallStatus.Downloading;
        itemV2.Status = InstallStatus.Downloading;

        installService.Queue.Add(itemV1);
        installService.Queue.Add(itemV2);

        installService.CancelInstallAsync(itemV1.Id).GetAwaiter().GetResult();

        itemV1.Status.ShouldBe(InstallStatus.Canceled);
        itemV2.Status.ShouldBe(InstallStatus.Downloading);
    }

    [Fact]
    public void Remove_removes_only_the_targeted_versions_queue_item()
    {
        var installService = CreateInstallService(out _, out _);

        var gameId = Guid.NewGuid();
        var itemV1 = MakeGameQueueItem(gameId, Guid.NewGuid(), "1.0.0", @"C:\Games\HalfLife");
        var itemV2 = MakeGameQueueItem(gameId, Guid.NewGuid(), "1.1.0", @"C:\Games\HalfLife (1.1.0)");

        installService.Queue.Add(itemV1);
        installService.Queue.Add(itemV2);

        installService.Remove(itemV1.Id);

        installService.Queue.Count.ShouldBe(1);
        installService.Queue.Single().Id.ShouldBe(itemV2.Id);
    }

    [Fact]
    public void ClearCompleted_with_an_archive_id_only_clears_that_specific_versions_stale_history()
    {
        var installService = CreateInstallService(out _, out _);

        var gameId = Guid.NewGuid();
        var archiveA = Guid.NewGuid();
        var archiveB = Guid.NewGuid();
        var itemV1 = MakeGameQueueItem(gameId, archiveA, "1.0.0", @"C:\Games\HalfLife");
        var itemV2 = MakeGameQueueItem(gameId, archiveB, "1.1.0", @"C:\Games\HalfLife (1.1.0)");
        itemV1.Status = InstallStatus.Complete;
        itemV2.Status = InstallStatus.Complete;

        installService.Queue.Add(itemV1);
        installService.Queue.Add(itemV2);

        installService.ClearCompleted(gameId, archiveA);

        installService.Queue.Count.ShouldBe(1);
        installService.Queue.Single().Id.ShouldBe(itemV2.Id);
    }

    [Fact]
    public void ClearCompleted_also_clears_dependents_of_the_targeted_version_only()
    {
        var installService = CreateInstallService(out _, out _);

        var gameId = Guid.NewGuid();
        var archiveA = Guid.NewGuid();
        var archiveB = Guid.NewGuid();
        var itemV1 = MakeGameQueueItem(gameId, archiveA, "1.0.0", @"C:\Games\HalfLife");
        var itemV2 = MakeGameQueueItem(gameId, archiveB, "1.1.0", @"C:\Games\HalfLife (1.1.0)");
        itemV1.Status = InstallStatus.Complete;
        itemV2.Status = InstallStatus.Complete;

        var toolForV1 = new InstallQueueTool(new SDK.Models.Tool { Id = Guid.NewGuid(), Name = "Tool" })
        {
            DependsOnId = itemV1.Id,
            ParentGameId = gameId,
            Status = InstallStatus.Complete,
        };
        var toolForV2 = new InstallQueueTool(new SDK.Models.Tool { Id = Guid.NewGuid(), Name = "Tool" })
        {
            DependsOnId = itemV2.Id,
            ParentGameId = gameId,
            Status = InstallStatus.Complete,
        };

        installService.Queue.Add(itemV1);
        installService.Queue.Add(itemV2);
        installService.Queue.Add(toolForV1);
        installService.Queue.Add(toolForV2);

        installService.ClearCompleted(gameId, archiveA);

        installService.Queue.Count.ShouldBe(2);
        installService.Queue.ShouldContain(i => i.Id == itemV2.Id);
        installService.Queue.ShouldContain(i => i.Id == toolForV2.Id);
    }

    [Fact]
    public async Task Move_relocates_only_the_targeted_installation_and_leaves_a_sibling_installation_untouched()
    {
        var installService = CreateInstallService(out var installationService, out var context);
        var tempRoot = Path.Combine(Path.GetTempPath(), $"lc-install-move-tests-{Guid.NewGuid():N}");

        try
        {
            var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life" };
            context.Games!.Add(game);
            await context.SaveChangesAsync();

            var oldDirectory = Path.Combine(tempRoot, "Old", "Half-Life");
            var newParentDirectory = Path.Combine(tempRoot, "New");
            // MoveAsync copies subdirectories first (before flat files), so give the fixture a
            // realistic nested layout rather than a single flat file directly under the root.
            Directory.CreateDirectory(Path.Combine(oldDirectory, "data"));
            await File.WriteAllTextAsync(Path.Combine(oldDirectory, "data", "marker.txt"), "hello");

            var installationToMove = new GameInstallation
            {
                Id = Guid.NewGuid(),
                GameId = game.Id,
                InstallDirectory = oldDirectory,
                InstalledOn = DateTime.UtcNow,
            };
            await installationService.AddInstallationAsync(installationToMove);

            // A sibling installation of a DIFFERENT game, whose directory must never be touched by
            // moving the game above.
            var untouchedDirectory = Path.Combine(tempRoot, "Untouched", "Some Other Game");
            Directory.CreateDirectory(Path.Combine(untouchedDirectory, "data"));
            await File.WriteAllTextAsync(Path.Combine(untouchedDirectory, "data", "marker.txt"), "leave me alone");

            var otherGame = new Game { Id = Guid.NewGuid(), Title = "Some Other Game" };
            context.Games!.Add(otherGame);
            await context.SaveChangesAsync();

            var untouchedInstallation = new GameInstallation
            {
                Id = Guid.NewGuid(),
                GameId = otherGame.Id,
                InstallDirectory = untouchedDirectory,
                InstalledOn = DateTime.UtcNow,
            };
            await installationService.AddInstallationAsync(untouchedInstallation);

            var remoteGame = new SDK.Models.Game { Id = game.Id, Title = "Half-Life", Type = GameType.MainGame, BaseGameId = Guid.Empty, DependentGames = [] };

            // InstallDirectory here is already the *exact* resolved destination, mirroring what
            // Add() actually populates the queue item with (see
            // InstallService.ResolveExactDestination / GameClient.GetInstallDirectory) — never a
            // bare parent folder. Move() must use it verbatim rather than re-resolving it through
            // GetInstallDirectory, which would re-append the game's title a second time.
            var expectedNewDirectory = Path.Combine(newParentDirectory, "Half-Life");
            var queueItem = new InstallQueueGame(remoteGame) { InstallDirectory = expectedNewDirectory };

            await installService.Move(queueItem, game, remoteGame, installationToMove);

            queueItem.Status.ShouldBe(InstallStatus.Complete);
            Directory.Exists(oldDirectory).ShouldBeFalse();
            Directory.Exists(expectedNewDirectory).ShouldBeTrue();
            File.Exists(Path.Combine(expectedNewDirectory, "data", "marker.txt")).ShouldBeTrue();

            // Regression guard for the double-nesting bug: the exact destination must be used
            // verbatim, never re-suffixed with the game's title a second time
            // (".../Half-Life/Half-Life").
            Directory.Exists(Path.Combine(expectedNewDirectory, "Half-Life")).ShouldBeFalse();

            var reloadedMoved = await installationService.GetAsync(installationToMove.Id);
            reloadedMoved!.InstallDirectory.ShouldBe(expectedNewDirectory);

            // The other installation's directory/record must be completely unaffected.
            var reloadedUntouched = await installationService.GetAsync(untouchedInstallation.Id);
            reloadedUntouched!.InstallDirectory.ShouldBe(untouchedDirectory);
            Directory.Exists(untouchedDirectory).ShouldBeTrue();
            File.Exists(Path.Combine(untouchedDirectory, "data", "marker.txt")).ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public async Task Move_rejects_a_destination_nested_under_the_source_and_leaves_source_intact()
    {
        // Regression test for the CRITICAL destructive-update bug: a caller resolving the move
        // destination from the installation's *own existing directory* (instead of a fresh
        // parent-folder hint) makes GetInstallDirectory re-suffix it with the game's title,
        // nesting the destination one level under the source. Move() must reject this outright
        // rather than copy into the nested destination and then delete the source (which would
        // also delete the copies it just made — total data loss).
        var installService = CreateInstallService(out var installationService, out var context);
        var tempRoot = Path.Combine(Path.GetTempPath(), $"lc-install-move-nested-tests-{Guid.NewGuid():N}");

        try
        {
            var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life" };
            context.Games!.Add(game);
            await context.SaveChangesAsync();

            var installDirectory = Path.Combine(tempRoot, "Half-Life");
            Directory.CreateDirectory(Path.Combine(installDirectory, "data"));
            await File.WriteAllTextAsync(Path.Combine(installDirectory, "data", "marker.txt"), "irreplaceable save data");

            var installation = new GameInstallation
            {
                Id = Guid.NewGuid(),
                GameId = game.Id,
                InstallDirectory = installDirectory,
                InstalledOn = DateTime.UtcNow,
            };
            await installationService.AddInstallationAsync(installation);

            var remoteGame = new SDK.Models.Game { Id = game.Id, Title = "Half-Life", Type = GameType.MainGame, BaseGameId = Guid.Empty, DependentGames = [] };

            // Reproduces the exact bug mechanism: the queue item's InstallDirectory is the
            // *existing* install directory itself, so GetInstallDirectory resolves it to a
            // subdirectory of itself.
            var queueItem = new InstallQueueGame(remoteGame) { InstallDirectory = installDirectory };

            await installService.Move(queueItem, game, remoteGame, installation);

            // Move() catches the guard's exception internally and fails the queue item rather
            // than throwing out to the caller — what matters is that nothing destructive happened.
            queueItem.Status.ShouldBe(InstallStatus.Failed);

            Directory.Exists(installDirectory).ShouldBeTrue();
            File.Exists(Path.Combine(installDirectory, "data", "marker.txt")).ShouldBeTrue();
            (await File.ReadAllTextAsync(Path.Combine(installDirectory, "data", "marker.txt")))
                .ShouldBe("irreplaceable save data");

            var reloaded = await installationService.GetAsync(installation.Id);
            reloaded!.InstallDirectory.ShouldBe(installDirectory);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, true);
        }
    }

    [Fact]
    public async Task FinalizeGameInstallStateAsync_propagates_persistence_failures_instead_of_swallowing_them()
    {
        // Regression test for "do not swallow AddInstallationAsync persistence failures
        // silently": Install() now wraps this method in a try/catch that converts *any*
        // exception into a Failed queue item instead of the old behavior (log-and-continue while
        // still marking the item Complete, even though nothing was actually persisted). That only
        // works if failures here actually propagate rather than being caught internally — which
        // this asserts directly.
        var installService = CreateInstallService(out _, out var context);

        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life" };
        context.Games!.Add(game);
        await context.SaveChangesAsync();

        // A blank InstallDirectory deterministically makes GameInstallationService's
        // AddInstallationAsync throw ArgumentException, standing in for any persistence failure
        // that only becomes apparent once persistence is actually attempted (e.g. a genuine
        // directory collision from a race between two pending installs).
        var queueItem = MakeGameQueueItem(game.Id, Guid.NewGuid(), "1.0.0", string.Empty);

        await Should.ThrowAsync<ArgumentException>(
            () => installService.FinalizeGameInstallStateAsync(queueItem, game));
    }
}
