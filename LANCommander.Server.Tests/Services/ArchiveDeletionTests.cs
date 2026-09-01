using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Exceptions;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

/// <summary>
/// Phase 5 (admin UX) service-level backstop: an archive that is a game's explicit
/// <see cref="Game.DefaultArchiveId"/> cannot be deleted until the default is cleared or
/// reassigned. This must hold even when a caller bypasses the admin UI (ArchiveEditor) and
/// calls <see cref="ArchiveService"/> directly. Deleting the owning game entirely remains
/// unaffected, since the default pointer is cleared as part of that deletion.
/// </summary>
[Collection("Application")]
public class ArchiveDeletionTests(ApplicationFixture fixture) : BaseTest(fixture)
{
    private async Task<(Game Game, Archive Older, Archive Newer)> CreateGameWithTwoArchivesAsync()
    {
        var gameService = GetService<GameService>();
        var archiveService = GetService<ArchiveService>();
        var storageLocationService = GetService<StorageLocationService>();

        await EnsureStorageLocationsExistAsync();

        var storageLocation = await storageLocationService.DefaultAsync(StorageLocationType.Archive);

        var game = await gameService.AddAsync(new Game { Title = "Archive Deletion Test Game " + Guid.NewGuid().ToString("N") });

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

        return (game, older, newer);
    }

    [Fact]
    public async Task DeletingExplicitDefaultArchiveIsRejected()
    {
        var gameService = GetService<GameService>();
        var archiveService = GetService<ArchiveService>();
        var (game, older, _) = await CreateGameWithTwoArchivesAsync();

        await gameService.SetDefaultArchiveAsync(game.Id, older.Id);

        await Should.ThrowAsync<CannotDeleteDefaultArchiveException>(
            async () => await archiveService.DeleteAsync(older));

        // The archive must still exist afterward - the rejection must not have partially deleted it.
        (await archiveService.AsNoTracking().GetAsync(older.Id)).ShouldNotBeNull();
    }

    [Fact]
    public async Task DeletingNonDefaultArchiveStillSucceeds()
    {
        var gameService = GetService<GameService>();
        var archiveService = GetService<ArchiveService>();
        var (game, older, newer) = await CreateGameWithTwoArchivesAsync();

        // "newer" is only the *effective* default via latest-by-date fallback - no explicit
        // default is set at all, so deleting the other (non-default) archive must succeed.
        await archiveService.DeleteAsync(older);

        (await archiveService.AsNoTracking().GetAsync(older.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task DeletingArchiveAfterClearingDefaultSucceeds()
    {
        var gameService = GetService<GameService>();
        var archiveService = GetService<ArchiveService>();
        var (game, older, _) = await CreateGameWithTwoArchivesAsync();

        await gameService.SetDefaultArchiveAsync(game.Id, older.Id);
        await gameService.SetDefaultArchiveAsync(game.Id, null);

        await archiveService.DeleteAsync(older);

        (await archiveService.AsNoTracking().GetAsync(older.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task DeletingArchiveAfterReassigningDefaultElsewhereSucceeds()
    {
        var gameService = GetService<GameService>();
        var archiveService = GetService<ArchiveService>();
        var (game, older, newer) = await CreateGameWithTwoArchivesAsync();

        await gameService.SetDefaultArchiveAsync(game.Id, older.Id);
        await gameService.SetDefaultArchiveAsync(game.Id, newer.Id);

        // The default moved to "newer"; "older" is no longer anyone's default and can be deleted.
        await archiveService.DeleteAsync(older);

        (await archiveService.AsNoTracking().GetAsync(older.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task DeletingArchiveWithStorageLocationOverloadIsAlsoRejectedForExplicitDefault()
    {
        var gameService = GetService<GameService>();
        var archiveService = GetService<ArchiveService>();
        var storageLocationService = GetService<StorageLocationService>();
        var (game, older, _) = await CreateGameWithTwoArchivesAsync();

        await gameService.SetDefaultArchiveAsync(game.Id, older.Id);

        var storageLocation = await storageLocationService.DefaultAsync(StorageLocationType.Archive);

        await Should.ThrowAsync<CannotDeleteDefaultArchiveException>(
            async () => await archiveService.DeleteAsync(older, storageLocation));
    }

    [Fact]
    public async Task DeletingGameWithExplicitDefaultArchiveStillRemovesAllArchives()
    {
        var gameService = GetService<GameService>();
        var archiveService = GetService<ArchiveService>();
        var (game, older, newer) = await CreateGameWithTwoArchivesAsync();

        await gameService.SetDefaultArchiveAsync(game.Id, older.Id);

        // Deleting the whole game must not be blocked by the archive-delete guard: the game
        // (and its DefaultArchiveId) is being removed anyway, so GameService clears the
        // default first as a deliberate part of that deletion.
        await gameService.DeleteAsync(game);

        (await archiveService.AsNoTracking().GetAsync(older.Id)).ShouldBeNull();
        (await archiveService.AsNoTracking().GetAsync(newer.Id)).ShouldBeNull();
        (await gameService.AsNoTracking().GetAsync(game.Id)).ShouldBeNull();
    }
}
