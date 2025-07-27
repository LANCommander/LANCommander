using System.IO.Compression;
using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.Server.Data.Models;
using LANCommander.Server.ImportExport.Exporters;
using LANCommander.Server.ImportExport.Importers;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZipArchive = System.IO.Compression.ZipArchive;

namespace LANCommander.Server.ImportExport;

public class ExportContext(
    BaseExporter<SDK.Models.Manifest.Game, Data.Models.Game> gameExporter,
    BaseExporter<SDK.Models.Manifest.Redistributable, Data.Models.Redistributable> redistributableExporter,
    BaseExporter<SDK.Models.Manifest.Server, Data.Models.Server> serverExporter,
    GameService gameService,
    RedistributableService redistributableService,
    ServerService serverService,
    ArchiveService archiveService,
    MediaService mediaService,
    GameSaveService saveService,
    BaseExporter<SDK.Models.Manifest.Action, Data.Models.Action> actionExporter,
    BaseExporter<SDK.Models.Manifest.Archive, Data.Models.Archive> archiveExporter,
    BaseExporter<SDK.Models.Manifest.Collection, Data.Models.Collection> collectionExporter,
    BaseExporter<SDK.Models.Manifest.GameCustomField, Data.Models.GameCustomField> customFieldExporter,
    DeveloperExporter developerExporter,
    PublisherExporter publisherExporter,
    BaseExporter<SDK.Models.Manifest.Engine, Data.Models.Engine> engineExporter,
    BaseExporter<SDK.Models.Manifest.Genre, Data.Models.Genre> genreExporter,
    BaseExporter<SDK.Models.Manifest.Key, Data.Models.Key> keyExporter,
    BaseExporter<SDK.Models.Manifest.Media, Data.Models.Media> mediaExporter,
    BaseExporter<SDK.Models.Manifest.MultiplayerMode, Data.Models.MultiplayerMode> multiplayerModeExporter,
    BaseExporter<SDK.Models.Manifest.Platform, Data.Models.Platform> platformExporter,
    BaseExporter<SDK.Models.Manifest.PlaySession, Data.Models.PlaySession> playSessionExporter,
    BaseExporter<SDK.Models.Manifest.Save, Data.Models.GameSave> saveExporter,
    BaseExporter<SDK.Models.Manifest.SavePath, Data.Models.SavePath> savePathExporter,
    BaseExporter<SDK.Models.Manifest.Script, Data.Models.Script> scriptExporter,
    BaseExporter<SDK.Models.Manifest.ServerConsole, Data.Models.ServerConsole> serverConsoleExporter,
    BaseExporter<SDK.Models.Manifest.ServerHttpPath, Data.Models.ServerHttpPath> serverHttpPathExporter,
    BaseExporter<SDK.Models.Manifest.Tag, Data.Models.Tag> tagExporter,
    IMapper mapper,
    ILogger<ExportContext> logger) : IDisposable
{
    public object Manifest { get; private set; }
    public BaseModel DataRecord { get; private set; }
    public StorageLocation ArchiveStorageLocation { get; }
    public ZipArchive Archive { get; private set; }

    public BaseExporter<SDK.Models.Manifest.Game, Data.Models.Game> Games { get; private set; } = gameExporter;
    public BaseExporter<SDK.Models.Manifest.Redistributable, Data.Models.Redistributable> Redistributables { get; private set; } = redistributableExporter;
    public BaseExporter<SDK.Models.Manifest.Server, Data.Models.Server> Servers { get; private set; } = serverExporter;

    public BaseExporter<SDK.Models.Manifest.Action, Data.Models.Action> Actions = actionExporter;
    public BaseExporter<SDK.Models.Manifest.Archive, Data.Models.Archive> Archives = archiveExporter;
    public BaseExporter<SDK.Models.Manifest.Collection, Data.Models.Collection> Collections = collectionExporter;
    public BaseExporter<SDK.Models.Manifest.GameCustomField, Data.Models.GameCustomField> CustomFields = customFieldExporter;
    public BaseExporter<SDK.Models.Manifest.Company, Data.Models.Company> Developers = developerExporter;
    public BaseExporter<SDK.Models.Manifest.Engine, Data.Models.Engine> Engines = engineExporter;
    public BaseExporter<SDK.Models.Manifest.Genre, Data.Models.Genre> Genres = genreExporter;
    public BaseExporter<SDK.Models.Manifest.Key, Data.Models.Key> Keys = keyExporter;
    public BaseExporter<SDK.Models.Manifest.Media, Data.Models.Media> Media = mediaExporter;
    public BaseExporter<SDK.Models.Manifest.MultiplayerMode, Data.Models.MultiplayerMode> MultiplayerModes = multiplayerModeExporter;
    public BaseExporter<SDK.Models.Manifest.Platform, Data.Models.Platform> Platforms = platformExporter;
    public BaseExporter<SDK.Models.Manifest.PlaySession, Data.Models.PlaySession> PlaySessions = playSessionExporter;
    public BaseExporter<SDK.Models.Manifest.Company, Data.Models.Company> Publishers = publisherExporter;
    public BaseExporter<SDK.Models.Manifest.Save, Data.Models.GameSave> Saves = saveExporter;
    public BaseExporter<SDK.Models.Manifest.SavePath, Data.Models.SavePath> SavePaths = savePathExporter;
    public BaseExporter<SDK.Models.Manifest.Script, Data.Models.Script> Scripts = scriptExporter;
    public BaseExporter<SDK.Models.Manifest.ServerConsole, Data.Models.ServerConsole> ServerConsoles = serverConsoleExporter;
    public BaseExporter<SDK.Models.Manifest.ServerHttpPath, Data.Models.ServerHttpPath> ServerHttpPaths = serverHttpPathExporter;
    public BaseExporter<SDK.Models.Manifest.Tag, Data.Models.Tag> Tags = tagExporter;

    public int Remaining => _queue.Count;
    public int Processed => _queue.Count(qi => qi.Processed);
    public int Total => _queue.Count;

    private List<ExportQueueItem> _queue { get; } = new();
    public Dictionary<ExportQueueItem, string> Errored { get; } = new();

    public EventHandler<ExportQueueItem> OnRecordAdded;
    public EventHandler<ExportQueueItem> OnRecordProcessed;
    public EventHandler<ExportQueueItem> OnRecordError;

    private void UseContext(ExportContext context)
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

    #region Prepare Export Queue
    public async Task PrepareExportQueueAsync(ExportRecordFlags importRecordFlags)
    {
        if (DataRecord is Data.Models.Game game)
            await PrepareGameExportQueueAsync(game, importRecordFlags);
        
        if (DataRecord is Data.Models.Redistributable redistributable)
            await PrepareRedistributableExportQueueAsync(redistributable, importRecordFlags);
        
        if (DataRecord is Data.Models.Server server)
            await PrepareServerExportQueueAsync(server, importRecordFlags);
    }
    
    public async Task PrepareGameExportQueueAsync(Game game, ExportRecordFlags importRecordFlags)
    {
        if (importRecordFlags.HasFlag(ExportRecordFlags.Actions))
            await AddToExportQueueAsync(ExportRecordFlags.Actions, game.Actions);
        
        if (importRecordFlags.HasFlag(ExportRecordFlags.Archives))
            await AddToExportQueueAsync(ExportRecordFlags.Archives, game.Archives);
        
        if (importRecordFlags.HasFlag(ExportRecordFlags.Collections))
            await AddToExportQueueAsync(ExportRecordFlags.Collections, game.Collections);
        
        if (importRecordFlags.HasFlag(ExportRecordFlags.CustomFields))
            await AddToExportQueueAsync(ExportRecordFlags.CustomFields, game.CustomFields);
        
        if (importRecordFlags.HasFlag(ExportRecordFlags.Developers))
            await AddToExportQueueAsync(ExportRecordFlags.Developers, game.Developers);
        
        if (importRecordFlags.HasFlag(ExportRecordFlags.Engine) && game.Engine != null)
            await AddToExportQueueAsync(ExportRecordFlags.Engine, [game.Engine]);
        
        if (importRecordFlags.HasFlag(ExportRecordFlags.Genres))
            await AddToExportQueueAsync(ExportRecordFlags.Genres, game.Genres);
        
        if (importRecordFlags.HasFlag(ExportRecordFlags.Keys))
            await AddToExportQueueAsync(ExportRecordFlags.Keys, game.Keys);
        
        if (importRecordFlags.HasFlag(ExportRecordFlags.Media))
            await AddToExportQueueAsync(ExportRecordFlags.Media, game.Media);
        
        if (importRecordFlags.HasFlag(ExportRecordFlags.MultiplayerModes))
            await AddToExportQueueAsync(ExportRecordFlags.MultiplayerModes, game.MultiplayerModes);
        
        if (importRecordFlags.HasFlag(ExportRecordFlags.Platforms))
            await AddToExportQueueAsync(ExportRecordFlags.Platforms, game.Platforms);
        
        if (importRecordFlags.HasFlag(ExportRecordFlags.PlaySessions))
            await AddToExportQueueAsync(ExportRecordFlags.PlaySessions, game.PlaySessions);

        if (importRecordFlags.HasFlag(ExportRecordFlags.Publishers))
            await AddToExportQueueAsync(ExportRecordFlags.Publishers, game.Publishers);

        if (importRecordFlags.HasFlag(ExportRecordFlags.Saves))
            await AddToExportQueueAsync(ExportRecordFlags.Saves, game.GameSaves);

        if (importRecordFlags.HasFlag(ExportRecordFlags.SavePaths))
            await AddToExportQueueAsync(ExportRecordFlags.SavePaths, game.SavePaths);
        
        if (importRecordFlags.HasFlag(ExportRecordFlags.Scripts))
            await AddToExportQueueAsync(ExportRecordFlags.Scripts, game.Scripts);
        
        if (importRecordFlags.HasFlag(ExportRecordFlags.Tags))
            await AddToExportQueueAsync(ExportRecordFlags.Tags, game.Tags);
    }

    public async Task PrepareRedistributableExportQueueAsync(Data.Models.Redistributable redistributable,
        ExportRecordFlags importRecordFlags)
    {
        if (importRecordFlags.HasFlag(ExportRecordFlags.Archives))
            await AddToExportQueueAsync(ExportRecordFlags.Archives, redistributable.Archives);
        
        if (importRecordFlags.HasFlag(ExportRecordFlags.Scripts))
            await AddToExportQueueAsync(ExportRecordFlags.Scripts, redistributable.Scripts);
    }

    public async Task PrepareServerExportQueueAsync(Data.Models.Server server,
        ExportRecordFlags importRecordFlags)
    {
        if (importRecordFlags.HasFlag(ExportRecordFlags.Actions))
            await AddToExportQueueAsync(ExportRecordFlags.Actions, server.Actions);
        
        if (importRecordFlags.HasFlag(ExportRecordFlags.Scripts))
            await AddToExportQueueAsync(ExportRecordFlags.Scripts, server.Scripts);
        
        if (importRecordFlags.HasFlag(ExportRecordFlags.ServerConsoles))
            await AddToExportQueueAsync(ExportRecordFlags.ServerConsoles, server.ServerConsoles);

        if (importRecordFlags.HasFlag(ExportRecordFlags.ServerHttpPaths))
            await AddToExportQueueAsync(ExportRecordFlags.ServerHttpPaths, server.HttpPaths);
    }
    #endregion
    
    private async Task AddToExportQueueAsync<TEntity>(ExportRecordFlags type, IEnumerable<TEntity> records) where TEntity : Data.Models.BaseModel
    {
        if (records != null)
            _queue.AddRange(records.Select(r => new ExportQueueItem(r.Id, type, r)));
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

        using (var export = new ZipArchive(stream, ZipArchiveMode.Create))
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
        }
    }

    public async Task<SDK.Models.Manifest.Game> ExportGameQueueAsync(Data.Models.Game game)
    {
        var manifest = await Games.ExportAsync(game.Id);
        
        foreach (var queueItem in _queue)
        {
            if (queueItem.Type == ExportRecordFlags.Actions)
                manifest.Actions.Add(await ExportRecordAsync(queueItem, Actions));
            else if (queueItem.Type == ExportRecordFlags.Archives)
                manifest.Archives.Add(await ExportRecordAsync(queueItem, Archives));
            else if (queueItem.Type == ExportRecordFlags.Collections)
                manifest.Collections.Add(await ExportRecordAsync(queueItem, Collections));
            else if (queueItem.Type == ExportRecordFlags.CustomFields)
                manifest.CustomFields.Add(await ExportRecordAsync(queueItem, CustomFields));
            else if (queueItem.Type == ExportRecordFlags.Developers)
                manifest.Developers.Add(await ExportRecordAsync(queueItem, Developers));
            else if (queueItem.Type == ExportRecordFlags.Publishers)
                manifest.Publishers.Add(await ExportRecordAsync(queueItem, Publishers));
            else if (queueItem.Type == ExportRecordFlags.Engine)
                manifest.Engine = await ExportRecordAsync(queueItem, Engines);
            else if (queueItem.Type == ExportRecordFlags.Genres)
                manifest.Genres.Add(await ExportRecordAsync(queueItem, Genres));
            else if (queueItem.Type == ExportRecordFlags.Keys)
                manifest.Keys.Add(await ExportRecordAsync(queueItem, Keys));
            else if (queueItem.Type == ExportRecordFlags.Media)
                manifest.Media.Add(await ExportRecordAsync(queueItem, Media));
            else if (queueItem.Type == ExportRecordFlags.MultiplayerModes)
                manifest.MultiplayerModes.Add(await ExportRecordAsync(queueItem, MultiplayerModes));
            else if (queueItem.Type == ExportRecordFlags.Platforms)
                manifest.Platforms.Add(await ExportRecordAsync(queueItem, Platforms));
            else if (queueItem.Type == ExportRecordFlags.PlaySessions)
                manifest.PlaySessions.Add(await ExportRecordAsync(queueItem, PlaySessions));
            else if (queueItem.Type == ExportRecordFlags.Saves)
                manifest.Saves.Add(await ExportRecordAsync(queueItem, Saves));
            else if (queueItem.Type == ExportRecordFlags.SavePaths)
                manifest.SavePaths.Add(await ExportRecordAsync(queueItem, SavePaths));
            else if (queueItem.Type == ExportRecordFlags.Scripts)
                manifest.Scripts.Add(await ExportRecordAsync(queueItem, Scripts));
            else if (queueItem.Type == ExportRecordFlags.Tags)
                manifest.Tags.Add(await ExportRecordAsync(queueItem, Tags));
        }

        return manifest;
    }

    public async Task<SDK.Models.Manifest.Redistributable> ExportRedistributableQueueAsync(Data.Models.Redistributable redistributable)
    {
        var manifest = await Redistributables.ExportAsync(redistributable.Id);
        
        foreach (var queueItem in _queue)
        {
            if (queueItem.Type == ExportRecordFlags.Archives)
                manifest.Archives.Add(await ExportRecordAsync(queueItem, Archives));
            else if (queueItem.Type == ExportRecordFlags.Scripts)
                manifest.Scripts.Add(await ExportRecordAsync(queueItem, Scripts));
        }

        return manifest;
    }

    public async Task<SDK.Models.Manifest.Server> ExportServerQueueAsync(Data.Models.Server server)
    {
        var manifest = await Servers.ExportAsync(server.Id);

        foreach (var queueItem in _queue)
        {
            if (queueItem.Type == ExportRecordFlags.Actions)
                manifest.Actions.Add(await ExportRecordAsync(queueItem, Actions));
            else if (queueItem.Type == ExportRecordFlags.Scripts)
                manifest.Scripts.Add(await ExportRecordAsync(queueItem, Scripts));
            else if (queueItem.Type == ExportRecordFlags.ServerConsoles)
                manifest.ServerConsoles.Add(await ExportRecordAsync(queueItem, ServerConsoles));
            else if (queueItem.Type == ExportRecordFlags.ServerHttpPaths)
                await ExportRecordAsync(queueItem, ServerHttpPaths);
        }

        return manifest;
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
    
    private async IAsyncEnumerable<ExportItemInfo> GetExportItemInfoAsync<TModel, TEntity>(IEnumerable<TEntity> records,
        BaseExporter<TModel, TEntity> exporter)
    {
        if (records != null)
            foreach (var record in records)
            {
                if (record != null && record.GetType() == typeof(TEntity))
                    yield return await exporter.GetExportInfoAsync(record);
            }
    }
    
    private async Task<TRecord> ExportRecordAsync<TRecord, TEntity>(ExportQueueItem queueItem, BaseExporter<TRecord, TEntity> exporter) where TEntity : BaseModel
    {
        var entity = queueItem.Record as TEntity;
        try
        {
            var record = await exporter.ExportAsync(entity.Id);

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