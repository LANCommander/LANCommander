using LANCommander.SDK;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.Server.Data.Models;
using LANCommander.Server.ImportExport.Importers;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.ImportExport.Services;
using LANCommander.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZipArchive = SharpCompress.Archives.Zip.ZipArchive;

namespace LANCommander.Server.ImportExport;

public class ImportContext : IDisposable
{
    private Guid? Id { get; set; }
    public object Manifest { get; private set; }
    public BaseModel DataRecord { get; private set; }
    public StorageLocation ArchiveStorageLocation { get; set; }
    public ZipArchive Archive { get; private set; }

    public IImportItemInfo CurrentItem { get; set; }
    public int Processed => Queue.Count(qi => qi.Processed);
    public int Total => Queue.Count;

    private Queue<IImportItemInfo> Queue { get; } = new();
    private IEnumerable<Guid> SelectedRecordIds { get; set; } = [];

    public AsyncEventHandler<ImportStatusUpdate> OnImportStarted { get; set; } = new();
    public AsyncEventHandler<ImportStatusUpdate> OnImportStatusUpdate { get; set; } = new();
    public AsyncEventHandler<ImportStatusUpdate> OnImportComplete { get; set; } = new();
    public AsyncEventHandler<ImportStatusUpdate> OnImportError = new();
    
    private readonly ImportService _importService;
    private readonly StorageLocationService _storageLocationService;
    private readonly ILogger<ImportContext> _logger;
    
    #region Importers
    private readonly ActionImporter _actions;
    private readonly ArchiveImporter _archives;
    private readonly CollectionImporter _collections;
    private readonly CustomFieldImporter _customFields;
    private readonly DeveloperImporter _developers;
    private readonly EngineImporter _engines;
    private readonly GameImporter _games;
    private readonly GenreImporter _genres;
    private readonly KeyImporter _keys;
    private readonly MediaImporter _media;
    private readonly MultiplayerModeImporter _multiplayerModes;
    private readonly PlatformImporter _platforms;
    private readonly PlaySessionImporter _playSessions;
    private readonly PublisherImporter _publishers;
    private readonly RedistributableImporter _redistributables;
    private readonly SaveImporter _saves;
    private readonly SavePathImporter _savePaths;
    private readonly ScriptImporter _scripts;
    private readonly ServerConsoleImporter _serverConsoles;
    private readonly ServerHttpPathImporter _serverHttpPaths;
    private readonly ServerImporter _servers;
    private readonly TagImporter _tags;
    #endregion

    public ImportContext(IServiceProvider serviceProvider)
    {
        _actions = serviceProvider.GetRequiredService<ActionImporter>();
        _archives = serviceProvider.GetRequiredService<ArchiveImporter>();
        _collections = serviceProvider.GetRequiredService<CollectionImporter>();
        _customFields = serviceProvider.GetRequiredService<CustomFieldImporter>();
        _developers = serviceProvider.GetRequiredService<DeveloperImporter>();
        _engines = serviceProvider.GetRequiredService<EngineImporter>();
        _games = serviceProvider.GetRequiredService<GameImporter>();
        _genres = serviceProvider.GetRequiredService<GenreImporter>();
        _keys = serviceProvider.GetRequiredService<KeyImporter>();
        _media = serviceProvider.GetRequiredService<MediaImporter>();
        _multiplayerModes = serviceProvider.GetRequiredService<MultiplayerModeImporter>();
        _platforms = serviceProvider.GetRequiredService<PlatformImporter>();
        _playSessions = serviceProvider.GetRequiredService<PlaySessionImporter>();
        _publishers = serviceProvider.GetRequiredService<PublisherImporter>();
        _redistributables = serviceProvider.GetRequiredService<RedistributableImporter>();
        _saves = serviceProvider.GetRequiredService<SaveImporter>();
        _savePaths = serviceProvider.GetRequiredService<SavePathImporter>();
        _scripts = serviceProvider.GetRequiredService<ScriptImporter>();
        _serverConsoles = serviceProvider.GetRequiredService<ServerConsoleImporter>();
        _serverHttpPaths = serviceProvider.GetRequiredService<ServerHttpPathImporter>();
        _servers = serviceProvider.GetRequiredService<ServerImporter>();
        _tags = serviceProvider.GetRequiredService<TagImporter>();
        
        _importService = serviceProvider.GetRequiredService<ImportService>();
        _storageLocationService = serviceProvider.GetRequiredService<StorageLocationService>();
        _logger = serviceProvider.GetRequiredService<ILogger<ImportContext>>();
    }

    internal bool InQueue<TRecord>(TRecord record, BaseImporter<TRecord> importer)
        where TRecord : class =>
        Queue.Any(qi => qi.Key == importer.GetKey(record));

    public void SetId(Guid id) => Id = id;

