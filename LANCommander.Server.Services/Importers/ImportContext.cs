using LANCommander.Server.Data.Models;
using Microsoft.Extensions.DependencyInjection;
using SharpCompress.Archives.Zip;

namespace LANCommander.Server.Services.Importers;

public class ImportContext<TRecord>(
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
    IImporter<SDK.Models.Manifest.Tag, Data.Models.Tag> tagImporter)
    where TRecord : Data.Models.BaseModel
{
    public TRecord Record { get; private set; }
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

    public void UseArchive(ZipArchive archive)
    {
        Archive = archive;
    }
    
    public void UseRecord(TRecord record)
    {
        Record = record;
    }

    public async Task AddToQueueAsync(IEnumerable<object> records)
    {
        _queue.AddRange(records);
    }

    public async Task AddToQueueAsync(object record)
    {
        _queue.Add(record);
    }

    public async Task ProcessQueueAsync()
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
}