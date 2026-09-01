using System.IO.Compression;
using System.Text;
using LANCommander.SDK;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

/// <summary>
/// Covers the MEDIUM "archive resolve failures abort install/modify/move" finding.
///
/// Install-plan generation resolves an archive per entity. Two of those resolutions could fail in
/// ordinary, non-exceptional situations and took the entire operation down with them:
///
/// 1. An add-on the server has no archive for made <c>ResolveArchiveAsync</c> return 404, which
///    aborted the whole base-game plan. Such an add-on can never be downloaded, so the plan must
///    simply skip it (and it must never silently substitute a different archive for one that was
///    explicitly selected).
/// 2. An installation pinned to an archive an administrator later deleted made resolution return
///    400, which aborted modify and move — neither of which re-downloads the base archive at all.
///    Those operations must keep the pin verbatim rather than requiring, or silently adopting, a
///    different (current-default) archive.
/// </summary>
[Collection("Application")]
public class ArchiveResolutionResilienceTests(ApplicationFixture fixture) : BaseTest(fixture)
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

    private async Task<(Game Game, Archive Archive)> CreateGameWithOneArchiveAsync(string titlePrefix, bool writeFiles = false)
    {
        var gameService = GetService<GameService>();
        var archiveService = GetService<ArchiveService>();
        var storageLocationService = GetService<StorageLocationService>();

        await EnsureStorageLocationsExistAsync();

        var storageLocation = await storageLocationService.DefaultAsync(StorageLocationType.Archive);

        var game = await gameService.AddAsync(new Game { Title = $"{titlePrefix} {Guid.NewGuid():N}" });

        var archive = await archiveService.AddAsync(new Archive
        {
            GameId = game.Id,
            Version = "1.0",
            ObjectKey = Guid.NewGuid().ToString(),
            StorageLocationId = storageLocation.Id,
        });

        if (writeFiles)
            WriteZip(AppPaths.ResolveStorageLocationPath(storageLocation.Path, archive.ObjectKey),
                new Dictionary<string, string> { ["marker.txt"] = "base-content" });

        return (game, archive);
    }

    private async Task<Game> CreateAddonAsync(Guid baseGameId, bool withArchive)
    {
        var gameService = GetService<GameService>();
        var archiveService = GetService<ArchiveService>();
        var storageLocationService = GetService<StorageLocationService>();

        var addon = await gameService.AddAsync(new Game
        {
            Title = $"Addon {Guid.NewGuid():N}",
            Type = GameType.Expansion,
            BaseGameId = baseGameId,
        });

        if (withArchive)
        {
            var storageLocation = await storageLocationService.DefaultAsync(StorageLocationType.Archive);

            await archiveService.AddAsync(new Archive
            {
                GameId = addon.Id,
                Version = "1.0",
                ObjectKey = Guid.NewGuid().ToString(),
                StorageLocationId = storageLocation.Id,
            });
        }

        return addon;
    }

    [Fact]
    public async Task GenerateInstallPlanAsync_SkipsAnAddonWithNoArchive_InsteadOfAbortingTheWholePlan()
    {
        await EnsureAdminUserCreatedAsync();
        await AuthenticateAsync(TestConstants.AdminUserName, TestConstants.AdminInitialPassword);

        var (game, archive) = await CreateGameWithOneArchiveAsync("Addon Skip Test Game");
        var addonWithArchive = await CreateAddonAsync(game.Id, withArchive: true);
        var addonWithoutArchive = await CreateAddonAsync(game.Id, withArchive: false);

        var plan = await GameClient.GenerateInstallPlanAsync(
            game.Id,
            GetTemporaryDirectory(),
            addonIds: [addonWithArchive.Id, addonWithoutArchive.Id]);

        // The base game is still planned, pinned to its own archive...
        var gameItem = plan.Items.Single(i => i.Type == InstallPlanItemType.Game);
        gameItem.ArchiveId.ShouldBe(archive.Id);

        // ...the installable add-on is still planned...
        var addonItems = plan.Items.Where(i => i.Type == InstallPlanItemType.Addon).ToList();
        addonItems.Select(i => i.EntityId).ShouldBe([addonWithArchive.Id]);

        // ...and the archive-less one is skipped outright rather than enqueued with a critical
        // download task it could never satisfy.
        addonItems.ShouldNotContain(i => i.EntityId == addonWithoutArchive.Id);
        addonItems.Single().ArchiveId.ShouldNotBeNull();
    }

    [Fact]
    public async Task GenerateInstallPlanAsync_WithOnlyArchivelessAddons_StillPlansTheBaseGame()
    {
        await EnsureAdminUserCreatedAsync();
        await AuthenticateAsync(TestConstants.AdminUserName, TestConstants.AdminInitialPassword);

        var (game, archive) = await CreateGameWithOneArchiveAsync("Addon Skip Only Test Game");
        var addonWithoutArchive = await CreateAddonAsync(game.Id, withArchive: false);

        var plan = await GameClient.GenerateInstallPlanAsync(
            game.Id,
            GetTemporaryDirectory(),
            addonIds: [addonWithoutArchive.Id]);

        plan.Items.Single(i => i.Type == InstallPlanItemType.Game).ArchiveId.ShouldBe(archive.Id);
        plan.Items.ShouldNotContain(i => i.Type == InstallPlanItemType.Addon);
    }

    [Fact]
    public async Task GenerateInstallPlanAsync_RequiringResolution_StillFailsLoudlyForADeletedPinnedArchive()
    {
        // A fresh install or an explicit version change must not quietly install something other
        // than what was asked for: an unresolvable explicit target is a hard error.
        await EnsureAdminUserCreatedAsync();
        await AuthenticateAsync(TestConstants.AdminUserName, TestConstants.AdminInitialPassword);

        var archiveService = GetService<ArchiveService>();
        var (game, deletedArchive) = await CreateGameWithOneArchiveAsync("Deleted Pin Strict Test Game");

        await archiveService.DeleteAsync(deletedArchive);

        await Should.ThrowAsync<HttpRequestException>(async () =>
            await GameClient.GenerateInstallPlanAsync(
                game.Id,
                GetTemporaryDirectory(),
                archiveId: deletedArchive.Id));
    }

    [Fact]
    public async Task GenerateInstallPlanAsync_NotRequiringResolution_KeepsADeletedPinnedArchiveVerbatim()
    {
        // Modify/move of an installation pinned to a since-deleted archive: the plan must still be
        // generated (neither operation re-downloads the base archive), the pin must be carried
        // through untouched, and it must NOT be reinterpreted as the game's current default.
        await EnsureAdminUserCreatedAsync();
        await AuthenticateAsync(TestConstants.AdminUserName, TestConstants.AdminInitialPassword);

        var archiveService = GetService<ArchiveService>();
        var storageLocationService = GetService<StorageLocationService>();

        var (game, deletedArchive) = await CreateGameWithOneArchiveAsync("Deleted Pin Lenient Test Game");

        await archiveService.DeleteAsync(deletedArchive);

        // A different archive is uploaded afterwards and becomes the game's effective default —
        // the pinned (deleted) archive must not silently become this one.
        var storageLocation = await storageLocationService.DefaultAsync(StorageLocationType.Archive);

        var replacement = await archiveService.AddAsync(new Archive
        {
            GameId = game.Id,
            Version = "2.0",
            ObjectKey = Guid.NewGuid().ToString(),
            StorageLocationId = storageLocation.Id,
        });

        var plan = await GameClient.GenerateInstallPlanAsync(
            game.Id,
            GetTemporaryDirectory(),
            archiveId: deletedArchive.Id,
            requireResolvableArchive: false);

        var gameItem = plan.Items.Single(i => i.Type == InstallPlanItemType.Game);

        gameItem.ArchiveId.ShouldBe(deletedArchive.Id);
        gameItem.ArchiveId.ShouldNotBe(replacement.Id);
        gameItem.ArchiveVersion.ShouldBeNull("a deleted archive has no version to report; inventing one would rewrite the installation's pinned metadata");
    }

    [Fact]
    public async Task TryResolveArchiveAsync_ReturnsNullInsteadOfThrowing_ForUnavailableArchives()
    {
        await EnsureAdminUserCreatedAsync();
        await AuthenticateAsync(TestConstants.AdminUserName, TestConstants.AdminInitialPassword);

        var gameService = GetService<GameService>();
        var archiveService = GetService<ArchiveService>();

        var (game, archive) = await CreateGameWithOneArchiveAsync("Try Resolve Test Game");
        var gameWithNoArchives = await gameService.AddAsync(new Game { Title = $"No Archives {Guid.NewGuid():N}" });

        // Game with no archives at all (server answers 404).
        (await GameClient.TryResolveArchiveAsync(gameWithNoArchives.Id)).ShouldBeNull();

        // Archive that does not belong to the game (server answers 400) — and, critically, it must
        // NOT fall back to the game's own effective default.
        (await GameClient.TryResolveArchiveAsync(game.Id, Guid.NewGuid())).ShouldBeNull();

        // A resolvable archive still resolves normally.
        (await GameClient.TryResolveArchiveAsync(game.Id, archive.Id))!.Id.ShouldBe(archive.Id);

        await archiveService.DeleteAsync(archive);

        (await GameClient.TryResolveArchiveAsync(game.Id, archive.Id)).ShouldBeNull();
    }
}
