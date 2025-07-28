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
    IMapper mapper,
    ILogger<ImportContext> logger) : IDisposable
{
    public object Manifest { get; private set; }
    public BaseModel DataRecord { get; private set; }
    public StorageLocation ArchiveStorageLocation { get; }
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
        if (!(await Games.ExistsAsync(gameManifest)))
            DataRecord = await Games.AddAsync(gameManifest);
        else
            DataRecord = await Games.UpdateAsync(gameManifest);
        
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
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Engine) && gameManifest.Engine != null)
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

        if (importRecordFlags.HasFlag(ImportRecordFlags.SavePaths))
            await AddToImportQueueAsync(ImportRecordFlags.SavePaths, gameManifest.SavePaths);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Scripts))
            await AddToImportQueueAsync(ImportRecordFlags.Scripts, gameManifest.Scripts);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Tags))
            await AddToImportQueueAsync(ImportRecordFlags.Tags, gameManifest.Tags);
    }

    public async Task PrepareRedistributableImportQueueAsync(SDK.Models.Manifest.Redistributable redistributableManifest,
        ImportRecordFlags importRecordFlags)
    { 
        if (!(await Redistributables.ExistsAsync(redistributableManifest)))
            DataRecord = await Redistributables.AddAsync(redistributableManifest);
        else
            DataRecord = await Redistributables.UpdateAsync(redistributableManifest);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Archives))
            await AddToImportQueueAsync(ImportRecordFlags.Archives, redistributableManifest.Archives);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Scripts))
            await AddToImportQueueAsync(ImportRecordFlags.Scripts, redistributableManifest.Scripts);
    }

    public async Task PrepareServerImportQueueAsync(SDK.Models.Manifest.Server serverManifest,
        ImportRecordFlags importRecordFlags)
    {
        if (!(await Servers.ExistsAsync(serverManifest)))
            DataRecord = await Servers.AddAsync(serverManifest);
        else
            DataRecord = await Servers.UpdateAsync(serverManifest);
        
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

    private async Task AddToImportQueueAsync<TRecord>(ImportRecordFlags type, IEnumerable<TRecord> records) where TRecord : SDK.Models.Manifest.BaseModel
    {
        if (records != null)
            _queue.AddRange(records.Select(r => new ImportQueueItem(type, r)));
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