using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Services;
using LANCommander.Launcher.ViewModels.Components;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace LANCommander.Launcher.Tests.Tests;

/// <summary>
/// Covers <see cref="GameActionBarViewModel"/>'s installation-selection state (Phase 5 UX):
/// loading installation instances derives IsInstalled/InstallDirectory from
/// <see cref="GameInstallationService"/> rather than the legacy Game.Installed mirror, and
/// switching the selected installation calls SelectInstallationAsync/SyncLegacyMirrorsAsync and
/// refreshes the view model to match. Also covers action routing: uninstalling acts on the
/// selected installation only, leaving a sibling side-by-side installation intact.
///
/// Builds a minimal real (not mocked) service graph backed by EF Core InMemory.
/// GameClient/ToolClient are constructed with null! network dependencies — the same pattern as
/// LANCommander.Launcher.Services.Tests/Helpers/ServiceTestFactory — which is safe here because
/// every call path exercised (GetActionsAsync, UninstallAsync on a directory with no on-disk
/// manifest, ...) already tolerates null internals and/or is wrapped in try/catch by the caller.
/// LibraryService is deliberately not registered: RefreshAsync resolves the core installation
/// state before it, so LibraryService's (expected) resolution failure is caught and logged
/// without affecting the state these tests assert on.
/// </summary>
public class GameActionBarInstallationSelectionTests
{
    private static GameClient CreateDummyGameClient() =>
        new(null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);

    private static ToolClient CreateDummyToolClient() =>
        new(null!, null!, null!, null!);

    private static IServiceProvider BuildServiceProvider(string dbName)
    {
        var services = new ServiceCollection();

        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<Data.DatabaseContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddScoped<GameInstallationService>();
        services.AddScoped<ToolService>();
        services.AddScoped(_ => CreateDummyGameClient());
        services.AddScoped(_ => CreateDummyToolClient());
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

        return services.BuildServiceProvider();
    }

    private static async Task<(Game Game, GameInstallation First, GameInstallation Second)> SeedGameWithTwoInstallationsAsync(
        IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Data.DatabaseContext>();
        var installationService = scope.ServiceProvider.GetRequiredService<GameInstallationService>();

        var game = new Game
        {
            Id = Guid.NewGuid(),
            Title = "Test Game",
            // Deliberately left false: IsInstalled must be derived from installation rows, not
            // this legacy mirror (Phase 5 requirement).
            Installed = false,
        };
        context.Games!.Add(game);
        await context.SaveChangesAsync();

        var first = new GameInstallation
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            Version = "1.0.0",
            InstallDirectory = @"C:\Games\TestGame",
            InstalledOn = DateTime.UtcNow.AddDays(-1),
        };
        await installationService.AddInstallationAsync(first);

