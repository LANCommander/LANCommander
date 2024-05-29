using LANCommander.PlaynitePlugin.Extensions;
using LANCommander.SDK;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LANCommander.PlaynitePlugin
{
    public class ImportController
    {
        public static readonly ILogger Logger = LogManager.GetLogger();

        private LANCommanderLibraryPlugin Plugin;
        private IGameDatabaseAPI Database;

        private IEnumerable<Genre> Genres;
        private IEnumerable<Tag> Tags;
        private IEnumerable<Company> Publishers;
        private IEnumerable<Company> Developers;
        private IEnumerable<Category> Collections;

        private string ImageCachePath;

        public ImportController(LANCommanderLibraryPlugin plugin)
        {
            Plugin = plugin;
            Database = Plugin.PlayniteApi.Database;

            ImageCachePath = Path.Combine(plugin.GetPluginUserDataPath(), "Cache");

            if (!Directory.Exists(ImageCachePath))
                Directory.CreateDirectory(ImageCachePath);
        }

        public IEnumerable<Game> ImportGames()
        {
            var playSessions = Plugin.LANCommanderClient.Profile.GetPlaySessions().Where(ps => ps.Start != null && ps.End != null).OrderByDescending(ps => ps.End);

            var games = Plugin.LANCommanderClient.Games.Get();

            Genres = ImportGenres(games.SelectMany(g => g.Genres).Select(g => g.Name).Distinct());
            Tags = ImportTags(games.SelectMany(g => g.Tags).Select(t => t.Name).Distinct());
            Publishers = ImportCompanies(games.SelectMany(g => g.Publishers).Select(c => c.Name).Distinct());
            Developers = ImportCompanies(games.SelectMany(g => g.Developers).Select(c => c.Name).Distinct());
            Collections = ImportCollections(games.SelectMany(g => g.Collections).Select(c => c.Name).Distinct());

            var playniteGames = new List<Game>();

            Parallel.ForEach(games, new ParallelOptions { 
                MaxDegreeOfParallelism = 8,
            }, (manifest) =>
            {
                Game game = null;

                try
                {
                    var gamePlaySessions = playSessions.Where(ps => ps.GameId == manifest.Id);

                    game = ImportGame(manifest, gamePlaySessions);
                }
                catch (Exception ex)
                {
                    Logger?.Error(ex, $"Game {manifest.Title} could not be imported!");
                }

                if (game != null)
                    playniteGames.Add(game);
            });

            #region Cleanup
            // Clean up any games we don't have access to
            var gamesToRemove = Database.Games.Where(g => g.PluginId == Plugin.Id && !games.Any(lg => lg.Id == g.Id)).ToList();

            Database.Games.Remove(gamesToRemove);
            #endregion

            return playniteGames;
        }

        public Game ImportGame(LANCommander.SDK.Models.Game game, IEnumerable<SDK.Models.PlaySession> playSessions)
        {
            bool exists = false;
            var playniteGame = Database.Games.Get(game.Id);

            if (playniteGame == null)
                playniteGame = new Game();
            else
                exists = true;

            playniteGame.Id = game.Id;
            playniteGame.GameId = game.Id.ToString();
            playniteGame.PluginId = Plugin.Id;
            playniteGame.Name = game.Title;
            playniteGame.SortingName = game.SortTitle;
            playniteGame.Description = game.Description;
            playniteGame.Notes = game.Notes;
            playniteGame.ReleaseDate = new ReleaseDate(game.ReleasedOn);

            if (playniteGame.IsInstalled)
            {
                var updateAvailable = false;
                var manifests = Plugin.GetGameManifests(playniteGame);

                foreach (var mani in manifests)
                {
                    if (game.DependentGames.Any(g => g != null && g.Id == mani.Id && playniteGame.Version != game.Archives.OrderByDescending(a => a.CreatedOn).FirstOrDefault()?.Version))
                        updateAvailable = true;
                }

                if (updateAvailable && playniteGame.Name.EndsWith(ResourceProvider.GetString("LOCLANCommanderUpdateAvailableSuffix")))
                    playniteGame.Name += ResourceProvider.GetString("LOCLANCommanderUpdateAvailableSuffix");
            }

            #region Play Sessions
            if (playSessions.Count() > 0)
            {
                playniteGame.LastActivity = playSessions.First().End;
                playniteGame.PlayCount = (ulong)playSessions.Count();
                playniteGame.Playtime = (ulong)playSessions.Sum(ps => ps.End.Value.Subtract(ps.Start.Value).TotalSeconds);
            }
            #endregion

            #region Actions
            if (playniteGame.GameActions == null)
                playniteGame.GameActions = new ObservableCollection<Playnite.SDK.Models.GameAction>();
            else
                playniteGame.GameActions.Clear();

            if (game.Actions == null)
            {
                Logger?.Warn($"Game {game.Title} does not have any actions defined and may not be playable");
                return null;
            }

            foreach (var action in playniteGame.GameActions.Where(a => a.IsPlayAction))
                playniteGame.GameActions.Remove(action);

            if (playniteGame.IsInstalled || playniteGame.IsInstalling)
                foreach (var action in game.Actions.OrderBy(a => a.SortOrder).Where(a => !a.IsPrimaryAction))
                {
                    var actionPath = action.Path?.ExpandEnvironmentVariables(playniteGame.InstallDirectory);
                    var actionWorkingDir = String.IsNullOrWhiteSpace(action.WorkingDirectory) ? playniteGame.InstallDirectory : action.WorkingDirectory.ExpandEnvironmentVariables(playniteGame.InstallDirectory);
                    var actionArguments = action.Arguments?.ExpandEnvironmentVariables(playniteGame.InstallDirectory);

                    if (actionPath.StartsWith(actionWorkingDir))
                        actionPath = actionPath.Substring(actionWorkingDir.Length).TrimStart(Path.DirectorySeparatorChar);

                    playniteGame.GameActions.Add(new Playnite.SDK.Models.GameAction()
                    {
                        Name = action.Name,
                        Arguments = action.Arguments,
                        Path = actionPath,
                        WorkingDir = actionWorkingDir,
                        IsPlayAction = false
                    });
                }
            #endregion

            // Genres
            if (game.Genres != null)
                playniteGame.GenreIds = Genres.Where(g => game.Genres.Any(gg => gg.Name == g.Name)).Select(g => g.Id).ToList();

            // Tags
            if (game.Tags != null)
                playniteGame.TagIds = Tags.Where(t => game.Tags.Any(gt => gt.Name == t.Name)).Select(t => t.Id).ToList();

            // Publishers
            if (game.Publishers != null)
                playniteGame.PublisherIds = Publishers.Where(c => game.Publishers.Any(gp => gp.Name == c.Name)).Select(p => p.Id).ToList();

            // Developers
            if (game.Developers != null)
                playniteGame.DeveloperIds = Developers.Where(c => game.Developers.Any(gd => gd.Name == c.Name)).Select(d => d.Id).ToList();

            // Collections
            if (game.Collections != null)
                playniteGame.CategoryIds = Collections.Where(c => game.Collections.Any(gc => gc.Name == c.Name)).Select(c => c.Id).ToList();

            // Media
            if (game.Media != null && game.Media.Any(m => m.Type == SDK.Enums.MediaType.Icon))
            {
                var task = ImportMedia(game.Media.First(m => m.Type == SDK.Enums.MediaType.Icon));

                task.Wait();

                if (playniteGame.Icon != task.Result)
                    playniteGame.Icon = task.Result;
            }

            if (game.Media != null && game.Media.Any(m => m.Type == SDK.Enums.MediaType.Cover))
            {
                var task = ImportMedia(game.Media.First(m => m.Type == SDK.Enums.MediaType.Cover));

                task.Wait();

                if (playniteGame.CoverImage != task.Result)
                    playniteGame.CoverImage = task.Result;
            }

            if (game.Media != null && game.Media.Any(m => m.Type == SDK.Enums.MediaType.Background))
            {
                var task = ImportMedia(game.Media.First(m => m.Type == SDK.Enums.MediaType.Background));

                task.Wait();

                if (playniteGame.BackgroundImage != task.Result)
                    playniteGame.BackgroundImage = task.Result;
            }

            // Features
            var features = ImportFeatures(game);

            playniteGame.FeatureIds = features.Select(f => f.Id).ToList();

            if (exists)
                Database.Games.Update(playniteGame);
            else
                Database.Games.Add(playniteGame);

            return playniteGame;
        }

        private IEnumerable<Genre> ImportGenres(IEnumerable<string> genreNames)
        {
            foreach (var genreName in genreNames)
            {
                var genre = Database.Genres.FirstOrDefault(g => g.Name == genreName);

                if (genre == null)
                {
                    genre = new Genre(genreName);

                    Database.Genres.Add(genre);
                }

                yield return genre;
            }
        }

        private IEnumerable<Tag> ImportTags(IEnumerable<string> tagNames)
        {
            foreach (var tagName in tagNames)
            {
                var tag = Database.Tags.FirstOrDefault(t => t.Name == tagName);

                if (tag == null)
                {
                    tag = new Tag(tagName);

                    Database.Tags.Add(tag);
                }

                yield return tag;
            }
        }

        private IEnumerable<Company> ImportCompanies(IEnumerable<string> companyNames)
        {
            foreach (var companyName in companyNames)
            {
                var company = Database.Companies.FirstOrDefault(c => c.Name == companyName);

                if (company == null)
                {
                    company = new Company(companyName);

                    Database.Companies.Add(company);
                }

                yield return company;
            }
        }

        private IEnumerable<Category> ImportCollections(IEnumerable<string> collectionNames)
        {
            foreach (var collectionName in collectionNames)
            {
                var category = Database.Categories.FirstOrDefault(c => c.Name == collectionName);

                if (category == null)
                {
                    category = new Category(collectionName);

                    Database.Categories.Add(category);
                }

                yield return category;
            }
        }

        private IEnumerable<GameFeature> ImportFeatures(SDK.Models.Game game)
        {
            if (game.Singleplayer)
            {
                var featureName = $"Singleplayer";
                var feature = Database.Features.FirstOrDefault(f => f.Name == featureName);

                if (feature == null)
                {
                    feature = new GameFeature(featureName);

                    Database.Features.Add(feature);
                }

                yield return feature;
            }

            if (game.MultiplayerModes != null && game.MultiplayerModes.Any())
            {
                var featureName = $"Multiplayer";
                var feature = Database.Features.FirstOrDefault(f => f.Name == featureName);

                if (feature == null)
                {
                    feature = new GameFeature(featureName);

                    Database.Features.Add(feature);
                }

                yield return feature;
            }

            foreach (var mode in game.MultiplayerModes)
            {
                var featureName = $"{mode.Type} Multiplayer {mode.GetPlayerCount()}".Trim();
                var feature = Database.Features.FirstOrDefault(f => f.Name == featureName);

                if (feature == null)
                {
                    feature = new GameFeature(featureName);

                    Database.Features.Add(feature);
                }

                yield return feature;
            }
        }

        private async Task<string> ImportMedia(SDK.Models.Media media)
        {
            var cacheFilePath = Path.Combine(ImageCachePath, $"{media.FileId}-{media.Crc32}.cache");

            if (!File.Exists(cacheFilePath))
            {
                var staleFiles = Directory.EnumerateFiles(ImageCachePath, $"{media.FileId}-*.cache");

                foreach (var staleFile in staleFiles)
                    File.Delete(staleFile);

                await Plugin.LANCommanderClient.Media.Download(media, cacheFilePath);
            }

            return cacheFilePath;
        }
    }
}
