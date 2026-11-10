using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.ImportExport.Factories;
using LANCommander.Server.ImportExport.Services;
using LANCommander.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Manifest = LANCommander.SDK.Models.Manifest;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// Game imports against the real (SQLite) provider. Covers a report that importing a game created
/// the game but none of its actions, saves or archives, and kept only one of two multiplayer modes
/// of the same type.
/// </summary>
[Collection("BUnit")]
public class GameImportTests
{
    private readonly BUnitServerFixture _fixture;

    public GameImportTests(BUnitServerFixture fixture) => _fixture = fixture;

    private static string TempDirectory() => ImportPackages.TempDirectory();

    private static byte[] ZipBytes(string content) => ImportPackages.ZipBytes(content);

    private static Manifest.Game BuildManifest() => ImportPackages.Game();

    private static Task<string> WritePackageAsync(Manifest.Game manifest) => ImportPackages.WriteAsync(manifest);

    private async Task<(Manifest.Game Manifest, Exception? Error)> ImportAsync(IServiceProvider services)
    {
        var manifest = BuildManifest();
        var path = await WritePackageAsync(manifest);

        try
        {
            await services.GetRequiredService<ImportRunner>().RunFileAsync(path, manifestType: ManifestType.Game);
            return (manifest, null);
        }
        catch (Exception ex)
        {
            return (manifest, ex);
        }
    }

    [Fact]
    public async Task ImportCompletesWithoutError()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();

        var (_, error) = await ImportAsync(scope.ServiceProvider);