        var second = new GameInstallation
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            Version = "2.0.0",
            InstallDirectory = @"C:\Games\TestGame (2.0.0)",
            InstalledOn = DateTime.UtcNow,
        };
        await installationService.AddInstallationAsync(second, select: false);

        return (game, first, second);
    }

    [Fact]
    public async Task LoadInstallationsAsync_DerivesIsInstalled_FromInstallationRows_NotLegacyFlag()
    {
        var services = BuildServiceProvider($"action-bar-{Guid.NewGuid()}");
        var (game, _, _) = await SeedGameWithTwoInstallationsAsync(services);

        var vm = new GameActionBarViewModel(services);
        await vm.LoadInstallationsAsync(game.Id);

        Assert.True(vm.IsInstalled);
        Assert.Equal(2, vm.Installations.Count);
        Assert.True(vm.HasMultipleInstallations);
    }

    [Fact]
    public async Task LoadInstallationsAsync_RaisesHasMultipleInstallationsChanged_AfterMutatingTheCollectionInPlace()
    {
        // Regression test for the MEDIUM "HasMultipleInstallations PropertyChanged not raised
        // after collection mutation" finding: LoadInstallationsAsync populates Installations via
        // Clear()/Add() on the existing ObservableCollection instance rather than assigning a new
        // one, so the [NotifyPropertyChangedFor(nameof(HasMultipleInstallations))] hookup
        // generated for the Installations *property's own setter* never fires on its own -
        // LoadInstallationsAsync must raise it (and the labels that also depend on it) itself.
        var services = BuildServiceProvider($"action-bar-{Guid.NewGuid()}");
        var (game, _, _) = await SeedGameWithTwoInstallationsAsync(services);

        var vm = new GameActionBarViewModel(services);

        var raisedProperties = new List<string>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != null)
                raisedProperties.Add(e.PropertyName);
        };

        await vm.LoadInstallationsAsync(game.Id);

        Assert.True(vm.HasMultipleInstallations);
        Assert.Contains(nameof(GameActionBarViewModel.HasMultipleInstallations), raisedProperties);
        Assert.Contains(nameof(GameActionBarViewModel.UninstallMenuLabel), raisedProperties);
        Assert.Contains(nameof(GameActionBarViewModel.ChangeVersionMenuLabel), raisedProperties);
    }

    [Fact]
    public async Task LoadInstallationsAsync_RaisesHasMultipleInstallationsChanged_EvenWhenGoingFromTwoInstallationsToOne()
    {
        // The notification must fire on every load that could have changed the count, not just
        // the very first population — otherwise a UI bound to HasMultipleInstallations would keep
        // showing/hiding the installation selector based on stale state after an uninstall.
        var services = BuildServiceProvider($"action-bar-{Guid.NewGuid()}");
        var (game, first, second) = await SeedGameWithTwoInstallationsAsync(services);

        var vm = new GameActionBarViewModel(services);
        await vm.LoadInstallationsAsync(game.Id);
        Assert.True(vm.HasMultipleInstallations);

        using (var scope = services.CreateScope())
        {
            var installationService = scope.ServiceProvider.GetRequiredService<GameInstallationService>();
            await installationService.DeleteInstallationAsync(second.Id);
        }

        var raisedProperties = new List<string>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != null)
                raisedProperties.Add(e.PropertyName);
        };

        await vm.LoadInstallationsAsync(game.Id);

        Assert.False(vm.HasMultipleInstallations);
        Assert.Contains(nameof(GameActionBarViewModel.HasMultipleInstallations), raisedProperties);
    }

    [Fact]
    public async Task LoadInstallationsAsync_PreselectsCurrentlySelectedInstallation()
    {
        var services = BuildServiceProvider($"action-bar-{Guid.NewGuid()}");
        var (game, first, _) = await SeedGameWithTwoInstallationsAsync(services);

        var vm = new GameActionBarViewModel(services);
        await vm.LoadInstallationsAsync(game.Id);

        // "first" was added with select:true (the default) and "second" with select:false, so
        // "first" remains the selected installation.
        Assert.NotNull(vm.SelectedInstallationItem);
        Assert.Equal(first.Id, vm.SelectedInstallationItem!.Id);
        Assert.Equal(first.InstallDirectory, vm.InstallDirectory);
    }

    [Fact]
    public async Task NoInstallations_ReportsNotInstalled()
    {
        var services = BuildServiceProvider($"action-bar-{Guid.NewGuid()}");

        using (var scope = services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<Data.DatabaseContext>();
            context.Games!.Add(new Game { Id = Guid.NewGuid(), Title = "Never Installed", Installed = false });
            await context.SaveChangesAsync();
        }

        var gameId = (await GetOnlyGameIdAsync(services));

        var vm = new GameActionBarViewModel(services);
        await vm.LoadInstallationsAsync(gameId);

        Assert.False(vm.IsInstalled);
        Assert.Empty(vm.Installations);
        Assert.Null(vm.SelectedInstallationItem);
        Assert.Null(vm.InstallDirectory);
    }

    [Fact]
    public async Task InstalledOverlayAddon_WithNoInstallationRows_ReportsInstalledFromLegacyFields()
    {
        // Regression test for the HIGH "overlay add-on falsely reports not installed" finding.
        // Expansion/Mod/StandaloneMod rows that have a base game are overlays: they install into
        // their base game's directory and are deliberately never given their own GameInstallation
        // row (the AddGameInstallations migration excludes them explicitly, because a second row
        // at the same InstallDirectory would violate the install-directory uniqueness invariant).
        // Their install state lives on the legacy Game fields, kept mirrored by
        // GameInstallationService.SyncLegacyMirrorsAsync — so deriving IsInstalled purely from
        // installation rows reported an installed add-on as not installed, hiding Play/Uninstall.
        var services = BuildServiceProvider($"action-bar-{Guid.NewGuid()}");

        var baseGameId = Guid.NewGuid();
        var addonId = Guid.NewGuid();
        const string sharedDirectory = @"C:\Games\TestGame";

        using (var scope = services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<Data.DatabaseContext>();

            context.Games!.Add(new Game { Id = baseGameId, Title = "Base Game", Installed = true, InstallDirectory = sharedDirectory });
            context.Games!.Add(new Game
            {
                Id = addonId,
                Title = "Expansion Pack",
                Type = GameType.Expansion,
                BaseGameId = baseGameId,
                Installed = true,
                InstallDirectory = sharedDirectory,
                InstalledVersion = "1.2.0",
                InstalledOn = DateTime.UtcNow,
            });

            await context.SaveChangesAsync();
        }

        var vm = new GameActionBarViewModel(services);
        await vm.LoadInstallationsAsync(addonId);

        Assert.True(vm.IsInstalled);
        Assert.Equal(sharedDirectory, vm.InstallDirectory);

        // The installation selector must still only ever reflect real installation rows: an
        // overlay add-on has none, so it must never be offered a bogus version choice.
        Assert.Empty(vm.Installations);
        Assert.False(vm.HasMultipleInstallations);
        Assert.Null(vm.SelectedInstallationItem);
    }

    [Fact]
    public async Task LegacyInstalledGame_WithNoInstallationRows_ReportsInstalledFromLegacyFields()
    {
        // The same fallback also covers pre-migration/stray data for a plain main game whose
        // legacy fields say installed but which has no installation row yet.
        var services = BuildServiceProvider($"action-bar-{Guid.NewGuid()}");

        var gameId = Guid.NewGuid();

        using (var scope = services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<Data.DatabaseContext>();

            context.Games!.Add(new Game
            {
                Id = gameId,
                Title = "Legacy Install",
                Installed = true,
                InstallDirectory = @"C:\Legacy\LegacyInstall",
                InstalledVersion = "0.9.0",
            });

            await context.SaveChangesAsync();
        }

        var vm = new GameActionBarViewModel(services);
        await vm.LoadInstallationsAsync(gameId);

        Assert.True(vm.IsInstalled);
        Assert.Equal(@"C:\Legacy\LegacyInstall", vm.InstallDirectory);
        Assert.Empty(vm.Installations);
        Assert.False(vm.HasMultipleInstallations);
    }

    [Fact]
    public async Task InstalledLegacyGame_ThatLaterGetsInstallationRows_PrefersTheInstallationRows()
    {
        // The legacy fallback must never win over real installation rows — otherwise a stale
        // legacy directory could shadow the actually-selected installation.
        var services = BuildServiceProvider($"action-bar-{Guid.NewGuid()}");
        var (game, first, _) = await SeedGameWithTwoInstallationsAsync(services);

        using (var scope = services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<Data.DatabaseContext>();
            var stored = await context.Games!.FindAsync(game.Id);

            stored!.Installed = true;
            stored.InstallDirectory = @"C:\Stale\LegacyPath";

            await context.SaveChangesAsync();
        }

        var vm = new GameActionBarViewModel(services);
        await vm.LoadInstallationsAsync(game.Id);

        Assert.True(vm.IsInstalled);
        Assert.Equal(first.InstallDirectory, vm.InstallDirectory);
        Assert.NotEqual(@"C:\Stale\LegacyPath", vm.InstallDirectory);
    }

    private static async Task<Guid> GetOnlyGameIdAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Data.DatabaseContext>();
        return (await context.Games!.SingleAsync()).Id;
    }

    [Fact]
    public async Task ChangingSelectedInstallationItem_SwitchesSelection_AndRefreshesInstallDirectory()
    {
        var services = BuildServiceProvider($"action-bar-{Guid.NewGuid()}");
        var (game, _, second) = await SeedGameWithTwoInstallationsAsync(services);

        var vm = new GameActionBarViewModel(services);
        await vm.LoadInstallationsAsync(game.Id);

        var secondItem = vm.Installations.Single(i => i.Id == second.Id);

        // Simulates the user picking a different entry in the installation-selector ComboBox.
        vm.SelectedInstallationItem = secondItem;

        // The selection-changed handler switches installations on a background task; wait for it
        // to settle by polling InstallDirectory, which only changes once
        // GameInstallationService has actually flipped the selected row and RefreshAsync has
        // re-loaded state from it.
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (vm.InstallDirectory != second.InstallDirectory && DateTime.UtcNow < deadline)
            await Task.Delay(25);

        Assert.Equal(second.InstallDirectory, vm.InstallDirectory);
        Assert.Equal(second.Id, vm.SelectedInstallationItem!.Id);

        // The switch must be durable in the database, not just reflected in the view model.
        using var scope = services.CreateScope();
        var installationService = scope.ServiceProvider.GetRequiredService<GameInstallationService>();
        var reloadedSelected = await installationService.GetSelectedInstallationAsync(game.Id);
        Assert.Equal(second.Id, reloadedSelected!.Id);

        // SyncLegacyMirrorsAsync must have mirrored the switch onto the legacy Game fields too,
        // for transitional callers that still read them directly.
        var context = scope.ServiceProvider.GetRequiredService<Data.DatabaseContext>();
        var reloadedGame = await context.Games!.FindAsync(game.Id);
        Assert.True(reloadedGame!.Installed);
        Assert.Equal(second.InstallDirectory, reloadedGame.InstallDirectory);
    }

    [Fact]
    public async Task UninstallingSelectedInstallation_LeavesSiblingInstalled_AndActsOnlyOnSelected()
    {
        var services = BuildServiceProvider($"action-bar-{Guid.NewGuid()}");
        var (game, first, second) = await SeedGameWithTwoInstallationsAsync(services);

        var vm = new GameActionBarViewModel(services);
        await vm.LoadInstallationsAsync(game.Id);

        // "first" is the selected installation (see SeedGameWithTwoInstallationsAsync) — uninstall
        // must act on exactly that one, mirroring GameActionBarViewModel.UninstallAsync's own
        // "act on SelectedInstallationItem" routing.
        Assert.Equal(first.Id, vm.SelectedInstallationItem!.Id);

        using (var scope = services.CreateScope())
        {
            var gameService = scope.ServiceProvider.GetRequiredService<GameService>();
            var installationService = scope.ServiceProvider.GetRequiredService<GameInstallationService>();

            var localGame = await gameService.GetAsync(game.Id);
            var installation = await installationService.GetAsync(vm.SelectedInstallationItem.Id);

            await gameService.UninstallAsync(localGame!, installation!);
        }

        await vm.LoadInstallationsAsync(game.Id);

        // Only "first" was removed; "second" remains, so the game must still report installed,
        // now pointing at the sibling's own directory (never both, never neither).
        Assert.True(vm.IsInstalled);
        var remaining = Assert.Single(vm.Installations);
        Assert.Equal(second.Id, remaining.Id);
        Assert.Equal(second.InstallDirectory, vm.InstallDirectory);
    }
}
