﻿using LANCommander.Launcher.Data;
using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using LANCommander.SDK.Helpers;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Services
{
    public class ImportService
    {
        private readonly SDK.Client Client;
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

        public delegate Task OnImportCompleteHandler();
        public event OnImportCompleteHandler OnImportComplete;

        public delegate void OnImportFailedHandler();
        public event OnImportFailedHandler OnImportFailed;

        public ImportService(
            SDK.Client client,
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
            DatabaseContext databaseContext) : base()
        {
            Client = client;
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
                OnImportFailed?.Invoke();
            }
        }

        public async Task ImportGamesAsync()
        {
            var localGames = await GameService.Get();
            var remoteGames = await Client.Games.GetAsync();

            IEnumerable<Collection> collections;
            IEnumerable<Company> companies;
            IEnumerable<Genre> genres;
            IEnumerable<Platform> platforms;
            IEnumerable<Tag> tags;
            IEnumerable<MultiplayerMode> multiplayerModes;

            #region Import Collections
            collections = await ImportBulk<Collection, SDK.Models.Collection, CollectionService>(remoteGames.SelectMany(g => g.Collections).DistinctBy(c => c.Id), CollectionService, (collection, importCollection) =>
            {
                collection.Name = importCollection.Name;

                return collection;
            });
            #endregion

            #region Import Companies
            var importCompanies = new List<SDK.Models.Company>();

            importCompanies.AddRange(remoteGames.SelectMany(g => g.Developers));
            importCompanies.AddRange(remoteGames.SelectMany(g => g.Publishers));

            companies = await ImportBulk<Company, SDK.Models.Company, CompanyService>(importCompanies.DistinctBy(c => c.Id), CompanyService, (company, importCompany) =>
            {
                company.Name = importCompany.Name;

                return company;
            });
            #endregion

            #region Import Engines
            var engines = await ImportBulk<Engine, SDK.Models.Engine, EngineService>(remoteGames.Where(g => g.Engine != null).Select(g => g.Engine).DistinctBy(e => e.Id), EngineService, (engine, importEngine) =>
            {
                engine.Name = importEngine.Name;

                return engine;
            });
            #endregion

            #region Import Genres
            genres = await ImportBulk<Genre, SDK.Models.Genre, GenreService>(remoteGames.Where(g => g.Genres != null).SelectMany(g => g.Genres).DistinctBy(g => g.Id), GenreService, (genre, importGenre) =>
            {
                genre.Name = importGenre.Name;

                return genre;
            });
            #endregion

            #region Import Platforms
            platforms = await ImportBulk<Platform, SDK.Models.Platform, PlatformService>(remoteGames.Where(g => g.Platforms != null).SelectMany(g => g.Platforms).DistinctBy(g => g.Id), PlatformService, (platform, importPlatform) =>
            {
                platform.Name = importPlatform.Name;

                return platform;
            });
            #endregion

            #region Import Tags
            tags = await ImportBulk<Tag, SDK.Models.Tag, TagService>(remoteGames.Where(g => g.Tags != null).SelectMany(g => g.Tags).DistinctBy(t => t.Id), TagService, (tag, importTag) =>
            {
                tag.Name = importTag.Name;

                return tag;
            });
            #endregion

            #region Import MultiplayerModes
            multiplayerModes = await ImportBulk<MultiplayerMode, SDK.Models.MultiplayerMode, MultiplayerModeService>(remoteGames.Where(g => g.MultiplayerModes != null).SelectMany(g => g.MultiplayerModes).DistinctBy(t => t.Id), MultiplayerModeService, (multiplayerMode, importMultiplayerMode) =>
            {
                multiplayerMode.Type = importMultiplayerMode.Type;
                multiplayerMode.NetworkProtocol = importMultiplayerMode.NetworkProtocol;
                multiplayerMode.Description = importMultiplayerMode.Description;
                multiplayerMode.MinPlayers = importMultiplayerMode.MinPlayers;
                multiplayerMode.MaxPlayers = importMultiplayerMode.MaxPlayers;
                multiplayerMode.Spectators = importMultiplayerMode.Spectators;

                return multiplayerMode;
            });
            #endregion

            using (var gameTransaction = DatabaseContext.Database.BeginTransaction())
            {
                foreach (var remoteGame in remoteGames.OrderBy(g => (int)g.Type))
                {
                    try
                    {
                        var localGame = localGames.FirstOrDefault(g => g.Id == remoteGame.Id);

                        if (localGame == null)
                            localGame = new Data.Models.Game();

                        localGame.Title = remoteGame.Title;
                        localGame.SortTitle = remoteGame.SortTitle;
                        localGame.Description = remoteGame.Description;
                        localGame.Notes = remoteGame.Notes;
                        localGame.ReleasedOn = remoteGame.ReleasedOn;
                        localGame.Type = (Data.Enums.GameType)(int)remoteGame.Type;
                        localGame.BaseGameId = remoteGame.BaseGame?.Id;
                        localGame.Singleplayer = remoteGame.Singleplayer;

                        #region Update Game Engine
                        if (remoteGame.Engine == null && localGame.Engine != null)
                        {
                            localGame.Engine = null;
                            localGame.EngineId = null;
                        }
                        else if (remoteGame.Engine != null)
                        {
                            var engine = engines.FirstOrDefault(e => e.Id == remoteGame.Engine.Id);

                            localGame.Engine = engine;
                            localGame.EngineId = engine.Id;
                        }
                        #endregion

                        #region Update Game Collections
                        if (localGame.Collections == null)
                            localGame.Collections = collections.Where(c => remoteGame.Collections.Any(rc => rc.Id == c.Id)).ToList();
                        else
                        {
                            var collectionsToRemove = localGame.Collections.Where(c => !remoteGame.Collections.Any(rc => rc.Id == c.Id)).ToList();
                            var collectionsToAdd = collections.Where(c => remoteGame.Collections.Any(rc => rc.Id == c.Id) && !localGame.Collections.Any(lc => lc.Id == c.Id)).ToList();

                            foreach (var collection in collectionsToRemove)
                                localGame.Collections.Remove(collection);

                            foreach (var collection in collectionsToAdd)
                                localGame.Collections.Add(collection);
                        }
                        #endregion

                        #region Update Game Developers
                        if (localGame.Developers == null)
                            localGame.Developers = companies.Where(c => remoteGame.Developers.Any(rc => rc.Id == c.Id)).ToList();
                        else
                        {
                            var developersToRemove = localGame.Developers.Where(c => !remoteGame.Developers.Any(rc => rc.Id == c.Id)).ToList();
                            var developersToAdd = companies.Where(c => remoteGame.Developers.Any(rc => rc.Id == c.Id) && !localGame.Developers.Any(lc => lc.Id == c.Id)).ToList();

                            foreach (var developer in developersToRemove)
                                localGame.Developers.Remove(developer);

                            foreach (var developer in developersToAdd)
                                localGame.Developers.Add(developer);
                        }
                        #endregion

                        #region Update Game Publishers
                        if (localGame.Publishers == null)
                            localGame.Publishers = companies.Where(c => remoteGame.Publishers.Any(rc => rc.Id == c.Id)).ToList();
                        else
                        {
                            var publishersToRemove = localGame.Publishers.Where(c => !remoteGame.Publishers.Any(rc => rc.Id == c.Id)).ToList();
                            var publishersToAdd = companies.Where(c => remoteGame.Publishers.Any(rc => rc.Id == c.Id) && !localGame.Publishers.Any(lc => lc.Id == c.Id)).ToList();

                            foreach (var publisher in publishersToRemove)
                                localGame.Publishers.Remove(publisher);

                            foreach (var publisher in publishersToAdd)
                                localGame.Publishers.Add(publisher);
                        }
                        #endregion

                        #region Update Game Genres
                        if (localGame.Genres == null)
                            localGame.Genres = genres.Where(c => remoteGame.Genres.Any(rc => rc.Id == c.Id)).ToList();
                        else
                        {
                            var genresToRemove = localGame.Genres.Where(c => !remoteGame.Genres.Any(rc => rc.Id == c.Id)).ToList();
                            var genresToAdd = genres.Where(c => remoteGame.Genres.Any(rc => rc.Id == c.Id) && !localGame.Genres.Any(lc => lc.Id == c.Id)).ToList();

                            foreach (var genre in genresToRemove)
                                localGame.Genres.Remove(genre);

                            foreach (var genre in genresToAdd)
                                localGame.Genres.Add(genre);
                        }
                        #endregion

                        #region Update Game Tags
                        if (localGame.Tags == null)
                            localGame.Tags = tags.Where(c => remoteGame.Tags.Any(rc => rc.Id == c.Id)).ToList();
                        else
                        {
                            var tagsToRemove = localGame.Tags.Where(c => !remoteGame.Tags.Any(rc => rc.Id == c.Id)).ToList();
                            var tagsToAdd = tags.Where(c => remoteGame.Tags.Any(rc => rc.Id == c.Id) && !localGame.Tags.Any(lc => lc.Id == c.Id)).ToList();

                            foreach (var tag in tagsToRemove)
                                localGame.Tags.Remove(tag);

                            foreach (var tag in tagsToAdd)
                                localGame.Tags.Add(tag);
                        }
                        #endregion

                        #region Update Game Multiplayer Modes
                        if (localGame.MultiplayerModes == null)
                            localGame.MultiplayerModes = multiplayerModes.Where(m => remoteGame.MultiplayerModes.Any(rm => rm.Id == m.Id)).ToList();
                        else
                        {
                            var modesToRemove = localGame.MultiplayerModes.Where(m => !remoteGame.MultiplayerModes.Any(rm => rm.Id == m.Id)).ToList();
                            var modesToAdd = multiplayerModes.Where(m => remoteGame.MultiplayerModes.Any(rm => rm.Id == m.Id) && !localGame.MultiplayerModes.Any(lm => lm.Id == m.Id)).ToList();

                            foreach (var mode in modesToRemove)
                                localGame.MultiplayerModes.Remove(mode);

                            foreach (var mode in modesToAdd)
                                localGame.MultiplayerModes.Add(mode);
                        }
                        #endregion

                        #region Update Play Sessions
                        // This needs to be fixed, the profile ID should _not_ pull out of settings
                        // It'd be better to pull directly from the authenticated client
                        foreach (var session in remoteGame.PlaySessions.Where(rps => rps.UserId == Settings.Profile.Id && !localGame.PlaySessions.Any(lps => lps.Start == rps.Start && lps.End == lps.End)))
                        {
                            localGame.PlaySessions.Add(new PlaySession
                            {
                                Start = session.Start,
                                End = session.End,
                                CreatedOn = session.CreatedOn,
                                UpdatedOn = session.UpdatedOn,
                                GameId = session.GameId,
                                UserId = Settings.Profile.Id
                            });
                        }
                        #endregion

                        #region Check Installation Status
                        var installDirectory = Client.Games.GetInstallDirectory(remoteGame);

                        if (Directory.Exists(installDirectory))
                        {
                            var manifestLocation = ManifestHelper.GetPath(installDirectory, remoteGame.Id);

                            if (File.Exists(manifestLocation))
                            {
                                var manifest = ManifestHelper.Read(installDirectory, remoteGame.Id);

                                localGame.Installed = true;
                                localGame.InstalledOn = DateTime.Now;
                                localGame.InstallDirectory = installDirectory;
                                localGame.InstalledVersion = manifest.Version;
                            }
                        }
                        #endregion

                        if (localGame.Id == Guid.Empty)
                        {
                            localGame.Id = remoteGame.Id;
                            localGame = await GameService.Add(localGame);
                        }
                        else
                            localGame = await GameService.Update(localGame);
                    }
                    catch (Exception ex)
                    {

                    }
                }

                // Potentially delete any games that no longer exist on the server or have been revoked
                foreach (var localGame in localGames)
                {
                    var remoteGame = remoteGames.FirstOrDefault(g => g.Id == localGame.Id);

                    if (remoteGame == null && !localGame.Installed)
                    {
                        await GameService.Delete(localGame);
                    }
                }

                await gameTransaction.CommitAsync();
            }

            #region Download Media
            // MediaId, GameId
            var mediaMap = new Dictionary<Guid, Guid>();

            foreach (var remoteGame in remoteGames)
            {
                foreach (var remoteMedia in remoteGame.Media)
                {
                    mediaMap[remoteMedia.Id] = remoteGame.Id;
                }
            }

            var medias = await ImportBulk<Media, SDK.Models.Media, MediaService>(remoteGames.SelectMany(g => g.Media), MediaService, (media, importMedia) =>
            {
                media.FileId = importMedia.FileId;
                media.Type = importMedia.Type;
                media.SourceUrl = importMedia.SourceUrl;
                media.MimeType = importMedia.MimeType;
                media.Crc32 = importMedia.Crc32.ToUpper();
                media.Name = importMedia.Name ?? String.Empty;
                media.GameId = mediaMap.ContainsKey(importMedia.Id) ? mediaMap[importMedia.Id] : Guid.Empty;

                return media;
            }, false);

            var mediaStoragePath = MediaService.GetStoragePath();

            foreach (var media in medias)
            {
                var localPath = MediaService.GetImagePath(media);

                if (!File.Exists(localPath) && media.Type != SDK.Enums.MediaType.Manual)
                {
                    var staleFiles = Directory.EnumerateFiles(mediaStoragePath, $"{media.FileId}-*");

                    foreach (var staleFile in staleFiles)
                        File.Delete(staleFile);

                    await Client.Media.Download(new SDK.Models.Media
                    {
                        Id = media.Id,
                        FileId = media.FileId
                    }, localPath);

                    MessageBusService.MediaChanged(media);
                }
            }
            #endregion
        }

        public async Task ImportRedistributables()
        {

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
                    await service.Delete(model);
                }
            }

            // Too slow?
            return await service.Get();
        }

    }
}
