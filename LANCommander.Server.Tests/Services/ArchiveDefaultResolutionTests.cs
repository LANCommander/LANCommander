using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Exceptions;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

[Collection("Application")]
public class ArchiveDefaultResolutionTests(ApplicationFixture fixture) : BaseTest(fixture)
{
    private async Task<(Game Game, Archive Older, Archive Newer)> CreateGameWithTwoArchivesAsync()
    {
        var gameService = GetService<GameService>();
        var archiveService = GetService<ArchiveService>();
        var storageLocationService = GetService<StorageLocationService>();

        await EnsureStorageLocationsExistAsync();

        var storageLocation = await storageLocationService.DefaultAsync(StorageLocationType.Archive);

        var game = await gameService.AddAsync(new Game { Title = "Test Game " + Guid.NewGuid().ToString("N") });

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

        // AddAsync always stamps CreatedOn = DateTime.UtcNow at insert time. Force explicit,
        // well-separated timestamps here so "newest by CreatedOn" is deterministic regardless
        // of how fast the two AddAsync calls above happened to run.
        older.CreatedOn = DateTime.UtcNow.AddDays(-2);
        newer.CreatedOn = DateTime.UtcNow.AddDays(-1);

        older = await archiveService.UpdateAsync(older);
        newer = await archiveService.UpdateAsync(newer);

        return (game, older, newer);
    }

    [Fact]
    public async Task ExplicitDefaultArchiveWinsOverNewerArchive()
    {
        var gameService = GetService<GameService>();
        var (game, older, newer) = await CreateGameWithTwoArchivesAsync();

        await gameService.SetDefaultArchiveAsync(game.Id, older.Id);

        var latest = await gameService.GetLatestArchiveAsync(game.Id);

        latest.Id.ShouldBe(older.Id);
        (await gameService.GetVersionAsync(game.Id)).ShouldBe(older.Version);
    }

    [Fact]
    public async Task NoExplicitDefaultFallsBackToNewestByCreatedOn()
    {
        var gameService = GetService<GameService>();
        var (game, _, newer) = await CreateGameWithTwoArchivesAsync();

        var latest = await gameService.GetLatestArchiveAsync(game.Id);

        latest.Id.ShouldBe(newer.Id);
    }

    [Fact]
    public async Task ClearingExplicitDefaultFallsBackToNewest()
    {
        var gameService = GetService<GameService>();
        var (game, older, newer) = await CreateGameWithTwoArchivesAsync();

        await gameService.SetDefaultArchiveAsync(game.Id, older.Id);
        (await gameService.GetLatestArchiveAsync(game.Id)).Id.ShouldBe(older.Id);

        await gameService.SetDefaultArchiveAsync(game.Id, null);

        var latest = await gameService.GetLatestArchiveAsync(game.Id);

        latest.Id.ShouldBe(newer.Id);
    }

    [Fact]
    public async Task SettingDefaultArchiveFromAnotherGameIsRejected()
    {
        var gameService = GetService<GameService>();
        var archiveService = GetService<ArchiveService>();
        var storageLocationService = GetService<StorageLocationService>();

        var (gameA, _, _) = await CreateGameWithTwoArchivesAsync();

        var storageLocation = await storageLocationService.DefaultAsync(StorageLocationType.Archive);

        var gameB = await gameService.AddAsync(new Game { Title = "Other Game " + Guid.NewGuid().ToString("N") });

        var archiveForGameB = await archiveService.AddAsync(new Archive
        {
            GameId = gameB.Id,
            Version = "1.0",
            ObjectKey = Guid.NewGuid().ToString(),
            StorageLocationId = storageLocation.Id,
        });

        await Should.ThrowAsync<InvalidDefaultArchiveException>(
            async () => await gameService.SetDefaultArchiveAsync(gameA.Id, archiveForGameB.Id));
    }
}
