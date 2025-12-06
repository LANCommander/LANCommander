using LANCommander.Launcher.Services.Import.Importers;
using LANCommander.SDK;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Import;

public class ImportContext
{
    public int Processed;
    public int Total;

    public AsyncEventHandler<ImportStatusUpdate> OnImportStarted { get; set; } = new();
    public AsyncEventHandler<ImportStatusUpdate> OnImportComplete { get; set; } = new();
    public AsyncEventHandler<ImportStatusUpdate> OnImportError { get; set; } = new();
    public AsyncEventHandler<ImportStatusUpdate> OnImportStatusUpdate { get; set; } = new();
    
    private Queue<IImportItemInfo> Queue { get; } = new();
    
    #region Importers
    private readonly CollectionImporter _collections;
    private readonly DeveloperImporter _developers;
    private readonly EngineImporter _engines;
    private readonly GameImporter _games;
    private readonly GenreImporter _genres;
    private readonly MediaImporter _media;
    private readonly MultiplayerModeImporter _multiplayerModes;
    private readonly PlatformImporter _platforms;
    private readonly PublisherImporter _publishers;
    private readonly TagImporter _tags;
    #endregion
    
    private readonly ILogger<ImportContext> _logger;

    public ImportContext(IServiceProvider serviceProvider)
    {
        _collections = serviceProvider.GetRequiredService<CollectionImporter>();
        _developers = serviceProvider.GetRequiredService<DeveloperImporter>();
        _engines = serviceProvider.GetRequiredService<EngineImporter>();
        _games = serviceProvider.GetRequiredService<GameImporter>();
        _genres = serviceProvider.GetRequiredService<GenreImporter>();
        _media = serviceProvider.GetRequiredService<MediaImporter>();
        _multiplayerModes = serviceProvider.GetRequiredService<MultiplayerModeImporter>();
        _platforms = serviceProvider.GetRequiredService<PlatformImporter>();
        _publishers = serviceProvider.GetRequiredService<PublisherImporter>();
        _tags = serviceProvider.GetRequiredService<TagImporter>();
        
        _logger = serviceProvider.GetRequiredService<ILogger<ImportContext>>();
    }

    public async Task AddAsync(Game game)
    {
        await AddAsync(game, game.Collections, _collections);
        await AddAsync(game, game.Developers, _developers);
        await AddAsync(game, game.Engine, _engines);
        await AddAsync(game, game.Genres, _genres);
        await AddAsync(game, game.Media, _media);
        await AddAsync(game, game.MultiplayerModes, _multiplayerModes);
        await AddAsync(game, game.Platforms, _platforms);
        await AddAsync(game, game.Publishers, _publishers);
        await AddAsync(game, game.Tags, _tags);
        await AddAsync(game, game, _games);
    }

    private async Task AddAsync<TRecord>(BaseManifest manifest, IEnumerable<TRecord> records, BaseImporter<TRecord> importer)
        where TRecord : class
    {
        foreach (var record in records)
            await AddAsync(manifest, record, importer);
    }

    private async Task AddAsync<TRecord>(BaseManifest manifest, TRecord? record, BaseImporter<TRecord> importer)
        where TRecord : class
    {
        if (record != null && !InQueue(record, importer) && await importer.CanImportAsync(record))
        {
            var importInfo = await importer.GetImportInfoAsync(record, manifest);

            importInfo.Key = importer.GetKey(record);

            Queue.Enqueue(importInfo);
        }
    }

    internal bool InQueue<TRecord>(TRecord record, BaseImporter<TRecord> importer)
        where TRecord : class =>
        Queue.Any(qi => qi.Key == importer.GetKey(record));

    public async Task ImportQueueAsync()
    {
        Processed = 0;
        Total = Queue.Count;

        await OnImportStarted?.InvokeAsync(new ImportStatusUpdate
        {
            Index = Processed,
            Total = Total,
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
                Processed++;
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
            Index = Total,
            Total = Total,
        })!;
    }

    private async Task<bool> TryImportAsync(IImportItemInfo queueItem)
    {
        try
        {
            switch (queueItem.Type)
            {
                case nameof(Collection):
                    return await _collections.ImportAsync(queueItem);

                case "Developer":
                    return await _developers.ImportAsync(queueItem);

                case nameof(Engine):
                    return await _engines.ImportAsync(queueItem);

                case nameof(Game):
                    return await _games.ImportAsync(queueItem);

                case nameof(Genre):
                    return await _genres.ImportAsync(queueItem);

                case nameof(Media):
                    return await _media.ImportAsync(queueItem);

                case nameof(MultiplayerMode):
                    return await _multiplayerModes.ImportAsync(queueItem);

                case nameof(Platform):
                    return await _platforms.ImportAsync(queueItem);

                case "Publisher":
                    return await _publishers.ImportAsync(queueItem);

                case nameof(Tag):
                    return await _tags.ImportAsync(queueItem);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing record {RecordName}", queueItem.Name);

            await OnImportError?.InvokeAsync(new ImportStatusUpdate
            {
                CurrentItem = queueItem,
                Index = Processed,
                Total = Total,
                Error = ex.Message,
            })!;
        }

        return false;
    }
}