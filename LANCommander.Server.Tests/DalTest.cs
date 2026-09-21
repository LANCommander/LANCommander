using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using GameAction = LANCommander.Server.Data.Models.Action;
using GameServer = LANCommander.Server.Data.Models.Server;

namespace LANCommander.Server.Tests;

/// <summary>
/// Shared fixtures for tests that exercise the data access layer. The in-memory database is shared
/// for the whole run, so every helper here names its entity uniquely and assertions must be scoped
/// to the entities a test created rather than to global counts.
/// </summary>
public abstract class DalTest(ApplicationFixture fixture) : BaseTest(fixture)
{
    protected static string Unique(string prefix) => $"{prefix}-{Guid.NewGuid():N}";

    protected async Task<StorageLocation> GetStorageLocationAsync(StorageLocationType type = StorageLocationType.Archive)
    {
        var storageLocationService = GetService<StorageLocationService>();

        var existing = await storageLocationService.FirstOrDefaultAsync(l => l.Type == type);

        if (existing != null)
            return existing;

        return await storageLocationService.AddAsync(new StorageLocation
        {
            Path = "Uploads",
            Type = type,
            Default = true,
        });
    }

    protected Task<Game> AddGameAsync(string? title = null) =>
        GetService<GameService>().AddAsync(new Game { Title = title ?? Unique("Game") });

    protected async Task<Archive> AddArchiveAsync(Guid gameId)
    {
        var storageLocation = await GetStorageLocationAsync();

        return await GetService<ArchiveService>().AddAsync(new Archive
        {
            GameId = gameId,
            ObjectKey = Unique("object"),
            Version = "1.0",
            StorageLocationId = storageLocation.Id,
        });
    }

    protected async Task<Archive> AddRedistributableArchiveAsync(Guid redistributableId)
    {
        var storageLocation = await GetStorageLocationAsync();

        return await GetService<ArchiveService>().AddAsync(new Archive
        {
            RedistributableId = redistributableId,
            ObjectKey = Unique("object"),
            Version = "1.0",
            StorageLocationId = storageLocation.Id,
        });
    }

    protected Task<Script> AddScriptAsync(Guid gameId) =>
        GetService<ScriptService>().AddAsync(new Script
        {
            Name = Unique("Script"),
            Contents = "echo hello",
            Type = ScriptType.Install,
            GameId = gameId,
        });

    protected Task<Key> AddKeyAsync(Guid gameId) =>
        GetService<KeyService>().AddAsync(new Key
        {
            Value = Unique("KEY"),
            GameId = gameId,
        });

    protected async Task<Media> AddMediaAsync(Guid gameId)
    {
        var storageLocation = await GetStorageLocationAsync(StorageLocationType.Media);

        return await GetService<MediaService>().AddAsync(new Media
        {
            Name = Unique("Media"),
            Type = MediaType.Background,
            Crc32 = "00000000",
            FileId = Guid.NewGuid(),
            StorageLocationId = storageLocation.Id,
            GameId = gameId,
        });
    }

    protected Task<GameCustomField> AddCustomFieldAsync(Guid gameId) =>
        GetService<GameCustomFieldService>().AddAsync(new GameCustomField
        {
            Name = Unique("Field"),
            Value = "value",
            GameId = gameId,
        });

    protected Task<GameAction> AddActionAsync(Guid gameId) =>
        GetService<ActionService>().AddAsync(new GameAction
        {
            Name = Unique("Action"),
            Path = "game.exe",
            GameId = gameId,
        });

    protected Task<SavePath> AddSavePathAsync(Guid gameId) =>
        GetService<SavePathService>().AddAsync(new SavePath
        {
            Path = "save",
            WorkingDirectory = "{InstallDir}",
            Type = SavePathType.File,
            GameId = gameId,
        });

    protected Task<MultiplayerMode> AddMultiplayerModeAsync(Guid gameId) =>
        GetService<MultiplayerModeService>().AddAsync(new MultiplayerMode
        {
            Type = MultiplayerType.LAN,
            MaxPlayers = 8,
            GameId = gameId,
        });

    protected Task<Tag> AddTagAsync() =>
        GetService<TagService>().AddAsync(new Tag { Name = Unique("Tag") });

    protected Task<Genre> AddGenreAsync() =>
        GetService<GenreService>().AddAsync(new Genre { Name = Unique("Genre") });

    protected Task<Platform> AddPlatformAsync() =>
        GetService<PlatformService>().AddAsync(new Platform { Name = Unique("Platform") });

    protected Task<Category> AddCategoryAsync() =>
        GetService<CategoryService>().AddAsync(new Category { Name = Unique("Category") });

    protected Task<Company> AddCompanyAsync() =>
        GetService<CompanyService>().AddAsync(new Company { Name = Unique("Company") });

    protected Task<Collection> AddCollectionAsync() =>
        GetService<CollectionService>().AddAsync(new Collection { Name = Unique("Collection") });

    protected Task<Engine> AddEngineAsync() =>
        GetService<EngineService>().AddAsync(new Engine { Name = Unique("Engine") });

    protected Task<Redistributable> AddRedistributableAsync() =>
        GetService<RedistributableService>().AddAsync(new Redistributable { Name = Unique("Redist") });

    protected Task<GameServer> AddServerAsync() =>
        GetService<ServerService>().AddAsync(new GameServer { Name = Unique("Server") });
}
