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

        private async Task ImportCollectionAsync<TKeyedModel, TModel>(IEnumerable<TKeyedModel> source, ICollection<TModel> target)
            where TKeyedModel : IKeyedModel
            where TModel : BaseModel
        {
            var dbSet = DatabaseContext.Set<TModel>();

            if (target == null)
                target = await dbSet.Where(x => source.Any(y => y.Id == x.Id)).ToListAsync();
            else
            {
                var toRemove = target.Where(x => !source.Any(y => y.Id == x.Id)).ToList();
                var toAdd = target.Where(x => source.Any(y => y.Id == x.Id) && !target.Any(y => y.Id == x.Id)).ToList();
                
                foreach (var item in toRemove)
                    target.Remove(item);
                
                foreach (var item in toAdd)
                    target.Add(item);
            }
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
                            await ImportGamesAsync(game.BaseGameId);

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
                        var engine = Engines.FirstOrDefault(e => e.Id == game.Engine.Id);

                        localGame.Engine = engine;
                        localGame.EngineId = engine.Id;
                    }
                    #endregion

                    await ImportCollectionAsync(game.Collections, localGame.Collections);
                    await ImportCollectionAsync(game.Developers, localGame.Developers);
                    await ImportCollectionAsync(game.Publishers, localGame.Publishers);
                    await ImportCollectionAsync(game.Genres, localGame.Genres);
                    await ImportCollectionAsync(game.Tags, localGame.Tags);
                    await ImportCollectionAsync(game.MultiplayerModes, localGame.MultiplayerModes);
                    await ImportCollectionAsync(game.Platforms, localGame.Platforms);
                    await ImportCollectionAsync(game.PlaySessions, localGame.PlaySessions);

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
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Could not import game {GameTitle}", game.Title);
                }

                op.Complete();
            }
        }

        /*public async Task ImportGamesAsync()
        {
            ICollection<Game> localGames;
            IEnumerable<SDK.Models.EntityReference> remoteGames;

            Logger?.LogInformation("Importing library games");

            using (var op = Logger.BeginOperation("Retrieving games from the database"))
            {
                localGames = await GameService.Get();

                op.Complete();
            }

            using (var op = Logger.BeginOperation("Retrieving games from the server"))
            {
                remoteGames = await Client.Library.GetAsync();

                op.Complete();
            }

            await ImportGamesAsync(localGames, remoteGames);
        }*/

        public async Task ImportGamesAsync(IEnumerable<SDK.Models.EntityReference> games)
        {
            Logger?.LogInformation("Importing games");

            int i = 1;

            foreach (var game in games)
            {
                try
                {
                    var remoteGame = await Client.Games.GetAsync(game.Id);

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

        /*private async Task ImportGamesAsync(IEnumerable<Game> localGames, IEnumerable<SDK.Models.EntityReference> remoteGames)
        {
            
            IEnumerable<Collection> collections;
            IEnumerable<Company> companies;
            IEnumerable<Engine> engines;
            IEnumerable<Genre> genres;
            IEnumerable<Platform> platforms;
            IEnumerable<Tag> tags;
            IEnumerable<MultiplayerMode> multiplayerModes;

            #region Import Collections
            using (var op = Logger.BeginOperation("Importing collections"))
            {
                Collections = await ImportBulk<Collection, SDK.Models.Collection, CollectionService>(remoteGames.SelectMany(g => g.Collections).DistinctBy(c => c.Id), CollectionService, (collection, importCollection) =>
                {
                    collection.Name = importCollection.Name;

                    return collection;
                });

                op.Complete();
            }
            #endregion

            #region Import Companies
            using (var op = Logger.BeginOperation("Importing companies"))
            {
                var importCompanies = new List<SDK.Models.Company>();

                importCompanies.AddRange(remoteGames.SelectMany(g => g.Developers));
                importCompanies.AddRange(remoteGames.SelectMany(g => g.Publishers));

                Companies = await ImportBulk<Company, SDK.Models.Company, CompanyService>(importCompanies.DistinctBy(c => c.Id), CompanyService, (company, importCompany) =>
                {
                    company.Name = importCompany.Name;

                    return company;
                });

                op.Complete();
            }
            #endregion

            #region Import Engines
            using (var op = Logger.BeginOperation("Importing engines"))
            {
                Engines = await ImportBulk<Engine, SDK.Models.Engine, EngineService>(remoteGames.Where(g => g.Engine != null).Select(g => g.Engine).DistinctBy(e => e.Id), EngineService, (engine, importEngine) =>
                {
                    engine.Name = importEngine.Name;

                    return engine;
                });

                op.Complete();
            }
            #endregion

            #region Import Genres
            using (var op = Logger.BeginOperation("Importing genres"))
            {
                Genres = await ImportBulk<Genre, SDK.Models.Genre, GenreService>(remoteGames.Where(g => g.Genres != null).SelectMany(g => g.Genres).DistinctBy(g => g.Id), GenreService, (genre, importGenre) =>
                {
                    genre.Name = importGenre.Name;

                    return genre;
                });

                op.Complete();
            }
            #endregion

            #region Import Platforms
            using (var op = Logger.BeginOperation("Importing platforms"))
            {
                Platforms = await ImportBulk<Platform, SDK.Models.Platform, PlatformService>(remoteGames.Where(g => g.Platforms != null).SelectMany(g => g.Platforms).DistinctBy(g => g.Id), PlatformService, (platform, importPlatform) =>
                {
                    platform.Name = importPlatform.Name;

                    return platform;
                });

                op.Complete();
            }
            #endregion

            #region Import Tags
            using (var op = Logger.BeginOperation("Importing tags"))
            {
                Tags = await ImportBulk<Tag, SDK.Models.Tag, TagService>(remoteGames.Where(g => g.Tags != null).SelectMany(g => g.Tags).DistinctBy(t => t.Id), TagService, (tag, importTag) =>
                {
                    tag.Name = importTag.Name;

                    return tag;
                });

                op.Complete();
            }
            #endregion

            #region Import MultiplayerModes
            using (var op = Logger.BeginOperation("Importing multiplayer modes"))
            {
                MultiplayerModes = await ImportBulk<MultiplayerMode, SDK.Models.MultiplayerMode, MultiplayerModeService>(remoteGames.Where(g => g.MultiplayerModes != null).SelectMany(g => g.MultiplayerModes).DistinctBy(t => t.Id), MultiplayerModeService, (multiplayerMode, importMultiplayerMode) =>
                {
                    multiplayerMode.Type = importMultiplayerMode.Type;
                    multiplayerMode.NetworkProtocol = importMultiplayerMode.NetworkProtocol;
                    multiplayerMode.Description = importMultiplayerMode.Description;
                    multiplayerMode.MinPlayers = importMultiplayerMode.MinPlayers;
                    multiplayerMode.MaxPlayers = importMultiplayerMode.MaxPlayers;
                    multiplayerMode.Spectators = importMultiplayerMode.Spectators;

                    return multiplayerMode;
                });

                op.Complete();

            }
            #endregion

            var importedGames = new List<SDK.Models.Game>();

            using (var gameTransaction = DatabaseContext.Database.BeginTransaction())
            {
                foreach (var remoteGame in remoteGames.OrderBy(g => (int)g.Type))
                {
                    if (importedGames.Any(g => g.Id == remoteGame.Id))
                        continue;

                    if (remoteGame.BaseGameId != Guid.Empty && !importedGames.Any(g => g.Id == remoteGame.BaseGameId))
                    {
                        var baseGame = remoteGames.FirstOrDefault(g => g.Id == remoteGame.BaseGameId);

                        if (baseGame == null)
                            baseGame = await Client.Games.GetAsync(remoteGame.BaseGameId);

                        await ImportGameAsync(baseGame, localGames);

                        importedGames.Add(baseGame);
                    }

                    await ImportGameAsync(remoteGame, localGames);

                    importedGames.Add(remoteGame);
                }

                // Potentially delete any games that no longer exist on the server or have been revoked
                foreach (var localGame in localGames)
                {
                    var remoteGame = remoteGames.FirstOrDefault(g => g.Id == localGame.Id);

                    if (remoteGame == null && !localGame.Installed)
                    {
                        using (var op = Logger.BeginOperation("Deleting game {GameTitle}", localGame.Title))
                        {
                            await GameService.Delete(localGame);

                            op.Complete();
                        }
                    }
                }

                using (var op = Logger.BeginOperation("Committing changes to games"))
                {
                    await gameTransaction.CommitAsync();

                    op.Complete();
                }
            }

            #region Download Media
            // MediaId, GameId
            IEnumerable<Media> medias;
            var mediaMap = new Dictionary<Guid, Guid>();

            foreach (var importedGame in importedGames)
            {
                foreach (var remoteMedia in importedGame.Media)
                {
                    mediaMap[remoteMedia.Id] = importedGame.Id;
                }
            }

            using (var op = Logger.BeginOperation("Importing media metadata"))
            {
                medias = await ImportBulk<Media, SDK.Models.Media, MediaService>(importedGames.SelectMany(g => g.Media), MediaService, (media, importMedia) =>
                {
                    media.FileId = importMedia.FileId;
                    media.Type = importMedia.Type;
                    media.SourceUrl = importMedia.SourceUrl;
                    media.MimeType = importMedia.MimeType;
                    media.Crc32 = importMedia.Crc32.ToUpper();
                    media.Name = importMedia.Name ?? String.Empty;
                    media.GameId = mediaMap.ContainsKey(importMedia.Id) ? mediaMap[importMedia.Id] : Guid.Empty;

                    return media;
                }, true);

                op.Complete();
            }

            var mediaStoragePath = MediaService.GetStoragePath();

            using (var op = Logger.BeginOperation("Downloading media files"))
            {
                foreach (var media in medias)
                {
                    var localPath = MediaService.GetImagePath(media);

                    if (!File.Exists(localPath) && media.Type != SDK.Enums.MediaType.Manual)
                    {
                        var staleFiles = Directory.EnumerateFiles(mediaStoragePath, $"{media.FileId}-*");

                        foreach (var staleFile in staleFiles)
                            File.Delete(staleFile);

                        await Client.Media.DownloadAsync(new SDK.Models.Media
                        {
                            Id = media.Id,
                            FileId = media.FileId
                        }, localPath);

                        MessageBusService.MediaChanged(media);
                    }
                }

                op.Complete();
            }
            #endregion
        }*/

        public async Task ImportRedistributables()
        {

        }

        public async Task<Media> ImportMedia(Guid importMediaId, Guid? gameId = null)
        {
            SDK.Models.Game game = null;

            if (gameId.HasValue)
                game = await Client.Games.GetAsync(gameId.Value);

            var media = await Client.Media.Get(importMediaId);

            return await ImportMedia(media, game);
        }

        public async Task<Media> ImportMedia(SDK.Models.Media importMedia, SDK.Models.Game game = null)
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
