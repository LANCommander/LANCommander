using LANCommander.Launcher.Avalonia.IntegrationTests.Helpers;
using LANCommander.Launcher.Data.Models;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Avalonia.IntegrationTests.Tests;

/// <summary>
/// Verifies the launcher's DatabaseContext + EF model configuration round-trips real
/// data. Uses EF InMemory directly (not the launcher DI graph) so a regression in the
/// model builder fails here clearly, without test-framework noise from upstream services.
/// </summary>
public class DatabaseContextIntegrationTests
{
    [Fact]
    public async Task Game_persists_with_genres_and_tags()
    {
        await using var writeDb = InMemoryDatabaseFactory.Create();

        writeDb.Games!.Add(new Game
        {
            Id     = Guid.NewGuid(),
            Title  = "Half-Life",
            Genres = [new Genre { Id = Guid.NewGuid(), Name = "FPS" }],
            Tags   = [new Tag   { Id = Guid.NewGuid(), Name = "Classic" }],
        });

        await writeDb.SaveChangesAsync();

        // Same in-memory database name in this scope — would not be the case across calls.
        var game = await writeDb.Games!
            .Include(g => g.Genres)
            .Include(g => g.Tags)
            .SingleAsync();

        game.Title.ShouldBe("Half-Life");
        game.Genres!.Single().Name.ShouldBe("FPS");
        game.Tags!.Single().Name.ShouldBe("Classic");
    }

    [Fact]
    public async Task GetImportedOnMapAsync_query_shape_returns_only_known_ids()
    {
        await using var db = InMemoryDatabaseFactory.Create();

        var known     = Guid.NewGuid();
        var alsoKnown = Guid.NewGuid();
        var unknown   = Guid.NewGuid();

        db.Games!.AddRange(
            new Game { Id = known,          Title = "Doom",  ImportedOn = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
            new Game { Id = alsoKnown,      Title = "Quake", ImportedOn = new DateTime(2024, 2, 2, 0, 0, 0, DateTimeKind.Utc) },
            new Game { Id = Guid.NewGuid(), Title = "Decoy", ImportedOn = new DateTime(2024, 3, 3, 0, 0, 0, DateTimeKind.Utc) });
        await db.SaveChangesAsync();

        var ids = new[] { known, alsoKnown, unknown }.ToHashSet();
        var map = await db.Games!
            .Where(g => ids.Contains(g.Id))
            .Select(g => new { g.Id, g.ImportedOn })
            .ToDictionaryAsync(g => g.Id, g => g.ImportedOn);

        map.Count.ShouldBe(2);
        map.ShouldContainKey(known);
        map.ShouldContainKey(alsoKnown);
        map.ShouldNotContainKey(unknown);
    }

    [Fact]
    public async Task Each_factory_call_returns_isolated_database()
    {
        await using var first  = InMemoryDatabaseFactory.Create();
        await using var second = InMemoryDatabaseFactory.Create();

        first.Games!.Add(new Game { Id = Guid.NewGuid(), Title = "Half-Life" });
        await first.SaveChangesAsync();

        (await second.Games!.AnyAsync()).ShouldBeFalse(
            customMessage: "Two factory calls must produce isolated databases — otherwise tests bleed into each other.");
    }
}
