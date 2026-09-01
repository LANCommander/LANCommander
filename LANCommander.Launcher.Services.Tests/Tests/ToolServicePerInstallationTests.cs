using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Services.Tests.Helpers;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Services.Tests.Tests;

/// <summary>
/// Covers per-installation tool tracking (GameInstallationTool, the canonical state now that a
/// game can have several side-by-side installations) and its mirroring onto the legacy per-game
/// GameTool rows for transitional callers — mirroring only ever reflects the game's currently
/// selected installation.
/// </summary>
public class ToolServicePerInstallationTests
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
    public async Task Tool_installed_for_one_installation_is_not_visible_on_a_sibling_installation()
    {
        await using var context = InMemoryDatabaseFactory.Create();
        var installationService = ServiceTestFactory.CreateGameInstallationService(context);
        var toolService = ServiceTestFactory.CreateToolService(context);

        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life" };
        var tool = new Tool { Id = Guid.NewGuid(), Name = "Hex Editor" };
        context.Games!.Add(game);
        context.Set<Tool>().Add(tool);
        await context.SaveChangesAsync();

        var installationA = MakeInstallation(game.Id, @"C:\Games\HalfLife");
        var installationB = MakeInstallation(game.Id, @"C:\Games\HalfLife (1.1.0)");
        await installationService.AddInstallationAsync(installationA);
        await installationService.AddInstallationAsync(installationB, select: false);

        await toolService.SetToolInstalledForInstallationAsync(installationA.Id, game.Id, tool.Id, installationA.InstallDirectory, "1.0");

        (await toolService.IsToolInstalledForInstallationAsync(installationA.Id, tool.Id)).ShouldBeTrue();
        (await toolService.IsToolInstalledForInstallationAsync(installationB.Id, tool.Id)).ShouldBeFalse();

        var installedForA = await toolService.GetInstalledToolsForInstallationAsync(installationA.Id);
        installedForA.ShouldContain(t => t.ToolId == tool.Id);

        var installedForB = await toolService.GetInstalledToolsForInstallationAsync(installationB.Id);
        installedForB.ShouldBeEmpty();
    }

    [Fact]
    public async Task Legacy_GameTool_mirror_only_reflects_the_selected_installations_tool_state()
    {
        await using var context = InMemoryDatabaseFactory.Create();
        var installationService = ServiceTestFactory.CreateGameInstallationService(context);
        var toolService = ServiceTestFactory.CreateToolService(context);

        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life" };
        var tool = new Tool { Id = Guid.NewGuid(), Name = "Hex Editor" };
        context.Games!.Add(game);
        context.Set<Tool>().Add(tool);
        await context.SaveChangesAsync();

        var installationA = MakeInstallation(game.Id, @"C:\Games\HalfLife");
        var installationB = MakeInstallation(game.Id, @"C:\Games\HalfLife (1.1.0)");
        await installationService.AddInstallationAsync(installationA);
        await installationService.AddInstallationAsync(installationB, select: true); // B is selected

        // Installed only for A (not selected) — the legacy per-game mirror must NOT show it as
        // installed, since that mirror represents "the selected installation" for transitional
        // callers (action bar, tool list) that only know about one game-wide tool state.
        await toolService.SetToolInstalledForInstallationAsync(installationA.Id, game.Id, tool.Id, installationA.InstallDirectory, "1.0");

        (await toolService.IsToolInstalledForGameAsync(game.Id, tool.Id)).ShouldBeFalse();

        // Installed for B (the selected installation) — the mirror must now reflect it.
        await toolService.SetToolInstalledForInstallationAsync(installationB.Id, game.Id, tool.Id, installationB.InstallDirectory, "1.0");

        (await toolService.IsToolInstalledForGameAsync(game.Id, tool.Id)).ShouldBeTrue();
        var installedGameTools = await toolService.GetInstalledToolsForGameAsync(game.Id);
        installedGameTools.Single(t => t.ToolId == tool.Id).InstallDirectory.ShouldBe(installationB.InstallDirectory);
    }

    [Fact]
    public async Task Uninstalling_a_tool_from_one_installation_leaves_the_other_installations_copy_installed()
    {
        await using var context = InMemoryDatabaseFactory.Create();
        var installationService = ServiceTestFactory.CreateGameInstallationService(context);
        var toolService = ServiceTestFactory.CreateToolService(context);

        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life" };
        var tool = new Tool { Id = Guid.NewGuid(), Name = "Hex Editor" };
        context.Games!.Add(game);
        context.Set<Tool>().Add(tool);
        await context.SaveChangesAsync();

        var installationA = MakeInstallation(game.Id, @"C:\Games\HalfLife");
        var installationB = MakeInstallation(game.Id, @"C:\Games\HalfLife (1.1.0)");
        await installationService.AddInstallationAsync(installationA);
        await installationService.AddInstallationAsync(installationB, select: false);

        await toolService.SetToolInstalledForInstallationAsync(installationA.Id, game.Id, tool.Id, installationA.InstallDirectory, "1.0");
        await toolService.SetToolInstalledForInstallationAsync(installationB.Id, game.Id, tool.Id, installationB.InstallDirectory, "1.0");

        await toolService.SetToolUninstalledForInstallationAsync(installationA.Id, game.Id, tool.Id);

        (await toolService.IsToolInstalledForInstallationAsync(installationA.Id, tool.Id)).ShouldBeFalse();
        (await toolService.IsToolInstalledForInstallationAsync(installationB.Id, tool.Id)).ShouldBeTrue();
    }
}
