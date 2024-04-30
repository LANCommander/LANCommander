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

            var manifests = Plugin.LANCommanderClient.Games.Get();

            Genres = ImportGenres(manifests.Where(m => m.Genre != null).SelectMany(m => m.Genre).Distinct());
            Tags = ImportTags(manifests.Where(m => m.Tags != null).SelectMany(m => m.Tags).Distinct());
            Publishers = ImportCompanies(manifests.Where(m => m.Publishers != null).SelectMany(m => m.Publishers).Distinct());
            Developers = ImportCompanies(manifests.Where(m => m.Developers != null).SelectMany(m => m.Developers).Distinct());
            Collections = ImportCollections(manifests.Where(m => m.Collections != null).SelectMany(m => m.Collections).Distinct());

            var games = new List<Game>();

            Parallel.ForEach(manifests, new ParallelOptions { 
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
                    games.Add(game);
            });

            #region Cleanup
            // Clean up any games we don't have access to
            var gamesToRemove = Database.Games.Where(g => g.PluginId == Plugin.Id && !manifests.Any(lg => lg.Id == g.Id)).ToList();

            Database.Games.Remove(gamesToRemove);
            #endregion

            return games;
        }

        public Game ImportGame(GameManifest manifest, IEnumerable<SDK.Models.PlaySession> playSessions)
        {
            bool exists = false;
            var game = Database.Games.Get(manifest.Id);

            if (game == null)
                game = new Game();
            else
                exists = true;

            game.Id = manifest.Id;
            game.GameId = manifest.Id.ToString();
            game.PluginId = Plugin.Id;
            game.Name = manifest.Title;
            game.SortingName = manifest.SortTitle;
            game.Description = manifest.Description;
            game.Notes = manifest.Notes;
            game.ReleaseDate = new ReleaseDate(manifest.ReleasedOn);

            if (game.IsInstalled && game.Version != manifest.Version)
            {
                if (!game.Name.EndsWith(" - Update Available"))
                    game.Name += " - Update Available";
            }

            #region Play Sessions
            if (playSessions.Count() > 0)
            {
                game.LastActivity = playSessions.First().End;
                game.PlayCount = (ulong)playSessions.Count();
                game.Playtime = (ulong)playSessions.Sum(ps => ps.End.Value.Subtract(ps.Start.Value).TotalSeconds);
            }
            #endregion

            #region Actions
            if (game.GameActions == null)
                game.GameActions = new ObservableCollection<Playnite.SDK.Models.GameAction>();
            else
                game.GameActions.Clear();

            if (manifest.Actions == null)
            {
                Logger?.Warn($"Game {manifest.Title} does not have any actions defined and may not be playable");
                return null;
            }

            foreach (var action in game.GameActions.Where(a => a.IsPlayAction))
                game.GameActions.Remove(action);

            if (game.IsInstalled || game.IsInstalling)
                foreach (var action in manifest.Actions.OrderBy(a => a.SortOrder).Where(a => !a.IsPrimaryAction))
                {
                    var actionPath = action.Path?.ExpandEnvironmentVariables(game.InstallDirectory);
                    var actionWorkingDir = String.IsNullOrWhiteSpace(action.WorkingDirectory) ? game.InstallDirectory : action.WorkingDirectory.ExpandEnvironmentVariables(game.InstallDirectory);
                    var actionArguments = action.Arguments?.ExpandEnvironmentVariables(game.InstallDirectory);

                    if (actionPath.StartsWith(actionWorkingDir))
                        actionPath = actionPath.Substring(actionWorkingDir.Length).TrimStart(Path.DirectorySeparatorChar);

                    game.GameActions.Add(new Playnite.SDK.Models.GameAction()
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
            if (manifest.Genre != null)
                game.GenreIds = Genres.Where(g => manifest.Genre.Contains(g.Name)).Select(g => g.Id).ToList();

            // Tags
            if (manifest.Tags != null)
                game.TagIds = Tags.Where(t => manifest.Tags.Contains(t.Name)).Select(t => t.Id).ToList();

            // Publishers
            if (manifest.Publishers != null)
                game.PublisherIds = Publishers.Where(p => manifest.Publishers.Contains(p.Name)).Select(p => p.Id).ToList();

            // Developers
            if (manifest.Developers != null)
                game.DeveloperIds = Developers.Where(d => manifest.Developers.Contains(d.Name)).Select(d => d.Id).ToList();

            // Collections
            if (manifest.Collections != null)
                game.CategoryIds = Collections.Where(c => manifest.Collections.Contains(c.Name)).Select(c => c.Id).ToList();

            // Media
            if (manifest.Media != null && manifest.Media.Any(m => m.Type == SDK.Enums.MediaType.Icon))
            {
                var task = ImportMedia(manifest.Media.First(m => m.Type == SDK.Enums.MediaType.Icon));

                task.Wait();

                if (game.Icon != task.Result)
                    game.Icon = task.Result;
            }

            if (manifest.Media != null && manifest.Media.Any(m => m.Type == SDK.Enums.MediaType.Cover))
            {
                var task = ImportMedia(manifest.Media.First(m => m.Type == SDK.Enums.MediaType.Cover));

                task.Wait();

                if (game.CoverImage != task.Result)
                    game.CoverImage = task.Result;
            }

            if (manifest.Media != null && manifest.Media.Any(m => m.Type == SDK.Enums.MediaType.Background))
            {
                var task = ImportMedia(manifest.Media.First(m => m.Type == SDK.Enums.MediaType.Background));

                task.Wait();

                if (game.BackgroundImage != task.Result)
                    game.BackgroundImage = task.Result;
            }

            // Features
            var features = ImportFeatures(manifest);

            game.FeatureIds = features.Select(f => f.Id).ToList();

            if (exists)
                Database.Games.Update(game);
            else
                Database.Games.Add(game);

            return game;
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

        private IEnumerable<GameFeature> ImportFeatures(GameManifest manifest)
        {
            if (manifest.Singleplayer)
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

            if (manifest.LocalMultiplayer != null || manifest.LanMultiplayer != null || manifest.OnlineMultiplayer != null)
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

            if (manifest.LocalMultiplayer != null)
            {
                var featureName = $"Local Multiplayer {manifest.LocalMultiplayer.GetPlayerCount()}".Trim();
                var feature = Database.Features.FirstOrDefault(f => f.Name == featureName);

                if (feature == null)
                {
                    feature = new GameFeature(featureName);

                    Database.Features.Add(feature);
                }

                yield return feature;
            }

            if (manifest.LanMultiplayer != null)
            {
                var featureName = $"LAN Multiplayer {manifest.LanMultiplayer.GetPlayerCount()}".Trim();
                var feature = Database.Features.FirstOrDefault(f => f.Name == featureName);

                if (feature == null)
                {
                    feature = new GameFeature(featureName);

                    Database.Features.Add(feature);
                }

                yield return feature;
            }

            if (manifest.OnlineMultiplayer != null)
            {
                var featureName = $"LAN Multiplayer {manifest.OnlineMultiplayer.GetPlayerCount()}".Trim();
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
