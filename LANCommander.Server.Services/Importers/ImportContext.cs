using System.IO.Compression;
using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.Server.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZipArchive = SharpCompress.Archives.Zip.ZipArchive;

namespace LANCommander.Server.Services.Importers;

public class ImportContext(
    BaseImporter<SDK.Models.Manifest.Game, Data.Models.Game> gameImporter,
    BaseImporter<SDK.Models.Manifest.Redistributable, Data.Models.Redistributable> redistributableImporter,
    BaseImporter<SDK.Models.Manifest.Server, Data.Models.Server> serverImporter,
    GameService gameService,
    RedistributableService redistributableService,
    ServerService serverService,
    ArchiveService archiveService,
    MediaService mediaService,
    GameSaveService saveService,
    BaseImporter<SDK.Models.Manifest.Action, Data.Models.Action> actionImporter,
    BaseImporter<SDK.Models.Manifest.Archive, Data.Models.Archive> archiveImporter,
    BaseImporter<SDK.Models.Manifest.Collection, Data.Models.Collection> collectionImporter,
    BaseImporter<SDK.Models.Manifest.GameCustomField, Data.Models.GameCustomField> customFieldImporter,
    DeveloperImporter developerImporter,
    PublisherImporter publisherImporter,
    BaseImporter<SDK.Models.Manifest.Engine, Data.Models.Engine> engineImporter,
    BaseImporter<SDK.Models.Manifest.Genre, Data.Models.Genre> genreImporter,
    BaseImporter<SDK.Models.Manifest.Key, Data.Models.Key> keyImporter,
    BaseImporter<SDK.Models.Manifest.Media, Data.Models.Media> mediaImporter,
    BaseImporter<SDK.Models.Manifest.MultiplayerMode, Data.Models.MultiplayerMode> multiplayerModeImporter,
    BaseImporter<SDK.Models.Manifest.Platform, Data.Models.Platform> platformImporter,
    BaseImporter<SDK.Models.Manifest.PlaySession, Data.Models.PlaySession> playSessionImporter,
    BaseImporter<SDK.Models.Manifest.Save, Data.Models.GameSave> saveImporter,
    BaseImporter<SDK.Models.Manifest.SavePath, Data.Models.SavePath> savePathImporter,
    BaseImporter<SDK.Models.Manifest.Script, Data.Models.Script> scriptImporter,
    BaseImporter<SDK.Models.Manifest.ServerConsole, Data.Models.ServerConsole> serverConsoleImporter,
    BaseImporter<SDK.Models.Manifest.ServerHttpPath, Data.Models.ServerHttpPath> serverHttpPathImporter,
    BaseImporter<SDK.Models.Manifest.Tag, Data.Models.Tag> tagImporter,
    IMapper mapper,
    ILogger<ImportContext> logger) : IDisposable
{
    public object Manifest { get; private set; }
    public BaseModel DataRecord { get; private set; }
    public StorageLocation ArchiveStorageLocation { get; }
    public ZipArchive Archive { get; private set; }

    public BaseImporter<SDK.Models.Manifest.Game, Data.Models.Game> Games { get; private set; } = gameImporter;
    public BaseImporter<SDK.Models.Manifest.Redistributable, Data.Models.Redistributable> Redistributables { get; private set; } = redistributableImporter;
    public BaseImporter<SDK.Models.Manifest.Server, Data.Models.Server> Servers { get; private set; } = serverImporter;

    public BaseImporter<SDK.Models.Manifest.Action, Data.Models.Action> Actions = actionImporter;
    public BaseImporter<SDK.Models.Manifest.Archive, Data.Models.Archive> Archives = archiveImporter;
    public BaseImporter<SDK.Models.Manifest.Collection, Data.Models.Collection> Collections = collectionImporter;
    public BaseImporter<SDK.Models.Manifest.GameCustomField, Data.Models.GameCustomField> CustomFields = customFieldImporter;
    public BaseImporter<SDK.Models.Manifest.Company, Data.Models.Company> Developers = developerImporter;
    public BaseImporter<SDK.Models.Manifest.Engine, Data.Models.Engine> Engines = engineImporter;
    public BaseImporter<SDK.Models.Manifest.Genre, Data.Models.Genre> Genres = genreImporter;
    public BaseImporter<SDK.Models.Manifest.Key, Data.Models.Key> Keys = keyImporter;
    public BaseImporter<SDK.Models.Manifest.Media, Data.Models.Media> Media = mediaImporter;
    public BaseImporter<SDK.Models.Manifest.MultiplayerMode, Data.Models.MultiplayerMode> MultiplayerModes = multiplayerModeImporter;
    public BaseImporter<SDK.Models.Manifest.Platform, Data.Models.Platform> Platforms = platformImporter;
    public BaseImporter<SDK.Models.Manifest.PlaySession, Data.Models.PlaySession> PlaySessions = playSessionImporter;
    public BaseImporter<SDK.Models.Manifest.Company, Data.Models.Company> Publishers = publisherImporter;
    public BaseImporter<SDK.Models.Manifest.Save, Data.Models.GameSave> Saves = saveImporter;
    public BaseImporter<SDK.Models.Manifest.SavePath, Data.Models.SavePath> SavePaths = savePathImporter;
    public BaseImporter<SDK.Models.Manifest.Script, Data.Models.Script> Scripts = scriptImporter;
    public BaseImporter<SDK.Models.Manifest.ServerConsole, Data.Models.ServerConsole> ServerConsoles = serverConsoleImporter;
    public BaseImporter<SDK.Models.Manifest.ServerHttpPath, Data.Models.ServerHttpPath> ServerHttpPaths = serverHttpPathImporter;
    public BaseImporter<SDK.Models.Manifest.Tag, Data.Models.Tag> Tags = tagImporter;

    public int Remaining => _queue.Count;
    public int Processed => _queue.Count(qi => qi.Processed);
    public int Total => _queue.Count;

    private List<ImportQueueItem> _queue { get; } = new();
    public Dictionary<ImportQueueItem, string> Errored { get; } = new();

    public EventHandler<ImportQueueItem> OnRecordAdded;
    public EventHandler<ImportQueueItem> OnRecordProcessed;
    public EventHandler<ImportQueueItem> OnRecordError;

    private void UseContext(ImportContext context)
    {
        Games.UseContext(context);
        Redistributables.UseContext(context);
        Servers.UseContext(context);
        
        Actions.UseContext(context);
        Archives.UseContext(context);
        Collections.UseContext(context);
        CustomFields.UseContext(context);
        Developers.UseContext(context);
        Engines.UseContext(context);
        Genres.UseContext(context);
        Keys.UseContext(context);
        Media.UseContext(context);
        MultiplayerModes.UseContext(context);
        Platforms.UseContext(context);
        PlaySessions.UseContext(context);
        Publishers.UseContext(context);
        Saves.UseContext(context);
        SavePaths.UseContext(context);
        Scripts.UseContext(context);
        ServerConsoles.UseContext(context);
        ServerHttpPaths.UseContext(context);
        Tags.UseContext(context);
    }
    
    #region Initialize Import
    public async Task<IEnumerable<ImportItemInfo>> InitializeImportAsync(string archivePath)
    {
        UseContext(this);
        
        Archive = ZipArchive.Open(archivePath);

        var manifestEntry = Archive.Entries.FirstOrDefault(e => e.Key == ManifestHelper.ManifestFilename);
        
        using (var reader = new StreamReader(manifestEntry.OpenEntryStream()))
        {
            var manifestContents = await reader.ReadToEndAsync();

            if (ManifestHelper.TryDeserialize<SDK.Models.Manifest.Game>(manifestContents, out var gameManifest))
                return await InitializeGameImportAsync(gameManifest);
            
            if (ManifestHelper.TryDeserialize<SDK.Models.Manifest.Redistributable>(manifestContents, out var redistributableManifest))
                return await InitializeRedistributableImportAsync(redistributableManifest);
            
            if (ManifestHelper.TryDeserialize<SDK.Models.Manifest.Server>(manifestContents, out var serverManifest))
                return await InitializeServerImportAsync(serverManifest);
                
            throw new InvalidOperationException("Unknown manifest file");
        }
    }

    private async Task<IEnumerable<ImportItemInfo>> InitializeGameImportAsync(SDK.Models.Manifest.Game gameManifest)
    {
        Manifest = gameManifest;
        
        var importItemInfo = new List<ImportItemInfo>();
        
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Actions, Actions).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Archives, Archives).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Collections, Collections).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.CustomFields, CustomFields).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Developers, Developers).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync([gameManifest.Engine], Engines).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Genres, Genres).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Keys, Keys).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Media, Media).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.MultiplayerModes, MultiplayerModes).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Platforms, Platforms).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.PlaySessions, PlaySessions).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Publishers, Publishers).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Saves, Saves).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.SavePaths, SavePaths).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Scripts, Scripts).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Tags, Tags).ToListAsync());

        return importItemInfo;
    }

    private async Task<IEnumerable<ImportItemInfo>> InitializeRedistributableImportAsync(SDK.Models.Manifest.Redistributable redistributableManifest)
    {
        Manifest = redistributableManifest;
        
        var importItemInfo = new List<ImportItemInfo>();
        
        importItemInfo.AddRange(await GetImportItemInfoAsync(redistributableManifest.Archives, Archives).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(redistributableManifest.Scripts, Scripts).ToListAsync());

        return importItemInfo;
    }

    private async Task<IEnumerable<ImportItemInfo>> InitializeServerImportAsync(SDK.Models.Manifest.Server serverManifest)
    {
        Manifest = serverManifest;
        
        var importItemInfo = new List<ImportItemInfo>();
        
        importItemInfo.AddRange(await GetImportItemInfoAsync(serverManifest.Actions, Actions).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(serverManifest.Scripts, Scripts).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(serverManifest.ServerConsoles, ServerConsoles).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(serverManifest.HttpPaths, ServerHttpPaths).ToListAsync());
        
        return importItemInfo;
    }
    #endregion
    
    #region Initialize Export
    public async Task<IEnumerable<ExportItemInfo>> InitializeExportAsync(Guid recordId, ExportRecordType recordType)
    {
        UseContext(this);
        
        switch (recordType)
        {
            case ExportRecordType.Game:
                return await InitializeGameExportAsync(recordId);
            
            case ExportRecordType.Redistributable:
                return await InitializeRedistributableExportAsync(recordId);
            
            case ExportRecordType.Server:
                return await InitializeServerExportAsync(recordId);
            
            default:
                throw new InvalidOperationException("Unknown record type");
        }
    }

    private async Task<IEnumerable<ExportItemInfo>> InitializeGameExportAsync(Guid gameId)
    {
        var game = await gameService.Query(q =>
        {
            return q
                .AsNoTracking()
                .AsSplitQuery()
                .Include(g => g.Actions)
                .Include(g => g.Archives)
                .Include(g => g.BaseGame)
                .Include(g => g.Categories)
                .Include(g => g.Collections)
                .Include(g => g.CustomFields)
                .Include(g => g.DependentGames)
                .Include(g => g.Developers)
                .Include(g => g.Engine)
                .Include(g => g.Genres)
                .Include(g => g.Media)
                .Include(g => g.MultiplayerModes)
                .Include(g => g.Platforms)
                .Include(g => g.Publishers)
                .Include(g => g.Redistributables)
                .Include(g => g.SavePaths)
                .Include(g => g.Tags);
        }).GetAsync(gameId);
        
        var gameManifest = mapper.Map<SDK.Models.Manifest.Game>(game);

        DataRecord = game;
        Manifest = gameManifest;
        
        var exportItemInfo = new List<ExportItemInfo>();
        
        exportItemInfo.AddRange(await GetExportItemInfoAsync(game.Actions, Actions).ToListAsync());
        exportItemInfo.AddRange(await GetExportItemInfoAsync(game.Archives, Archives).ToListAsync());
        exportItemInfo.AddRange(await GetExportItemInfoAsync(game.Collections, Collections).ToListAsync());
        exportItemInfo.AddRange(await GetExportItemInfoAsync(game.CustomFields, CustomFields).ToListAsync());
        exportItemInfo.AddRange(await GetExportItemInfoAsync(game.Developers, Developers).ToListAsync());
        exportItemInfo.AddRange(await GetExportItemInfoAsync([game.Engine], Engines).ToListAsync());
        exportItemInfo.AddRange(await GetExportItemInfoAsync(game.Genres, Genres).ToListAsync());
        exportItemInfo.AddRange(await GetExportItemInfoAsync(game.Keys, Keys).ToListAsync());
        exportItemInfo.AddRange(await GetExportItemInfoAsync(game.Media, Media).ToListAsync());
        exportItemInfo.AddRange(await GetExportItemInfoAsync(game.MultiplayerModes, MultiplayerModes).ToListAsync());
        exportItemInfo.AddRange(await GetExportItemInfoAsync(game.Platforms, Platforms).ToListAsync());
        exportItemInfo.AddRange(await GetExportItemInfoAsync(game.PlaySessions, PlaySessions).ToListAsync());
        exportItemInfo.AddRange(await GetExportItemInfoAsync(game.Publishers, Publishers).ToListAsync());
        exportItemInfo.AddRange(await GetExportItemInfoAsync(game.GameSaves, Saves).ToListAsync());
        exportItemInfo.AddRange(await GetExportItemInfoAsync(game.SavePaths, SavePaths).ToListAsync());
        exportItemInfo.AddRange(await GetExportItemInfoAsync(game.Scripts, Scripts).ToListAsync());
        exportItemInfo.AddRange(await GetExportItemInfoAsync(game.Tags, Tags).ToListAsync());

        return exportItemInfo;
    }

    private async Task<IEnumerable<ExportItemInfo>> InitializeRedistributableExportAsync(Guid redistributableId)
    {
        var redistributable = await redistributableService
            .AsNoTracking()
            .AsSplitQuery()
            .Query(q =>
            {
                return q
                    .Include(r => r.Archives)
                    .Include(r => r.Scripts);
            })
            .GetAsync(redistributableId);
        
        Manifest = mapper.Map<SDK.Models.Manifest.Redistributable>(redistributable);
        
        var exportItemInfo = new List<ExportItemInfo>();
        
        exportItemInfo.AddRange(await GetExportItemInfoAsync(redistributable.Archives, Archives).ToListAsync());
        exportItemInfo.AddRange(await GetExportItemInfoAsync(redistributable.Scripts, Scripts).ToListAsync());

        return exportItemInfo;
    }

    private async Task<IEnumerable<ExportItemInfo>> InitializeServerExportAsync(Guid serverId)
    {
        var server = await serverService 
            .AsNoTracking()
            .AsSplitQuery()
            .Query(q =>
            {
                return q
                    .Include(s => s.Actions)
                    .Include(s => s.HttpPaths)
                    .Include(s => s.ServerConsoles)
                    .Include(s => s.Scripts);

            })
            .GetAsync(serverId);
        
        Manifest = mapper.Map<SDK.Models.Manifest.Server>(server);
        
        var exportItemInfo = new List<ExportItemInfo>();
        
        exportItemInfo.AddRange(await GetExportItemInfoAsync(server.Actions, Actions).ToListAsync());
        exportItemInfo.AddRange(await GetExportItemInfoAsync(server.Scripts, Scripts).ToListAsync());
        exportItemInfo.AddRange(await GetExportItemInfoAsync(server.ServerConsoles, ServerConsoles).ToListAsync());
        exportItemInfo.AddRange(await GetExportItemInfoAsync(server.HttpPaths, ServerHttpPaths).ToListAsync());
        
        return exportItemInfo;
    }
    #endregion
    
    #region Prepare Import Queue
    public async Task PrepareImportQueueAsync(ImportRecordFlags importRecordFlags)
    {
        if (Manifest is SDK.Models.Manifest.Game gameManifest)
            await PrepareGameImportQueueAsync(gameManifest, importRecordFlags);
        
        if (Manifest is SDK.Models.Manifest.Redistributable redistributableManifest)
            await PrepareRedistributableImportQueueAsync(redistributableManifest, importRecordFlags);
        
        if (Manifest is SDK.Models.Manifest.Server serverManifest)
            await PrepareServerImportQueueAsync(serverManifest, importRecordFlags);
    }

    public async Task PrepareGameImportQueueAsync(SDK.Models.Manifest.Game gameManifest, ImportRecordFlags importRecordFlags)
    {
        if (!(await gameImporter.ExistsAsync(gameManifest)))
            DataRecord = await gameImporter.AddAsync(gameManifest);
        else
            DataRecord = await gameImporter.UpdateAsync(gameManifest);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Actions))
            await AddToImportQueueAsync(ImportRecordFlags.Actions, gameManifest.Actions);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Archives))
            await AddToImportQueueAsync(ImportRecordFlags.Archives, gameManifest.Archives);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Collections))
            await AddToImportQueueAsync(ImportRecordFlags.Collections, gameManifest.Collections);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.CustomFields))
            await AddToImportQueueAsync(ImportRecordFlags.CustomFields, gameManifest.CustomFields);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Developers))
            await AddToImportQueueAsync(ImportRecordFlags.Developers, gameManifest.Developers);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Engine))
            await AddToImportQueueAsync(ImportRecordFlags.Engine, [gameManifest.Engine]);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Genres))
            await AddToImportQueueAsync(ImportRecordFlags.Genres, gameManifest.Genres);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Keys))
            await AddToImportQueueAsync(ImportRecordFlags.Keys, gameManifest.Keys);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Media))
            await AddToImportQueueAsync(ImportRecordFlags.Media, gameManifest.Media);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.MultiplayerModes))
            await AddToImportQueueAsync(ImportRecordFlags.MultiplayerModes, gameManifest.MultiplayerModes);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Platforms))
            await AddToImportQueueAsync(ImportRecordFlags.Platforms, gameManifest.Platforms);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.PlaySessions))
            await AddToImportQueueAsync(ImportRecordFlags.PlaySessions, gameManifest.PlaySessions);

        if (importRecordFlags.HasFlag(ImportRecordFlags.Publishers))
            await AddToImportQueueAsync(ImportRecordFlags.Publishers, gameManifest.Publishers);

        if (importRecordFlags.HasFlag(ImportRecordFlags.Saves))
            await AddToImportQueueAsync(ImportRecordFlags.Saves, gameManifest.Saves);

        if (importRecordFlags.HasFlag(ImportRecordFlags.Scripts))
            await AddToImportQueueAsync(ImportRecordFlags.SavePaths, gameManifest.SavePaths);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Scripts))
            await AddToImportQueueAsync(ImportRecordFlags.Scripts, gameManifest.Scripts);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Archives))
            await AddToImportQueueAsync(ImportRecordFlags.Tags, gameManifest.Tags);
    }

    public async Task PrepareRedistributableImportQueueAsync(SDK.Models.Manifest.Redistributable redistributableManifest,
        ImportRecordFlags importRecordFlags)
    { 
        if (!(await redistributableImporter.ExistsAsync(redistributableManifest)))
            DataRecord = await redistributableImporter.AddAsync(redistributableManifest);
        else
            DataRecord = await redistributableImporter.UpdateAsync(redistributableManifest);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Archives))
            await AddToImportQueueAsync(ImportRecordFlags.Archives, redistributableManifest.Archives);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Scripts))
            await AddToImportQueueAsync(ImportRecordFlags.Scripts, redistributableManifest.Scripts);
    }

    public async Task PrepareServerImportQueueAsync(SDK.Models.Manifest.Server serverManifest,
        ImportRecordFlags importRecordFlags)
    {
        if (!(await serverImporter.ExistsAsync(serverManifest)))
            DataRecord = await serverImporter.AddAsync(serverManifest);
        else
            DataRecord = await serverImporter.UpdateAsync(serverManifest);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Actions))
            await AddToImportQueueAsync(ImportRecordFlags.Actions, serverManifest.Actions);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Scripts))
            await AddToImportQueueAsync(ImportRecordFlags.Scripts, serverManifest.Scripts);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.ServerConsoles))
            await AddToImportQueueAsync(ImportRecordFlags.ServerConsoles, serverManifest.ServerConsoles);

        if (importRecordFlags.HasFlag(ImportRecordFlags.ServerHttpPaths))
            await AddToImportQueueAsync(ImportRecordFlags.ServerHttpPaths, serverManifest.HttpPaths);
    }
    #endregion
    
    #region Prepare Export Queue
    public async Task PrepareExportQueueAsync(ImportRecordFlags importRecordFlags)
    {
        if (DataRecord is Data.Models.Game game)
            await PrepareGameExportQueueAsync(game, importRecordFlags);
        
        if (DataRecord is Data.Models.Redistributable redistributable)
            await PrepareRedistributableExportQueueAsync(redistributable, importRecordFlags);
        
        if (DataRecord is Data.Models.Server server)
            await PrepareServerExportQueueAsync(server, importRecordFlags);
    }
    
    public async Task PrepareGameExportQueueAsync(Game game, ImportRecordFlags importRecordFlags)
    {
        if (importRecordFlags.HasFlag(ImportRecordFlags.Actions))
            await AddToExportQueueAsync(ImportRecordFlags.Actions, game.Actions);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Archives))
            await AddToExportQueueAsync(ImportRecordFlags.Archives, game.Archives);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Collections))
            await AddToExportQueueAsync(ImportRecordFlags.Collections, game.Collections);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.CustomFields))
            await AddToExportQueueAsync(ImportRecordFlags.CustomFields, game.CustomFields);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Developers))
            await AddToExportQueueAsync(ImportRecordFlags.Developers, game.Developers);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Engine) && game.Engine != null)
            await AddToExportQueueAsync(ImportRecordFlags.Engine, [game.Engine]);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Genres))
            await AddToExportQueueAsync(ImportRecordFlags.Genres, game.Genres);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Keys))
            await AddToExportQueueAsync(ImportRecordFlags.Keys, game.Keys);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Media))
            await AddToExportQueueAsync(ImportRecordFlags.Media, game.Media);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.MultiplayerModes))
            await AddToExportQueueAsync(ImportRecordFlags.MultiplayerModes, game.MultiplayerModes);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Platforms))
            await AddToExportQueueAsync(ImportRecordFlags.Platforms, game.Platforms);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.PlaySessions))
            await AddToExportQueueAsync(ImportRecordFlags.PlaySessions, game.PlaySessions);

        if (importRecordFlags.HasFlag(ImportRecordFlags.Publishers))
            await AddToExportQueueAsync(ImportRecordFlags.Publishers, game.Publishers);

        if (importRecordFlags.HasFlag(ImportRecordFlags.Saves))
            await AddToExportQueueAsync(ImportRecordFlags.Saves, game.GameSaves);

        if (importRecordFlags.HasFlag(ImportRecordFlags.SavePaths))
            await AddToExportQueueAsync(ImportRecordFlags.SavePaths, game.SavePaths);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Scripts))
            await AddToExportQueueAsync(ImportRecordFlags.Scripts, game.Scripts);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Tags))
            await AddToExportQueueAsync(ImportRecordFlags.Tags, game.Tags);
    }

    public async Task PrepareRedistributableExportQueueAsync(Data.Models.Redistributable redistributable,
        ImportRecordFlags importRecordFlags)
    {
        if (importRecordFlags.HasFlag(ImportRecordFlags.Archives))
            await AddToExportQueueAsync(ImportRecordFlags.Archives, redistributable.Archives);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Scripts))
            await AddToExportQueueAsync(ImportRecordFlags.Scripts, redistributable.Scripts);
    }

    public async Task PrepareServerExportQueueAsync(Data.Models.Server server,
        ImportRecordFlags importRecordFlags)
    {
        if (importRecordFlags.HasFlag(ImportRecordFlags.Actions))
            await AddToExportQueueAsync(ImportRecordFlags.Actions, server.Actions);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Scripts))
            await AddToExportQueueAsync(ImportRecordFlags.Scripts, server.Scripts);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.ServerConsoles))
            await AddToExportQueueAsync(ImportRecordFlags.ServerConsoles, server.ServerConsoles);

        if (importRecordFlags.HasFlag(ImportRecordFlags.ServerHttpPaths))
            await AddToExportQueueAsync(ImportRecordFlags.ServerHttpPaths, server.HttpPaths);
    }
    #endregion
    
    private async Task AddToExportQueueAsync<TEntity>(ImportRecordFlags type, IEnumerable<TEntity> records) where TEntity : Data.Models.BaseModel
    {
        if (records != null)
            _queue.AddRange(records.Select(r => new ImportQueueItem(r.Id, type, r)));
    }

    private async Task AddToImportQueueAsync<TRecord>(ImportRecordFlags type, IEnumerable<TRecord> records) where TRecord : SDK.Models.Manifest.BaseModel
    {
        if (records != null)
            _queue.AddRange(records.Select(r => new ImportQueueItem(type, r)));
    }

    private async Task AddToQueueAsync(Guid id, ImportRecordFlags type, object record)
    {
        if (record != null)
            _queue.Add(new ImportQueueItem(id, type, record));
    }

    public async Task ImportQueueAsync()
    {
        foreach (var queueItem in _queue)
        {
            if (queueItem.Type == ImportRecordFlags.Actions)
                await ImportRecordAsync(queueItem, Actions);
            else if (queueItem.Type == ImportRecordFlags.Archives)
                await ImportRecordAsync(queueItem, Archives);
            else if (queueItem.Type == ImportRecordFlags.Collections)
                await ImportRecordAsync(queueItem, Collections);
            else if (queueItem.Type == ImportRecordFlags.CustomFields)
                await ImportRecordAsync(queueItem, CustomFields);
            else if (queueItem.Type == ImportRecordFlags.Developers)
                await ImportRecordAsync(queueItem, Developers);
            else if (queueItem.Type == ImportRecordFlags.Publishers)
                await ImportRecordAsync(queueItem, Publishers);
            else if (queueItem.Type == ImportRecordFlags.Engine)
                await ImportRecordAsync(queueItem, Engines);
            else if (queueItem.Type == ImportRecordFlags.Genres)
                await ImportRecordAsync(queueItem, Genres);
            else if (queueItem.Type == ImportRecordFlags.Keys)
                await ImportRecordAsync(queueItem, Keys);
            else if (queueItem.Type == ImportRecordFlags.Media)
                await ImportRecordAsync(queueItem, Media);
            else if (queueItem.Type == ImportRecordFlags.MultiplayerModes)
                await ImportRecordAsync(queueItem, MultiplayerModes);
            else if (queueItem.Type == ImportRecordFlags.Platforms)
                await ImportRecordAsync(queueItem, Platforms);
            else if (queueItem.Type == ImportRecordFlags.PlaySessions)
                await ImportRecordAsync(queueItem, PlaySessions);
            else if (queueItem.Type == ImportRecordFlags.Saves)
                await ImportRecordAsync(queueItem, Saves);
            else if (queueItem.Type == ImportRecordFlags.SavePaths)
                await ImportRecordAsync(queueItem, SavePaths);
            else if (queueItem.Type == ImportRecordFlags.Scripts)
                await ImportRecordAsync(queueItem, Scripts);
            else if (queueItem.Type == ImportRecordFlags.ServerConsoles)
                await ImportRecordAsync(queueItem, ServerConsoles);
            else if (queueItem.Type == ImportRecordFlags.ServerHttpPaths)
                await ImportRecordAsync(queueItem, ServerHttpPaths);
            else if (queueItem.Type == ImportRecordFlags.Tags)
                await ImportRecordAsync(queueItem, Tags);
        }
    }

    /// <summary>
    /// Process the context's data record and write the archive to the supplied stream
    /// </summary>
    /// <param name="stream">Output stream for the resulting archive to be written to</param>
    public async Task ExportQueueAsync(Stream stream)
    {
        SDK.Models.Manifest.Game gameManifest = null;
        SDK.Models.Manifest.Redistributable redistributableManifest = null;
        SDK.Models.Manifest.Server serverManifest = null;
        
        if (DataRecord is Data.Models.Game game)
            gameManifest = await ExportGameQueueAsync(game);
        else if (DataRecord is Data.Models.Redistributable redistributable)
            redistributableManifest = await ExportRedistributableQueueAsync(redistributable);
        else if (DataRecord is Data.Models.Server server)
            serverManifest = await ExportServerQueueAsync(server);

        using (var export = new System.IO.Compression.ZipArchive(stream, ZipArchiveMode.Create))
        {
            using (var ms = new MemoryStream())
            using (var writer = new StreamWriter(ms))
            {
                if (gameManifest != null)
                    await writer.WriteAsync(ManifestHelper.Serialize(gameManifest));
                else if (redistributableManifest != null)
                    await writer.WriteAsync(ManifestHelper.Serialize(redistributableManifest));
                else if (serverManifest != null)
                    await writer.WriteAsync(ManifestHelper.Serialize(serverManifest));
                
                await writer.FlushAsync();

                var manifestEntry = export.CreateEntry(ManifestHelper.ManifestFilename, CompressionLevel.NoCompression);

                await using (var entryStream = manifestEntry.Open())
                {
                    ms.Seek(0, SeekOrigin.Begin);
                    await ms.CopyToAsync(entryStream);
                }
            }

            #region Add Game Files
            if (gameManifest != null)
            {
                if (gameManifest.Archives != null)
                    foreach (var archive in gameManifest.Archives)
                        if (archive != null)
                            await AddArchiveToExport(archive, export);

                if (gameManifest.Media != null)
                    foreach (var media in gameManifest.Media)
                        if (media != null)
                            await AddMediaToExport(media, export);
                
                if (gameManifest.Saves != null)
                    foreach (var save in gameManifest.Saves)
                        if (save != null)
                            await AddSaveToExport(save, export);
                
                if (gameManifest.Scripts != null)
                    foreach (var script in gameManifest.Scripts)
                        if (script != null)
                            await AddScriptToExport(script, export);
            }
            #endregion
            
            #region Add Redistributable Files
            if (redistributableManifest != null)
            {
                if (redistributableManifest.Archives != null)
                    foreach (var archive in redistributableManifest.Archives)
                        if (archive != null)
                            await AddArchiveToExport(archive, export);
                
                if (redistributableManifest.Scripts != null)
                    foreach (var script in redistributableManifest.Scripts)
                        if (script != null)
                            await AddScriptToExport(script, export);
            }
            #endregion
            
            #region Add Server Files
            #warning TODO: Expand this out, add all files from the server's root directory.
            // The server's root directory may be different from its working directory. The server model
            // should be expanded upon to add this property. When the root directory is changed, the
            // working directory should be changed as well.
            //
            // Should the files from the HTTP paths be included? Maybe the files for those need to be
            // added as another export flag.
            #endregion
        }
    }

    public async Task<SDK.Models.Manifest.Game> ExportGameQueueAsync(Data.Models.Game game)
    {
        var manifest = await Games.ExportAsync(game.Id);
        
        foreach (var queueItem in _queue)
        {
            if (queueItem.Type == ImportRecordFlags.Actions)
                manifest.Actions.Add(await ExportRecordAsync(queueItem, Actions));
            else if (queueItem.Type == ImportRecordFlags.Archives)
                manifest.Archives.Add(await ExportRecordAsync(queueItem, Archives));
            else if (queueItem.Type == ImportRecordFlags.Collections)
                manifest.Collections.Add(await ExportRecordAsync(queueItem, Collections));
            else if (queueItem.Type == ImportRecordFlags.CustomFields)
                manifest.CustomFields.Add(await ExportRecordAsync(queueItem, CustomFields));
            else if (queueItem.Type == ImportRecordFlags.Developers)
                manifest.Developers.Add(await ExportRecordAsync(queueItem, Developers));
            else if (queueItem.Type == ImportRecordFlags.Publishers)
                manifest.Publishers.Add(await ExportRecordAsync(queueItem, Publishers));
            else if (queueItem.Type == ImportRecordFlags.Engine)
                manifest.Engine = await ExportRecordAsync(queueItem, Engines);
            else if (queueItem.Type == ImportRecordFlags.Genres)
                manifest.Genres.Add(await ExportRecordAsync(queueItem, Genres));
            else if (queueItem.Type == ImportRecordFlags.Keys)
                manifest.Keys.Add(await ExportRecordAsync(queueItem, Keys));
            else if (queueItem.Type == ImportRecordFlags.Media)
                manifest.Media.Add(await ExportRecordAsync(queueItem, Media));
            else if (queueItem.Type == ImportRecordFlags.MultiplayerModes)
                manifest.MultiplayerModes.Add(await ExportRecordAsync(queueItem, MultiplayerModes));
            else if (queueItem.Type == ImportRecordFlags.Platforms)
                manifest.Platforms.Add(await ExportRecordAsync(queueItem, Platforms));
            else if (queueItem.Type == ImportRecordFlags.PlaySessions)
                manifest.PlaySessions.Add(await ExportRecordAsync(queueItem, PlaySessions));
            else if (queueItem.Type == ImportRecordFlags.Saves)
                manifest.Saves.Add(await ExportRecordAsync(queueItem, Saves));
            else if (queueItem.Type == ImportRecordFlags.SavePaths)
                manifest.SavePaths.Add(await ExportRecordAsync(queueItem, SavePaths));
            else if (queueItem.Type == ImportRecordFlags.Scripts)
                manifest.Scripts.Add(await ExportRecordAsync(queueItem, Scripts));
            else if (queueItem.Type == ImportRecordFlags.Tags)
                manifest.Tags.Add(await ExportRecordAsync(queueItem, Tags));
        }

        return manifest;
    }

    public async Task<SDK.Models.Manifest.Redistributable> ExportRedistributableQueueAsync(Data.Models.Redistributable redistributable)
    {
        var manifest = await Redistributables.ExportAsync(redistributable.Id);
        
        foreach (var queueItem in _queue)
        {
            if (queueItem.Type == ImportRecordFlags.Archives)
                manifest.Archives.Add(await ExportRecordAsync(queueItem, Archives));
            else if (queueItem.Type == ImportRecordFlags.Scripts)
                manifest.Scripts.Add(await ExportRecordAsync(queueItem, Scripts));
        }

        return manifest;
    }

    public async Task<SDK.Models.Manifest.Server> ExportServerQueueAsync(Data.Models.Server server)
    {
        var manifest = await Servers.ExportAsync(server.Id);

        foreach (var queueItem in _queue)
        {
            if (queueItem.Type == ImportRecordFlags.Actions)
                manifest.Actions.Add(await ExportRecordAsync(queueItem, Actions));
            else if (queueItem.Type == ImportRecordFlags.Scripts)
                manifest.Scripts.Add(await ExportRecordAsync(queueItem, Scripts));
            else if (queueItem.Type == ImportRecordFlags.ServerConsoles)
                manifest.ServerConsoles.Add(await ExportRecordAsync(queueItem, ServerConsoles));
            else if (queueItem.Type == ImportRecordFlags.ServerHttpPaths)
                await ExportRecordAsync(queueItem, ServerHttpPaths);
        }

        return manifest;
    }

    private async Task AddArchiveToExport(SDK.Models.Manifest.Archive archive, System.IO.Compression.ZipArchive zip)
    {
        try
        {
            var archivePath = await archiveService.GetArchiveFileLocationAsync(archive.ObjectKey);

            if (Path.Exists(archivePath))
            {
                var archiveEntry = zip.CreateEntry($"Archives/{archive.Id}");

                using (var archiveEntryStream = archiveEntry.Open())
                using (var archiveFileStream = new FileStream(archivePath, FileMode.Open))
                {
                    await archiveFileStream.CopyToAsync(archiveEntryStream);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Could not add archive {archive.Id} to export file");
        }
    }
    
    private async Task AddMediaToExport(SDK.Models.Manifest.Media media, System.IO.Compression.ZipArchive zip)
    {
        try
        {
            var mediaPath = await mediaService.GetMediaPathAsync(media.Id);

            if (Path.Exists(mediaPath))
            {
                var mediaEntry = zip.CreateEntry($"Media/{media.Id}");

                using (var mediaEntryStream = mediaEntry.Open())
                using (var mediaFileStream = new FileStream(mediaPath, FileMode.Open))
                {
                    await mediaFileStream.CopyToAsync(mediaEntryStream);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Could not add media {media.Id} to export file");
        }
    }

    private async Task AddSaveToExport(SDK.Models.Manifest.Save save, System.IO.Compression.ZipArchive zip)
    {
        try
        {
            var savePath = await saveService.GetSavePathAsync(save.Id);

            if (Path.Exists(savePath))
            {
                var saveEntry = zip.CreateEntry($"Saves/{save.Id}");

                using (var saveEntryStream = saveEntry.Open())
                using (var saveFileStream = new FileStream(savePath, FileMode.Open))
                {
                    await saveFileStream.CopyToAsync(saveEntryStream);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Could not add save {save.Id} to export file");
        }
    }

    private async Task AddScriptToExport(SDK.Models.Manifest.Script script, System.IO.Compression.ZipArchive zip)
    {
        try
        {
            var scriptEntry = zip.CreateEntry($"Scripts/{script.Id}");

            using (var ms = new MemoryStream())
            using (var writer = new StreamWriter(ms))
            {
                await writer.WriteAsync(script.Contents);
                await writer.FlushAsync();

                await using (var entryStream = scriptEntry.Open())
                {
                    ms.Seek(0, SeekOrigin.Begin);
                    await ms.CopyToAsync(entryStream);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Could not add script {script.Id} to export file");
        }
    }
    
    private async IAsyncEnumerable<ImportItemInfo> GetImportItemInfoAsync<TModel, TEntity>(IEnumerable<TModel> records,
        BaseImporter<TModel, TEntity> importer)
    {
        if (records != null)
            foreach (var record in records)
            {
                if (record != null && record.GetType() == typeof(TModel))
                    yield return await importer.GetImportInfoAsync(record);
            }
    }

    private async IAsyncEnumerable<ExportItemInfo> GetExportItemInfoAsync<TModel, TEntity>(IEnumerable<TEntity> records,
        BaseImporter<TModel, TEntity> importer)
    {
        if (records != null)
            foreach (var record in records)
            {
                if (record != null && record.GetType() == typeof(TEntity))
                    yield return await importer.GetExportInfoAsync(record);
            }
    }

    private async Task ImportRecordAsync<TRecord, TEntity>(ImportQueueItem queueItem, BaseImporter<TRecord, TEntity> importer) where TRecord : class
    {
        var record = queueItem.Record as TRecord;
        
        try
        {
            if (await importer.ExistsAsync(record))
                await importer.UpdateAsync(record);
            else
                await importer.AddAsync(record);

            queueItem.Processed = true;
            
            OnRecordProcessed?.Invoke(this, queueItem);
        }
        catch (Exception ex)
        {
            Errored.Add(queueItem, ex.Message);
            OnRecordError?.Invoke(this, queueItem);
        }
    }
    
    private async Task<TRecord> ExportRecordAsync<TRecord, TEntity>(ImportQueueItem queueItem, BaseImporter<TRecord, TEntity> importer) where TEntity : BaseModel
    {
        var entity = queueItem.Record as TEntity;
        try
        {
            var record = await importer.ExportAsync(entity.Id);

            queueItem.Processed = true;
            
            OnRecordProcessed?.Invoke(this, queueItem);

            return record;
        }
        catch (Exception ex)
        {
            Errored.Add(queueItem, ex.Message);
            OnRecordError?.Invoke(this, queueItem);

            return default;
        }
    }

    public void Dispose()
    {
        if (Archive != null)
            Archive.Dispose();
    }
}