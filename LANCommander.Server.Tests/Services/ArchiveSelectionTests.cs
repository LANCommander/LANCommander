using System.IO.Compression;
using System.Text;
using LANCommander.SDK;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

/// <summary>
/// Phase 2 (archive-aware server/SDK contracts) tests: exact archive selection, cross-game
/// rejection, omitted-archiveId default fallback, and install-plan stability once a newer
/// archive appears after the plan was generated.
/// </summary>
[Collection("Application")]
public class ArchiveSelectionTests(ApplicationFixture fixture) : BaseTest(fixture)
{
    private static void WriteZip(string path, IDictionary<string, string> entries)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);

        foreach (var (name, content) in entries)
        {
            var entry = zip.CreateEntry(name);

            using var stream = entry.Open();
            using var writer = new StreamWriter(stream, Encoding.UTF8);

            writer.Write(content);
        }
    }

    private async Task<(Game Game, Archive Older, Archive Newer, StorageLocation StorageLocation)> CreateGameWithTwoArchivesAsync(
        bool writeFiles = false)
    {
        var gameService = GetService<GameService>();
        var archiveService = GetService<ArchiveService>();
        var storageLocationService = GetService<StorageLocationService>();

        await EnsureStorageLocationsExistAsync();

        var storageLocation = await storageLocationService.DefaultAsync(StorageLocationType.Archive);

        var game = await gameService.AddAsync(new Game { Title = "Archive Selection Test Game " + Guid.NewGuid().ToString("N") });

        var older = await archiveService.AddAsync(new Archive
        {
            GameId = game.Id,
            Version = "1.0",
            ObjectKey = Guid.NewGuid().ToString(),
            StorageLocationId = storageLocation.Id,
        });

        var newer = await archiveService.AddAsync(new Archive
        {
            GameId = game.Id,
            Version = "2.0",
            ObjectKey = Guid.NewGuid().ToString(),
            StorageLocationId = storageLocation.Id,
        });

        older.CreatedOn = DateTime.UtcNow.AddDays(-2);
        newer.CreatedOn = DateTime.UtcNow.AddDays(-1);

        older = await archiveService.UpdateAsync(older);
        newer = await archiveService.UpdateAsync(newer);

        if (writeFiles)
        {
            WriteZip(AppPaths.ResolveStorageLocationPath(storageLocation.Path, older.ObjectKey),
                new Dictionary<string, string> { ["marker.txt"] = "old-content-v1" });

            WriteZip(AppPaths.ResolveStorageLocationPath(storageLocation.Path, newer.ObjectKey),
                new Dictionary<string, string> { ["marker.txt"] = "new-content-v2" });
        }

        return (game, older, newer, storageLocation);
    }

    [Fact]
    public async Task ResolveArchiveAsync_ExplicitArchiveIdIsHonoredOverNewerArchive()
    {
        var gameService = GetService<GameService>();
        var (game, older, newer, _) = await CreateGameWithTwoArchivesAsync();

        var resolved = await gameService.ResolveArchiveAsync(game.Id, older.Id);

        resolved.Id.ShouldBe(older.Id);
        resolved.Version.ShouldBe(older.Version);
    }

    [Fact]
    public async Task ResolveArchiveAsync_OmittedArchiveIdFallsBackToEffectiveDefault()
    {
        var gameService = GetService<GameService>();
        var (game, _, newer, _) = await CreateGameWithTwoArchivesAsync();

        var resolved = await gameService.ResolveArchiveAsync(game.Id, null);

        resolved.Id.ShouldBe(newer.Id);
    }

    [Fact]
    public async Task ResolveArchiveAsync_ExplicitDefaultStillWinsWhenArchiveIdOmitted()
    {
        var gameService = GetService<GameService>();
        var (game, older, _, _) = await CreateGameWithTwoArchivesAsync();

        await gameService.SetDefaultArchiveAsync(game.Id, older.Id);

        var resolved = await gameService.ResolveArchiveAsync(game.Id, null);

        resolved.Id.ShouldBe(older.Id);
    }

    [Fact]
    public async Task ResolveArchiveAsync_CrossGameArchiveIdIsRejected()
    {
        var gameService = GetService<GameService>();
        var archiveService = GetService<ArchiveService>();
        var storageLocationService = GetService<StorageLocationService>();

        var (gameA, _, _, _) = await CreateGameWithTwoArchivesAsync();

        var storageLocation = await storageLocationService.DefaultAsync(StorageLocationType.Archive);

        var gameB = await gameService.AddAsync(new Game { Title = "Other Game " + Guid.NewGuid().ToString("N") });

        var archiveForGameB = await archiveService.AddAsync(new Archive
        {
            GameId = gameB.Id,
            Version = "1.0",
            ObjectKey = Guid.NewGuid().ToString(),
            StorageLocationId = storageLocation.Id,
        });

        await Should.ThrowAsync<ArchiveNotFoundForGameException>(
            async () => await gameService.ResolveArchiveAsync(gameA.Id, archiveForGameB.Id));
    }

    [Fact]
    public async Task ResolveArchiveAsync_UnknownGameThrowsKeyNotFound()
    {
        var gameService = GetService<GameService>();

        await Should.ThrowAsync<KeyNotFoundException>(
            async () => await gameService.ResolveArchiveAsync(Guid.NewGuid(), null));
    }

    [Fact]
    public async Task GetSelectableArchivesAsync_ReturnsAllArchivesWithEffectiveDefaultFlag()
    {
        var gameService = GetService<GameService>();
        var (game, older, newer, _) = await CreateGameWithTwoArchivesAsync();

        var (returnedGame, effectiveDefault) = await gameService.GetSelectableArchivesAsync(game.Id);

        returnedGame.ShouldNotBeNull();
        returnedGame.Archives.Select(a => a.Id).ShouldBe(new[] { older.Id, newer.Id }, ignoreOrder: true);
        effectiveDefault.Id.ShouldBe(newer.Id);

        await gameService.SetDefaultArchiveAsync(game.Id, older.Id);

        var (_, effectiveDefaultAfterPin) = await gameService.GetSelectableArchivesAsync(game.Id);

        effectiveDefaultAfterPin.Id.ShouldBe(older.Id);
    }

    [Fact]
    public async Task GetSelectableArchivesAsync_UnknownGameReturnsNull()
    {
        var gameService = GetService<GameService>();

        var (returnedGame, effectiveDefault) = await gameService.GetSelectableArchivesAsync(Guid.NewGuid());

        returnedGame.ShouldBeNull();
        effectiveDefault.ShouldBeNull();
    }

    [Fact]
    public async Task GetArchivesAsync_FlagsOlderExplicitDefault_NotJustNewest()
    {
        // Regression test for the launcher's version-selector preselection bug: the launcher
        // must source selectable archives from GameClient.GetArchivesAsync (this endpoint) so
        // IsDefault/IsEffectiveDefault are real — Game.Archives from GameClient.GetAsync maps
        // straight from Data.Models.Archive, which has no such properties at all, so those flags
        // are always false there and an admin-pinned *older* default would be silently ignored in
        // favor of "newest by CreatedOn".
        await EnsureAdminUserCreatedAsync();
        await AuthenticateAsync(TestConstants.AdminUserName, TestConstants.AdminInitialPassword);

        var gameService = GetService<GameService>();
        var (game, older, newer, _) = await CreateGameWithTwoArchivesAsync();

        // Explicitly pin the OLDER archive as the default — the effective default must follow
        // the explicit pin, not "newest by CreatedOn".
        await gameService.SetDefaultArchiveAsync(game.Id, older.Id);

        var archives = (await GameClient.GetArchivesAsync(game.Id)).ToList();

        archives.Count.ShouldBe(2);

        var olderDto = archives.Single(a => a.Id == older.Id);
        var newerDto = archives.Single(a => a.Id == newer.Id);

        olderDto.IsDefault.ShouldBeTrue();
        olderDto.IsEffectiveDefault.ShouldBeTrue();
        newerDto.IsDefault.ShouldBeFalse();
        newerDto.IsEffectiveDefault.ShouldBeFalse();
    }

    [Fact]
    public async Task GetArchivesAsync_FlagsNewestAsEffectiveDefault_WhenNoExplicitDefaultIsSet()
    {
        await EnsureAdminUserCreatedAsync();
        await AuthenticateAsync(TestConstants.AdminUserName, TestConstants.AdminInitialPassword);

        var (game, older, newer, _) = await CreateGameWithTwoArchivesAsync();

        var archives = (await GameClient.GetArchivesAsync(game.Id)).ToList();

        archives.Single(a => a.Id == newer.Id).IsEffectiveDefault.ShouldBeTrue();
        archives.Single(a => a.Id == older.Id).IsEffectiveDefault.ShouldBeFalse();
    }

    [Fact]
    public async Task GetUpdatesAsync_PrefersExactInstalledArchiveIdOverVersionString()
    {
        var gameService = GetService<GameService>();
        var (game, older, newer, _) = await CreateGameWithTwoArchivesAsync();

        // Version string says "2.0" is installed (would normally mean no updates), but the
        // installed archive ID identifies the older archive - the exact archive ID must win.
        var updates = (await gameService.GetUpdatesAsync(game.Id, newer.Version, older.Id)).ToList();

        updates.ShouldContain(a => a.Id == newer.Id);
        updates.ShouldNotContain(a => a.Id == older.Id);
    }

    [Fact]
    public async Task GetUpdatesAsync_NoUpdatesWhenInstalledArchiveIdIsAlreadyNewest()
    {
        var gameService = GetService<GameService>();
        var (game, _, newer, _) = await CreateGameWithTwoArchivesAsync();

        var updates = (await gameService.GetUpdatesAsync(game.Id, string.Empty, newer.Id)).ToList();

        updates.ShouldBeEmpty();
    }

    [Fact]
    public async Task GenerateInstallPlanAsync_PinsResolvedArchiveOnPlanItem()
    {
        await EnsureAdminUserCreatedAsync();
        await AuthenticateAsync(TestConstants.AdminUserName, TestConstants.AdminInitialPassword);

        var (game, _, newer, _) = await CreateGameWithTwoArchivesAsync();

        var plan = await GameClient.GenerateInstallPlanAsync(game.Id, GetTemporaryDirectory());

        var gameItem = plan.Items.Single(i => i.Type == InstallPlanItemType.Game);

        gameItem.ArchiveId.ShouldBe(newer.Id);
        gameItem.ArchiveVersion.ShouldBe(newer.Version);

        var downloadTask = gameItem.Tasks.Single(t => t.Type == InstallTaskType.DownloadAndExtract);

        downloadTask.Parameters["ArchiveId"].ShouldBe(newer.Id.ToString());
        downloadTask.Parameters["ArchiveVersion"].ShouldBe(newer.Version);
    }

    [Fact]
    public async Task GenerateInstallPlanAsync_HonorsExplicitArchiveIdOverEffectiveDefault()
    {
        await EnsureAdminUserCreatedAsync();
        await AuthenticateAsync(TestConstants.AdminUserName, TestConstants.AdminInitialPassword);

        var (game, older, newer, _) = await CreateGameWithTwoArchivesAsync();

        var plan = await GameClient.GenerateInstallPlanAsync(game.Id, GetTemporaryDirectory(), archiveId: older.Id);

        var gameItem = plan.Items.Single(i => i.Type == InstallPlanItemType.Game);

        gameItem.ArchiveId.ShouldBe(older.Id);
        gameItem.ArchiveId.ShouldNotBe(newer.Id);
    }

    [Fact]
    public async Task PlanRemainsStableAndExecutesPinnedArchiveAfterNewerArchiveAppears()
    {
        await EnsureAdminUserCreatedAsync();
        await AuthenticateAsync(TestConstants.AdminUserName, TestConstants.AdminInitialPassword);

        var gameService = GetService<GameService>();
        var archiveService = GetService<ArchiveService>();
        var storageLocationService = GetService<StorageLocationService>();

        await EnsureStorageLocationsExistAsync();

        var storageLocation = await storageLocationService.DefaultAsync(StorageLocationType.Archive);

        var game = await gameService.AddAsync(new Game { Title = "Plan Stability Test Game " + Guid.NewGuid().ToString("N") });

        // Only one archive exists when the plan is generated - it should be pinned as the
        // resolved archive for the plan item regardless of what happens afterward.
        var original = await archiveService.AddAsync(new Archive
        {
            GameId = game.Id,
            Version = "1.0",
            ObjectKey = Guid.NewGuid().ToString(),
            StorageLocationId = storageLocation.Id,
        });

        WriteZip(AppPaths.ResolveStorageLocationPath(storageLocation.Path, original.ObjectKey),
            new Dictionary<string, string> { ["marker.txt"] = "original-content" });

        var installDirectory = GetTemporaryDirectory();

        var plan = await GameClient.GenerateInstallPlanAsync(game.Id, installDirectory);
        var gameItem = plan.Items.Single(i => i.Type == InstallPlanItemType.Game);

        gameItem.ArchiveId.ShouldBe(original.Id);
        gameItem.ArchiveVersion.ShouldBe("1.0");

        // A newer archive now appears after the plan was generated but before it executes.
        var newerAppearsLater = await archiveService.AddAsync(new Archive
        {
            GameId = game.Id,
            Version = "2.0",
            ObjectKey = Guid.NewGuid().ToString(),
            StorageLocationId = storageLocation.Id,
        });

        newerAppearsLater.CreatedOn = DateTime.UtcNow.AddDays(1);
        await archiveService.UpdateAsync(newerAppearsLater);

        WriteZip(AppPaths.ResolveStorageLocationPath(storageLocation.Path, newerAppearsLater.ObjectKey),
            new Dictionary<string, string> { ["marker.txt"] = "newer-content" });

        // Sanity check: the newer archive really is now the effective default/latest.
        (await gameService.GetLatestArchiveAsync(game.Id)).Id.ShouldBe(newerAppearsLater.Id);

        try
        {
            // Execute only the download/manifest tasks that this scope covers (scripts/saves are
            // untouched by Phase 2 and would otherwise pull in unrelated behavior); what matters
            // here is that execution streams the exact archive pinned on the plan item, not the
            // effective default at execution time.
            var executionItem = new SDK.Models.InstallPlanItem
            {
                EntityId = gameItem.EntityId,
                Title = gameItem.Title,
                Type = InstallPlanItemType.Game,
                InstallDirectory = gameItem.InstallDirectory,
                ArchiveId = gameItem.ArchiveId,
                ArchiveVersion = gameItem.ArchiveVersion,
            };

            executionItem.Tasks.Add(gameItem.Tasks.Single(t => t.Type == InstallTaskType.DownloadAndExtract));
            executionItem.Tasks.Add(gameItem.Tasks.Single(t => t.Type == InstallTaskType.WriteManifest));

            var installResult = await GameClient.ExecuteInstallPlanItemAsync(executionItem);

            var extractedFile = Path.Combine(installResult.InstallDirectory, "marker.txt");

            File.Exists(extractedFile).ShouldBeTrue();
            (await File.ReadAllTextAsync(extractedFile)).ShouldBe("original-content");

            var manifest = await GameClient.GetManifestAsync(game.Id, gameItem.ArchiveId);
            manifest.Version.ShouldBe("1.0");
        }
        finally
        {
            if (Directory.Exists(installDirectory))
                Directory.Delete(installDirectory, true);
        }
    }
}
