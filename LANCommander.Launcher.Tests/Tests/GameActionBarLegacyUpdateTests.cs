using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using LANCommander.Launcher.Services;
using LANCommander.Launcher.Tests.Helpers;
using LANCommander.Launcher.ViewModels.Components;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using SdkArchive = LANCommander.SDK.Models.Archive;
using SdkGame = LANCommander.SDK.Models.Game;

namespace LANCommander.Launcher.Tests.Tests;

/// <summary>
/// Regression coverage for the HIGH "update-available installed overlay/legacy entry cannot be
/// updated" finding, at the view-model level.
///
/// <see cref="GameActionBarViewModel.LoadInstallationsAsync"/> reports overlay add-ons
/// (Expansion/Mod/StandaloneMod installed into their base game's directory, which deliberately
/// never get a <see cref="GameInstallation"/> row) and pre-migration legacy installs as installed
/// from the legacy Game fields — so PrimaryAction routes their "update available" state into the
/// update command. That command required an installation row and threw
/// "No installation found to update", surfacing as a failure alert instead of an update.
///
/// These tests run the real command against a real service graph (EF InMemory + a
/// <see cref="GameClient"/> over a recording HTTP stack) and assert an install/update was actually
/// queued rather than failing.
///
/// They run as <c>[AvaloniaFact]</c> so the failure path — which shows an alert overlay through
/// the UI dispatcher — is executable too: a regression fails the assertion instead of deadlocking.
/// </summary>
public class GameActionBarLegacyUpdateTests
{
    private const string SharedDirectory = @"C:\Games\Half-Life";

    private sealed record Fixture(IServiceProvider Services, RecordingHttpMessageHandler Handler);

    private static Fixture BuildServices(string dbName)
    {
        var handler = new RecordingHttpMessageHandler();
        var settingsProvider = new FakeSettingsProvider();
        var services = new ServiceCollection();

        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<Data.DatabaseContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddSingleton<ISettingsProvider>(settingsProvider);
        services.AddScoped<GameInstallationService>();
        services.AddScoped<ToolService>();
        services.AddSingleton(FakeApiFactory.CreateGameClient(handler, settingsProvider));
        services.AddSingleton(new ToolClient(null!, null!, null!, null!));
        services.AddScoped(sp => new GameService(
            sp.GetRequiredService<Data.DatabaseContext>(),
            sp.GetRequiredService<ILogger<GameService>>(),
            authenticationService: null!,
            playSessionService: null!,
            profileClient: null!,
            gameClient: sp.GetRequiredService<GameClient>(),
            toolService: sp.GetRequiredService<ToolService>(),
            toolClient: sp.GetRequiredService<ToolClient>(),
            gameInstallationService: sp.GetRequiredService<GameInstallationService>(),
            connectionClient: null!,
            serviceProvider: sp));

        // A single shared InstallService so the test can inspect the queue the view model filled.
        services.AddSingleton(sp => new InstallService(
            sp.GetRequiredService<ILogger<InstallService>>(),
            sp.GetRequiredService<GameService>(),
            sp.GetRequiredService<ToolService>(),
            importService: null!,
            sp.GetRequiredService<GameInstallationService>(),
            sp.GetRequiredService<GameClient>(),
            redistributableClient: null!,
            sp.GetRequiredService<ToolClient>(),
            mediaClient: null!));

        var provider = services.BuildServiceProvider();

        // Park a permanently "active" queue item so Add() enqueues without auto-starting a real
        // download — these tests assert what gets queued, not execution.
        provider.GetRequiredService<InstallService>().Queue.Add(
            new InstallQueueGame(new SdkGame { Id = Guid.NewGuid(), Title = "Busy" })
            {
                Status = InstallStatus.Downloading,
            });

        return new Fixture(provider, handler);
    }

