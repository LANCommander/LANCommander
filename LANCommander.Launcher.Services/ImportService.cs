using LANCommander.Launcher.Models;
using Microsoft.Extensions.Logging;
using LANCommander.Launcher.Services.Import;
using LANCommander.Launcher.Services.Import.Factories;
using LANCommander.SDK.Services;
using Collection = LANCommander.Launcher.Data.Models.Collection;
using Company = LANCommander.Launcher.Data.Models.Company;
using Engine = LANCommander.Launcher.Data.Models.Engine;
using Genre = LANCommander.Launcher.Data.Models.Genre;
using MultiplayerMode = LANCommander.Launcher.Data.Models.MultiplayerMode;
using Platform = LANCommander.Launcher.Data.Models.Platform;
using Tag = LANCommander.Launcher.Data.Models.Tag;

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

        public EventHandler OnImportStarted;
        public EventHandler<ImportStatusUpdate> OnImportStatusUpdate;
        public EventHandler OnImportComplete;
        public EventHandler<Exception> OnImportError;

        private IEnumerable<Collection> Collections;
        private IEnumerable<Company> Companies;
        private IEnumerable<Engine> Engines;
        private IEnumerable<Genre> Genres;
        private IEnumerable<Platform> Platforms;
        private IEnumerable<Tag> Tags;
        private IEnumerable<MultiplayerMode> MultiplayerModes;

        public async Task ImportAsync()
        {
            try
            {
                await ImportLibraryAsync();

                OnImportComplete?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                OnImportError?.Invoke(this, ex);
            }
        }

        public async Task ImportLibraryAsync()
        {
            var remoteLibrary = await libraryClient.GetAsync();
            
            Logger?.LogInformation("Starting library import");

            var importContext = importContextFactory.Create();
            
            importContext.OnImportProgress += OnImportStatusUpdate;

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
    }
}