    #region Initialize Import
    public async Task<IEnumerable<IImportItemInfo>> InitializeImportAsync(string archivePath)
    {
        _actions.UseContext(this);
        _archives.UseContext(this);
        _collections.UseContext(this);
        _customFields.UseContext(this);
        _developers.UseContext(this);
        _engines.UseContext(this);
        _games.UseContext(this);
        _genres.UseContext(this);
        _keys.UseContext(this);
        _media.UseContext(this);
        _multiplayerModes.UseContext(this);
        _platforms.UseContext(this);
        _playSessions.UseContext(this);
        _publishers.UseContext(this);
        _redistributables.UseContext(this);
        _saves.UseContext(this);
        _savePaths.UseContext(this);
        _scripts.UseContext(this);
        _serverConsoles.UseContext(this);
        _serverHttpPaths.UseContext(this);
        _servers.UseContext(this);
        _tags.UseContext(this);
        
        Archive = ZipArchive.Open(archivePath);

        var manifestEntry = Archive.Entries.FirstOrDefault(e => e.Key == ManifestHelper.ManifestFilename);

        if (manifestEntry == null)
            throw new InvalidOperationException("Invalid import file, cannot load manifest");
        
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

    private async Task<IEnumerable<IImportItemInfo>> InitializeGameImportAsync(SDK.Models.Manifest.Game gameManifest)
    {
        Manifest = gameManifest;
        
        var importItemInfo = new List<IImportItemInfo>();
        
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Actions, _actions).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Archives, _archives).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Collections, _collections).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.CustomFields, _customFields).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Developers, _developers).ToListAsync());
        
        if (gameManifest.Engine != null)
            importItemInfo.AddRange(await GetImportItemInfoAsync([gameManifest.Engine], _engines).ToListAsync());
        
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Genres, _genres).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Keys, _keys).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Media, _media).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.MultiplayerModes, _multiplayerModes).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Platforms, _platforms).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.PlaySessions, _playSessions).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Publishers, _publishers).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Saves, _saves).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.SavePaths, _savePaths).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Scripts, _scripts).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(gameManifest.Tags, _tags).ToListAsync());

        return importItemInfo;
    }

    private async Task<IEnumerable<IImportItemInfo>> InitializeRedistributableImportAsync(SDK.Models.Manifest.Redistributable redistributableManifest)
    {
        Manifest = redistributableManifest;
        
        var importItemInfo = new List<IImportItemInfo>();
        
        importItemInfo.AddRange(await GetImportItemInfoAsync(redistributableManifest.Archives, _archives).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(redistributableManifest.Scripts, _scripts).ToListAsync());

        return importItemInfo;
    }

    private async Task<IEnumerable<IImportItemInfo>> InitializeServerImportAsync(SDK.Models.Manifest.Server serverManifest)
    {
        Manifest = serverManifest;
        
        var importItemInfo = new List<IImportItemInfo>();
        
        importItemInfo.AddRange(await GetImportItemInfoAsync(serverManifest.Actions, _actions).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(serverManifest.Scripts, _scripts).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(serverManifest.ServerConsoles, _serverConsoles).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(serverManifest.HttpPaths, _serverHttpPaths).ToListAsync());
        
        return importItemInfo;
    }
    #endregion
    
    public async Task PrepareImportQueueAsync(IEnumerable<Guid> selectedRecordIds, Guid storageLocationId)
    {
        SelectedRecordIds = selectedRecordIds;
        
        ArchiveStorageLocation = await _storageLocationService.GetAsync(storageLocationId);

        if (Manifest is SDK.Models.Manifest.Game gameManifest)
            await AddAsync(gameManifest);
        
        if (Manifest is SDK.Models.Manifest.Redistributable redistributableManifest)
            await AddAsync(redistributableManifest);
        
        if (Manifest is SDK.Models.Manifest.Server serverManifest)
            await AddAsync(serverManifest);
    }

    public async Task AddAsync(SDK.Models.Manifest.Game game)
    {
        await AddAsync(game.Actions, _actions);
        await AddAsync(game.Archives, _archives);
        await AddAsync(game.Collections, _collections);
        await AddAsync(game.CustomFields, _customFields);
        await AddAsync(game.Developers, _developers);
        await AddAsync(game.Engine, _engines);
        await AddAsync(game.Genres, _genres);
        await AddAsync(game.Keys, _keys);
        await AddAsync(game.Media, _media);
        await AddAsync(game.MultiplayerModes, _multiplayerModes);
        await AddAsync(game.Platforms, _platforms);
        await AddAsync(game.PlaySessions, _playSessions);
        await AddAsync(game.Publishers, _publishers);
        await AddAsync(game.Saves, _saves);
        await AddAsync(game.Scripts, _scripts);
        await AddAsync(game.Tags, _tags);
        await AddAsync(game, _games);
    }

    public async Task AddAsync(SDK.Models.Manifest.Redistributable redistributable)
    {
        await AddAsync(redistributable.Archives, _archives);
        await AddAsync(redistributable.Scripts, _scripts);
        await AddAsync(redistributable, _redistributables);
    }

    public async Task AddAsync(SDK.Models.Manifest.Server server)
    {
        await AddAsync(server.Actions, _actions);
        await AddAsync(server.Scripts, _scripts);
        await AddAsync(server.HttpPaths, _serverHttpPaths);
        await AddAsync(server.ServerConsoles, _serverConsoles);
        await AddAsync(server, _servers);
    }
    
    private async Task AddAsync<TRecord>(IEnumerable<TRecord> records, BaseImporter<TRecord> importer)
        where TRecord : class
    {
        foreach (var record in records)
            await AddAsync(record, importer);
    }

    private async Task AddAsync<TRecord>(TRecord? record, BaseImporter<TRecord> importer)
        where TRecord : class
    {
        if (record != null && !InQueue(record, importer) && await importer.CanImportAsync(record))
            Queue.Enqueue(await importer.GetImportInfoAsync(record));
    }

    public async Task ImportQueueAsync()
    {
        await OnImportStarted?.InvokeAsync(new ImportStatusUpdate
        {
            Index = -1,
            Total = Queue.Count,
        })!;

        int deferred = 0;

        while (Queue.Count > 0)
        {
            var queueItem = Queue.Dequeue();
            
            await OnImportStatusUpdate?.InvokeAsync(new ImportStatusUpdate
            {
                CurrentItem = queueItem,
                Index = Processed,
                Total = Total,
            })!;
            
            var success = await TryImportAsync(queueItem);

            if (success)
            {
                deferred = 0;
                continue;
            }
            
            Queue.Enqueue(queueItem);
            deferred++;

            if (deferred >= Queue.Count)
                throw new InvalidOperationException("Import deadlocked: remaining jobs cannot be satisfied.");
        }

        await OnImportComplete?.InvokeAsync(new ImportStatusUpdate
        {
            Index = Total - 1,
            Total = Total,
        })!;

        _importService.RemoveContext(Id.Value);
    }

    private async Task<bool> TryImportAsync(IImportItemInfo queueItem)
    {
        try
        {
            switch (queueItem.Type)
            {
                case ImportExportRecordType.Action:
                    return await _actions.ImportAsync(queueItem);

                case ImportExportRecordType.Archive:
                    return await _archives.ImportAsync(queueItem);

                case ImportExportRecordType.Collection:
                    return await _collections.ImportAsync(queueItem);

                case ImportExportRecordType.CustomField:
                    return await _customFields.ImportAsync(queueItem);

                case ImportExportRecordType.Developer:
                    return await _developers.ImportAsync(queueItem);

                case ImportExportRecordType.Engine:
                    return await _engines.ImportAsync(queueItem);

                case ImportExportRecordType.Game:
                    return await _games.ImportAsync(queueItem);

                case ImportExportRecordType.Genre:
                    return await _genres.ImportAsync(queueItem);

                case ImportExportRecordType.Key:
                    return await _keys.ImportAsync(queueItem);

                case ImportExportRecordType.Media:
                    return await _media.ImportAsync(queueItem);

                case ImportExportRecordType.MultiplayerMode:
                    return await _multiplayerModes.ImportAsync(queueItem);

                case ImportExportRecordType.Platform:
                    return await _platforms.ImportAsync(queueItem);

                case ImportExportRecordType.PlaySession:
                    return await _playSessions.ImportAsync(queueItem);

                case ImportExportRecordType.Publisher:
                    return await _publishers.ImportAsync(queueItem);

                case ImportExportRecordType.Redistributable:
                    return await _redistributables.ImportAsync(queueItem);

                case ImportExportRecordType.Save:
                    return await _saves.ImportAsync(queueItem);

                case ImportExportRecordType.SavePath:
                    return await _savePaths.ImportAsync(queueItem);

                case ImportExportRecordType.Script:
                    return await _scripts.ImportAsync(queueItem);

                case ImportExportRecordType.Server:
                    return await _servers.ImportAsync(queueItem);

                case ImportExportRecordType.ServerConsole:
                    return await _serverConsoles.ImportAsync(queueItem);

                case ImportExportRecordType.ServerHttpPath:
                    return await _serverHttpPaths.ImportAsync(queueItem);

                case ImportExportRecordType.Tag:
                    return await _tags.ImportAsync(queueItem);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing record {RecordName}", queueItem.Name);
            
            await OnImportError?.InvokeAsync(new ImportStatusUpdate
            {
                CurrentItem = CurrentItem,
                Index = Processed,
                Total = Total,
                Error = ex.Message,
            })!;
        }

        return false;
    }
    
    private async IAsyncEnumerable<ImportItemInfo<TRecord>> GetImportItemInfoAsync<TRecord>(IEnumerable<TRecord> records,
        BaseImporter<TRecord> importer) where TRecord : class
    {
        if (records != null)
            foreach (var record in records)
            {
                if (record != null && record.GetType() == typeof(TRecord))
                    yield return await importer.GetImportInfoAsync(record);
            }
    }

    public void Dispose()
    {
        if (Archive != null)
            Archive.Dispose();
    }
}