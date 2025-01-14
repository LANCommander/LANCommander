using LANCommander.Launcher.Data;
using LANCommander.Launcher.Models;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using LANCommander.Launcher.Services.Extensions;
using LANCommander.SDK.Models;
using Microsoft.EntityFrameworkCore.Storage;
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
    public class ImportService : BaseService
    {
        private readonly MediaService MediaService;
        private readonly CollectionService CollectionService;
        private readonly CompanyService CompanyService;
        private readonly EngineService EngineService;
        private readonly GameService GameService;
        private readonly GenreService GenreService;
        private readonly PlatformService PlatformService;
        private readonly MultiplayerModeService MultiplayerModeService;
        private readonly RedistributableService RedistributableService;
        private readonly TagService TagService;
        private readonly MessageBusService MessageBusService;
        private readonly Settings Settings;
        private readonly DatabaseContext DatabaseContext;

        public delegate Task OnImportUpdatedHandler(ImportStatusUpdate update);
        public event OnImportUpdatedHandler OnImportUpdated;
        
        public delegate Task OnImportCompleteHandler();
        public event OnImportCompleteHandler OnImportComplete;

        public delegate void OnImportFailedHandler(Exception ex);
        public event OnImportFailedHandler OnImportFailed;

        private IEnumerable<Collection> Collections;
        private IEnumerable<Company> Companies;
        private IEnumerable<Engine> Engines;
        private IEnumerable<Genre> Genres;
        private IEnumerable<Platform> Platforms;
        private IEnumerable<Tag> Tags;
        private IEnumerable<MultiplayerMode> MultiplayerModes;

        public ImportService(
            SDK.Client client,
            ILogger<ImportService> logger,
            MediaService mediaService,
            CollectionService collectionService,
            CompanyService companyService,
            EngineService engineService,
            GameService gameService,
            GenreService genreService,
            PlatformService platformService,
            MultiplayerModeService multiplayerModeService,
            RedistributableService redistributableService,
            TagService tagService,
            MessageBusService messageBusService,
            DatabaseContext databaseContext) : base(client, logger)
        {
            MediaService = mediaService;
            CollectionService = collectionService;
            CompanyService = companyService;
            EngineService = engineService;
            GameService = gameService;
            GenreService = genreService;
            PlatformService = platformService;
            MultiplayerModeService = multiplayerModeService;
            RedistributableService = redistributableService;
            TagService = tagService;
            MessageBusService = messageBusService;
            DatabaseContext = databaseContext;

            Settings = SettingService.GetSettings();
        }

        public void ImportHasCompleted()
        {
            OnImportComplete?.Invoke();
        }

        public async Task ImportAsync()
        {
            try
            {
                await ImportGamesAsync();
                await ImportRedistributables();

                OnImportComplete?.Invoke();
            }
            catch (Exception ex)
            {
                OnImportFailed?.Invoke(ex);
            }
        }

        private async Task<ICollection<TModel>> ImportBulkAsync<TModel, TKeyedModel>(
            ICollection<TModel> target,
            IEnumerable<TKeyedModel> source,
            Action<TModel, TKeyedModel> updateAction)
            where TModel : BaseModel
            where TKeyedModel : IKeyedModel
        {
            foreach (var sourceItem in source)
            {
                var existingItem = await DatabaseContext.Set<TModel>().FirstOrDefaultAsync(i => i.Id == sourceItem.Id);

                if (existingItem != null)
                {
                    updateAction(existingItem, sourceItem);
                    
                    DatabaseContext.Update(existingItem);
                    
                    await DatabaseContext.SaveChangesAsync();
                }
                else
                {
                    // Add
                    var item = (TModel)Activator.CreateInstance(typeof(TModel));
                    
                    item.Id = sourceItem.Id;
                    
                    updateAction(item, sourceItem);

                    var result = await DatabaseContext.Set<TModel>().AddAsync(item);

                    await DatabaseContext.SaveChangesAsync();
                    
                    target.Add(result.Entity);
                }
            }
            
            var toRemove = target.Where(x => !source.Any(y => y.Id == x.Id)).ToList();

            foreach (var item in toRemove)
            {
                target.Remove(item);
            }

            return target;
        }

        private async Task ImportGameAsync(Guid id)
        {
            var game = await Client.Games.GetAsync(id);
            
            await ImportGameAsync(game);
        }

        private async Task ImportGameAsync(SDK.Models.Game game)
        {
            using (var op = Logger.BeginOperation("Importing game {GameTitle}", game.Title))
            {
                try
                {
                    var localGame = await GameService.Get(game.Id);

                    if (localGame == null)
                        localGame = new Game();

                    localGame.Title = game.Title;
                    localGame.SortTitle = game.SortTitle;
                    localGame.Description = game.Description;
                    localGame.Notes = game.Notes;
                    localGame.ReleasedOn = game.ReleasedOn;
                    localGame.Type = (Data.Enums.GameType)(int)game.Type;
                    localGame.Singleplayer = game.Singleplayer;

                    if (game.BaseGameId != Guid.Empty && localGame.BaseGameId != game.BaseGameId)
                    {
                        var baseGame = await GameService.Get(game.BaseGameId);

                        if (baseGame == null)
                        {
                            await ImportGameAsync(game.BaseGameId);

                            baseGame = await GameService.Get(game.BaseGameId);
                        }

                        localGame.BaseGameId = game.BaseGameId;
                    }

                    #region Update Game Engine
                    if (game.Engine == null && localGame.Engine != null)
                    {
                        localGame.Engine = null;
                        localGame.EngineId = null;
                    }
                    else if (game.Engine != null)
                    {
                        var engine = await EngineService.Get(game.Engine.Id);

                        if (engine != null)
                        {
                            localGame.Engine = engine;
                            localGame.EngineId = engine.Id;                            
                        }
                    }
                    #endregion

                    await DatabaseContext.BulkImport<Collection, SDK.Models.Collection>()
                        .SetTarget(localGame.Collections)
                        .UseSource(game.Collections)
                        .Include(c => c.Games)
                        .Assign((t, s) => t.Games.Add(localGame))
                        .ImportAsync();

                    await DatabaseContext.BulkImport<Genre, SDK.Models.Genre>()
                        .SetTarget(localGame.Genres)
                        .UseSource(game.Genres)
                        .Include(g => g.Games)
                        .Assign((t, s) => t.Games.Add(localGame))
                        .ImportAsync();
                    
                    await DatabaseContext.BulkImport<Company, SDK.Models.Company>()
                        .SetTarget(localGame.Publishers)
                        .UseSource(game.Publishers)
                        .Include(c => c.PublishedGames)
                        .Assign((t, s) => t.PublishedGames.Add(localGame))
                        .ImportAsync();
                    
                    await DatabaseContext.BulkImport<Company, SDK.Models.Company>()
                        .SetTarget(localGame.Developers)
                        .UseSource(game.Developers)
                        .Include(c => c.DevelopedGames)
                        .Assign((t, s) => t.DevelopedGames.Add(localGame))
                        .ImportAsync();
                    
                    await DatabaseContext.BulkImport<Tag, SDK.Models.Tag>()
                        .SetTarget(localGame.Tags)
                        .UseSource(game.Tags)
                        .Include(t => t.Games)
                        .Assign((t, s) => t.Games.Add(localGame))
                        .ImportAsync();
                    
                    await DatabaseContext.BulkImport<MultiplayerMode, SDK.Models.MultiplayerMode>()
                        .SetTarget(localGame.MultiplayerModes)
                        .UseSource(game.MultiplayerModes)
                        .Include(m => m.Game)
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
                        .ImportAsync();
                    
                    await DatabaseContext.BulkImport<Platform, SDK.Models.Platform>()
                        .SetTarget(localGame.Platforms)
                        .UseSource(game.Platforms)
                        .Include(p => p.Games)
                        .Assign((t, s) => t.Games.Add(localGame))
                        .ImportAsync();
                    
                    await DatabaseContext.BulkImport<PlaySession, SDK.Models.PlaySession>()
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
                        .ImportAsync();
                    
                    /*localGame.Collections = await ImportBulkAsync(localGame.Collections, game.Collections, (target, source) =>
                    {
                        DatabaseContext.Entry(target).Collection(t => t.Games).Load();
                        target.Name = source.Name;
                    });
                    
                    localGame.Genres = await ImportBulkAsync(localGame.Genres, game.Genres, (target, source) =>
                    {
                        DatabaseContext.Entry(target).Collection(t => t.Games).Load();
                        
                        target.Name = source.Name;
                    });
                    
                    localGame.Publishers = await ImportBulkAsync(localGame.Publishers, game.Publishers, (target, source) =>
                    {
                        DatabaseContext.Entry(target).Collection(t => t.PublishedGames).Load();
                        
                        target.Name = source.Name;
                    });
                    
                    localGame.Tags = await ImportBulkAsync(localGame.Tags, game.Tags, (target, source) =>
                    {
                        DatabaseContext.Entry(target).Collection(t => t.Games).Load();
                        
                        target.Name = source.Name;
                    });
                    
                    localGame.MultiplayerModes = await ImportBulkAsync(localGame.MultiplayerModes, game.MultiplayerModes, (target, source) =>
                    {
                        target.Description = source.Description;
                        target.MinPlayers = source.MinPlayers;
                        target.MaxPlayers = source.MaxPlayers;
                        target.Spectators = source.Spectators;
                        target.Type = source.Type;
                        target.NetworkProtocol = source.NetworkProtocol;
                    });
                    
                    localGame.Platforms = await ImportBulkAsync(localGame.Platforms, game.Platforms, (target, source) =>
                    {
                        DatabaseContext.Entry(target).Collection(t => t.Games).Load();
                        
                        target.Name = source.Name;
                    });
                    
                    localGame.PlaySessions = await ImportBulkAsync(localGame.PlaySessions, game.PlaySessions, (target, source) =>
                    {
                        target.Start = source.Start;
                        target.End = source.End;
                        target.UserId = source.UserId;
                    });*/

                    #region Check Installation Status
                    foreach (var installDirectory in Settings.Games.InstallDirectories)
                    {
                        var gameDirectory = await Client.Games.GetInstallDirectory(game, installDirectory);

                        if (Directory.Exists(gameDirectory))
                        {
                            var manifestLocation = ManifestHelper.GetPath(gameDirectory, game.Id);

                            if (File.Exists(manifestLocation))
                            {
                                var manifest = ManifestHelper.Read(gameDirectory, game.Id);

                                localGame.Installed = true;
                                localGame.InstalledOn = DateTime.Now;
                                localGame.InstallDirectory = gameDirectory;
                                localGame.InstalledVersion = manifest.Version;
                            }
                        }
                    }
                    #endregion

                    if (localGame.Id == Guid.Empty)
                    {
                        localGame.Id = game.Id;
                        localGame = await GameService.Add(localGame);
                    }
                    else
                        localGame = await GameService.Update(localGame);

                    foreach (var media in game.Media)
                    {
                        try
                        {
                            await ImportMediaAsync(media, game);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, "Could not import {MediaType} for game {GameTitle}", media.Type, game.Title);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Could not import game {GameTitle}", game.Title);
                }

                op.Complete();
            }
        }

        public async Task ImportGamesAsync()
        {
            var library = await Client.Library.GetAsync();
            
            await ImportGamesAsync(library);
        }

        public async Task ImportGamesAsync(IEnumerable<SDK.Models.EntityReference> games)
        {
            Logger?.LogInformation("Importing games");

            int i = 1;

            foreach (var game in games)
            {
                try
                {
                    var remoteGame = await Client.Games.GetAsync(game.Id);

                    if (OnImportUpdated != null)
                        await OnImportUpdated(new ImportStatusUpdate
                        {
                            CurrentItem = new ImportItem(game.Id, game.Name),
                            Index = i,
                            Total = games.Count()
                        });

                    await ImportGameAsync(remoteGame);
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "Could not import game {GameTitle}", game.Name);
                }
                finally
                {
                    i++;
                }
            }
            
            Logger?.LogInformation("Importing games completed");
        }

        public async Task ImportGamesAsync(params Guid[] ids)
        {
            var games = await Client.Library.GetAsync();
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

            await ImportGamesAsync(toImport);
        }

        public async Task ImportRedistributables()
        {

        }

        public async Task<Media> ImportMediaAsync(Guid importMediaId, Guid? gameId = null)
        {
            SDK.Models.Game game = null;

            if (gameId.HasValue)
                game = await Client.Games.GetAsync(gameId.Value);

            var media = await Client.Media.Get(importMediaId);

            return await ImportMediaAsync(media, game);
        }

        public async Task<Media> ImportMediaAsync(SDK.Models.Media importMedia, SDK.Models.Game game = null)
        {
            var media = await MediaService.Get(importMedia.Id);

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

                await MediaService.Add(media);
            }
            else
                await MediaService.Update(media);

            var mediaStoragePath = MediaService.GetStoragePath();
            var localPath = MediaService.GetImagePath(media);

            if (!File.Exists(localPath) && media.Type != SDK.Enums.MediaType.Manual)
            {
                await Client.Media.DownloadAsync(new SDK.Models.Media
                {
                    Id = media.Id,
                    FileId = media.FileId
                }, localPath);

                MessageBusService.MediaChanged(media);
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
            var models = await service.Get();

            foreach (var importModel in importModels)
            {
                using (var transaction = DatabaseContext.Database.BeginTransaction())
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

                            model = await service.Add(model);
                        }
                        else
                            model = await service.Update(model);

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
                        await service.Delete(model);
                    }
                    catch
                    {

                    }
                }
            }

            // Too slow?
            return await service.Get();
        }

    }
}
