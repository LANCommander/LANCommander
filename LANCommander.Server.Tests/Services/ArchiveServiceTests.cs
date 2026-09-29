using LANCommander.SDK.Enums;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

[Collection("Application")]
public class ArchiveServiceTests(ApplicationFixture fixture) : BaseTest(fixture)
{
    // The Games list's header reports the library's size: each game's newest archive, not every
    // version it ever had, and nothing for archives that belong to tools or redistributables.
    [Fact]
    public async Task LibrarySizeCountsEachGamesLatestArchiveOnly()
    {
        await EnsureStorageLocationsExistAsync();

        var archiveService = GetService<ArchiveService>();
        var contextFactory = GetService<IDbContextFactory<DatabaseContext>>();

        var before = await archiveService.GetLibrarySizeAsync();

        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            var storageLocation = await context.Set<StorageLocation>().FirstAsync(s => s.Type == StorageLocationType.Archive);

            var versioned = new Game { Id = Guid.NewGuid(), Title = "Library Size Versioned" };
            var single = new Game { Id = Guid.NewGuid(), Title = "Library Size Single" };

            context.AddRange(versioned, single);

            Archive ArchiveOf(Guid? gameId, long size, DateTime createdOn) => new()
            {
                Id = Guid.NewGuid(),
                GameId = gameId,
                StorageLocationId = storageLocation.Id,
                ObjectKey = Guid.NewGuid().ToString(),
                Version = createdOn.ToString("yyyyMMdd"),
                CompressedSize = size,
                CreatedOn = createdOn,
            };

            context.AddRange(
                ArchiveOf(versioned.Id, 1_000, new DateTime(2026, 1, 1)),
                ArchiveOf(versioned.Id, 2_500, new DateTime(2026, 3, 1)),
                ArchiveOf(versioned.Id, 1_750, new DateTime(2026, 2, 1)),
                ArchiveOf(single.Id, 400, new DateTime(2026, 1, 1)),
                // Not a game's: a tool's or redistributable's archive has no GameId
                ArchiveOf(null, 99_999, new DateTime(2026, 1, 1)));

            await context.SaveChangesAsync();
        }

        (await archiveService.GetLibrarySizeAsync()).ShouldBe(before + 2_500 + 400);
    }
}
