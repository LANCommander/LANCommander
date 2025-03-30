using LANCommander.Server.Data.Models;
using Microsoft.Extensions.DependencyInjection;
using SharpCompress.Archives.Zip;

namespace LANCommander.Server.Services.Importers;

public class ImportContext<TRecord>(
    ServiceProvider serviceProvider,
    ZipArchive archive,
    TRecord record) : IDisposable
{
    public TRecord Record { get; } = record;
    public StorageLocation ArchiveStorageLocation { get; }
    public ZipArchive Archive { get; } = archive;
    
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
    public PublisherImporter<TRecord> Publishers { get; private set; }
    public SaveImporter<TRecord> Saves { get; private set; }
    public SavePathImporter<TRecord> SavePaths { get; private set; }
    public ScriptImporter<TRecord> Scripts { get; private set; }
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
    
    public void Initialize(ZipArchive archive, TRecord record)
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
        Publishers = new PublisherImporter<TRecord>(serviceProvider, this);
        Saves = new SaveImporter<TRecord>(serviceProvider, this);
        SavePaths = new SavePathImporter<TRecord>(serviceProvider, this);
        Scripts = new ScriptImporter<TRecord>(serviceProvider, this);
        Tags = new TagImporter<TRecord>(serviceProvider, this);
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
            if (record is SDK.Models.Action action)
                await ImportRecordAsync(action, Actions);
            else if (record is SDK.Models.Archive archive)
                await ImportRecordAsync(archive, Archives);
            else if (record is SDK.Models.Collection collection)
                await ImportRecordAsync(collection, Collections);
            else if (record is SDK.Models.GameCustomField customField)
                await ImportRecordAsync(customField, CustomFields);
            else if (record is SDK.Models.Company company)
            {
                await ImportRecordAsync(company, Developers);
                await ImportRecordAsync(company, Publishers);
            }
            else if (record is SDK.Models.Engine engine)
                await ImportRecordAsync(engine, Engines);
            else if (record is SDK.Models.Genre genre)
                await ImportRecordAsync(genre, Genres);
            else if (record is SDK.Models.Key key)
                await ImportRecordAsync(key, Keys);
            else if (record is SDK.Models.Media media)
                await ImportRecordAsync(media, Media);
            else if (record is SDK.Models.MultiplayerMode multiplayerMode)
                await ImportRecordAsync(multiplayerMode, MultiplayerModes);
            else if (record is SDK.Models.Platform platform)
                await ImportRecordAsync(platform, Platforms);
            else if (record is SDK.Models.GameSave gameSave)
                await ImportRecordAsync(gameSave, Saves);
            else if (record is SDK.Models.SavePath savePath)
                await ImportRecordAsync(savePath, SavePaths);
            else if (record is SDK.Models.Script script)
                await ImportRecordAsync(script, Scripts);
            else if (record is SDK.Models.Tag tag)
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