    private static void MapGame(RecordingHttpMessageHandler handler, SdkGame game, SdkArchive archive)
    {
        handler.MapJson(FakeApiFactory.GameRoute(game.Id), game);
        handler.MapJson(FakeApiFactory.ResolveArchiveRoute(game.Id), archive);
        handler.MapJson(FakeApiFactory.ResolveArchiveRoute(game.Id, archive.Id), archive);
    }

    private static async Task<(Guid BaseGameId, Guid OverlayId)> SeedInstalledOverlayAsync(
        Fixture fixture,
        GameType overlayType = GameType.Expansion)
    {
        var baseGameId = Guid.NewGuid();
        var overlayId = Guid.NewGuid();

        using var scope = fixture.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Data.DatabaseContext>();

        context.Games!.Add(new Game
        {
            Id = baseGameId,
            Title = "Half-Life",
            Type = GameType.MainGame,
            Installed = true,
            InstallDirectory = SharedDirectory,
        });

        context.Games!.Add(new Game
        {
            Id = overlayId,
            Title = "Opposing Force",
            Type = overlayType,
            BaseGameId = baseGameId,
            Installed = true,
            InstallDirectory = SharedDirectory,
            InstalledVersion = "1.0.0",
            InstalledOn = DateTime.UtcNow,
        });

        await context.SaveChangesAsync();

        return (baseGameId, overlayId);
    }

    private static InstallService GetInstallService(Fixture fixture) =>
        fixture.Services.GetRequiredService<InstallService>();

    private static InstallQueueGame? QueuedItemFor(Fixture fixture, Guid entityId) =>
        GetInstallService(fixture).Queue.OfType<InstallQueueGame>().FirstOrDefault(i => i.EntityId == entityId);

    [AvaloniaFact]
    public async Task UpdateGame_ForAnInstalledOverlayWithNoInstallationRow_QueuesAnInstallInsteadOfFailing()
    {
        var fixture = BuildServices($"action-bar-legacy-{Guid.NewGuid()}");
        var (baseGameId, overlayId) = await SeedInstalledOverlayAsync(fixture);
        var archiveId = Guid.NewGuid();

        MapGame(fixture.Handler,
            new SdkGame { Id = overlayId, Title = "Opposing Force", Type = GameType.Expansion, BaseGameId = baseGameId },
            new SdkArchive { Id = archiveId, Version = "2.0.0" });

        var vm = new GameActionBarViewModel(fixture.Services);
        await vm.LoadInstallationsAsync(overlayId);

        // The overlay reports installed purely from its legacy fields — no installation rows.
        Assert.True(vm.IsInstalled);
        Assert.Empty(vm.Installations);

        vm.IsUpdateAvailable = true;

        var installRequested = false;
        vm.InstallRequested += (_, _) => installRequested = true;

        await vm.UpdateGameCommand.ExecuteAsync(null);

        var queued = QueuedItemFor(fixture, overlayId);

        Assert.NotNull(queued);
        Assert.Equal(SharedDirectory, queued!.InstallDirectory);
        Assert.Equal(archiveId, queued.ArchiveId);
        Assert.Equal("Added to download queue", vm.StatusMessage);
        Assert.True(installRequested);
    }

    [AvaloniaFact]
    public async Task UpdateGame_ForAnInstalledOverlay_DoesNotReportAFailure()
    {
        var fixture = BuildServices($"action-bar-legacy-{Guid.NewGuid()}");
        var (baseGameId, overlayId) = await SeedInstalledOverlayAsync(fixture, GameType.Mod);
        var archiveId = Guid.NewGuid();

        MapGame(fixture.Handler,
            new SdkGame { Id = overlayId, Title = "Opposing Force", Type = GameType.Mod, BaseGameId = baseGameId },
            new SdkArchive { Id = archiveId, Version = "2.0.0" });

        var vm = new GameActionBarViewModel(fixture.Services);
        await vm.LoadInstallationsAsync(overlayId);
        vm.IsUpdateAvailable = true;

        await vm.UpdateGameCommand.ExecuteAsync(null);

        Assert.DoesNotContain("Failed to update", vm.StatusMessage ?? string.Empty);
        Assert.False(vm.IsInstalling);
    }

