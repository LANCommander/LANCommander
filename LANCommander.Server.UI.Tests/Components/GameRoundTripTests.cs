using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.ImportExport.Factories;
using LANCommander.Server.ImportExport.Services;
using LANCommander.Server.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// Exports a fully populated game, removes it, imports the export and compares every field, the way
/// moving a game to another server would. Mismatches are collected so one run shows all of them.
/// </summary>
[Collection("BUnit")]
public class GameRoundTripTests
{
    private readonly BUnitServerFixture _fixture;

    public GameRoundTripTests(BUnitServerFixture fixture) => _fixture = fixture;

    private sealed record Seeded(
        Game Game,
        Redistributable Redistributable,
        Game BaseGame,
        Archive Archive,
        GameSave Save,
        Media Media,
        byte[] MediaBytes,
        Script Script,
        Key Key,
        SavePath SavePath,
        User Owner);

    private const string OptionSchema = "Options:\n  - Key: Resolution\n    Type: String\n";
    private const string RedistributableOptions = "{\"Shim.Layer\":\"server\"}";

    private static async Task<Seeded> SeedAsync(IServiceProvider services)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var gameService = services.GetRequiredService<GameService>();
        var gameVersionService = services.GetRequiredService<GameVersionService>();
        var storageLocationService = services.GetRequiredService<StorageLocationService>();

        var redistributable = await services.GetRequiredService<RedistributableService>().AddAsync(new Redistributable { Name = $"Runtime {suffix}" });
        var baseGame = await gameService.AddAsync(new Game { Title = $"Base {suffix}", Type = GameType.MainGame, ReleasedOn = new DateTime(2001, 3, 1) });

        var game = await gameService.AddAsync(new Game
        {
            Title = $"Round Trip {suffix}",
            SortTitle = $"Round Trip, The {suffix}",
            Description = "A game with one of everything",
            Notes = "Imported from another server",
            ReleasedOn = new DateTime(2004, 11, 16),
            Singleplayer = true,
            Type = GameType.Expansion,
            InstallTo = GameInstallLocation.BaseGameDirectory,
            ShowInLibrary = false,
            DirectoryName = $"RoundTrip{suffix}",
            BaseGameId = baseGame.Id,
            ExternalIds = [new GameExternalId { Provider = "Steam", ExternalId = suffix }],
            Genres = [new Genre { Name = $"Genre {suffix}" }],
            Tags = [new Tag { Name = $"Tag {suffix}" }],
            Developers = [new Company { Name = $"Developer {suffix}" }],
            Publishers = [new Company { Name = $"Publisher {suffix}" }],
            Platforms = [new Platform { Name = $"Platform {suffix}" }],
            Engine = new Engine { Name = $"Engine {suffix}" },
        });

        // Adding a game links existing collections but doesn't create them
        var collection = await services.GetRequiredService<CollectionService>().AddAsync(new Collection { Name = $"Collection {suffix}" });
        await gameService.SyncRelatedCollectionAsync(game, g => g.Collections, [collection.Name], name => c => c.Name == name);
        game.Collections = [collection];

        await gameService.SetCustomFieldAsync(game.Id, "Region", "EU");

        var versionId = await gameVersionService.GetOrCreateLatestIdAsync(game.Id);
        await gameVersionService.SetOptionSchemaAsync(versionId, OptionSchema);
        await gameVersionService.SetRedistributablesAsync(versionId, [redistributable.Id]);
        await gameVersionService.SetRedistributableOptionsAsync(versionId, redistributable.Id, RedistributableOptions);

        var actionService = services.GetRequiredService<ActionService>();
        await actionService.AddAsync(new Data.Models.Action { GameId = game.Id, Name = "Play", Path = "game.exe", Arguments = "-windowed", WorkingDirectory = "{InstallDir}", Type = ActionType.Primary, Platforms = RuntimePlatform.Windows });
        await actionService.AddAsync(new Data.Models.Action { GameId = game.Id, Name = "Play", Path = "game.sh", Type = ActionType.Primary, Platforms = RuntimePlatform.Linux, SortOrder = 1 });

