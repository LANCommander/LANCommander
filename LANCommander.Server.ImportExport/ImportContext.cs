using System.IO.Compression;
using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.Server.Data.Models;
using LANCommander.Server.ImportExport.Importers;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZipArchive = SharpCompress.Archives.Zip.ZipArchive;

namespace LANCommander.Server.ImportExport;

public class ImportContext(
    GameImporter gameImporter,
    RedistributableImporter redistributableImporter,
    ServerImporter serverImporter,
    ActionImporter actionImporter,
    ArchiveImporter archiveImporter,
    CollectionImporter collectionImporter,
    CustomFieldImporter customFieldImporter,
    DeveloperImporter developerImporter,
    PublisherImporter publisherImporter,
    EngineImporter engineImporter,
    GenreImporter genreImporter,
    KeyImporter keyImporter,
    MediaImporter mediaImporter,
    MultiplayerModeImporter multiplayerModeImporter,
    PlatformImporter platformImporter,
    PlaySessionImporter playSessionImporter,
    SaveImporter saveImporter,
    SavePathImporter savePathImporter,
    ScriptImporter scriptImporter,
    ServerConsoleImporter serverConsoleImporter,
    ServerHttpPathImporter serverHttpPathImporter,
    TagImporter tagImporter,
    StorageLocationService storageLocationService,
    IMapper mapper,
    ILogger<ImportContext> logger) : IDisposable
{
    public object Manifest { get; private set; }
    public BaseModel DataRecord { get; private set; }
    public StorageLocation ArchiveStorageLocation { get; set; }
    public ZipArchive Archive { get; private set; }

    public GameImporter Games = gameImporter;
    public RedistributableImporter Redistributables = redistributableImporter;
    public ServerImporter Servers = serverImporter;

    public ActionImporter Actions = actionImporter;
    public ArchiveImporter Archives = archiveImporter;
    public CollectionImporter Collections = collectionImporter;
    public CustomFieldImporter CustomFields = customFieldImporter;
    public DeveloperImporter Developers = developerImporter;
    public EngineImporter Engines = engineImporter;
    public GenreImporter Genres = genreImporter;
    public KeyImporter Keys = keyImporter;
    public MediaImporter Media = mediaImporter;
    public MultiplayerModeImporter MultiplayerModes = multiplayerModeImporter;
    public PlatformImporter Platforms = platformImporter;
    public PlaySessionImporter PlaySessions = playSessionImporter;
    public PublisherImporter Publishers = publisherImporter;
    public SaveImporter Saves = saveImporter;
    public SavePathImporter SavePaths = savePathImporter;
    public ScriptImporter Scripts = scriptImporter;
    public ServerConsoleImporter ServerConsoles = serverConsoleImporter;
    public ServerHttpPathImporter ServerHttpPaths = serverHttpPathImporter;
    public TagImporter Tags = tagImporter;

    public int Remaining => _queue.Count;
    public int Processed => _queue.Count(qi => qi.Processed);
    public int Total => _queue.Count;

    private List<ImportQueueItem> _queue { get; } = new();
    private IEnumerable<Guid> _selectedRecordIds { get; set; }
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
        
        if (gameManifest.Engine != null)
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
    
    #region Prepare Import Queue
    public async Task PrepareImportQueueAsync(IEnumerable<Guid> selectedRecordIds, Guid storageLocationId)
    {
        _selectedRecordIds = selectedRecordIds;
        
        ArchiveStorageLocation = await storageLocationService.GetAsync(storageLocationId);
        
        if (Manifest is SDK.Models.Manifest.Game gameManifest)
            await PrepareGameImportQueueAsync(gameManifest);
        
        if (Manifest is SDK.Models.Manifest.Redistributable redistributableManifest)
            await PrepareRedistributableImportQueueAsync(redistributableManifest);
        
        if (Manifest is SDK.Models.Manifest.Server serverManifest)
            await PrepareServerImportQueueAsync(serverManifest);
    }

    public async Task PrepareGameImportQueueAsync(SDK.Models.Manifest.Game gameManifest)
    {
        if (!(await Games.ExistsAsync(gameManifest)))
            DataRecord = await Games.AddAsync(gameManifest);
        else
            DataRecord = await Games.UpdateAsync(gameManifest);
            await AddToImportQueueAsync(ImportExportRecordType.Action, gameManifest.Actions);
            await AddToImportQueueAsync(ImportExportRecordType.Archive, gameManifest.Archives);
            await AddToImportQueueAsync(ImportExportRecordType.Collection, gameManifest.Collections);
            await AddToImportQueueAsync(ImportExportRecordType.CustomField, gameManifest.CustomFields);
            await AddToImportQueueAsync(ImportExportRecordType.Developer, gameManifest.Developers);
            await AddToImportQueueAsync(ImportExportRecordType.Engine, [gameManifest.Engine]);
            await AddToImportQueueAsync(ImportExportRecordType.Genre, gameManifest.Genres);
            await AddToImportQueueAsync(ImportExportRecordType.Key, gameManifest.Keys);
            await AddToImportQueueAsync(ImportExportRecordType.Media, gameManifest.Media);
            await AddToImportQueueAsync(ImportExportRecordType.MultiplayerMode, gameManifest.MultiplayerModes);
            await AddToImportQueueAsync(ImportExportRecordType.Platform, gameManifest.Platforms);
            await AddToImportQueueAsync(ImportExportRecordType.PlaySession, gameManifest.PlaySessions);
            await AddToImportQueueAsync(ImportExportRecordType.Publisher, gameManifest.Publishers);
            await AddToImportQueueAsync(ImportExportRecordType.Save, gameManifest.Saves);
            await AddToImportQueueAsync(ImportExportRecordType.SavePath, gameManifest.SavePaths);
            await AddToImportQueueAsync(ImportExportRecordType.Script, gameManifest.Scripts);
            await AddToImportQueueAsync(ImportExportRecordType.Tag, gameManifest.Tags);
    }

    public async Task PrepareRedistributableImportQueueAsync(SDK.Models.Manifest.Redistributable redistributableManifest)
    { 
        if (!(await Redistributables.ExistsAsync(redistributableManifest)))
            DataRecord = await Redistributables.AddAsync(redistributableManifest);
        else
            DataRecord = await Redistributables.UpdateAsync(redistributableManifest);
        
        await AddToImportQueueAsync(ImportExportRecordType.Archive, redistributableManifest.Archives);
        await AddToImportQueueAsync(ImportExportRecordType.Script, redistributableManifest.Scripts);
    }

    public async Task PrepareServerImportQueueAsync(SDK.Models.Manifest.Server serverManifest)
    {
        if (!(await Servers.ExistsAsync(serverManifest)))
            DataRecord = await Servers.AddAsync(serverManifest);
        else
            DataRecord = await Servers.UpdateAsync(serverManifest);
        
        await AddToImportQueueAsync(ImportExportRecordType.Action, serverManifest.Actions);
        await AddToImportQueueAsync(ImportExportRecordType.Script, serverManifest.Scripts);
        await AddToImportQueueAsync(ImportExportRecordType.ServerConsole, serverManifest.ServerConsoles);
        await AddToImportQueueAsync(ImportExportRecordType.ServerHttpPath, serverManifest.HttpPaths);
    }
    #endregion

    private async Task AddToImportQueueAsync<TRecord>(ImportExportRecordType type, IEnumerable<TRecord> records) where TRecord : SDK.Models.Manifest.BaseModel
    {
        if (records != null)
            _queue.AddRange(records.Select(r => new ImportQueueItem(type, r)));
    }

    public async Task ImportQueueAsync()
    {
        foreach (var queueItem in _queue)
        {
            if (queueItem.Type == ImportExportRecordType.Action)
                await ImportRecordAsync(queueItem, Actions);
            else if (queueItem.Type == ImportExportRecordType.Archive)
                await ImportRecordAsync(queueItem, Archives);
            else if (queueItem.Type == ImportExportRecordType.Collection)
                await ImportRecordAsync(queueItem, Collections);
            else if (queueItem.Type == ImportExportRecordType.CustomField)
                await ImportRecordAsync(queueItem, CustomFields);
            else if (queueItem.Type == ImportExportRecordType.Developer)
                await ImportRecordAsync(queueItem, Developers);
            else if (queueItem.Type == ImportExportRecordType.Publisher)
                await ImportRecordAsync(queueItem, Publishers);
            else if (queueItem.Type == ImportExportRecordType.Engine)
                await ImportRecordAsync(queueItem, Engines);
            else if (queueItem.Type == ImportExportRecordType.Genre)
                await ImportRecordAsync(queueItem, Genres);
            else if (queueItem.Type == ImportExportRecordType.Key)
                await ImportRecordAsync(queueItem, Keys);
            else if (queueItem.Type == ImportExportRecordType.Media)
                await ImportRecordAsync(queueItem, Media);
            else if (queueItem.Type == ImportExportRecordType.MultiplayerMode)
                await ImportRecordAsync(queueItem, MultiplayerModes);
            else if (queueItem.Type == ImportExportRecordType.Platform)
                await ImportRecordAsync(queueItem, Platforms);
            else if (queueItem.Type == ImportExportRecordType.PlaySession)
                await ImportRecordAsync(queueItem, PlaySessions);
            else if (queueItem.Type == ImportExportRecordType.Save)
                await ImportRecordAsync(queueItem, Saves);
            else if (queueItem.Type == ImportExportRecordType.SavePath)
                await ImportRecordAsync(queueItem, SavePaths);
            else if (queueItem.Type == ImportExportRecordType.Script)
                await ImportRecordAsync(queueItem, Scripts);
            else if (queueItem.Type == ImportExportRecordType.ServerConsole)
                await ImportRecordAsync(queueItem, ServerConsoles);
            else if (queueItem.Type == ImportExportRecordType.ServerHttpPath)
                await ImportRecordAsync(queueItem, ServerHttpPaths);
            else if (queueItem.Type == ImportExportRecordType.Tag)
                await ImportRecordAsync(queueItem, Tags);
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

    public void Dispose()
    {
        if (Archive != null)
            Archive.Dispose();
    }
}