using LANCommander.SDK.Enums;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

[Collection("Application")]
public class MediaServiceTests(ApplicationFixture fixture) : BaseTest(fixture)
{
    // Media is written through its own service rather than through the game, so the launcher's
    // change detection (which keys off Game.UpdatedOn) only sees media edits if the media service
    // touches the owning game.
    [Fact]
    public async Task AddingMediaTouchesTheOwningGame()
    {
        await EnsureStorageLocationsExistAsync();

        var gameService = GetService<GameService>();
        var mediaService = GetService<MediaService>();

        var game = await gameService.AddAsync(new Game { Title = "Media Add Touch" });

        var stale = await MakeGameStaleAsync(game.Id);

        await mediaService.AddAsync(new Media
        {
            GameId = game.Id,
            Type = MediaType.Cover,
            Crc32 = string.Empty,
            StorageLocation = await mediaService.GetDefaultStorageLocationAsync(),
        });

        (await GetGameUpdatedOnAsync(game.Id)).ShouldBeGreaterThan(stale);
    }

    [Fact]
    public async Task UpdatingMediaTouchesTheOwningGame()
    {
        await EnsureStorageLocationsExistAsync();

        var gameService = GetService<GameService>();
        var mediaService = GetService<MediaService>();

        var game = await gameService.AddAsync(new Game { Title = "Media Update Touch" });

        var media = await mediaService.AddAsync(new Media
        {
            GameId = game.Id,
            Type = MediaType.Cover,
            Crc32 = string.Empty,
            StorageLocation = await mediaService.GetDefaultStorageLocationAsync(),
        });

        var stale = await MakeGameStaleAsync(game.Id);

        media.Crc32 = "deadbeef";

        await mediaService.UpdateAsync(media);

        (await GetGameUpdatedOnAsync(game.Id)).ShouldBeGreaterThan(stale);
    }

    [Fact]
    public async Task DeletingMediaTouchesTheOwningGame()
    {
        await EnsureStorageLocationsExistAsync();

        var gameService = GetService<GameService>();
        var mediaService = GetService<MediaService>();

        var game = await gameService.AddAsync(new Game { Title = "Media Delete Touch" });

        var media = await mediaService.AddAsync(new Media
        {
            GameId = game.Id,
            Type = MediaType.Cover,
            Crc32 = string.Empty,
            StorageLocation = await mediaService.GetDefaultStorageLocationAsync(),
        });

        var stale = await MakeGameStaleAsync(game.Id);

        await mediaService.DeleteAsync(media);

        (await GetGameUpdatedOnAsync(game.Id)).ShouldBeGreaterThan(stale);
    }

    /// <summary>
    /// Backdates the game's modified timestamp so the assertion does not depend on the wall clock
    /// advancing between two operations that run microseconds apart.
    /// </summary>
    private async Task<DateTime> MakeGameStaleAsync(Guid gameId)
    {
        var stale = DateTime.UtcNow.AddDays(-1);

        var contextFactory = GetService<IDbContextFactory<DatabaseContext>>();

        await using var context = await contextFactory.CreateDbContextAsync();

        var game = await context.Games!.FirstAsync(g => g.Id == gameId);

        game.UpdatedOn = stale;

        await context.SaveChangesAsync();

        return stale;
    }

    private async Task<DateTime> GetGameUpdatedOnAsync(Guid gameId)
    {
        var contextFactory = GetService<IDbContextFactory<DatabaseContext>>();

        await using var context = await contextFactory.CreateDbContextAsync();

        return await context.Games!
            .AsNoTracking()
            .Where(g => g.Id == gameId)
            .Select(g => g.UpdatedOn)
            .FirstAsync();
    }
}