        var multiplayerModeService = services.GetRequiredService<MultiplayerModeService>();
        await multiplayerModeService.AddAsync(new MultiplayerMode { GameId = game.Id, Type = MultiplayerType.LAN, NetworkProtocol = NetworkProtocol.TCPIP, Description = "Versus", MinPlayers = 2, MaxPlayers = 8 });
        await multiplayerModeService.AddAsync(new MultiplayerMode { GameId = game.Id, Type = MultiplayerType.LAN, NetworkProtocol = NetworkProtocol.TCPIP, Description = "Co-op", MinPlayers = 2, MaxPlayers = 4, Spectators = 2 });

        var savePath = await services.GetRequiredService<SavePathService>().AddAsync(new SavePath { GameId = game.Id, Type = SavePathType.File, Path = "saves/*.sav", WorkingDirectory = "{InstallDir}", IsRegex = true });

        var script = await services.GetRequiredService<ScriptService>().AddAsync(new Script
        {
            GameId = game.Id,
            Name = "Install",
            Description = "Writes the config",
            Type = ScriptType.Install,
            RequiresAdmin = true,
            Contents = "Write-Host 'installed'",
        });

        var key = await services.GetRequiredService<KeyService>().AddAsync(new Key { GameId = game.Id, Value = $"KEY-{suffix}", AllocationMethod = KeyAllocationMethod.UserAccount });

        var mediaService = services.GetRequiredService<MediaService>();
        var mediaLocation = await storageLocationService.DefaultAsync(StorageLocationType.Media);
        var media = await mediaService.AddAsync(new Media
        {
            GameId = game.Id,
            FileId = Guid.NewGuid(),
            Type = MediaType.Cover,
            Name = "Front",
            MimeType = "image/png",
            SourceUrl = "https://example.com/cover.png",
            Crc32 = "",
            StorageLocationId = mediaLocation!.Id,
        });
        var mediaBytes = Guid.NewGuid().ToByteArray();
        var mediaPath = MediaService.GetMediaPath(media.FileId, mediaLocation);
        Directory.CreateDirectory(Path.GetDirectoryName(mediaPath)!);
        await File.WriteAllBytesAsync(mediaPath, mediaBytes);

        var archiveService = services.GetRequiredService<ArchiveService>();
        var archiveLocation = await storageLocationService.DefaultAsync(StorageLocationType.Archive);
        var archive = await archiveService.AddAsync(new Archive { GameId = game.Id, ObjectKey = Guid.NewGuid().ToString(), Version = "1.0", StorageLocationId = archiveLocation!.Id });
        await File.WriteAllBytesAsync(Path.Combine(archiveLocation.Path, archive.ObjectKey), ImportPackages.ZipBytes("game files"));
        await gameVersionService.LinkArchiveAsync(archive.Id, changelog: "First release");

        var owner = await services.GetRequiredService<UserService>().GetAsync(TestConstants.AdminUserName);
        var gameSaveService = services.GetRequiredService<GameSaveService>();
        var save = await gameSaveService.AddAsync(new GameSave { GameId = game.Id, UserId = owner!.Id, StorageLocation = await gameSaveService.GetDefaultStorageLocationAsync() });
        var saveFile = gameSaveService.GetSavePath(save);
        Directory.CreateDirectory(Path.GetDirectoryName(saveFile)!);
        await File.WriteAllBytesAsync(saveFile, ImportPackages.ZipBytes("save"));

