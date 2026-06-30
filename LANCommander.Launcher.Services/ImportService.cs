using LANCommander.Launcher.Data;
using LANCommander.Launcher.Models;
using Microsoft.EntityFrameworkCore;
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
        PlaySessionClient playSessionClient,
        DatabaseContext dbContext,
        GameService gameService,
        AuthenticationService authenticationService) : BaseService(logger)
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

            // Make the local library membership match the remote library exactly
            await ReconcileLibraryMembershipAsync(remoteLibrary.Select(g => g.Id).ToList());

            // Sync play sessions for all library games
            await SyncPlaySessionsAsync(remoteLibrary.Select(g => g.Id));
        }

        internal async Task ReconcileLibraryMembershipAsync(IReadOnlyCollection<Guid> remoteGameIds)
        {
            // The library endpoint returns an empty list both when the library is genuinely
            // empty and when it fails server-side. Treating an empty result as authoritative
            // would wipe the entire local library on a transient error, so skip reconciliation
            // in that case.
            if (remoteGameIds.Count == 0)
            {
                Logger?.LogDebug("Skipping library reconciliation because the remote library returned no games");
                return;
            }

            var userId = authenticationService.GetUserId();

            var library = await dbContext.Libraries
                .Include(l => l.Games)
                .FirstOrDefaultAsync(l => l.UserId == userId);

            if (library == null)
                return;

            var remoteGameIdSet = remoteGameIds.ToHashSet();

            // Remove games that are no longer present in the remote library.
            var staleGames = library.Games
                .Where(g => !remoteGameIdSet.Contains(g.Id))
                .ToList();

            foreach (var staleGame in staleGames)
                library.Games.Remove(staleGame);

            // Add games that belong in the remote library and already exist locally but are
            // missing from the local library. The import above skips games whose cached records
            // are unchanged, so toggling "Enable User Libraries" off on the server (which makes
            // the endpoint return every game) would otherwise leave previously-dropped games
            // hidden from the library view.
            var localGameIds = library.Games
                .Select(g => g.Id)
                .ToHashSet();

            var missingGameIds = remoteGameIdSet
                .Where(id => !localGameIds.Contains(id))
                .ToList();

            var gamesToAdd = missingGameIds.Count == 0
                ? new List<Data.Models.Game>()
                : await dbContext.Games
                    .Where(g => missingGameIds.Contains(g.Id))
                    .ToListAsync();

            foreach (var game in gamesToAdd)
                library.Games.Add(game);

            if (staleGames.Count == 0 && gamesToAdd.Count == 0)
                return;

            await dbContext.SaveChangesAsync();

            if (staleGames.Count > 0)
                Logger?.LogInformation("Removed {Count} game(s) from the local library that are no longer present on the server", staleGames.Count);

            if (gamesToAdd.Count > 0)
                Logger?.LogInformation("Added {Count} game(s) to the local library that were already cached locally", gamesToAdd.Count);
        }

        public async Task ImportGameAsync(Guid gameId)
        {
            var importContext = importContextFactory.Create();

            var manifest = await gameClient.GetManifestAsync(gameId);

            await importContext.AddAsync(manifest);
            await importContext.ImportQueueAsync();
            await importContext.DownloadPendingMediaAsync();

            await SyncPlaySessionsAsync([gameId]);
        }

        private async Task SyncPlaySessionsAsync(IEnumerable<Guid> gameIds)
        {
            try
            {
                var localSessionIds = (await dbContext.Set<Data.Models.PlaySession>()
                    .Select(ps => ps.Id)
                    .ToListAsync())
                    .ToHashSet();

                foreach (var gameId in gameIds)
                {
                    try
                    {
                        var remoteSessions = await playSessionClient.GetAsync(gameId);

                        if (remoteSessions == null)
                            continue;

                        foreach (var remote in remoteSessions)
                        {
                            if (!localSessionIds.Contains(remote.Id))
                            {
                                dbContext.Set<Data.Models.PlaySession>().Add(new Data.Models.PlaySession
                                {
                                    Id = remote.Id,
                                    GameId = remote.GameId,
                                    UserId = remote.UserId,
                                    Start = remote.Start,
                                    End = remote.End,
                                    CreatedOn = remote.CreatedOn,
                                    UpdatedOn = remote.UpdatedOn,
                                });

                                localSessionIds.Add(remote.Id);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogError(ex, "Failed to sync play sessions for game {GameId}", gameId);
                    }
                }

                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Failed to sync play sessions");
            }
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
