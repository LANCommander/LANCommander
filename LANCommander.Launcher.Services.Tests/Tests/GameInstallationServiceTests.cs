using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Services.Tests.Helpers;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Services.Tests.Tests;

/// <summary>
/// Covers the invariants <see cref="GameInstallationService"/> owns that the database can't fully
/// enforce on its own: install directory uniqueness, at most one selected installation per game
/// (with Game.SelectedInstallationId kept in sync), selection fallback on delete, and collision-safe
/// sibling directory naming for additional side-by-side installations.
/// </summary>
public class GameInstallationServiceTests
{
    private static GameInstallation MakeInstallation(Guid gameId, string installDirectory, string? version = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            InstallDirectory = installDirectory,
            Version = version,
            InstalledOn = DateTime.UtcNow,
        };

    [Fact]
    public async Task AddInstallationAsync_first_installation_is_always_selected()
    {
        await using var context = InMemoryDatabaseFactory.Create();
        var service = new GameInstallationService(context, NullLogger<GameInstallationService>.Instance);

        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life" };
        context.Games!.Add(game);
        await context.SaveChangesAsync();

        var installation = MakeInstallation(game.Id, @"C:\Games\HalfLife", "1.0.0");

        await service.AddInstallationAsync(installation, select: false);

        installation.IsSelected.ShouldBeTrue();

        var reloadedGame = await context.Games!.FindAsync(game.Id);
        reloadedGame!.SelectedInstallationId.ShouldBe(installation.Id);
    }