    [AvaloniaFact]
    public async Task UpdateGame_ForAnInstalledOverlay_NeverCreatesAConflictingInstallationRow()
    {
        // The overlay must keep living purely on the legacy fields: a GameInstallation for it
        // would point at the base game's directory and break the uniqueness invariant.
        var fixture = BuildServices($"action-bar-legacy-{Guid.NewGuid()}");
        var (baseGameId, overlayId) = await SeedInstalledOverlayAsync(fixture);

        MapGame(fixture.Handler,
            new SdkGame { Id = overlayId, Title = "Opposing Force", Type = GameType.Expansion, BaseGameId = baseGameId },
            new SdkArchive { Id = Guid.NewGuid(), Version = "2.0.0" });

        var vm = new GameActionBarViewModel(fixture.Services);
        await vm.LoadInstallationsAsync(overlayId);
        vm.IsUpdateAvailable = true;

        await vm.UpdateGameCommand.ExecuteAsync(null);

        using var scope = fixture.Services.CreateScope();
        var installationService = scope.ServiceProvider.GetRequiredService<GameInstallationService>();

        Assert.False(await installationService.HasInstallationsAsync(overlayId));
    }

    [AvaloniaFact]
    public async Task UpdateGame_ForALegacyMainGameWithNoInstallationRow_QueuesAgainstItsExistingDirectory()
    {
        var fixture = BuildServices($"action-bar-legacy-{Guid.NewGuid()}");
        var gameId = Guid.NewGuid();
        var archiveId = Guid.NewGuid();

        using (var scope = fixture.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<Data.DatabaseContext>();

            context.Games!.Add(new Game
            {
                Id = gameId,
                Title = "Half-Life",
                Type = GameType.MainGame,
                Installed = true,
                InstallDirectory = SharedDirectory,
                InstalledVersion = "0.9.0",
            });

            await context.SaveChangesAsync();
        }

        MapGame(fixture.Handler,
            new SdkGame { Id = gameId, Title = "Half-Life", Type = GameType.MainGame, BaseGameId = Guid.Empty },
            new SdkArchive { Id = archiveId, Version = "1.0.0" });

        var vm = new GameActionBarViewModel(fixture.Services);
        await vm.LoadInstallationsAsync(gameId);
        vm.IsUpdateAvailable = true;

        await vm.UpdateGameCommand.ExecuteAsync(null);

        var queued = QueuedItemFor(fixture, gameId);

        Assert.NotNull(queued);
        Assert.Equal(SharedDirectory, queued!.InstallDirectory);
        Assert.Equal("Added to download queue", vm.StatusMessage);
    }

    [AvaloniaFact]
    public async Task UpdateGame_ForAGameThatIsNotInstalledAtAll_QueuesNothing()
    {
        // The legacy fallback must stay narrow: it only applies to entries the launcher genuinely
        // considers installed, never as a way to smuggle a fresh install through the update path.
        var fixture = BuildServices($"action-bar-legacy-{Guid.NewGuid()}");
        var gameId = Guid.NewGuid();

        using (var scope = fixture.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<Data.DatabaseContext>();

            context.Games!.Add(new Game { Id = gameId, Title = "Never Installed", Type = GameType.MainGame, Installed = false });

            await context.SaveChangesAsync();
        }

        var vm = new GameActionBarViewModel(fixture.Services);
        await vm.LoadInstallationsAsync(gameId);

        Assert.False(vm.IsInstalled);

        // Force both preconditions the command itself checks so the legacy resolution is reached.
        vm.IsInstalled = true;
        vm.IsUpdateAvailable = true;

        await vm.UpdateGameCommand.ExecuteAsync(null);

        Assert.Null(QueuedItemFor(fixture, gameId));
        Assert.Contains("Failed to update", vm.StatusMessage ?? string.Empty);
    }
}
