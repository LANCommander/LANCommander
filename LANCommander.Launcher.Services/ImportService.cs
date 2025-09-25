using LANCommander.Launcher.Data;
using LANCommander.Launcher.Models;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using LANCommander.Launcher.Services.Extensions;
using LANCommander.SDK.Models;
using BaseModel = LANCommander.Launcher.Data.Models.BaseModel;
using Collection = LANCommander.Launcher.Data.Models.Collection;
using Company = LANCommander.Launcher.Data.Models.Company;
using Engine = LANCommander.Launcher.Data.Models.Engine;
using Game = LANCommander.Launcher.Data.Models.Game;
using Genre = LANCommander.Launcher.Data.Models.Genre;
using Media = LANCommander.Launcher.Data.Models.Media;
using MultiplayerMode = LANCommander.Launcher.Data.Models.MultiplayerMode;
using Platform = LANCommander.Launcher.Data.Models.Platform;
using PlaySession = LANCommander.Launcher.Data.Models.PlaySession;
using Settings = LANCommander.Launcher.Models.Settings;
using Tag = LANCommander.Launcher.Data.Models.Tag;

namespace LANCommander.Launcher.Services
{
    public class ImportService(
        ILogger<ImportService> logger,
        DatabaseContext databaseContext,
        LibraryService libraryService,
        MediaService mediaService,
        EngineService engineService,
        GameService gameService,
        MessageBusService messageBusService,
        SDK.Client client) : BaseService(logger)
    {
        private ImportProgress _importProgress = new();
        public ImportProgress Progress => _importProgress;

        public delegate Task OnImportUpdatedHandler(ImportStatusUpdate update);
        public event OnImportUpdatedHandler OnImportUpdated;
        
        public delegate Task OnImportCompleteHandler();
        public event OnImportCompleteHandler OnImportComplete;

        public delegate Task OnImportFailedHandler(Exception ex);
        public event OnImportFailedHandler OnImportFailed;

        private IEnumerable<Collection> Collections;
        private IEnumerable<Company> Companies;
        private IEnumerable<Engine> Engines;
        private IEnumerable<Genre> Genres;
        private IEnumerable<Platform> Platforms;
        private IEnumerable<Tag> Tags;
        private IEnumerable<MultiplayerMode> MultiplayerModes;

        private Settings Settings = SettingService.GetSettings();

        public void ImportHasCompleted()
        {
            OnImportComplete?.Invoke();
        }

        public async Task ImportAsync()
        {
            try
            {
                await ImportLibraryAsync();

                OnImportComplete?.Invoke();
            }
            catch (Exception ex)
            {
                OnImportFailed?.Invoke(ex);
            }
        }
        
        public async Task<Game> ImportGameAsync(Guid id)
        {
            var game = await client.Games.GetAsync(id);
            
            return await ImportGameAsync(game);
        }

        public async Task<Game> ImportGameAsync(SDK.Models.Game game)
        {
            using (var op = Logger.BeginOperation("Importing game {GameTitle}", game.Title))
            {
                try
                {
                    var existing = false;
                    var localGame = await gameService.GetAsync(game.Id);

                    if (localGame == null)
                        localGame = new Game()
                        {
                            Id = game.Id,
                        };
                    else
                        existing = true;

                    if (localGame.ImportedOn > game.UpdatedOn)
                    {
                        Logger.LogInformation($"Skipping {game.Title}, no new information to grab");
                        return localGame;
                    }
                    
                    localGame.Title = game.Title;
                    localGame.SortTitle = String.IsNullOrWhiteSpace(game.SortTitle) ? String.Empty : game.SortTitle;
                    localGame.Description = String.IsNullOrWhiteSpace(game.Description) ? String.Empty : game.Description;
                    localGame.Notes = String.IsNullOrWhiteSpace(game.Notes) ? String.Empty : game.Notes;
                    localGame.ReleasedOn = game.ReleasedOn;
                    localGame.ImportedOn = DateTime.UtcNow;
                    localGame.Type = (Data.Enums.GameType)(int)game.Type;
                    localGame.Singleplayer = game.Singleplayer;
                    
                    if (!existing)
                        localGame = await gameService.AddAsync(localGame);

                    if (game.BaseGameId != Guid.Empty && localGame.BaseGameId != game.BaseGameId)
                    {
                        var baseGame = await gameService.GetAsync(game.BaseGameId);

                        if (baseGame == null)
                        {
                            await ImportGameAsync(game.BaseGameId);

                            localGame.BaseGame = await gameService.GetAsync(game.BaseGameId);
                        }
                        else
                        {
                            localGame.BaseGame = baseGame;
                        }
                    }

                    var childGamesIds = game.DependentGames.ToArray();
                    foreach (var childGameId in childGamesIds)
                    {
                        await ImportGameAsync(childGameId);
                    }
                    
                    #region Update Game Engine

                    if (game.Engine == null && localGame.Engine != null)
                    {
                        localGame.Engine = null;
                        localGame.EngineId = null;
                    }
                    else if (game.Engine != null)
                    {
                        var engine = await engineService.GetAsync(game.Engine.Id);

                        if (engine != null)
                        {
                            localGame.Engine = engine;
                            localGame.EngineId = engine.Id;
                        }
                    }

                    #endregion

                    await databaseContext.BulkImport<Collection, SDK.Models.Collection>()
                        .SetTarget(localGame.Collections)
                        .UseSource(game.Collections)
                        .AsBatch()
                        .Assign((t, s) =>
                        {
                            t.Name = s.Name;
                        })
                        .AssignRelationships((t, s) =>
                        {
                            // Ensure the game is tracked before checking relationships
                            if (databaseContext.Entry(localGame).State == EntityState.Detached)
                                databaseContext.Attach(localGame);
                            
                            // Check if the relationship already exists
                            var linked = databaseContext.Entry(t)
                                .Collection(x => x.Games)
                                .Query()
                                .Any(g => g.Id == localGame.Id);
                            
                            if (!linked)
                                t.Games.Add(localGame);
                        })
                        .ImportAsync();

                    await databaseContext.BulkImport<Genre, SDK.Models.Genre>()
                        .SetTarget(localGame.Genres)
                        .UseSource(game.Genres)
                        .AsBatch()
                        .Assign((t, s) =>
                        {
                            t.Name = s.Name;
                        })
                        .AssignRelationships((t, s) =>
                        {
                            // Ensure the game is tracked before checking relationships
                            if (databaseContext.Entry(localGame).State == EntityState.Detached)
                                databaseContext.Attach(localGame);
                            
                            // Check if the relationship already exists
                            var linked = databaseContext.Entry(t)
                                .Collection(x => x.Games)
                                .Query()
                                .Any(g => g.Id == localGame.Id);
                            
                            if (!linked)
                                t.Games.Add(localGame);
                        })
                        .ImportAsync();

                    await databaseContext.BulkImport<Company, SDK.Models.Company>()
                        .SetTarget(localGame.Publishers)
                        .UseSource(game.Publishers)
                        .AsBatch()
                        .Assign((t, s) =>
                        {
                            t.Name = s.Name;
                        })
                        .AssignRelationships((t, s) =>
                        {
                            // Ensure the game is tracked before checking relationships
                            if (databaseContext.Entry(localGame).State == EntityState.Detached)
                                databaseContext.Attach(localGame);
                            
                            // Check if the relationship already exists
                            var linked = databaseContext.Entry(t)
                                .Collection(x => x.PublishedGames)
                                .Query()
                                .Any(g => g.Id == localGame.Id);
                            
                            if (!linked)
                                t.PublishedGames.Add(localGame);
                        })
                        .ImportAsync();

                    await databaseContext.BulkImport<Company, SDK.Models.Company>()
                        .SetTarget(localGame.Developers)
                        .UseSource(game.Developers)
                        .AsBatch()
                        .Assign((t, s) =>
                        {
                            t.Name = s.Name;
                        })
                        .AssignRelationships((t, s) =>
                        {
                            // Ensure the game is tracked before checking relationships
                            if (databaseContext.Entry(localGame).State == EntityState.Detached)
                                databaseContext.Attach(localGame);
                            
                            // Check if the relationship already exists
                            var linked = databaseContext.Entry(t)
                                .Collection(x => x.DevelopedGames)
                                .Query()
                                .Any(g => g.Id == localGame.Id);
                            
                            if (!linked)
                                t.DevelopedGames.Add(localGame);
                        })
                        .ImportAsync();

                    await databaseContext.BulkImport<Tag, SDK.Models.Tag>()
                        .SetTarget(localGame.Tags)
                        .UseSource(game.Tags)
                        .AsBatch()
                        .Assign((t, s) =>
                        {
                            t.Name = s.Name;
                        })
                        .AssignRelationships((t, s) =>
                        {
                            // Ensure the game is tracked before checking relationships
                            if (databaseContext.Entry(localGame).State == EntityState.Detached)
                                databaseContext.Attach(localGame);
                            
                            // Check if the relationship already exists
                            var linked = databaseContext.Entry(t)
                                .Collection(x => x.Games)
                                .Query()
                                .Any(g => g.Id == localGame.Id);
                            
                            if (!linked)
                                t.Games.Add(localGame);
                        })
                        .ImportAsync();

                    await databaseContext.BulkImport<MultiplayerMode, SDK.Models.MultiplayerMode>()
                        .SetTarget(localGame.MultiplayerModes)
                        .UseSource(game.MultiplayerModes)
                        .AsBatch()
                        .Assign((t, s) =>
                        {
                            t.Game = localGame;
                            t.Description = s.Description;
                            t.MinPlayers = s.MinPlayers;
                            t.MaxPlayers = s.MaxPlayers;
                            t.Spectators = s.Spectators;
                            t.Type = s.Type;
                            t.NetworkProtocol = s.NetworkProtocol;
                        })
                        .AssignRelationships((t, s) =>
                        {
                            // Ensure the game is tracked before checking relationships
                            if (databaseContext.Entry(localGame).State == EntityState.Detached)
                                databaseContext.Attach(localGame);

                            var linked = t.GameId == localGame.Id;
                            
                            if (!linked)
                                t.Game = localGame;
                        })
                        .ImportAsync();

                    await databaseContext.BulkImport<Platform, SDK.Models.Platform>()
                        .SetTarget(localGame.Platforms)
                        .UseSource(game.Platforms)
                        .AsBatch()
                        .Assign((t, s) =>
                        {
                            t.Name = s.Name;
                            t.Games.Add(localGame);
                        })
                        .AssignRelationships((t, s) =>
                        {
                            // Ensure the game is tracked before checking relationships
                            if (databaseContext.Entry(localGame).State == EntityState.Detached)
                                databaseContext.Attach(localGame);
                            
                            // Check if the relationship already exists
                            var linked = databaseContext.Entry(t)
                                .Collection(x => x.Games)
                                .Query()
                                .Any(g => g.Id == localGame.Id);
                            
                            if (!linked)
                                t.Games.Add(localGame);
                        })
                        .ImportAsync();

                    /*await databaseContext.BulkImport<PlaySession, SDK.Models.PlaySession>()
                        .SetTarget(localGame.PlaySessions)
                        .UseSource(game.PlaySessions)
                        .Include(p => p.Game)
                        .Assign((t, s) =>
                        {
                            t.Game = localGame;
                            t.Start = s.Start;
                            t.End = s.End;
                            t.UserId = s.UserId;
                        })
                        .AsNoRemove()
                        .ImportAsync();*/
                    
                    var importedMedia = await databaseContext.BulkImport<Media, SDK.Models.Media>()
                        .SetTarget(localGame.Media)
                        .UseSource(game.Media)
                        .AsBatch()
                        .Assign((t, s) =>
                        {
                            t.Name = s.Name;
                            t.Crc32 = s.Crc32;
                            t.Type = s.Type;
                            t.FileId = s.FileId;
                            t.MimeType = s.MimeType;
                            t.SourceUrl = s.SourceUrl;
                        })
                        .AssignRelationships((t, s) =>
                        {
                            // Ensure the game is tracked before checking relationships
                            if (databaseContext.Entry(localGame).State == EntityState.Detached)
                                databaseContext.Attach(localGame);

                            var linked = t.GameId == localGame.Id;
                            
                            if (!linked)
                                t.Game = localGame;
                        })
                        .AsNoRemove()
                        .ImportAsync();

                    foreach (var media in importedMedia)
                    {
                        var localPath = MediaService.GetImagePath(media);

                        if (!File.Exists(localPath) && media.Type != SDK.Enums.MediaType.Manual)
                        {
                            await client.Media.DownloadAsync(new SDK.Models.Media
                            {
                                Id = media.Id,
                                FileId = media.FileId
                            }, localPath);

                            messageBusService.MediaChanged(media);
                        }
                    }

                    #region Check Installation Status

                    foreach (var installDirectory in Settings.Games.InstallDirectories)
                    {
                        var gameDirectory = await client.Games.GetInstallDirectory(game, installDirectory);

                        if (Directory.Exists(gameDirectory))
                        {
                            var manifestLocation = ManifestHelper.GetPath(gameDirectory, game.Id);

                            if (File.Exists(manifestLocation))
                            {
                                var manifest = await ManifestHelper.ReadAsync<SDK.GameManifest>(gameDirectory, game.Id);

                                localGame.Installed = true;
                                localGame.InstalledOn = DateTime.Now;
                                localGame.InstallDirectory = gameDirectory;
                                localGame.InstalledVersion = manifest.Version;
                            }
                        }
                    }

                    #endregion

                    var playSessions = await client.PlaySessions.GetAsync(localGame.Id);

                    if (playSessions != null)
                    {
                        await databaseContext.BulkImport<PlaySession, SDK.Models.PlaySession>()
                            .SetTarget(localGame.PlaySessions)
                            .UseSource(playSessions)
                            .Include(p => p.Game)
                            .Assign((t, s) =>
                            {
                                t.Start = s.Start;
                                t.End = s.End;
                                t.UserId = s.UserId;
                            })
                            .AssignRelationships((t, s) =>
                            {
                                // Ensure the game is tracked before checking relationships
                                if (databaseContext.Entry(localGame).State == EntityState.Detached)
                                    databaseContext.Attach(localGame);

                                var linked = t.GameId == localGame.Id;
                            
                                if (!linked)
                                    t.Game = localGame;
                            })
                            .AsNoRemove()
                            .ImportAsync();
                    }
                    
                    // Save all pending changes from batch operations
                    await databaseContext.SaveChangesAsync();
                    
                    localGame = await gameService.UpdateAsync(localGame);

                    return localGame;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Could not import game {GameTitle}", game.Title);

                    return null;
                }
                finally
                {
                    op.Complete();                    
                }
            }
        }

        public async Task ImportLibraryAsync()
        {
            var remoteLibrary = await client.Library.GetAsync();
            
            try
            {
                await ImportLibraryAsync(remoteLibrary);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Could not import library!");
                OnImportFailed?.Invoke(ex);
            }
        }

        public async Task ImportLibraryAsync(IEnumerable<SDK.Models.EntityReference> games)
        {
            Logger?.LogInformation("Starting library import");

            _importProgress.IsImporting = true;
            _importProgress.Index = 1;
            _importProgress.Total = games.Count();

            foreach (var game in games)
            {
                using var transaction = await databaseContext.Database.BeginTransactionAsync();
                
                try
                {
                    var remoteGame = await client.Games.GetAsync(game.Id);

                    _importProgress.CurrentItem = new ImportItem(game.Id, game.Name);
                    
                    if (OnImportUpdated != null)
                    {
                        await OnImportUpdated.Invoke(new ImportStatusUpdate 
                        {
                            Index = _importProgress.Index,
                            Total = _importProgress.Total,
                            CurrentItem = _importProgress.CurrentItem
                        });
                    }

                    if (game != null && remoteGame != null)
                    {
                        var importedGame = await ImportGameAsync(remoteGame);

                        await libraryService.AddToLibraryAsync(importedGame);
                    }
                    
                    await databaseContext.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    
                    Logger?.LogError(ex, "Could not import game {GameTitle}", game.Name);
                }
                finally
                {
                    _importProgress.Index++;
                }
            }

            _importProgress.IsImporting = false;
            if (OnImportComplete != null)
                await OnImportComplete.Invoke();
            
            Logger?.LogInformation("Importing games completed");
        }

        public async Task ImportGamesAsync(params Guid[] ids)
        {
            var games = await client.Library.GetAsync();
            var toImport = new List<EntityReference>();

            foreach (var id in ids)
            {
                var libraryGame = games.FirstOrDefault(g => g.Id == id);

                if (libraryGame == null)
                {
                    Logger?.LogInformation("Game with ID {GameId} not currently in library", id);
                    continue;
                }

                toImport.Add(libraryGame);
            }

            await ImportLibraryAsync(toImport);
        }

        public async Task ImportRedistributables()
        {

        }

        public async Task<Media> ImportMediaAsync(Guid importMediaId, Guid? gameId = null)
        {
            SDK.Models.Game game = null;

            if (gameId.HasValue)
                game = await client.Games.GetAsync(gameId.Value);

            var media = await client.Media.GetAsync(importMediaId);

            return await ImportMediaAsync(media, game);
        }

        public async Task<Media> ImportMediaAsync(SDK.Models.Media importMedia, SDK.Models.Game game = null)
        {
            var media = await mediaService.GetAsync(importMedia.Id);

            if (media == null)
                media = new Media();

            media.FileId = importMedia.FileId;
            media.Type = importMedia.Type;
            media.SourceUrl = importMedia.SourceUrl;
            media.MimeType = importMedia.MimeType;
            media.Crc32 = importMedia.Crc32;
            media.Name = importMedia.Name ?? String.Empty;
            media.GameId = game?.Id ?? Guid.Empty;

            if (media.Id == Guid.Empty)
            {
                media.Id = importMedia.Id;

                await mediaService.AddAsync(media);
            }
            else
                await mediaService.UpdateAsync(media);

            var mediaStoragePath = MediaService.GetStoragePath();
            var localPath = MediaService.GetImagePath(media);

            if (!File.Exists(localPath) && media.Type != SDK.Enums.MediaType.Manual)
            {
                await client.Media.DownloadAsync(new SDK.Models.Media
                {
                    Id = media.Id,
                    FileId = media.FileId
                }, localPath);

                messageBusService.MediaChanged(media);
            }

            return media;
        }
        
        

        // Could use something like automapper, but that's slow.
        public async Task<IEnumerable<T>> ImportBulk<T, U, V>(IEnumerable<U> importModels, V service, Func<T, U, T> additionalMapping, bool clean = true)
            where T : BaseModel
            where U : SDK.Models.KeyedModel
            where V : BaseDatabaseService<T>
        {
            // This could be handled better... DI?
            var models = await service.GetAsync();

            foreach (var importModel in importModels)
            {
                using (var transaction = databaseContext.Database.BeginTransaction())
                {
                    try
                    {
                        var model = models.FirstOrDefault(m => m.Id == importModel.Id);

                        if (model == null)
                            model = (T)Activator.CreateInstance(typeof(T));

                        model = additionalMapping.Invoke(model, importModel);

                        if (model.Id == Guid.Empty)
                        {
                            model.Id = importModel.Id;

                            model = await service.AddAsync(model);
                        }
                        else
                            model = await service.UpdateAsync(model);

                        transaction.Commit();
                    }
                    catch (DbUpdateException ex)
                    {
                        transaction.Rollback();

                        continue;
                    }
                    catch (Exception ex)
                    {

                    }
                }
            }

            if (clean)
            {
                foreach (var model in models.Where(m => !importModels.Any(im => im.Id == m.Id)))
                {
                    try
                    {
                        await service.DeleteAsync(model);
                    }
                    catch
                    {

                    }
                }
            }

            // Too slow?
            return await service.GetAsync();
        }

    }
}
