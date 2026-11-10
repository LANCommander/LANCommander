using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Services.Tests.Helpers;
using LANCommander.SDK.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;
using Manifest = LANCommander.SDK.Models.Manifest;

namespace LANCommander.Launcher.Services.Tests.Tests;

/// <summary>
/// The launcher imported multiplayer modes as shared records matched on type and protocol only, and
/// never attached them to a game: every game's TCP/IP LAN mode updated the same row, two modes of one
/// type collapsed, and no game had any modes for the player-count filters to find.
/// </summary>
public class GameMultiplayerModeSyncTests
{
    private static DatabaseContext CreateContext() =>
        new(
            NullLoggerFactory.Instance,
            new DbContextOptionsBuilder()
                .UseInMemoryDatabase($"MultiplayerModeSync-{Guid.NewGuid()}")
                .Options);

    // Only the database is exercised; the rest of the graph is null!
    private static GameService CreateSubject(DatabaseContext context) =>
        new(context, NullLogger<GameService>.Instance, null!, null!, null!, null!, null!, null!, null!, null!, null!);

    private static async Task<Game> SeedGameAsync(DatabaseContext context, string title)
    {
        var game = GameFactory.Make(title);

        context.Games!.Add(game);
        await context.SaveChangesAsync();

        return game;
    }

    private static Manifest.MultiplayerMode Lan(string description, int maxPlayers) => new()
    {
        Type = MultiplayerType.LAN,
        NetworkProtocol = NetworkProtocol.TCPIP,
        Description = description,
        MinPlayers = 2,
        MaxPlayers = maxPlayers,
    };

    private static async Task<List<MultiplayerMode>> ModesAsync(DatabaseContext context, Guid gameId)
    {
        context.ChangeTracker.Clear();

        return await context.Set<MultiplayerMode>().Where(m => m.GameId == gameId).ToListAsync();
    }

    [Fact]
    public async Task Sync_stores_every_mode_of_the_same_type_against_the_game()
    {
        await using var context = CreateContext();
        var game = await SeedGameAsync(context, "Age of Empires II");

        await CreateSubject(context).SyncMultiplayerModesAsync(game.Id, [Lan("Versus", 8), Lan("Co-op", 4)]);

        var modes = await ModesAsync(context, game.Id);
        modes.Select(m => m.Description).ShouldBe(["Co-op", "Versus"], ignoreOrder: true);
    }

    [Fact]
    public async Task Games_with_the_same_mode_each_keep_their_own()
    {
        await using var context = CreateContext();
        var first = await SeedGameAsync(context, "Quake III Arena");
        var second = await SeedGameAsync(context, "Unreal Tournament");
        var subject = CreateSubject(context);

        await subject.SyncMultiplayerModesAsync(first.Id, [Lan("Deathmatch", 16)]);
        await subject.SyncMultiplayerModesAsync(second.Id, [Lan("Deathmatch", 16)]);

        (await ModesAsync(context, first.Id)).Count.ShouldBe(1);
        (await ModesAsync(context, second.Id)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Resyncing_unchanged_modes_leaves_them_alone()
    {
        await using var context = CreateContext();
        var game = await SeedGameAsync(context, "StarCraft");
        var subject = CreateSubject(context);
        Manifest.MultiplayerMode[] modes = [Lan("Versus", 8), Lan("Co-op", 4)];

        await subject.SyncMultiplayerModesAsync(game.Id, modes);
        var before = (await ModesAsync(context, game.Id)).Select(m => m.Id).ToList();

        (await subject.MultiplayerModesDifferAsync(game.Id, modes)).ShouldBeFalse();

        await subject.SyncMultiplayerModesAsync(game.Id, modes);

        (await ModesAsync(context, game.Id)).Select(m => m.Id).ShouldBe(before, ignoreOrder: true);
    }

    [Fact]
    public async Task Edited_and_removed_modes_are_replaced_and_dropped()
    {
        await using var context = CreateContext();
        var game = await SeedGameAsync(context, "Warcraft III");
        var subject = CreateSubject(context);

        await subject.SyncMultiplayerModesAsync(game.Id, [Lan("Versus", 8), Lan("Co-op", 4)]);

        Manifest.MultiplayerMode[] updated = [Lan("Versus", 12)];

        (await subject.MultiplayerModesDifferAsync(game.Id, updated)).ShouldBeTrue();

        await subject.SyncMultiplayerModesAsync(game.Id, updated);

        var modes = await ModesAsync(context, game.Id);
        modes.Count.ShouldBe(1);
        modes[0].MaxPlayers.ShouldBe(12);
    }

    [Fact]
    public async Task A_game_imported_without_its_modes_is_seen_as_changed()
    {
        await using var context = CreateContext();
        var game = await SeedGameAsync(context, "Half-Life");

        (await CreateSubject(context).MultiplayerModesDifferAsync(game.Id, [Lan("Deathmatch", 16)])).ShouldBeTrue();
    }

    [Fact]
    public async Task Sync_clears_modes_left_without_a_game_by_older_launchers()
    {
        await using var context = CreateContext();
        var game = await SeedGameAsync(context, "Doom");

        context.Set<MultiplayerMode>().Add(new MultiplayerMode { Type = MultiplayerType.LAN, NetworkProtocol = NetworkProtocol.TCPIP });
        await context.SaveChangesAsync();

        await CreateSubject(context).SyncMultiplayerModesAsync(game.Id, [Lan("Deathmatch", 4)]);

        context.ChangeTracker.Clear();
        (await context.Set<MultiplayerMode>().CountAsync(m => m.GameId == null)).ShouldBe(0);
    }
}
