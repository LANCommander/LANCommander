using LANCommander.Launcher.Services.Import.Importers;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Launcher.Services.Import;

public class ImportContext
{
    public IImportItemInfo CurrentItem { get; set; }
    public int Processed => _queue.Count(qi => qi.Processed);
    public int Total => _queue.Count;
    
    public EventHandler OnImportStarted { get; set; }
    public EventHandler<ImportStatusUpdate> OnImportProgress { get; set; }
    public EventHandler OnImportCompleted { get; set; }
    
    private List<IImportItemInfo> _queue { get; set; } = new();
    
    #region Importers

    private CollectionImporter _collections;
    private DeveloperImporter _developers;
    private EngineImporter _engines;
    private GameImporter _games;
    private GenreImporter _genres;
    private MultiplayerModeImporter _multiplayerModes;
    private PlatformImporter _platforms;
    private PublisherImporter _publishers;
    private TagImporter _tags;
    #endregion

    public ImportContext(IServiceProvider serviceProvider)
    {
        _collections = serviceProvider.GetRequiredService<CollectionImporter>();
        _developers = serviceProvider.GetRequiredService<DeveloperImporter>();
        _engines = serviceProvider.GetRequiredService<EngineImporter>();
        _games = serviceProvider.GetRequiredService<GameImporter>();
        _genres = serviceProvider.GetRequiredService<GenreImporter>();
        _multiplayerModes = serviceProvider.GetRequiredService<MultiplayerModeImporter>();
        _platforms = serviceProvider.GetRequiredService<PlatformImporter>();
        _publishers = serviceProvider.GetRequiredService<PublisherImporter>();
        _tags = serviceProvider.GetRequiredService<TagImporter>();
    }

    public async Task AddAsync(Game game)
    {
        foreach (var collection in game.Collections)
            if (!InQueue(collection, _collections) && await _collections.CanImportAsync(collection))
                _queue.Add(await _collections.GetImportInfoAsync(collection));
        
        foreach (var developer in game.Developers)
            if (!InQueue(developer, _developers) && await _developers.CanImportAsync(developer))
                _queue.Add(await _developers.GetImportInfoAsync(developer));
        
        if (!InQueue(game.Engine, _engines) && await _engines.CanImportAsync(game.Engine))
            _queue.Add(await _engines.GetImportInfoAsync(game.Engine));
        
        foreach (var genre in game.Genres)
            if (!InQueue(genre, _genres) && await _genres.CanImportAsync(genre))
                _queue.Add(await _genres.GetImportInfoAsync(genre));
        
        foreach (var multiplayerMode in game.MultiplayerModes)
            if (!InQueue(multiplayerMode, _multiplayerModes) && await _multiplayerModes.CanImportAsync(multiplayerMode))
                _queue.Add(await _multiplayerModes.GetImportInfoAsync(multiplayerMode));
        
        foreach (var platform in game.Platforms)
            if (!InQueue(platform, _platforms) && await _platforms.CanImportAsync(platform))
                _queue.Add(await _platforms.GetImportInfoAsync(platform));
        
        foreach (var publisher in game.Publishers)
            if (!InQueue(publisher, _publishers) && await _publishers.CanImportAsync(publisher))
                _queue.Add(await _publishers.GetImportInfoAsync(publisher));
        
        foreach (var tag in game.Tags)
            if (!InQueue(tag, _tags) && await _tags.CanImportAsync(tag))
                _queue.Add(await _tags.GetImportInfoAsync(tag));
        
        if (!InQueue(game, _games) && await _games.CanImportAsync(game))
            _queue.Add(await _games.GetImportInfoAsync(game));
    }

    private bool InQueue<TRecord, TEntity>(TRecord record, BaseImporter<TRecord, TEntity> importer)
        where TRecord : class =>
        _queue.Any(qi => qi.Key == importer.GetKey(record));

    public async Task ImportQueueAsync()
    {
        OnImportStarted?.Invoke(this, EventArgs.Empty);
        
        // Import metadata before games
        _queue = _queue
            .OrderBy(qi => qi.Type == nameof(Game))
            .ThenBy(qi => qi.Type)
            .ThenBy(qi => qi.Name)
            .ToList();
        
        foreach (var queueItem in _queue)
        {
            CurrentItem = queueItem;
            
            OnImportProgress?.Invoke(this, new ImportStatusUpdate
            {
                CurrentItem = CurrentItem,
                Index = Processed,
                Total = Total,
            });
            
            switch (queueItem.Type)
            {
                case nameof(Collection):
                    await _collections.ImportAsync(queueItem);
                    break;
                
                case "Developer":
                    await _developers.ImportAsync(queueItem);
                    break;
                
                case nameof(Engine):
                    await _engines.ImportAsync(queueItem);
                    break;
                
                case nameof(Game):
                    await _games.ImportAsync(queueItem);
                    break;
                
                case nameof(Genre):
                    await _genres.ImportAsync(queueItem);
                    break;
                
                case nameof(MultiplayerMode):
                    await _multiplayerModes.ImportAsync(queueItem);
                    break;
                
                case nameof(Platform):
                    await _platforms.ImportAsync(queueItem);
                    break;
                
                case "Publisher":
                    await _publishers.ImportAsync(queueItem);
                    break;
                
                case nameof(Tag):
                    await _tags.ImportAsync(queueItem);
                    break;
            }
        }
        
        OnImportCompleted?.Invoke(this, EventArgs.Empty);
    }
}