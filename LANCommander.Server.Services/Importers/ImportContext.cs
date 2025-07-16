using System.IO.Compression;
using System.Text.Json;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.Server.Data.Models;
using Microsoft.Extensions.DependencyInjection;
using SharpCompress.Readers;
using ZipArchive = SharpCompress.Archives.Zip.ZipArchive;

namespace LANCommander.Server.Services.Importers;

public class ImportContext(
    GameImporter gameImporter,
    RedistributableImporter redistributableImporter,
    ServerImporter serverImporter,
    GameService gameService,
    RedistributableService redistributableService,
    ServerService serverService,
    ArchiveService archiveService,
    MediaService mediaService,
    GameSaveService saveService,
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
    
    #region Initialize Import
    public async Task<IEnumerable<ImportItemInfo>> InitializeImportAsync(string archivePath)
    {
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
    public async Task<IEnumerable<ImportItemInfo>> InitializeExportAsync(Guid recordId, ExportRecordType recordType)
    {
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

    private async Task<IEnumerable<ImportItemInfo>> InitializeGameExportAsync(Guid gameId)
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

    private async Task<IEnumerable<ImportItemInfo>> InitializeRedistributableExportAsync(Guid redistributableId)
    {
        var redistributableManifest = await redistributableService.GetManifestAsync(redistributableId);
        
        Manifest = redistributableManifest;
        
        var importItemInfo = new List<ImportItemInfo>();
        
        importItemInfo.AddRange(await GetImportItemInfoAsync(redistributableManifest.Archives, Archives).ToListAsync());
        importItemInfo.AddRange(await GetImportItemInfoAsync(redistributableManifest.Scripts, Scripts).ToListAsync());

        return importItemInfo;
    }

    private async Task<IEnumerable<ImportItemInfo>> InitializeServerExportAsync(Guid serverId)
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

    public async Task PrepareGameImportQueueAsync(SDK.Models.Manifest.Game record, ImportRecordFlags importRecordFlags)
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

    public async Task PrepareRedistributableImportQueueAsync(SDK.Models.Manifest.Redistributable record,
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

    public async Task PrepareServerImportQueueAsync(SDK.Models.Manifest.Server record,
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
    #endregion
    
    #region Prepare Export Queue
    public async Task PrepareExportQueueAsync(ImportRecordFlags importRecordFlags)
    {
        if (DataRecord is Game game)
            await PrepareGameExportQueueAsync(game, importRecordFlags);
        
        if (DataRecord is Redistributable redistributable)
            await PrepareRedistributableExportQueueAsync(redistributable, importRecordFlags);
        
        if (Manifest is Data.Models.Server server)
            await PrepareServerExportQueueAsync(server, importRecordFlags);
    }
    
    public async Task PrepareGameExportQueueAsync(Game record, ImportRecordFlags importRecordFlags)
    {
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
            await AddToQueueAsync(record.GameSaves);

        if (importRecordFlags.HasFlag(ImportRecordFlags.SavePaths))
            await AddToQueueAsync(record.SavePaths);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Scripts))
            await AddToQueueAsync(record.Scripts);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Tags))
            await AddToQueueAsync(record.Tags);
    }

    public async Task PrepareRedistributableExportQueueAsync(Redistributable record,
        ImportRecordFlags importRecordFlags)
    {
        if (importRecordFlags.HasFlag(ImportRecordFlags.Archives))
            await AddToQueueAsync(record.Archives);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Scripts))
            await AddToQueueAsync(record.Scripts);
    }

    public async Task PrepareServerExportQueueAsync(Data.Models.Server record,
        ImportRecordFlags importRecordFlags)
    {
        if (importRecordFlags.HasFlag(ImportRecordFlags.Actions))
            await AddToQueueAsync(record.Actions);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.Scripts))
            await AddToQueueAsync(record.Scripts);
        
        if (importRecordFlags.HasFlag(ImportRecordFlags.ServerConsoles))
            await AddToQueueAsync(record.ServerConsoles);

        if (importRecordFlags.HasFlag(ImportRecordFlags.ServerHttpPaths))
            await AddToQueueAsync(record.HttpPaths);
    }
    #endregion

    private async Task AddToQueueAsync(IEnumerable<object> records)
    {
        _queue.AddRange(records);
    }

    private async Task AddToQueueAsync(object record)
    {
        _queue.Add(record);
    }

    public async Task ImportQueueAsync()
    {
        foreach (var record in _queue)
        {
            if (record is SDK.Models.Manifest.Action action)
                await ImportRecordAsync(action, Actions);
            else if (record is SDK.Models.Manifest.Archive archive)
                await ImportRecordAsync(archive, Archives);
            else if (record is SDK.Models.Manifest.Collection collection)
                await ImportRecordAsync(collection, Collections);
            else if (record is SDK.Models.Manifest.GameCustomField customField)
                await ImportRecordAsync(customField, CustomFields);
            else if (record is SDK.Models.Manifest.Company company)
            {
                await ImportRecordAsync(company, Developers);
                await ImportRecordAsync(company, Publishers);
            }
            else if (record is SDK.Models.Manifest.Engine engine)
                await ImportRecordAsync(engine, Engines);
            else if (record is SDK.Models.Manifest.Genre genre)
                await ImportRecordAsync(genre, Genres);
            else if (record is SDK.Models.Manifest.Key key)
                await ImportRecordAsync(key, Keys);
            else if (record is SDK.Models.Manifest.Media media)
                await ImportRecordAsync(media, Media);
            else if (record is SDK.Models.Manifest.MultiplayerMode multiplayerMode)
                await ImportRecordAsync(multiplayerMode, MultiplayerModes);
            else if (record is SDK.Models.Manifest.Platform platform)
                await ImportRecordAsync(platform, Platforms);
            else if (record is SDK.Models.Manifest.PlaySession playSession)
                await ImportRecordAsync(playSession, PlaySessions);
            else if (record is SDK.Models.Manifest.Save save)
                await ImportRecordAsync(save, Saves);
            else if (record is SDK.Models.Manifest.SavePath savePath)
                await ImportRecordAsync(savePath, SavePaths);
            else if (record is SDK.Models.Manifest.Script script)
                await ImportRecordAsync(script, Scripts);
            else if (record is SDK.Models.Manifest.ServerConsole serverConsole)
                await ImportRecordAsync(serverConsole, ServerConsoles);
            else if (record is SDK.Models.Manifest.ServerHttpPath serverHttpPath)
                await ImportRecordAsync(serverHttpPath, ServerHttpPaths);
            else if (record is SDK.Models.Manifest.Tag tag)
                await ImportRecordAsync(tag, Tags);
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
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(ManifestHelper.Serialize(Manifest));
                writer.Flush();

                var manifestEntry = export.CreateEntry(ManifestHelper.ManifestFilename, CompressionLevel.Fastest);

                using (var entryStream = manifestEntry.Open())
                {
                    ms.Seek(0, SeekOrigin.Begin);
                    await ms.CopyToAsync(entryStream);
                }
            }

            #region Add Game Files
            if (gameManifest != null)
            {
                foreach (var archive in gameManifest.Archives)
                    await AddArchiveToExport(archive, export);

                foreach (var media in gameManifest.Media)
                    await AddMediaToExport(media, export);
                
                foreach (var save in gameManifest.Saves)
                    await AddSaveToExport(save, export);
            }
            #endregion
            
            #region Add Redistributable Files
            if (redistributableManifest != null)
            {
                foreach (var archive in redistributableManifest.Archives)
                    await AddArchiveToExport(archive, export);
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
        var manifest = await Games.ExportAsync(game);
        
        foreach (var record in _queue)
        {
            if (record is Data.Models.Action action)
                manifest.Actions.Add(await ExportRecordAsync(action, Actions));
            else if (record is Data.Models.Archive archive)
                manifest.Archives.Add(await ExportRecordAsync(archive, Archives));
            else if (record is Data.Models.Collection collection)
                manifest.Collections.Add(await ExportRecordAsync(collection, Collections));
            else if (record is Data.Models.GameCustomField customField)
                manifest.CustomFields.Add(await ExportRecordAsync(customField, CustomFields));
            else if (record is Data.Models.Company company)
            {
                manifest.Developers.Add(await ExportRecordAsync(company, Developers));
                manifest.Publishers.Add(await ExportRecordAsync(company, Publishers));
            }
            else if (record is Data.Models.Engine engine)
                manifest.Engine = await ExportRecordAsync(engine, Engines);
            else if (record is Data.Models.Genre genre)
                manifest.Genres.Add(await ExportRecordAsync(genre, Genres));
            else if (record is Data.Models.Key key)
                manifest.Keys.Add(await ExportRecordAsync(key, Keys));
            else if (record is Data.Models.Media media)
                manifest.Media.Add(await ExportRecordAsync(media, Media));
            else if (record is Data.Models.MultiplayerMode multiplayerMode)
                manifest.MultiplayerModes.Add(await ExportRecordAsync(multiplayerMode, MultiplayerModes));
            else if (record is Data.Models.Platform platform)
                manifest.Platforms.Add(await ExportRecordAsync(platform, Platforms));
            else if (record is Data.Models.PlaySession playSession)
                manifest.PlaySessions.Add(await ExportRecordAsync(playSession, PlaySessions));
            else if (record is Data.Models.GameSave save)
                manifest.Saves.Add(await ExportRecordAsync(save, Saves));
            else if (record is Data.Models.SavePath savePath)
                manifest.SavePaths.Add(await ExportRecordAsync(savePath, SavePaths));
            else if (record is Data.Models.Script script)
                manifest.Scripts.Add(await ExportRecordAsync(script, Scripts));
            else if (record is Data.Models.Tag tag)
                manifest.Tags.Add(await ExportRecordAsync(tag, Tags));
        }

        return manifest;
    }

    public async Task<SDK.Models.Manifest.Redistributable> ExportRedistributableQueueAsync(Data.Models.Redistributable redistributable)
    {
        var manifest = await Redistributables.ExportAsync(redistributable);
        
        foreach (var record in _queue)
        {
            if (record is Data.Models.Archive archive)
                manifest.Archives.Add(await ExportRecordAsync(archive, Archives));
            else if (record is Data.Models.Script script)
                manifest.Scripts.Add(await ExportRecordAsync(script, Scripts));
        }

        return manifest;
    }

    public async Task<SDK.Models.Manifest.Server> ExportServerQueueAsync(Data.Models.Server server)
    {
        var manifest = await Servers.ExportAsync(server);

        foreach (var record in _queue)
        {
            if (record is Data.Models.Action action)
                manifest.Actions.Add(await ExportRecordAsync(action, Actions));
            else if (record is Data.Models.Script script)
                manifest.Scripts.Add(await ExportRecordAsync(script, Scripts));
            else if (record is Data.Models.ServerConsole serverConsole)
                manifest.ServerConsoles.Add(await ExportRecordAsync(serverConsole, ServerConsoles));
            else if (record is Data.Models.ServerHttpPath serverHttpPath)
                await ExportRecordAsync(serverHttpPath, ServerHttpPaths);
        }

        return manifest;
    }

    private async Task AddArchiveToExport(SDK.Models.Manifest.Archive archive, System.IO.Compression.ZipArchive zip)
    {
        var archiveEntry = zip.CreateEntry($"Archives/{archive.Id}");
        var archivePath = await archiveService.GetArchiveFileLocationAsync(archive.ObjectKey);

        using (var archiveEntryStream = archiveEntry.Open())
        using (var archiveFileStream = new FileStream(archivePath, FileMode.Open))
        {
            await archiveFileStream.CopyToAsync(archiveEntryStream);
        }
    }
    
    private async Task AddMediaToExport(SDK.Models.Manifest.Media media, System.IO.Compression.ZipArchive zip)
    {
        var mediaEntry = zip.CreateEntry($"Media/{media.Id}");
        var mediaPath = await mediaService.GetMediaPathAsync(media.Id);

        using (var mediaEntryStream = mediaEntry.Open())
        using (var mediaFileStream = new FileStream(mediaPath, FileMode.Open))
        {
            await mediaFileStream.CopyToAsync(mediaEntryStream);
        }
    }

    private async Task AddSaveToExport(SDK.Models.Manifest.Save save, System.IO.Compression.ZipArchive zip)
    {
        var saveEntry = zip.CreateEntry($"Saves/{save.Id}");
        var savePath = await saveService.GetSavePathAsync(save.Id);
        
        using (var saveEntryStream = saveEntry.Open())
        using (var saveFileStream = new FileStream(savePath, FileMode.Open))
        {
            await saveFileStream.CopyToAsync(saveEntryStream);
        }
    }
    
    private async IAsyncEnumerable<ImportItemInfo> GetImportItemInfoAsync<TModel, TEntity>(IEnumerable<TModel> records,
        IImporter<TModel, TEntity> importer)
    {
        foreach (var record in records)
        {
            if (record.GetType() == typeof(TModel))
                yield return await importer.GetImportInfoAsync(record);
        }
    }

    private async Task ImportRecordAsync<TRecord, TEntity>(TRecord record, IImporter<TRecord, TEntity> importer)
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
    
    private async Task<TRecord> ExportRecordAsync<TRecord, TEntity>(TEntity entity, IImporter<TRecord, TEntity> importer)
    {
        try
        {
            var record = await importer.ExportAsync(entity);

            _queue.Remove(entity);
            _processed.Add(record);
            
            OnRecordProcessed?.Invoke(this, record);

            return record;
        }
        catch (Exception ex)
        {
            _queue.Remove(entity);
            Errored.Add(entity, ex.Message);
            OnRecordError?.Invoke(this, ex);

            return default;
        }
    }

    public void Dispose()
    {
        Archive.Dispose();
    }
}