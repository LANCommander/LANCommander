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
        LibraryClient libraryClient) : BaseService(logger)
    {
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

            foreach (var game in remoteLibrary)
            {
                try
                {
                    var manifest = await gameClient.GetManifestAsync(game.Id);

                    await importContext.AddAsync(manifest);
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "Could not add game with ID {GameId} to import queue", game.Id);
                }
            }

            await importContext.ImportQueueAsync();
        }

        public async Task ImportGameAsync(Guid gameId)
        {
            var importContext = importContextFactory.Create();
            
            var manifest = await gameClient.GetManifestAsync(gameId);
            
            await importContext.AddAsync(manifest);
            await importContext.ImportQueueAsync();
        }
    }
}
