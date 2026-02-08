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
    GameExporter gameExporter,
    RedistributableExporter redistributableExporter,
    ServerExporter serverExporter,
    ToolExporter toolExporter,
    GameService gameService,
    RedistributableService redistributableService,
    ServerService serverService,
    ToolService toolService,
    ActionExporter actionExporter,
    ArchiveExporter archiveExporter,
    CollectionExporter collectionExporter,
    CustomFieldExporter customFieldExporter,
    DeveloperExporter developerExporter,
    PublisherExporter publisherExporter,
    EngineExporter engineExporter,
    GenreExporter genreExporter,
    KeyExporter keyExporter,
    MediaExporter mediaExporter,
    MultiplayerModeExporter multiplayerModeExporter,
    PlatformExporter platformExporter,
    PlaySessionExporter playSessionExporter,
    SaveExporter saveExporter,
    SavePathExporter savePathExporter,
    ScriptExporter scriptExporter,
    ServerConsoleExporter serverConsoleExporter,
    ServerHttpPathExporter serverHttpPathExporter,
    TagExporter tagExporter,
    IMapper mapper,
    ILogger<ExportContext> logger) : IDisposable
{
    public object Manifest { get; private set; }
    public BaseModel DataRecord { get; private set; }
    public StorageLocation ArchiveStorageLocation { get; }
    public ZipArchive Archive { get; private set; }

    public GameExporter Games = gameExporter;
    public RedistributableExporter Redistributables = redistributableExporter;
    public ServerExporter Servers = serverExporter;
    public ToolExporter Tools = toolExporter;

    public ActionExporter Actions = actionExporter;
    public ArchiveExporter Archives = archiveExporter;
    public CollectionExporter Collections = collectionExporter;
    public CustomFieldExporter CustomFields = customFieldExporter;
    public DeveloperExporter Developers = developerExporter;
    public EngineExporter Engines = engineExporter;
    public GenreExporter Genres = genreExporter;
    public KeyExporter Keys = keyExporter;
    public MediaExporter Media = mediaExporter;
    public MultiplayerModeExporter MultiplayerModes = multiplayerModeExporter;
    public PlatformExporter Platforms = platformExporter;
    public PlaySessionExporter PlaySessions = playSessionExporter;
    public PublisherExporter Publishers = publisherExporter;
    public SaveExporter Saves = saveExporter;
    public SavePathExporter SavePaths = savePathExporter;
    public ScriptExporter Scripts = scriptExporter;
    public ServerConsoleExporter ServerConsoles = serverConsoleExporter;
    public ServerHttpPathExporter ServerHttpPaths = serverHttpPathExporter;
    public TagExporter Tags = tagExporter;

    public int Remaining => _queue.Count;
    public int Processed => _queue.Count(qi => qi.Processed);
    public int Total => _queue.Count;

    private List<ExportQueueItem> _queue { get; } = new();
    private IEnumerable<Guid> _selectedRecordIds { get; set; }
    public Dictionary<ExportQueueItem, string> Errored { get; } = new();

    public EventHandler<ExportQueueItem> OnRecordAdded;
    public EventHandler<ExportQueueItem> OnRecordProcessed;
    public EventHandler<ExportQueueItem> OnRecordError;

    private void UseContext(ExportContext context)
    {
        Games.UseContext(context);
        Redistributables.UseContext(context);
        Servers.UseContext(context);
        Tools.UseContext(context);
        
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

    public string GetName()
    {
        if (DataRecord is Data.Models.Game game)
            return game.Title; 
        
        if (DataRecord is Data.Models.Redistributable redistributable)
            return redistributable.Name;
        
        if (DataRecord is Data.Models.Server server)
            return server.Name;

        if (DataRecord is Data.Models.Tool tool)
            return tool.Name;

        return string.Empty;
    }
    
    #region Initialize Export
    public async Task<IEnumerable<ExportItemInfo>> InitializeExportAsync(Guid recordId, ImportExportRecordType recordType)
    {
        UseContext(this);
        
        switch (recordType)
        {
            case ImportExportRecordType.Game:
                return await InitializeGameExportAsync(recordId);
            
            case ImportExportRecordType.Redistributable:
                return await InitializeRedistributableExportAsync(recordId);
            
            case ImportExportRecordType.Server:
                return await InitializeServerExportAsync(recordId);
            
            case ImportExportRecordType.Tool:
                return await InitializeToolExportAsync(recordId);
            
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
                .Include(g => g.Scripts)
                .Include(g => g.SavePaths)
                .Include(g => g.Tags)
                .Include(g => g.Tools);
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
        
        DataRecord = redistributable;
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

        DataRecord = server;
        Manifest = mapper.Map<SDK.Models.Manifest.Server>(server);
        
        var exportItemInfo = new List<ExportItemInfo>();
        
        exportItemInfo.AddRange(await GetExportItemInfoAsync(server.Actions, Actions).ToListAsync());
        exportItemInfo.AddRange(await GetExportItemInfoAsync(server.Scripts, Scripts).ToListAsync());
        exportItemInfo.AddRange(await GetExportItemInfoAsync(server.ServerConsoles, ServerConsoles).ToListAsync());
        exportItemInfo.AddRange(await GetExportItemInfoAsync(server.HttpPaths, ServerHttpPaths).ToListAsync());
        
        return exportItemInfo;
    }
    
    private async Task<IEnumerable<ExportItemInfo>> InitializeToolExportAsync(Guid toolId)
    {
        var tool = await toolService
            .AsNoTracking()
            .AsSplitQuery()
            .Query(q =>
            {
                return q
                    .Include(t => t.Archives)
                    .Include(t => t.Scripts);
            })
            .GetAsync(toolId);

        DataRecord = tool;
        Manifest = mapper.Map<SDK.Models.Manifest.Tool>(tool);
        
        var exportItemInfo = new List<ExportItemInfo>();
        
        exportItemInfo.AddRange(await GetExportItemInfoAsync(tool.Archives, Archives).ToListAsync());
        exportItemInfo.AddRange(await GetExportItemInfoAsync(tool.Scripts, Scripts).ToListAsync());

        return exportItemInfo;
    }
    #endregion

    #region Prepare Export Queue
    public async Task PrepareExportQueueAsync(IEnumerable<Guid> selectedRecordIds)
    {
        _selectedRecordIds = selectedRecordIds;
        
        if (DataRecord is Data.Models.Game game)
            await PrepareGameExportQueueAsync(game);
        
        if (DataRecord is Data.Models.Redistributable redistributable)
            await PrepareRedistributableExportQueueAsync(redistributable);
        
        if (DataRecord is Data.Models.Server server)
            await PrepareServerExportQueueAsync(server);

        if (DataRecord is Data.Models.Tool tool)
            await PrepareToolExportQueueAsync(tool);
    }
    
    public async Task PrepareGameExportQueueAsync(Game game)
    {
        await AddToExportQueueAsync(ImportExportRecordType.Action, game.Actions);
        await AddToExportQueueAsync(ImportExportRecordType.Archive, game.Archives);
        await AddToExportQueueAsync(ImportExportRecordType.Collection, game.Collections);
        await AddToExportQueueAsync(ImportExportRecordType.CustomField, game.CustomFields);
        await AddToExportQueueAsync(ImportExportRecordType.Developer, game.Developers);
        await AddToExportQueueAsync(ImportExportRecordType.Engine, [game.Engine]);
        await AddToExportQueueAsync(ImportExportRecordType.Genre, game.Genres);
        await AddToExportQueueAsync(ImportExportRecordType.Key, game.Keys);
        await AddToExportQueueAsync(ImportExportRecordType.Media, game.Media);
        await AddToExportQueueAsync(ImportExportRecordType.MultiplayerMode, game.MultiplayerModes);
        await AddToExportQueueAsync(ImportExportRecordType.Platform, game.Platforms);
        await AddToExportQueueAsync(ImportExportRecordType.PlaySession, game.PlaySessions);
        await AddToExportQueueAsync(ImportExportRecordType.Publisher, game.Publishers);
        await AddToExportQueueAsync(ImportExportRecordType.Save, game.GameSaves);
        await AddToExportQueueAsync(ImportExportRecordType.SavePath, game.SavePaths);
        await AddToExportQueueAsync(ImportExportRecordType.Script, game.Scripts);
        await AddToExportQueueAsync(ImportExportRecordType.Tag, game.Tags);
    }

    public async Task PrepareRedistributableExportQueueAsync(Data.Models.Redistributable redistributable)
    {
        await AddToExportQueueAsync(ImportExportRecordType.Archive, redistributable.Archives);
        await AddToExportQueueAsync(ImportExportRecordType.Script, redistributable.Scripts);
    }

    public async Task PrepareServerExportQueueAsync(Data.Models.Server server)
    {
        await AddToExportQueueAsync(ImportExportRecordType.Action, server.Actions);
        await AddToExportQueueAsync(ImportExportRecordType.Script, server.Scripts);
        await AddToExportQueueAsync(ImportExportRecordType.ServerConsole, server.ServerConsoles);
        await AddToExportQueueAsync(ImportExportRecordType.ServerHttpPath, server.HttpPaths);
    }
    
    public async Task PrepareToolExportQueueAsync(Data.Models.Tool tool)
    {
        await AddToExportQueueAsync(ImportExportRecordType.Archive, tool.Archives);
        await AddToExportQueueAsync(ImportExportRecordType.Script, tool.Scripts);
    }
    #endregion
    
    private async Task AddToExportQueueAsync<TEntity>(ImportExportRecordType type, IEnumerable<TEntity> records) where TEntity : Data.Models.BaseModel
    {
        if (records != null)
            _queue.AddRange(records.Where(r => r != null && _selectedRecordIds.Contains(r.Id)).Select(r => new ExportQueueItem(r.Id, type, r)));
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
        SDK.Models.Manifest.Tool toolManifest = null;
        
        Archive = new ZipArchive(stream, ZipArchiveMode.Create);
        
        if (DataRecord is Data.Models.Game game)
            gameManifest = await ExportGameQueueAsync(game);
        else if (DataRecord is Data.Models.Redistributable redistributable)
            redistributableManifest = await ExportRedistributableQueueAsync(redistributable);
        else if (DataRecord is Data.Models.Server server)
            serverManifest = await ExportServerQueueAsync(server);
        else if (DataRecord is Data.Models.Tool tool)
            toolManifest = await ExportToolQueueAsync(tool);
        
        using (var ms = new MemoryStream())
        using (var writer = new StreamWriter(ms))
        {
            if (gameManifest != null)
                await writer.WriteAsync(ManifestHelper.Serialize(gameManifest));
            else if (redistributableManifest != null)
                await writer.WriteAsync(ManifestHelper.Serialize(redistributableManifest));
            else if (serverManifest != null)
                await writer.WriteAsync(ManifestHelper.Serialize(serverManifest));
            else if (toolManifest != null)
                await writer.WriteAsync(ManifestHelper.Serialize(toolManifest));
            
            await writer.FlushAsync();

            var manifestEntry = Archive.CreateEntry(ManifestHelper.ManifestFilename, CompressionLevel.NoCompression);

            await using (var entryStream = manifestEntry.Open())
            {
                ms.Seek(0, SeekOrigin.Begin);
                await ms.CopyToAsync(entryStream);
            }
        }
        
        Archive.Dispose();
    }

    public async Task<SDK.Models.Manifest.Game> ExportGameQueueAsync(Data.Models.Game game)
    {
        var manifest = await Games.ExportAsync(game.Id);

        manifest.ManifestVersion = VersionHelper.GetCurrentVersion().ToString();
        
        foreach (var queueItem in _queue)
        {
            if (queueItem.Type == ImportExportRecordType.Action)
                manifest.Actions.Add(await ExportRecordAsync(queueItem, Actions));
            else if (queueItem.Type == ImportExportRecordType.Archive)
                manifest.Archives.Add(await ExportRecordAsync(queueItem, Archives));
            else if (queueItem.Type == ImportExportRecordType.Collection)
                manifest.Collections.Add(await ExportRecordAsync(queueItem, Collections));
            else if (queueItem.Type == ImportExportRecordType.CustomField)
                manifest.CustomFields.Add(await ExportRecordAsync(queueItem, CustomFields));
            else if (queueItem.Type == ImportExportRecordType.Developer)
                manifest.Developers.Add(await ExportRecordAsync(queueItem, Developers));
            else if (queueItem.Type == ImportExportRecordType.Publisher)
                manifest.Publishers.Add(await ExportRecordAsync(queueItem, Publishers));
            else if (queueItem.Type == ImportExportRecordType.Engine)
                manifest.Engine = await ExportRecordAsync(queueItem, Engines);
            else if (queueItem.Type == ImportExportRecordType.Genre)
                manifest.Genres.Add(await ExportRecordAsync(queueItem, Genres));
            else if (queueItem.Type == ImportExportRecordType.Key)
                manifest.Keys.Add(await ExportRecordAsync(queueItem, Keys));
            else if (queueItem.Type == ImportExportRecordType.Media)
                manifest.Media.Add(await ExportRecordAsync(queueItem, Media));
            else if (queueItem.Type == ImportExportRecordType.MultiplayerMode)
                manifest.MultiplayerModes.Add(await ExportRecordAsync(queueItem, MultiplayerModes));
            else if (queueItem.Type == ImportExportRecordType.Platform)
                manifest.Platforms.Add(await ExportRecordAsync(queueItem, Platforms));
            else if (queueItem.Type == ImportExportRecordType.PlaySession)
                manifest.PlaySessions.Add(await ExportRecordAsync(queueItem, PlaySessions));
            else if (queueItem.Type == ImportExportRecordType.Save)
                manifest.Saves.Add(await ExportRecordAsync(queueItem, Saves));
            else if (queueItem.Type == ImportExportRecordType.SavePath)
                manifest.SavePaths.Add(await ExportRecordAsync(queueItem, SavePaths));
            else if (queueItem.Type == ImportExportRecordType.Script)
                manifest.Scripts.Add(await ExportRecordAsync(queueItem, Scripts));
            else if (queueItem.Type == ImportExportRecordType.Tag)
                manifest.Tags.Add(await ExportRecordAsync(queueItem, Tags));
        }

        return manifest;
    }

    public async Task<SDK.Models.Manifest.Redistributable> ExportRedistributableQueueAsync(Data.Models.Redistributable redistributable)
    {
        var manifest = await Redistributables.ExportAsync(redistributable.Id);
        
        manifest.ManifestVersion = VersionHelper.GetCurrentVersion().ToString();
        
        foreach (var queueItem in _queue)
        {
            if (queueItem.Type == ImportExportRecordType.Archive)
                manifest.Archives.Add(await ExportRecordAsync(queueItem, Archives));
            else if (queueItem.Type == ImportExportRecordType.Script)
                manifest.Scripts.Add(await ExportRecordAsync(queueItem, Scripts));
        }

        return manifest;
    }

    public async Task<SDK.Models.Manifest.Server> ExportServerQueueAsync(Data.Models.Server server)
    {
        var manifest = await Servers.ExportAsync(server.Id);
        
        manifest.ManifestVersion = VersionHelper.GetCurrentVersion().ToString();

        foreach (var queueItem in _queue)
        {
            if (queueItem.Type == ImportExportRecordType.Action)
                manifest.Actions.Add(await ExportRecordAsync(queueItem, Actions));
            else if (queueItem.Type == ImportExportRecordType.Script)
                manifest.Scripts.Add(await ExportRecordAsync(queueItem, Scripts));
            else if (queueItem.Type == ImportExportRecordType.ServerConsole)
                manifest.ServerConsoles.Add(await ExportRecordAsync(queueItem, ServerConsoles));
            else if (queueItem.Type == ImportExportRecordType.ServerHttpPath)
                await ExportRecordAsync(queueItem, ServerHttpPaths);
        }

        return manifest;
    }
    
    public async Task<SDK.Models.Manifest.Tool> ExportToolQueueAsync(Data.Models.Tool tool)
    {
        var manifest = await Tools.ExportAsync(tool.Id);
        
        manifest.ManifestVersion = VersionHelper.GetCurrentVersion().ToString();
        
        foreach (var queueItem in _queue)
        {
            if (queueItem.Type == ImportExportRecordType.Archive)
                manifest.Archives.Add(await ExportRecordAsync(queueItem, Archives));
            else if (queueItem.Type == ImportExportRecordType.Script)
                manifest.Scripts.Add(await ExportRecordAsync(queueItem, Scripts));
        }

        return manifest;
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