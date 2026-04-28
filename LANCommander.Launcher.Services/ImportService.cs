using LANCommander.Launcher.Models;
using Microsoft.Extensions.Logging;
using LANCommander.Launcher.Services.Import;
using LANCommander.Launcher.Services.Import.Factories;
using LANCommander.SDK;
using LANCommander.SDK.Services;

namespace LANCommander.Launcher.Services
{
    public class ImportService(
        ILogger<ImportService> logger,
        ImportContextFactory importContextFactory,
        GameClient gameClient,
        ToolClient toolClient,
        LibraryClient libraryClient,
        GameService gameService) : BaseService(logger)
    {
        private const int MaxConcurrentManifestFetches = 8;

        private ImportProgress _importProgress = new();
        public ImportProgress Progress => _importProgress;

        public AsyncEventHandler<ImportStatusUpdate> OnImportStarted { get; } = new();
        public AsyncEventHandler<ImportStatusUpdate> OnImportStatusUpdate { get; } = new();
        public AsyncEventHandler<ImportStatusUpdate> OnImportComplete { get; } = new();
        public AsyncEventHandler<ImportStatusUpdate> OnImportError { get; } = new();

        public async Task ImportAsync()
        {
            await ImportLibraryAsync();
        }

        public async Task ImportLibraryAsync()
        {
            var remoteLibrary = await libraryClient.GetAsync();

            Logger?.LogInformation("Starting library import");

            var importContext = importContextFactory.Create();

            importContext.OnImportStarted = OnImportStarted;
            importContext.OnImportComplete = OnImportComplete;
            importContext.OnImportError = OnImportError;
            importContext.OnImportStatusUpdate = OnImportStatusUpdate;

            // Pre-fetch all local import timestamps in a single query
            var gameIds = remoteLibrary.Select(g => g.Id);
            var importedOnMap = await gameService.GetImportedOnMapAsync(gameIds);

            // Filter to only games that need importing
            var gamesToImport = remoteLibrary.Where(game =>
            {
                if (importedOnMap.TryGetValue(game.Id, out var importedOn) && game.UpdatedOn <= importedOn)
                {
                    Logger?.LogDebug("Skipping unchanged game {GameId}", game.Id);
                    return false;
                }
                return true;
            }).ToList();

            Logger?.LogInformation("Importing {Count} games ({Skipped} skipped as unchanged)",
                gamesToImport.Count, remoteLibrary.Count() - gamesToImport.Count);

            // Fetch manifests concurrently
            var semaphore = new SemaphoreSlim(MaxConcurrentManifestFetches);
            var addLock = new object();

            var tasks = gamesToImport.Select(async game =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var manifest = await gameClient.GetManifestAsync(game.Id);

                    lock (addLock)
                    {
                        importContext.AddAsync(manifest).GetAwaiter().GetResult();
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "Could not add game with ID {GameId} to import queue", game.Id);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            await importContext.ImportQueueAsync();
            await importContext.DownloadPendingMediaAsync();
        }

        public async Task ImportGameAsync(Guid gameId)
        {
            var importContext = importContextFactory.Create();
            
            var manifest = await gameClient.GetManifestAsync(gameId);
            
            await importContext.AddAsync(manifest);
            await importContext.ImportQueueAsync();
        }

        public async Task ImportToolAsync(Guid toolId)
        {
            var importContext = importContextFactory.Create();
            
            var manifest = await toolClient.GetManifestAsync(toolId);
            
            await importContext.AddAsync(manifest);
            await importContext.ImportQueueAsync();
        }
    }
}
