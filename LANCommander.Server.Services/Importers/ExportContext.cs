using System.Text.Json;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.Server.Data.Models;
using Microsoft.Extensions.DependencyInjection;
using SharpCompress.Archives.Zip;

namespace LANCommander.Server.Services.Importers;

public class ExportContext(
    GameService gameService,
    RedistributableService redistributableService,
    ServerService serverService,
    GameImporter gameImporter,
    RedistributableImporter redistributableImporter,
    ServerImporter serverImporter,
    IImporter<SDK.Models.Manifest.Action, Data.Models.Action> actionImporter,
    IImporter<SDK.Models.Manifest.Archive, Data.Models.Archive> archiveImporter,
    IImporter<SDK.Models.Manifest.Collection, Data.Models.Collection> collectionImporter,
    IImporter<SDK.Models.Manifest.GameCustomField, Data.Models.GameCustomField> customFieldImporter,
    IImporter<SDK.Models.Manifest.Company, Data.Models.Company> developerImporter,
    IImporter<SDK.Models.Manifest.Engine, Data.Models.Engine> engineImporter,
    IImporter<SDK.Models.Manifest.Genre, Data.Models.Genre> genreImporter,
    IImporter<SDK.Models.Manifest.Key, Data.Models.Key> keyImporter,
    IImporter<SDK.Models.Manifest.Media, Data.Models.Media> mediaImporter,
    IImporter<SDK.Models.Manifest.MultiplayerMode, Data.Models.MultiplayerMode> multiplayerModeImporter,
    IImporter<SDK.Models.Manifest.Platform, Data.Models.Platform> platformImporter,
    IImporter<SDK.Models.Manifest.PlaySession, Data.Models.PlaySession> playSessionImporter,
    IImporter<SDK.Models.Manifest.Company, Data.Models.Company> publisherImporter,
    IImporter<SDK.Models.Manifest.Save, Data.Models.GameSave> saveImporter,
    IImporter<SDK.Models.Manifest.SavePath, Data.Models.SavePath> savePathImporter,
    IImporter<SDK.Models.Manifest.Script, Data.Models.Script> scriptImporter,
    IImporter<SDK.Models.Manifest.ServerConsole, Data.Models.ServerConsole> serverConsoleImporter,
    IImporter<SDK.Models.Manifest.ServerHttpPath, Data.Models.ServerHttpPath> serverHttpPathImporter,
    IImporter<SDK.Models.Manifest.Tag, Data.Models.Tag> tagImporter) : IDisposable
{
    public object Manifest { get; private set; }
    public BaseModel DataRecord { get; private set; }
    public StorageLocation ArchiveStorageLocation { get; }
    public ZipArchive Archive { get; private set; }

    public GameImporter Games { get; private set; } = gameImporter;
    public RedistributableImporter Redistributables { get; private set; } = redistributableImporter;
    public ServerImporter Servers { get; private set; } = serverImporter;

    public IImporter<SDK.Models.Manifest.Action, Data.Models.Action> Actions = actionImporter;
    public IImporter<SDK.Models.Manifest.Archive, Data.Models.Archive> Archives = archiveImporter;
    public IImporter<SDK.Models.Manifest.Collection, Data.Models.Collection> Collections = collectionImporter;
    public IImporter<SDK.Models.Manifest.GameCustomField, Data.Models.GameCustomField> CustomFields = customFieldImporter;
    public IImporter<SDK.Models.Manifest.Company, Data.Models.Company> Developers = developerImporter;
    public IImporter<SDK.Models.Manifest.Engine, Data.Models.Engine> Engines = engineImporter;
    public IImporter<SDK.Models.Manifest.Genre, Data.Models.Genre> Genres = genreImporter;
    public IImporter<SDK.Models.Manifest.Key, Data.Models.Key> Keys = keyImporter;
    public IImporter<SDK.Models.Manifest.Media, Data.Models.Media> Media = mediaImporter;
    public IImporter<SDK.Models.Manifest.MultiplayerMode, Data.Models.MultiplayerMode> MultiplayerModes = multiplayerModeImporter;
    public IImporter<SDK.Models.Manifest.Platform, Data.Models.Platform> Platforms = platformImporter;
    public IImporter<SDK.Models.Manifest.PlaySession, Data.Models.PlaySession> PlaySessions = playSessionImporter;
    public IImporter<SDK.Models.Manifest.Company, Data.Models.Company> Publishers = publisherImporter;
    public IImporter<SDK.Models.Manifest.Save, Data.Models.GameSave> Saves = saveImporter;
    public IImporter<SDK.Models.Manifest.SavePath, Data.Models.SavePath> SavePaths = savePathImporter;
    public IImporter<SDK.Models.Manifest.Script, Data.Models.Script> Scripts = scriptImporter;
    public IImporter<SDK.Models.Manifest.ServerConsole, Data.Models.ServerConsole> ServerConsoles = serverConsoleImporter;
    public IImporter<SDK.Models.Manifest.ServerHttpPath, Data.Models.ServerHttpPath> ServerHttpPaths = serverHttpPathImporter;
    public IImporter<SDK.Models.Manifest.Tag, Data.Models.Tag> Tags = tagImporter;

    public int Remaining => _queue.Count;
    public int Processed => _processed.Count;
    public int Total => _queue.Count + _processed.Count + Errored.Count;

    private List<object> _queue { get; } = new();
    private List<object> _processed { get; } = new();
    public Dictionary<object, string> Errored { get; } = new();

    public EventHandler<object> OnRecordAdded;
    public EventHandler<object> OnRecordProcessed;
    public EventHandler<object> OnRecordError;
    
    public async Task<IEnumerable<ImportItemInfo>> InitializeAsync(Guid recordId, ExportRecordType recordType)
    {
        switch (recordType)
        {
            case ExportRecordType.Game:
                return await InitializeGameAsync(recordId);
            
            case ExportRecordType.Redistributable:
                return await InitializeRedistributableAsync(recordId);
            
            case ExportRecordType.Server:
                return await InitializeServerAsync(recordId);
            
            default:
                throw new InvalidOperationException("Unknown record type");
        }
    }

    private async Task<IEnumerable<ImportItemInfo>> InitializeGameAsync(Guid gameId)
    {
        var gameManifest = await gameService.GetManifestAsync(gameId);
        
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

    private async Task<IEnumerable<ImportItemInfo>> InitializeRedistributableAsync(Guid redistributableId)
    {
        var redistributableManifest = await redistributableService.GetManifestAsync(redistributableId);
        
        Manifest = redistributableManifest;
        
        var importItemInfo = new List<ImportItemInfo>();
        
        importItemInfo.AddRange(await GetImportItemInfoAsync(redistributableManifest.Archives, Archives).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(redistributableManifest.Scripts, Scripts).ToListAsync());

        return importItemInfo;
    }

    private async Task<IEnumerable<ImportItemInfo>> InitializeServerAsync(Guid serverId)
    {
        var serverManifest = await serverService.GetManifestAsync(serverId);
        
        Manifest = serverManifest;
        
        var importItemInfo = new List<ImportItemInfo>();
        
        importItemInfo.AddRange(await GetImportItemInfoAsync(serverManifest.Actions, Actions).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(serverManifest.Scripts, Scripts).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(serverManifest.ServerConsoles, ServerConsoles).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(serverManifest.HttpPaths, ServerHttpPaths).ToListAsync());
        
        return importItemInfo;
    }

    public async Task PrepareExportQueueAsync(ImportRecordFlags importRecordFlags)
    {
        if (Manifest is SDK.Models.Manifest.Game gameManifest)
            await PrepareGameExportQueueAsync(gameManifest, importRecordFlags);
        
        if (Manifest is SDK.Models.Manifest.Redistributable redistributableManifest)
            await PrepareRedistributableExportQueueAsync(redistributableManifest, importRecordFlags);
        
        if (Manifest is SDK.Models.Manifest.Server serverManifest)
            await PrepareServerExportQueueAsync(serverManifest, importRecordFlags);
    }

    public async Task PrepareGameExportQueueAsync(SDK.Models.Manifest.Game record, ImportRecordFlags importRecordFlags)
    {
        if (!(await gameImporter.ExistsAsync(record)))
            DataRecord = await gameImporter.AddAsync(record);
        else
            DataRecord = await gameImporter.UpdateAsync(record);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Actions))
            await AddToQueueAsync(record.Actions);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Archives))
            await AddToQueueAsync(record.Archives);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Collections))
            await AddToQueueAsync(record.Collections);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.CustomFields))
            await AddToQueueAsync(record.CustomFields);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Developers))
            await AddToQueueAsync(record.Developers);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Engine))
            await AddToQueueAsync(record.Engine);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Genres))
            await AddToQueueAsync(record.Genres);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Keys))
            await AddToQueueAsync(record.Keys);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Media))
            await AddToQueueAsync(record.Media);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.MultiplayerModes))
            await AddToQueueAsync(record.MultiplayerModes);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Platforms))
            await AddToQueueAsync(record.Platforms);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.PlaySessions))
            await AddToQueueAsync(record.PlaySessions);

        if (importRecordFlags.HasFlag(ImportRecordFlags.Publishers))
            await AddToQueueAsync(record.Publishers);

        if (importRecordFlags.HasFlag(ImportRecordFlags.Saves))
            await AddToQueueAsync(record.Saves);

        if (importRecordFlags.HasFlag(ImportRecordFlags.SavePaths))
            await AddToQueueAsync(record.SavePaths);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Scripts))
            await AddToQueueAsync(record.Scripts);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Tags))
            await AddToQueueAsync(record.Tags);
    }

    public async Task PrepareRedistributableExportQueueAsync(SDK.Models.Manifest.Redistributable record,
        ImportRecordFlags importRecordFlags)
    { 
        if (!(await redistributableImporter.ExistsAsync(record)))
            DataRecord = await redistributableImporter.AddAsync(record);
        else
            DataRecord = await redistributableImporter.UpdateAsync(record);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Archives))
            await AddToQueueAsync(record.Archives);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Scripts))
            await AddToQueueAsync(record.Scripts);
    }

    public async Task PrepareServerExportQueueAsync(SDK.Models.Manifest.Server record,
        ImportRecordFlags importRecordFlags)
    {
        if (!(await serverImporter.ExistsAsync(record)))
            DataRecord = await serverImporter.AddAsync(record);
        else
            DataRecord = await serverImporter.UpdateAsync(record);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Actions))
            await AddToQueueAsync(record.Actions);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Scripts))
            await AddToQueueAsync(record.Scripts);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.ServerConsoles))
            await AddToQueueAsync(record.ServerConsoles);

        if (importRecordFlags.HasFlag(ImportRecordFlags.ServerHttpPaths))
            await AddToQueueAsync(record.HttpPaths);
    }

    private async Task AddToQueueAsync(IEnumerable<object> records)
    {
        _queue.AddRange(records);
    }

    private async Task AddToQueueAsync(object record)
    {
        _queue.Add(record);
    }

    public async Task ProcessQueueAsync()
    {
        foreach (var record in _queue)
        {
            if (record is SDK.Models.Manifest.Action action)
                await ExportRecordAsync(action, Actions);
            else if (record is SDK.Models.Manifest.Archive archive)
                await ExportRecordAsync(archive, Archives);
            else if (record is SDK.Models.Manifest.Collection collection)
                await ExportRecordAsync(collection, Collections);
            else if (record is SDK.Models.Manifest.GameCustomField customField)
                await ExportRecordAsync(customField, CustomFields);
            else if (record is SDK.Models.Manifest.Company company)
            {
                await ExportRecordAsync(company, Developers);
                await ExportRecordAsync(company, Publishers);
            }
            else if (record is SDK.Models.Manifest.Engine engine)
                await ExportRecordAsync(engine, Engines);
            else if (record is SDK.Models.Manifest.Genre genre)
                await ExportRecordAsync(genre, Genres);
            else if (record is SDK.Models.Manifest.Key key)
                await ExportRecordAsync(key, Keys);
            else if (record is SDK.Models.Manifest.Media media)
                await ExportRecordAsync(media, Media);
            else if (record is SDK.Models.Manifest.MultiplayerMode multiplayerMode)
                await ExportRecordAsync(multiplayerMode, MultiplayerModes);
            else if (record is SDK.Models.Manifest.Platform platform)
                await ExportRecordAsync(platform, Platforms);
            else if (record is SDK.Models.Manifest.PlaySession playSession)
                await ExportRecordAsync(playSession, PlaySessions);
            else if (record is SDK.Models.Manifest.Save save)
                await ExportRecordAsync(save, Saves);
            else if (record is SDK.Models.Manifest.SavePath savePath)
                await ExportRecordAsync(savePath, SavePaths);
            else if (record is SDK.Models.Manifest.Script script)
                await ExportRecordAsync(script, Scripts);
            else if (record is SDK.Models.Manifest.ServerConsole serverConsole)
                await ExportRecordAsync(serverConsole, ServerConsoles);
            else if (record is SDK.Models.Manifest.ServerHttpPath serverHttpPath)
                await ExportRecordAsync(serverHttpPath, ServerHttpPaths);
            else if (record is SDK.Models.Manifest.Tag tag)
                await ExportRecordAsync(tag, Tags);
        }
    }

    private async IAsyncEnumerable<ImportItemInfo> GetImportItemInfoAsync<TModel, TEntity>(IEnumerable<TModel> records,
        IImporter<TModel, TEntity> importer)
    {
        foreach (var record in records)
        {
            if (record.GetType() == typeof(TModel))
                yield return await importer.InfoAsync(record);
        }
    }

    private async Task ExportRecordAsync<TRecord, TEntity>(TRecord record, IImporter<TRecord, TEntity> importer)
    {
        try
        {
            if (await importer.ExistsAsync(record))
                await importer.UpdateAsync(record);
            else
                await importer.AddAsync(record);

            _queue.Remove(record);
            _processed.Add(record);
            
            OnRecordProcessed?.Invoke(this, record);
        }
        catch (Exception ex)
        {
            _queue.Remove(record);
            Errored.Add(record, ex.Message);
            OnRecordError?.Invoke(this, ex);
        }
    }

    public void Dispose()
    {
        Archive.Dispose();
    }
}