using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Services.Tests.Helpers;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Services.Tests.Tests;

/// <summary>
/// Covers GameService's installation-scoped uninstall and the pure directory-resolution helper
/// backing Run(): uninstalling one installation must remove only that installation (files, DB row,
/// per-installation tool state) and leave any sibling installation of the same game completely
/// untouched, with a sensible fallback selection when the removed installation was selected.
///
/// GameClient here is a real instance built with null dependencies (see ServiceTestFactory) —
/// UninstallAsync's early-return path (no on-disk manifest found at the given directory) never
/// dereferences any of them, so this exercises the real production code end to end without a
/// network or mocking framework.
/// </summary>
public class GameServiceInstallationTests
{
    private static GameInstallation MakeInstallation(Guid gameId, string installDirectory) =>
        new()
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            InstallDirectory = installDirectory,
            InstalledOn = DateTime.UtcNow,
        };

    [Fact]
    public async Task UninstallAsync_removes_only_the_targeted_installation_and_keeps_the_other_selected_and_intact()
    {
        await using var context = InMemoryDatabaseFactory.Create();
        var installationService = ServiceTestFactory.CreateGameInstallationService(context);
        var toolService = ServiceTestFactory.CreateToolService(context);
        var gameService = ServiceTestFactory.CreateGameService(context, toolService, installationService);

        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life", Installed = true };
        context.Games!.Add(game);
        await context.SaveChangesAsync();

        var installationA = MakeInstallation(game.Id, @"C:\Games\HalfLife (1.0.0)");
        var installationB = MakeInstallation(game.Id, @"C:\Games\HalfLife (1.1.0)");
        await installationService.AddInstallationAsync(installationA);
        await installationService.AddInstallationAsync(installationB, select: true);

        await gameService.UninstallAsync(game, installationA);

        var remaining = await installationService.GetInstallationsForGameAsync(game.Id);
        remaining.Count.ShouldBe(1);
        remaining.Single().Id.ShouldBe(installationB.Id);
        remaining.Single().IsSelected.ShouldBeTrue();

        var reloadedGame = await context.Games!.FindAsync(game.Id);
        reloadedGame!.SelectedInstallationId.ShouldBe(installationB.Id);
        reloadedGame.InstallDirectory.ShouldBe(installationB.InstallDirectory);
        reloadedGame.Installed.ShouldBeTrue();
    }

    [Fact]
    public async Task UninstallAsync_falls_back_to_a_remaining_installation_when_the_selected_one_is_removed()
    {
        await using var context = InMemoryDatabaseFactory.Create();
        var installationService = ServiceTestFactory.CreateGameInstallationService(context);
        var toolService = ServiceTestFactory.CreateToolService(context);
        var gameService = ServiceTestFactory.CreateGameService(context, toolService, installationService);

        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life", Installed = true };
        context.Games!.Add(game);
        await context.SaveChangesAsync();

        var installationA = MakeInstallation(game.Id, @"C:\Games\HalfLife (1.0.0)");
        var installationB = MakeInstallation(game.Id, @"C:\Games\HalfLife (1.1.0)");
        await installationService.AddInstallationAsync(installationA);
        await installationService.AddInstallationAsync(installationB, select: true); // B selected

        // Uninstall the SELECTED installation (B) — a fallback selection must kick in automatically.
        await gameService.UninstallAsync(game, installationB);

        var remaining = await installationService.GetInstallationsForGameAsync(game.Id);
        remaining.Count.ShouldBe(1);
        remaining.Single().Id.ShouldBe(installationA.Id);
        remaining.Single().IsSelected.ShouldBeTrue();

        var reloadedGame = await context.Games!.FindAsync(game.Id);
        reloadedGame!.SelectedInstallationId.ShouldBe(installationA.Id);
        reloadedGame.InstallDirectory.ShouldBe(installationA.InstallDirectory);
    }

    [Fact]
    public async Task UninstallAsync_clears_the_game_when_the_only_installation_is_removed()
    {
        await using var context = InMemoryDatabaseFactory.Create();
        var installationService = ServiceTestFactory.CreateGameInstallationService(context);
        var toolService = ServiceTestFactory.CreateToolService(context);
        var gameService = ServiceTestFactory.CreateGameService(context, toolService, installationService);

        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life", Installed = true };
        context.Games!.Add(game);
        await context.SaveChangesAsync();

        var only = MakeInstallation(game.Id, @"C:\Games\HalfLife");
        await installationService.AddInstallationAsync(only);

        await gameService.UninstallAsync(game, only);

        (await installationService.GetInstallationsForGameAsync(game.Id)).ShouldBeEmpty();

        var reloadedGame = await context.Games!.FindAsync(game.Id);
        reloadedGame!.Installed.ShouldBeFalse();
        reloadedGame.InstallDirectory.ShouldBeNull();
        reloadedGame.SelectedInstallationId.ShouldBeNull();
    }

    [Fact]
    public async Task UninstallAsync_removes_only_the_targeted_installations_own_tool_state()
    {
        await using var context = InMemoryDatabaseFactory.Create();
        var installationService = ServiceTestFactory.CreateGameInstallationService(context);
        var toolService = ServiceTestFactory.CreateToolService(context);
        var gameService = ServiceTestFactory.CreateGameService(context, toolService, installationService);

        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life", Installed = true };
        var tool = new Tool { Id = Guid.NewGuid(), Name = "Hex Editor" };
        context.Games!.Add(game);
        context.Set<Tool>().Add(tool);
        await context.SaveChangesAsync();

        var installationA = MakeInstallation(game.Id, @"C:\Games\HalfLife (1.0.0)");
        var installationB = MakeInstallation(game.Id, @"C:\Games\HalfLife (1.1.0)");
        await installationService.AddInstallationAsync(installationA);
        await installationService.AddInstallationAsync(installationB, select: false);

        await toolService.SetToolInstalledForInstallationAsync(installationA.Id, game.Id, tool.Id, installationA.InstallDirectory, "1.0");
        await toolService.SetToolInstalledForInstallationAsync(installationB.Id, game.Id, tool.Id, installationB.InstallDirectory, "1.0");

        await gameService.UninstallAsync(game, installationA);

        (await toolService.IsToolInstalledForInstallationAsync(installationB.Id, tool.Id)).ShouldBeTrue("uninstalling installation A must not touch installation B's own tool state");
    }

    [Fact]
    public void ResolveInstallDirectory_prefers_the_given_installations_directory_over_the_legacy_field()
    {
        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life", InstallDirectory = @"C:\Legacy\HalfLife" };
        var installationA = new GameInstallation { Id = Guid.NewGuid(), GameId = game.Id, InstallDirectory = @"C:\Games\HalfLife (1.0.0)" };
        var installationB = new GameInstallation { Id = Guid.NewGuid(), GameId = game.Id, InstallDirectory = @"C:\Games\HalfLife (1.1.0)" };

        GameService.ResolveInstallDirectory(game, installationA).ShouldBe(installationA.InstallDirectory);
        GameService.ResolveInstallDirectory(game, installationB).ShouldBe(installationB.InstallDirectory);
        GameService.ResolveInstallDirectory(game, installationA).ShouldNotBe(GameService.ResolveInstallDirectory(game, installationB));
    }

    [Fact]
    public void ResolveInstallDirectory_falls_back_to_the_legacy_field_when_no_installation_is_supplied()
    {
        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life", InstallDirectory = @"C:\Legacy\HalfLife" };

        GameService.ResolveInstallDirectory(game, null).ShouldBe(@"C:\Legacy\HalfLife");
    }
}