        Assert.Null(error);
    }

    [Fact]
    public async Task ImportCreatesTheGamesActions()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();

        var (manifest, _) = await ImportAsync(scope.ServiceProvider);

        var actions = await scope.ServiceProvider.GetRequiredService<ActionService>().GetAsync(a => a.GameId == manifest.Id);

        Assert.Equal(["Editor", "Play"], actions.Select(a => a.Name).Order());
    }

    [Fact]
    public async Task ImportCreatesTheGamesArchiveAndItsFile()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();
        var archiveService = scope.ServiceProvider.GetRequiredService<ArchiveService>();

        var (manifest, _) = await ImportAsync(scope.ServiceProvider);

        var archive = Assert.Single(await archiveService.GetAsync(a => a.GameId == manifest.Id));

        Assert.True(File.Exists(await archiveService.GetArchiveFileLocationAsync(archive)), "The archive's file was not written");
    }

    [Fact]
    public async Task ImportCreatesTheGamesSaveAndItsFile()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();
        var gameSaveService = scope.ServiceProvider.GetRequiredService<GameSaveService>();

        var (manifest, _) = await ImportAsync(scope.ServiceProvider);

        var save = Assert.Single(await gameSaveService.GetAsync(s => s.GameId == manifest.Id));

        Assert.True(File.Exists(gameSaveService.GetSavePath(save)), "The save's file was not written");
    }

    [Fact]
    public async Task ImportKeepsMultiplayerModesOfTheSameType()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();

        var (manifest, _) = await ImportAsync(scope.ServiceProvider);

        var modes = await scope.ServiceProvider.GetRequiredService<MultiplayerModeService>().GetAsync(m => m.GameId == manifest.Id);

        Assert.Equal(["Co-op", "Versus"], modes.Select(m => m.Description).Order());
    }

    [Fact]
    public async Task ReimportingDoesNotDuplicateMultiplayerModes()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();
        var importRunner = scope.ServiceProvider.GetRequiredService<ImportRunner>();

        var manifest = BuildManifest();
        var path = await WritePackageAsync(manifest);

        await importRunner.RunFileAsync(path, manifestType: ManifestType.Game);
        await importRunner.RunFileAsync(path, manifestType: ManifestType.Game);

        var modes = await scope.ServiceProvider.GetRequiredService<MultiplayerModeService>().GetAsync(m => m.GameId == manifest.Id);
        var actions = await scope.ServiceProvider.GetRequiredService<ActionService>().GetAsync(a => a.GameId == manifest.Id);

        Assert.Equal(["Co-op", "Versus"], modes.Select(m => m.Description).Order());
        Assert.Equal(2, actions.Count);
    }

    /// <summary>
    /// Continuing an import from the upload indicator skips the dialog's location picker, so no
    /// storage location is passed.
    /// </summary>
    [Fact]
    public async Task ImportWithoutAStorageLocationPutsArchivesInTheDefaultLocation()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();
        var archiveService = scope.ServiceProvider.GetRequiredService<ArchiveService>();

        var manifest = BuildManifest();
        var path = await WritePackageAsync(manifest);

        using var context = scope.ServiceProvider.GetRequiredService<ImportContextFactory>().Create();

        await context.InitializeImportAsync(path);
        await context.PrepareImportQueueAsync(null,Guid.Empty);
        await context.ImportQueueAsync();

        var archive = Assert.Single(await archiveService.GetAsync(a => a.GameId == manifest.Id));
        var defaultLocation = await scope.ServiceProvider.GetRequiredService<StorageLocationService>().DefaultAsync(StorageLocationType.Archive);

        Assert.Equal(defaultLocation!.Id, archive.StorageLocationId);
        Assert.True(File.Exists(await archiveService.GetArchiveFileLocationAsync(archive)));
    }

    /// <summary>A save whose owner isn't on this server is left out; it doesn't sink the whole import.</summary>
    [Fact]
    public async Task SaveForAnUnknownUserIsSkippedAndTheRestImports()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();

        var manifest = BuildManifest();
        manifest.Saves.Single().User = $"nobody-{Guid.NewGuid():N}";

        var path = await WritePackageAsync(manifest);
        var errors = new List<string>();

        using var context = scope.ServiceProvider.GetRequiredService<ImportContextFactory>().Create();
        context.OnImportError.EventRaised += update =>
        {
            errors.Add(update.Error!);
            return Task.CompletedTask;
        };

        var storageLocation = await scope.ServiceProvider.GetRequiredService<StorageLocationService>().DefaultAsync(StorageLocationType.Archive);

        await context.InitializeImportAsync(path);
        await context.PrepareImportQueueAsync(null,storageLocation!.Id);
        await context.ImportQueueAsync();

        var saves = await scope.ServiceProvider.GetRequiredService<GameSaveService>().GetAsync(s => s.GameId == manifest.Id);
        var actions = await scope.ServiceProvider.GetRequiredService<ActionService>().GetAsync(a => a.GameId == manifest.Id);

        Assert.Empty(saves);
        Assert.Equal(2, actions.Count);
        Assert.Contains(errors, e => e.Contains("owner", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ImportContextsDoNotHearEachOther()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<ImportContextFactory>();

        using var listening = factory.Create();
        using var other = factory.Create();

        var heard = 0;
        listening.OnImportComplete.EventRaised += _ =>
        {
            heard++;
            return Task.CompletedTask;
        };

        await other.OnImportComplete.InvokeAsync(new());

        Assert.Equal(0, heard);
    }

    /// <summary>
    /// A Blazor session is one DI scope, and the import dialog, the game list and the metadata lookup
    /// can each create an import context in it. Reading a second package must not redirect the
    /// first context's import.
    /// </summary>
    [Fact]
    public async Task TwoImportContextsInOneScopeImportIndependently()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();
        var services = scope.ServiceProvider;
        var factory = services.GetRequiredService<ImportContextFactory>();
        var actionService = services.GetRequiredService<ActionService>();
        var storageLocation = await services.GetRequiredService<StorageLocationService>().DefaultAsync(StorageLocationType.Archive);

        var first = BuildManifest();
        var second = BuildManifest();

        using var firstContext = factory.Create();
        using var secondContext = factory.Create();

        await firstContext.InitializeImportAsync(await WritePackageAsync(first));
        await secondContext.InitializeImportAsync(await WritePackageAsync(second));

        await firstContext.PrepareImportQueueAsync(null, storageLocation!.Id);
        await firstContext.ImportQueueAsync();

        Assert.Equal(2, (await actionService.GetAsync(a => a.GameId == first.Id)).Count);
        Assert.Empty(await actionService.GetAsync(a => a.GameId == second.Id));

        await secondContext.PrepareImportQueueAsync(null, storageLocation.Id);
        await secondContext.ImportQueueAsync();

        Assert.Equal(2, (await actionService.GetAsync(a => a.GameId == second.Id)).Count);
    }

    /// <summary>A context that never runs (a cancelled dialog, a package that failed to read) is still released.</summary>
    [Fact]
    public void DisposedImportContextIsReleased()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();
        var importService = scope.ServiceProvider.GetRequiredService<ImportService>();

        var context = scope.ServiceProvider.GetRequiredService<ImportContextFactory>().Create();

        Assert.Contains(context, importService.GetContexts());

        context.Dispose();

        Assert.DoesNotContain(context, importService.GetContexts());
    }

    /// <summary>
    /// A record whose import fails for good (here an archive whose payload isn't a ZIP) is reported
    /// once and left out. It used to be retried as if waiting on its game until the whole import
    /// gave up, with the cause only in the server log.
    /// </summary>
    [Fact]
    public async Task CorruptArchiveIsReportedAndTheRestImports()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();
        var services = scope.ServiceProvider;

        var manifest = BuildManifest();
        var path = await ImportPackages.WriteAsync(manifest, entry => entry.StartsWith("Archives/") ? "not a zip"u8.ToArray() : ImportPackages.ZipBytes(entry));

        var result = await services.GetRequiredService<ImportRunner>().RunFileAsync(path, manifestType: ManifestType.Game);

        Assert.Contains(result.Errors, e => e.Contains("Archive", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, (await services.GetRequiredService<ActionService>().GetAsync(a => a.GameId == manifest.Id)).Count);
        Assert.Single(await services.GetRequiredService<GameSaveService>().GetAsync(s => s.GameId == manifest.Id));
    }

    /// <summary>
    /// Each archive becomes a version, oldest first, so the newest archive is the version launchers
    /// are offered; exports may list archives in any order.
    /// </summary>
    [Fact]
    public async Task ArchivesBecomeVersionsInTheOrderTheyWereMade()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();
        var services = scope.ServiceProvider;

        var manifest = BuildManifest();
        manifest.Archives =
        [
            new Manifest.Archive { Id = Guid.NewGuid(), Version = "1.1", ObjectKey = Guid.NewGuid().ToString(), Changelog = "Patch", CreatedOn = new DateTime(2024, 6, 1) },
            new Manifest.Archive { Id = Guid.NewGuid(), Version = "1.0", ObjectKey = Guid.NewGuid().ToString(), Changelog = "Release", CreatedOn = new DateTime(2024, 1, 1) },
        ];

        var result = await services.GetRequiredService<ImportRunner>().RunFileAsync(await WritePackageAsync(manifest), manifestType: ManifestType.Game);

        Assert.Empty(result.Errors);

        var versions = (await services.GetRequiredService<GameVersionService>().GetAllAsync(manifest.Id)).ToList();

        Assert.Equal(["1.1", "1.0"], versions.Select(v => v.Version));
        Assert.Equal(["Patch", "Release"], versions.Select(v => v.Changelog));
        Assert.All(versions, v => Assert.Equal(v.Version, v.Archive?.Version));

        // The version launchers are offered has the game's actions
        var actions = await services.GetRequiredService<ActionService>().GetAsync(a => a.GameVersionId == versions[0].Id);
        Assert.Equal(2, actions.Count);
    }

    /// <summary>
    /// A package's game merges into an existing game with its title, so a game moved between
    /// servers isn't duplicated. A different game that shares the title, such as a remake, must not
    /// be overwritten.
    /// </summary>
    [Fact]
    public async Task GameWithTheSameTitleFromAnotherYearIsNotOverwritten()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();
        var services = scope.ServiceProvider;
        var gameService = services.GetRequiredService<GameService>();

        var title = $"Doom {Guid.NewGuid():N}";
        var existing = await gameService.AddAsync(new Game { Title = title, Description = "The 1993 original", ReleasedOn = new DateTime(1993, 12, 10), Type = GameType.MainGame });

        var manifest = BuildManifest();
        manifest.Title = title;
        manifest.Description = "The 2016 remake";
        manifest.ReleasedOn = new DateTime(2016, 5, 13);

        var result = await services.GetRequiredService<ImportRunner>().RunFileAsync(await WritePackageAsync(manifest), manifestType: ManifestType.Game);

        Assert.NotEqual(existing.Id, result.RecordId);
        Assert.Equal("The 1993 original", (await gameService.GetAsync(existing.Id))!.Description);
        Assert.Equal("The 2016 remake", (await gameService.GetAsync(result.RecordId))!.Description);
    }

    [Fact]
    public async Task GameWithTheSameTitleAndYearMergesIntoTheExistingGame()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();
        var services = scope.ServiceProvider;
        var gameService = services.GetRequiredService<GameService>();

        var title = $"Half-Life {Guid.NewGuid():N}";
        var existing = await gameService.AddAsync(new Game { Title = title, ReleasedOn = new DateTime(1998, 11, 19), Type = GameType.MainGame });

        var manifest = BuildManifest();
        manifest.Title = title;
        manifest.ReleasedOn = new DateTime(1998, 11, 19);

        var result = await services.GetRequiredService<ImportRunner>().RunFileAsync(await WritePackageAsync(manifest), manifestType: ManifestType.Game);

        Assert.Equal(existing.Id, result.RecordId);
        Assert.Equal(2, (await services.GetRequiredService<ActionService>().GetAsync(a => a.GameId == existing.Id)).Count);
    }

    /// <summary>Imports a package straight through the context, collecting every error it reports.</summary>
    private async Task<(Exception? Error, List<string> Errors)> RunImportAsync(IServiceProvider services, string path)
    {
        var errors = new List<string>();

        using var context = services.GetRequiredService<ImportContextFactory>().Create();
        context.OnImportError.EventRaised += update =>
        {
            errors.Add($"{update.CurrentItem?.Type} \"{update.CurrentItem?.Name}\": {update.Error}");
            return Task.CompletedTask;
        };

        var storageLocation = await services.GetRequiredService<StorageLocationService>().DefaultAsync(StorageLocationType.Archive);

        try
        {
            await context.InitializeImportAsync(path);
            await context.PrepareImportQueueAsync(null,storageLocation!.Id);
            await context.ImportQueueAsync();

            return (null, errors);
        }
        catch (Exception ex)
        {
            return (ex, errors);
        }
    }

    /// <summary>
    /// A game can have one "Play" action per platform. Actions are keyed by name, so these may
    /// collapse into one the way two LAN modes did.
    /// </summary>
    [Fact]
    public async Task ActionsWithTheSameNameOnDifferentPlatformsAreAllImported()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();

        var manifest = BuildManifest();
        manifest.Actions =
        [
            new Manifest.Action { Name = "Play", Path = "game.exe", Type = ActionType.Primary, Platforms = RuntimePlatform.Windows },
            new Manifest.Action { Name = "Play", Path = "game.sh", Type = ActionType.Primary, Platforms = RuntimePlatform.Linux, SortOrder = 1 },
        ];

        var (error, errors) = await RunImportAsync(scope.ServiceProvider, await WritePackageAsync(manifest));

        Assert.Null(error);
        Assert.Empty(errors);

        var actions = await scope.ServiceProvider.GetRequiredService<ActionService>().GetAsync(a => a.GameId == manifest.Id);

        Assert.Equal(["game.exe", "game.sh"], actions.Select(a => a.Path).Order());
    }

    [Fact]
    public async Task ReimportingSameNamedActionsDoesNotMergeOrDuplicateThem()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();

        var manifest = BuildManifest();
        manifest.Actions =
        [
            new Manifest.Action { Name = "Play", Path = "game.exe", Type = ActionType.Primary, Platforms = RuntimePlatform.Windows },
            new Manifest.Action { Name = "Play", Path = "game.sh", Type = ActionType.Primary, Platforms = RuntimePlatform.Linux, SortOrder = 1 },
        ];

        var path = await WritePackageAsync(manifest);

        await RunImportAsync(scope.ServiceProvider, path);
        var (error, _) = await RunImportAsync(scope.ServiceProvider, path);

        Assert.Null(error);

        var actions = await scope.ServiceProvider.GetRequiredService<ActionService>().GetAsync(a => a.GameId == manifest.Id);

        Assert.Equal(["game.exe", "game.sh"], actions.Select(a => a.Path).Order());
    }

    public enum GameLink
    {
        MissingRedistributable,
        ExistingRedistributableWithOptions,
        MissingBaseGame,
        ExistingBaseGame,
        OptionSchema,
        ExternalIds,
        Metadata,
    }

    /// <summary>
    /// The game row is saved first and its links (redistributables, base game, option schema,
    /// metadata) are synced afterwards. If that sync throws, the game exists but is put back in the
    /// queue, and every action, archive and save waits on it: "the game is created, but nothing else".
    /// </summary>
    [Theory]
    [InlineData(GameLink.MissingRedistributable)]
    [InlineData(GameLink.ExistingRedistributableWithOptions)]
    [InlineData(GameLink.MissingBaseGame)]
    [InlineData(GameLink.ExistingBaseGame)]
    [InlineData(GameLink.OptionSchema)]
    [InlineData(GameLink.ExternalIds)]
    [InlineData(GameLink.Metadata)]
    public async Task LinkedGameImportsWithItsActionsArchivesAndSaves(GameLink link)
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();
        var services = scope.ServiceProvider;
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var manifest = BuildManifest();

        switch (link)
        {
            case GameLink.MissingRedistributable:
                manifest.Redistributables = [new Manifest.Redistributable { Id = Guid.NewGuid(), Name = $"Missing {suffix}" }];
                break;

            case GameLink.ExistingRedistributableWithOptions:
                var redistributable = await services.GetRequiredService<RedistributableService>().AddAsync(new Redistributable { Name = $"Existing {suffix}" });
                manifest.Redistributables =
                [
                    new Manifest.Redistributable { Id = redistributable.Id, Name = redistributable.Name, Options = new() { ["Shim.Layer"] = "server" } },
                ];
                break;

            case GameLink.MissingBaseGame:
                manifest.Type = GameType.Expansion;
                manifest.BaseGameId = Guid.NewGuid();
                manifest.BaseGame = $"Missing Base {suffix}";
                break;

            case GameLink.ExistingBaseGame:
                var baseGame = await services.GetRequiredService<GameService>().AddAsync(new Game { Title = $"Base {suffix}", Type = GameType.MainGame });
                manifest.Type = GameType.Expansion;
                manifest.BaseGameId = baseGame.Id;
                manifest.BaseGame = baseGame.Title;
                break;

            case GameLink.OptionSchema:
                manifest.OptionSchema = "Options:\n  - Key: Resolution\n    Type: String\n";
                break;

            case GameLink.ExternalIds:
                manifest.ExternalIds = [new Manifest.GameExternalId { Provider = "Steam", ExternalId = suffix }];
                break;

            case GameLink.Metadata:
                manifest.Genres = [new Manifest.Genre { Name = $"Genre {suffix}" }];
                manifest.Tags = [new Manifest.Tag { Name = $"Tag {suffix}" }];
                manifest.Developers = [new Manifest.Company { Name = $"Developer {suffix}" }];
                manifest.Publishers = [new Manifest.Company { Name = $"Publisher {suffix}" }];
                manifest.Platforms = [new Manifest.Platform { Name = $"Platform {suffix}" }];
                manifest.Collections = [new Manifest.Collection { Name = $"Collection {suffix}" }];
                manifest.CustomFields = [new Manifest.GameCustomField($"Field {suffix}", "Value")];
                manifest.Engine = new Manifest.Engine { Name = $"Engine {suffix}" };
                break;
        }

        var (error, errors) = await RunImportAsync(services, await WritePackageAsync(manifest));

        Assert.Null(error);
        Assert.Empty(errors);

        Assert.Equal(2, (await services.GetRequiredService<ActionService>().GetAsync(a => a.GameId == manifest.Id)).Count);
        Assert.Single(await services.GetRequiredService<ArchiveService>().GetAsync(a => a.GameId == manifest.Id));
        Assert.Single(await services.GetRequiredService<GameSaveService>().GetAsync(s => s.GameId == manifest.Id));
    }

    /// <summary>What the reporter did: export a game from one server and import it on another.</summary>
    [Fact]
    public async Task ExportedGameRoundTripsActionsArchivesAndMultiplayerModes()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();

        var storageLocationService = scope.ServiceProvider.GetRequiredService<StorageLocationService>();
        var gameService = scope.ServiceProvider.GetRequiredService<GameService>();
        var archiveService = scope.ServiceProvider.GetRequiredService<ArchiveService>();
        var actionService = scope.ServiceProvider.GetRequiredService<ActionService>();
        var multiplayerModeService = scope.ServiceProvider.GetRequiredService<MultiplayerModeService>();

        var storageLocation = (await storageLocationService.GetAsync(l => l.Type == StorageLocationType.Archive)).First();

        var game = await gameService.AddAsync(new Game { Title = $"Round Trip {Guid.NewGuid():N}", Type = GameType.MainGame });

        await actionService.AddAsync(new Data.Models.Action { GameId = game.Id, Name = "Play", Path = "game.exe", Type = ActionType.Primary });
        await actionService.AddAsync(new Data.Models.Action { GameId = game.Id, Name = "Editor", Path = "editor.exe", Type = ActionType.Secondary, SortOrder = 1 });

        await multiplayerModeService.AddAsync(new Data.Models.MultiplayerMode { GameId = game.Id, Type = MultiplayerType.LAN, NetworkProtocol = NetworkProtocol.TCPIP, Description = "Versus", MaxPlayers = 8 });
        await multiplayerModeService.AddAsync(new Data.Models.MultiplayerMode { GameId = game.Id, Type = MultiplayerType.LAN, NetworkProtocol = NetworkProtocol.TCPIP, Description = "Co-op", MaxPlayers = 4 });

        var archive = await archiveService.AddAsync(new Archive
        {
            GameId = game.Id,
            ObjectKey = Guid.NewGuid().ToString(),
            Version = "1.0",
            StorageLocationId = storageLocation.Id,
        });

        await File.WriteAllBytesAsync(Path.Combine(storageLocation.Path, archive.ObjectKey), ZipBytes("round trip"));

        var gameSaveService = scope.ServiceProvider.GetRequiredService<GameSaveService>();
        var owner = await scope.ServiceProvider.GetRequiredService<UserService>().GetAsync(TestConstants.AdminUserName);

        var save = await gameSaveService.AddAsync(new GameSave
        {
            GameId = game.Id,
            UserId = owner!.Id,
            StorageLocation = await gameSaveService.GetDefaultStorageLocationAsync(),
        });

        var savePath = gameSaveService.GetSavePath(save);
        Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
        await File.WriteAllBytesAsync(savePath, ZipBytes("round trip save"));

        var exportContext = scope.ServiceProvider.GetRequiredService<ExportContextFactory>().Create();
        var items = (await exportContext.InitializeExportAsync(game.Id, ImportExportRecordType.Game)).ToList();

        await exportContext.PrepareExportQueueAsync(items.Select(i => i.Id));

        var exportPath = Path.Combine(TempDirectory(), "round-trip.lcx");

        await using (var fs = new FileStream(exportPath, FileMode.Create, FileAccess.Write))
            await exportContext.ExportQueueAsync(fs);

        // Importing onto a different server: none of the records exist yet. Deleting a game leaves
        // its archives and saves behind, so remove those too.
        await archiveService.DeleteAsync(await archiveService.GetAsync(archive.Id));
        await gameSaveService.DeleteAsync(await gameSaveService.GetAsync(save.Id));
        await gameService.DeleteAsync(await gameService.GetAsync(game.Id));

        await scope.ServiceProvider.GetRequiredService<ImportRunner>().RunFileAsync(exportPath, manifestType: ManifestType.Game);

        var actions = await actionService.GetAsync(a => a.GameId == game.Id);
        var archives = await archiveService.GetAsync(a => a.GameId == game.Id);
        var modes = await multiplayerModeService.GetAsync(m => m.GameId == game.Id);
        var importedSave = Assert.Single(await gameSaveService.GetAsync(s => s.GameId == game.Id));

        Assert.Equal(["Editor", "Play"], actions.Select(a => a.Name).Order());
        Assert.True(File.Exists(await archiveService.GetArchiveFileLocationAsync(Assert.Single(archives))));
        Assert.Equal(["Co-op", "Versus"], modes.Select(m => m.Description).Order());
        Assert.Equal(owner.Id, importedSave.UserId);
        Assert.True(File.Exists(gameSaveService.GetSavePath(importedSave)), "The save's file was not written");
    }
}