    [Fact]
    public async Task AddInstallationAsync_supports_multiple_side_by_side_installations()
    {
        await using var context = InMemoryDatabaseFactory.Create();
        var service = new GameInstallationService(context, NullLogger<GameInstallationService>.Instance);

        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life" };
        context.Games!.Add(game);
        await context.SaveChangesAsync();

        var first = MakeInstallation(game.Id, @"C:\Games\HalfLife", "1.0.0");
        var second = MakeInstallation(game.Id, @"C:\Games\HalfLife (1.1.0)", "1.1.0");

        await service.AddInstallationAsync(first);
        await service.AddInstallationAsync(second, select: false);

        var installations = await service.GetInstallationsForGameAsync(game.Id);
        installations.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AddInstallationAsync_enforces_at_most_one_selected_installation_per_game()
    {
        await using var context = InMemoryDatabaseFactory.Create();
        var service = new GameInstallationService(context, NullLogger<GameInstallationService>.Instance);

        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life" };
        context.Games!.Add(game);
        await context.SaveChangesAsync();

        var first = MakeInstallation(game.Id, @"C:\Games\HalfLife", "1.0.0");
        var second = MakeInstallation(game.Id, @"C:\Games\HalfLife (1.1.0)", "1.1.0");

        await service.AddInstallationAsync(first);
        await service.AddInstallationAsync(second, select: true);

        var installations = await service.GetInstallationsForGameAsync(game.Id);
        installations.Count(i => i.IsSelected).ShouldBe(1);
        installations.Single(i => i.IsSelected).Id.ShouldBe(second.Id);

        var reloadedGame = await context.Games!.FindAsync(game.Id);
        reloadedGame!.SelectedInstallationId.ShouldBe(second.Id);
    }

    [Fact]
    public async Task SelectInstallationAsync_moves_selection_and_updates_game_pointer()
    {
        await using var context = InMemoryDatabaseFactory.Create();
        var service = new GameInstallationService(context, NullLogger<GameInstallationService>.Instance);

        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life" };
        context.Games!.Add(game);
        await context.SaveChangesAsync();

        var first = MakeInstallation(game.Id, @"C:\Games\HalfLife", "1.0.0");
        var second = MakeInstallation(game.Id, @"C:\Games\HalfLife (1.1.0)", "1.1.0");

        await service.AddInstallationAsync(first);
        await service.AddInstallationAsync(second, select: false);

        await service.SelectInstallationAsync(second.Id);

        var selected = await service.GetSelectedInstallationAsync(game.Id);
        selected!.Id.ShouldBe(second.Id);

        var reloadedGame = await context.Games!.FindAsync(game.Id);
        reloadedGame!.SelectedInstallationId.ShouldBe(second.Id);
    }

    [Fact]
    public async Task DeleteInstallationAsync_selects_a_remaining_installation_when_the_selected_one_is_removed()
    {
        await using var context = InMemoryDatabaseFactory.Create();
        var service = new GameInstallationService(context, NullLogger<GameInstallationService>.Instance);

        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life" };
        context.Games!.Add(game);
        await context.SaveChangesAsync();

        var first = MakeInstallation(game.Id, @"C:\Games\HalfLife", "1.0.0");
        var second = MakeInstallation(game.Id, @"C:\Games\HalfLife (1.1.0)", "1.1.0");

        await service.AddInstallationAsync(first);
        await service.AddInstallationAsync(second, select: true);

        await service.DeleteInstallationAsync(second.Id);

        var remaining = await service.GetInstallationsForGameAsync(game.Id);
        remaining.Count.ShouldBe(1);
        remaining.Single().Id.ShouldBe(first.Id);
        remaining.Single().IsSelected.ShouldBeTrue();

        var reloadedGame = await context.Games!.FindAsync(game.Id);
        reloadedGame!.SelectedInstallationId.ShouldBe(first.Id);
    }

    [Fact]
    public async Task DeleteInstallationAsync_clears_game_selection_when_no_installations_remain()
    {
        await using var context = InMemoryDatabaseFactory.Create();
        var service = new GameInstallationService(context, NullLogger<GameInstallationService>.Instance);

        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life" };
        context.Games!.Add(game);
        await context.SaveChangesAsync();

        var only = MakeInstallation(game.Id, @"C:\Games\HalfLife", "1.0.0");
        await service.AddInstallationAsync(only);

        await service.DeleteInstallationAsync(only.Id);

        (await service.GetInstallationsForGameAsync(game.Id)).ShouldBeEmpty();

        var reloadedGame = await context.Games!.FindAsync(game.Id);
        reloadedGame!.SelectedInstallationId.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteInstallationAsync_leaves_selection_untouched_when_deleting_a_non_selected_installation()
    {
        await using var context = InMemoryDatabaseFactory.Create();
        var service = new GameInstallationService(context, NullLogger<GameInstallationService>.Instance);

        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life" };
        context.Games!.Add(game);
        await context.SaveChangesAsync();

        var first = MakeInstallation(game.Id, @"C:\Games\HalfLife", "1.0.0");
        var second = MakeInstallation(game.Id, @"C:\Games\HalfLife (1.1.0)", "1.1.0");

        await service.AddInstallationAsync(first);
        await service.AddInstallationAsync(second, select: false);

        await service.DeleteInstallationAsync(second.Id);

        var reloadedGame = await context.Games!.FindAsync(game.Id);
        reloadedGame!.SelectedInstallationId.ShouldBe(first.Id);
    }

    [Fact]
    public async Task AddInstallationAsync_rejects_a_directory_already_used_by_another_installation()
    {
        await using var context = InMemoryDatabaseFactory.Create();
        var service = new GameInstallationService(context, NullLogger<GameInstallationService>.Instance);

        var gameA = new Game { Id = Guid.NewGuid(), Title = "Half-Life" };
        var gameB = new Game { Id = Guid.NewGuid(), Title = "Half-Life 2" };
        context.Games!.AddRange(gameA, gameB);
        await context.SaveChangesAsync();

        await service.AddInstallationAsync(MakeInstallation(gameA.Id, @"C:\Games\Shared"));

        await Should.ThrowAsync<InvalidOperationException>(
            () => service.AddInstallationAsync(MakeInstallation(gameB.Id, @"C:\Games\Shared")));
    }

    [Fact]
    public async Task AddInstallationAsync_rejects_a_directory_differing_only_by_case()
    {
        await using var context = InMemoryDatabaseFactory.Create();
        var service = new GameInstallationService(context, NullLogger<GameInstallationService>.Instance);

        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life" };
        context.Games!.Add(game);
        await context.SaveChangesAsync();

        await service.AddInstallationAsync(MakeInstallation(game.Id, @"C:\Games\HalfLife"));

        await Should.ThrowAsync<InvalidOperationException>(
            () => service.AddInstallationAsync(MakeInstallation(game.Id, @"c:\games\halflife")));
    }

    [Fact]
    public async Task GenerateInstallDirectoryAsync_keeps_the_base_directory_for_the_first_installation()
    {
        await using var context = InMemoryDatabaseFactory.Create();
        var service = new GameInstallationService(context, NullLogger<GameInstallationService>.Instance);

        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life" };
        context.Games!.Add(game);
        await context.SaveChangesAsync();

        var directory = await service.GenerateInstallDirectoryAsync(game.Id, @"C:\Games\HalfLife", "1.0.0");

        directory.ShouldBe(@"C:\Games\HalfLife");
    }

    [Fact]
    public async Task GenerateInstallDirectoryAsync_generates_a_sanitized_sibling_for_additional_installations()
    {
        await using var context = InMemoryDatabaseFactory.Create();
        var service = new GameInstallationService(context, NullLogger<GameInstallationService>.Instance);

        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life" };
        context.Games!.Add(game);
        await context.SaveChangesAsync();

        await service.AddInstallationAsync(MakeInstallation(game.Id, @"C:\Games\HalfLife", "1.0.0"));

        var directory = await service.GenerateInstallDirectoryAsync(game.Id, @"C:\Games\HalfLife", "1.1.0:Beta");

        // ':' is not a valid Windows filename character and must be sanitized out of the suffix.
        directory.ShouldBe(@"C:\Games\HalfLife (1.1.0Beta)");
    }

    [Fact]
    public async Task GenerateInstallDirectoryAsync_numerically_disambiguates_when_the_sibling_name_collides()
    {
        await using var context = InMemoryDatabaseFactory.Create();
        var service = new GameInstallationService(context, NullLogger<GameInstallationService>.Instance);

        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life" };
        context.Games!.Add(game);
        await context.SaveChangesAsync();

        await service.AddInstallationAsync(MakeInstallation(game.Id, @"C:\Games\HalfLife", "1.0.0"));
        await service.AddInstallationAsync(
            MakeInstallation(game.Id, @"C:\Games\HalfLife (1.1.0)", "1.1.0"), select: false);

        var directory = await service.GenerateInstallDirectoryAsync(game.Id, @"C:\Games\HalfLife", "1.1.0");

        directory.ShouldBe(@"C:\Games\HalfLife (1.1.0) (2)");
    }

    [Fact]
    public async Task GenerateInstallDirectoryAsync_avoids_reserved_directories_not_yet_persisted()
    {
        // Simulates two pending Add() calls for the same (blank-version) side-by-side install
        // racing before either has persisted its own GameInstallation row: without passing the
        // first call's already-computed candidate as "reserved", the second call would compute
        // the exact same sibling directory since IsInstallDirectoryInUseAsync alone can't see it
        // yet.
        await using var context = InMemoryDatabaseFactory.Create();
        var service = new GameInstallationService(context, NullLogger<GameInstallationService>.Instance);

        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life" };
        context.Games!.Add(game);
        await context.SaveChangesAsync();

        await service.AddInstallationAsync(MakeInstallation(game.Id, @"C:\Games\HalfLife", "1.0.0"));

        var firstPendingCandidate = await service.GenerateInstallDirectoryAsync(game.Id, @"C:\Games\HalfLife", version: null);

        var secondCandidate = await service.GenerateInstallDirectoryAsync(
            game.Id,
            @"C:\Games\HalfLife",
            version: null,
            reservedDirectories: new[] { firstPendingCandidate });

        secondCandidate.ShouldNotBe(firstPendingCandidate);
        secondCandidate.ShouldBe(@"C:\Games\HalfLife (New Install) (2)");
    }

    [Fact]
    public async Task GenerateInstallDirectoryAsync_reservation_also_applies_to_the_natural_first_install_path()
    {
        // A brand-new game (no installations yet) whose natural directory is reserved by another
        // pending, not-yet-persisted request must also be diverted — not just the sibling-suffix
        // path.
        await using var context = InMemoryDatabaseFactory.Create();
        var service = new GameInstallationService(context, NullLogger<GameInstallationService>.Instance);

        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life" };
        context.Games!.Add(game);
        await context.SaveChangesAsync();

        var directory = await service.GenerateInstallDirectoryAsync(
            game.Id,
            @"C:\Games\HalfLife",
            version: null,
            reservedDirectories: new[] { @"C:\Games\HalfLife" });

        directory.ShouldNotBe(@"C:\Games\HalfLife");
        directory.ShouldBe(@"C:\Games\HalfLife (New Install)");
    }
}