        return new Seeded(game, redistributable, baseGame, archive, save, media, mediaBytes, script, key, savePath, owner);
    }

    private static async Task<string> ExportAsync(IServiceProvider services, Guid gameId)
    {
        var exportContext = services.GetRequiredService<ExportContextFactory>().Create();
        var items = (await exportContext.InitializeExportAsync(gameId, ImportExportRecordType.Game)).ToList();

        await exportContext.PrepareExportQueueAsync(items.Select(i => i.Id));

        var path = Path.Combine(ImportPackages.TempDirectory(), "round-trip.lcx");

        await using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            await exportContext.ExportQueueAsync(fs);

        return path;
    }

    /// <summary>Removes the game and everything it owns, like a server that never had it.</summary>
    private static async Task RemoveAsync(IServiceProvider services, Seeded seeded)
    {
        var gameId = seeded.Game.Id;

        foreach (var action in await services.GetRequiredService<ActionService>().GetAsync(a => a.GameId == gameId))
            await services.GetRequiredService<ActionService>().DeleteAsync(action);

        foreach (var mode in await services.GetRequiredService<MultiplayerModeService>().GetAsync(m => m.GameId == gameId))
            await services.GetRequiredService<MultiplayerModeService>().DeleteAsync(mode);

        await services.GetRequiredService<ScriptService>().DeleteAsync(await services.GetRequiredService<ScriptService>().GetAsync(seeded.Script.Id));
        await services.GetRequiredService<KeyService>().DeleteAsync(await services.GetRequiredService<KeyService>().GetAsync(seeded.Key.Id));
        await services.GetRequiredService<SavePathService>().DeleteAsync(await services.GetRequiredService<SavePathService>().GetAsync(seeded.SavePath.Id));
        await services.GetRequiredService<MediaService>().DeleteAsync(await services.GetRequiredService<MediaService>().GetAsync(seeded.Media.Id));
        await services.GetRequiredService<ArchiveService>().DeleteAsync(await services.GetRequiredService<ArchiveService>().GetAsync(seeded.Archive.Id));
        await services.GetRequiredService<GameSaveService>().DeleteAsync(await services.GetRequiredService<GameSaveService>().GetAsync(seeded.Save.Id));

        var gameService = services.GetRequiredService<GameService>();
        await gameService.DeleteAsync(await gameService.GetAsync(gameId));
    }

    private sealed class Mismatches : List<string>
    {
        public void Check<T>(string field, T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                Add($"{field}: expected <{expected}> but was <{actual}>");
        }

        public void CheckAll(string field, IEnumerable<string?> expected, IEnumerable<string?>? actual)
        {
            var expectedList = expected.Order().ToList();
            var actualList = (actual ?? []).Order().ToList();

            if (!expectedList.SequenceEqual(actualList))
                Add($"{field}: expected [{String.Join(", ", expectedList)}] but was [{String.Join(", ", actualList)}]");
        }
    }

    [Fact]
    public async Task ExportedGameImportsWithEveryField()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();
        var services = scope.ServiceProvider;

        var seeded = await SeedAsync(services);
        var source = seeded.Game;
        var exportPath = await ExportAsync(services, source.Id);

        await RemoveAsync(services, seeded);

        var result = await services.GetRequiredService<ImportRunner>().RunFileAsync(exportPath);

        var mismatches = new Mismatches();
        mismatches.CheckAll("Import errors", [], result.Errors);

        var game = await services.GetRequiredService<GameService>()
            .Include(g => g.Genres, g => g.Tags, g => g.Developers, g => g.Publishers, g => g.Platforms, g => g.Collections, g => g.Engine!, g => g.ExternalIds!, g => g.CustomFields!)
            .GetAsync(source.Id);

        Assert.NotNull(game);

        mismatches.Check("Title", source.Title, game.Title);
        mismatches.Check("SortTitle", source.SortTitle, game.SortTitle);
        mismatches.Check("Description", source.Description, game.Description);
        mismatches.Check("Notes", source.Notes, game.Notes);
        mismatches.Check("ReleasedOn", source.ReleasedOn, game.ReleasedOn);
        mismatches.Check("Singleplayer", source.Singleplayer, game.Singleplayer);
        mismatches.Check("Type", source.Type, game.Type);
        mismatches.Check("InstallTo", source.InstallTo, game.InstallTo);
        mismatches.Check("ShowInLibrary", source.ShowInLibrary, game.ShowInLibrary);
        mismatches.Check("DirectoryName", source.DirectoryName, game.DirectoryName);
        mismatches.Check("BaseGameId", seeded.BaseGame.Id, game.BaseGameId);
        mismatches.CheckAll("ExternalIds", source.ExternalIds!.Select(e => $"{e.Provider}:{e.ExternalId}"), game.ExternalIds?.Select(e => $"{e.Provider}:{e.ExternalId}"));
        mismatches.CheckAll("Genres", source.Genres!.Select(g => g.Name), game.Genres?.Select(g => g.Name));
        mismatches.CheckAll("Tags", source.Tags!.Select(t => t.Name), game.Tags?.Select(t => t.Name));
        mismatches.CheckAll("Developers", source.Developers!.Select(d => d.Name), game.Developers?.Select(d => d.Name));
        mismatches.CheckAll("Publishers", source.Publishers!.Select(p => p.Name), game.Publishers?.Select(p => p.Name));
        mismatches.CheckAll("Platforms", source.Platforms!.Select(p => p.Name), game.Platforms?.Select(p => p.Name));
        mismatches.CheckAll("Collections", source.Collections!.Select(c => c.Name), game.Collections?.Select(c => c.Name));
        mismatches.Check("Engine", source.Engine!.Name, game.Engine?.Name);
        mismatches.CheckAll("CustomFields", ["Region=EU"], game.CustomFields?.Select(c => $"{c.Name}={c.Value}"));

        var actions = await services.GetRequiredService<ActionService>().GetAsync(a => a.GameId == source.Id);
        mismatches.CheckAll("Actions", ["Play|game.exe|-windowed|{InstallDir}|Windows|0|Primary", "Play|game.sh|||Linux|1|Primary"],
            actions.Select(a => $"{a.Name}|{a.Path}|{a.Arguments}|{a.WorkingDirectory}|{a.Platforms}|{a.SortOrder}|{a.Type}"));

        var modes = await services.GetRequiredService<MultiplayerModeService>().GetAsync(m => m.GameId == source.Id);
        mismatches.CheckAll("MultiplayerModes", ["LAN|TCPIP|Co-op|2-4|2", "LAN|TCPIP|Versus|2-8|0"],
            modes.Select(m => $"{m.Type}|{m.NetworkProtocol}|{m.Description}|{m.MinPlayers}-{m.MaxPlayers}|{m.Spectators}"));

        var savePaths = await services.GetRequiredService<SavePathService>().GetAsync(p => p.GameId == source.Id);
        mismatches.CheckAll("SavePaths", ["File|saves/*.sav|{InstallDir}|True"], savePaths.Select(p => $"{p.Type}|{p.Path}|{p.WorkingDirectory}|{p.IsRegex}"));

        var scripts = await services.GetRequiredService<ScriptService>().GetAsync(s => s.GameId == source.Id);
        mismatches.CheckAll("Scripts", ["Install|Install|Writes the config|True|Write-Host 'installed'"],
            scripts.Select(s => $"{s.Type}|{s.Name}|{s.Description}|{s.RequiresAdmin}|{s.Contents}"));

        var keys = await services.GetRequiredService<KeyService>().GetAsync(k => k.GameId == source.Id);
        mismatches.CheckAll("Keys", [$"{seeded.Key.Value}|UserAccount"], keys.Select(k => $"{k.Value}|{k.AllocationMethod}"));

        var mediaService = services.GetRequiredService<MediaService>();
        var media = await mediaService.Include(m => m.StorageLocation!).GetAsync(m => m.GameId == source.Id);
        mismatches.CheckAll("Media", ["Cover|Front|image/png|https://example.com/cover.png"], media.Select(m => $"{m.Type}|{m.Name}|{m.MimeType}|{m.SourceUrl}"));

        if (media.Count == 1)
        {
            var mediaFile = MediaService.GetMediaPath(media.First());
            mismatches.Check("Media file", Convert.ToBase64String(seeded.MediaBytes), File.Exists(mediaFile) ? Convert.ToBase64String(await File.ReadAllBytesAsync(mediaFile)) : "(missing)");
        }

        var archiveService = services.GetRequiredService<ArchiveService>();
        var archives = await archiveService.GetAsync(a => a.GameId == source.Id);
        mismatches.CheckAll("Archives", ["1.0"], archives.Select(a => a.Version));

        if (archives.Count == 1)
            mismatches.Check("Archive file", true, File.Exists(await archiveService.GetArchiveFileLocationAsync(archives.First())));

        var gameSaveService = services.GetRequiredService<GameSaveService>();
        var saves = await gameSaveService.Include(s => s.StorageLocation!).GetAsync(s => s.GameId == source.Id);
        mismatches.CheckAll("Saves", [seeded.Owner.Id.ToString()], saves.Select(s => s.UserId.ToString()));

        if (saves.Count == 1)
            mismatches.Check("Save file", true, File.Exists(gameSaveService.GetSavePath(saves.First())));

        var gameVersionService = services.GetRequiredService<GameVersionService>();
        var versions = (await gameVersionService.GetAllAsync(source.Id)).ToList();
        mismatches.CheckAll("Versions", ["1.0"], versions.Select(v => v.Version));

        var latest = versions.FirstOrDefault();

        if (latest != null)
        {
            mismatches.Check("Changelog", "First release", latest.Changelog);

            // Actions, scripts and save paths belong to versions; the game screens and launchers read them from there
            mismatches.Check("Actions on the version", 2, (await services.GetRequiredService<ActionService>().GetAsync(a => a.GameVersionId == latest.Id)).Count);
            mismatches.Check("Scripts on the version", 1, (await services.GetRequiredService<ScriptService>().GetAsync(s => s.GameVersionId == latest.Id)).Count);
            mismatches.Check("Save paths on the version", 1, (await services.GetRequiredService<SavePathService>().GetAsync(p => p.GameVersionId == latest.Id)).Count);
            mismatches.Check("OptionSchema", OptionSchema, await gameVersionService.GetOptionSchemaAsync(latest.Id));
            mismatches.CheckAll("Redistributables", [seeded.Redistributable.Id.ToString()],
                (await gameVersionService.GetRedistributablesAsync(latest.Id)).Select(r => r.RedistributableId.ToString()));
            mismatches.Check("RedistributableOptions", RedistributableOptions,
                await gameVersionService.GetRedistributableOptionsAsync(latest.Id, seeded.Redistributable.Id));
        }

        Assert.True(mismatches.Count == 0, String.Join(Environment.NewLine, mismatches));
    }

    /// <summary>Importing the same export again leaves exactly the same records.</summary>
    [Fact]
    public async Task ReimportingAnExportDuplicatesNothing()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();
        var services = scope.ServiceProvider;

        var seeded = await SeedAsync(services);
        var gameId = seeded.Game.Id;
        var exportPath = await ExportAsync(services, gameId);

        async Task<string> SnapshotAsync() => String.Join(" / ",
            $"actions={(await services.GetRequiredService<ActionService>().GetAsync(a => a.GameId == gameId)).Count}",
            $"modes={(await services.GetRequiredService<MultiplayerModeService>().GetAsync(m => m.GameId == gameId)).Count}",
            $"scripts={(await services.GetRequiredService<ScriptService>().GetAsync(s => s.GameId == gameId)).Count}",
            $"keys={(await services.GetRequiredService<KeyService>().GetAsync(k => k.GameId == gameId)).Count}",
            $"media={(await services.GetRequiredService<MediaService>().GetAsync(m => m.GameId == gameId)).Count}",
            $"archives={(await services.GetRequiredService<ArchiveService>().GetAsync(a => a.GameId == gameId)).Count}",
            $"saves={(await services.GetRequiredService<GameSaveService>().GetAsync(s => s.GameId == gameId)).Count}",
            $"savePaths={(await services.GetRequiredService<SavePathService>().GetAsync(p => p.GameId == gameId)).Count}",
            $"versions={(await services.GetRequiredService<GameVersionService>().GetAllAsync(gameId)).Count()}");

        var before = await SnapshotAsync();

        var importRunner = services.GetRequiredService<ImportRunner>();
        var first = await importRunner.RunFileAsync(exportPath);
        var second = await importRunner.RunFileAsync(exportPath);

        Assert.Empty(first.Errors);
        Assert.Empty(second.Errors);
        Assert.Equal(before, await SnapshotAsync());
    }
}
