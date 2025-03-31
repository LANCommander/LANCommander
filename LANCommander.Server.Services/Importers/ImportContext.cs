using LANCommander.Server.Data.Models;
using Microsoft.Extensions.DependencyInjection;
using SharpCompress.Archives.Zip;

namespace LANCommander.Server.Services.Importers;

public class ImportContext<TRecord>(
    ServiceProvider serviceProvider,
    ZipArchive archive) : IDisposable
{
    public TRecord Record { get; private set; }
    public StorageLocation ArchiveStorageLocation { get; }
    public ZipArchive Archive { get; } = archive;
    
    public GameImporter<TRecord> Games { get; private set; }
    public RedistributableImporter<TRecord> Redistributables { get; private set; }
    public ServerImporter<TRecord> Servers { get; private set; }
    
    public ActionImporter<TRecord> Actions { get; private set; }
    public ArchiveImporter<TRecord> Archives { get; private set; }
    public CollectionImporter<TRecord> Collections { get; private set; }
    public CustomFieldImporter<TRecord> CustomFields { get; private set; }
    public DeveloperImporter<TRecord> Developers { get; private set; }
    public EngineImporter<TRecord> Engines { get; private set; }
    public GenreImporter<TRecord> Genres { get; private set; }
    public KeyImporter<TRecord> Keys { get; private set; }
    public MediaImporter<TRecord> Media { get; private set; }
    public MultiplayerModeImporter<TRecord> MultiplayerModes { get; private set; }
    public PlatformImporter<TRecord> Platforms { get; private set; }
    public PlaySessionImporter<TRecord> PlaySessions { get; private set; }
    public PublisherImporter<TRecord> Publishers { get; private set; }
    public SaveImporter<TRecord> Saves { get; private set; }
    public SavePathImporter<TRecord> SavePaths { get; private set; }
    public ScriptImporter<TRecord> Scripts { get; private set; }
    public ServerConsoleImporter<TRecord> ServerConsoles { get; private set; }
    public ServerHttpPathImporter<TRecord> ServerHttpPaths { get; private set; }
    public TagImporter<TRecord> Tags { get; private set; }

    public int Remaining => _queue.Count;
    public int Processed => _processed.Count;
    public int Total => _queue.Count + _processed.Count + Errored.Count;

    private List<object> _queue { get; } = new();
    private List<object> _processed { get; } = new();
    public Dictionary<object, string> Errored { get; } = new();

    public EventHandler<object> OnRecordAdded;
    public EventHandler<object> OnRecordProcessed;
    public EventHandler<object> OnRecordError;
    
    public void Initialize()
    {
        Actions = new ActionImporter<TRecord>(serviceProvider, this);
        Archives = new ArchiveImporter<TRecord>(serviceProvider, this);
        Collections = new CollectionImporter<TRecord>(serviceProvider, this);
        CustomFields = new CustomFieldImporter<TRecord>(serviceProvider, this);
        Developers = new DeveloperImporter<TRecord>(serviceProvider, this);
        Engines = new EngineImporter<TRecord>(serviceProvider, this);
        Genres = new GenreImporter<TRecord>(serviceProvider, this);
        Keys = new KeyImporter<TRecord>(serviceProvider, this);
        Media = new MediaImporter<TRecord>(serviceProvider, this);
        MultiplayerModes = new MultiplayerModeImporter<TRecord>(serviceProvider, this);
        Platforms = new PlatformImporter<TRecord>(serviceProvider, this);
        PlaySessions = new PlaySessionImporter<TRecord>(serviceProvider, this);
        Publishers = new PublisherImporter<TRecord>(serviceProvider, this);
        Saves = new SaveImporter<TRecord>(serviceProvider, this);
        SavePaths = new SavePathImporter<TRecord>(serviceProvider, this);
        Scripts = new ScriptImporter<TRecord>(serviceProvider, this);
        ServerConsoles = new ServerConsoleImporter<TRecord>(serviceProvider, this);
        ServerHttpPaths = new ServerHttpPathImporter<TRecord>(serviceProvider, this);
        Tags = new TagImporter<TRecord>(serviceProvider, this);
        
        Games = new GameImporter<TRecord>(serviceProvider, this);
        Redistributables = new RedistributableImporter<TRecord>(serviceProvider, this);
        Servers = new ServerImporter<TRecord>(serviceProvider, this);
    }

    public void Use(TRecord record)
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
    
    public void Dispose()
    {
        archive.Dispose();
    }
}