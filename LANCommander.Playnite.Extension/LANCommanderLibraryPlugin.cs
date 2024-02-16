using LANCommander.PlaynitePlugin.Extensions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.PowerShell;
using LANCommander.SDK.Extensions;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PN = Playnite;
using LANCommander.SDK;
using System.Reflection;
using LANCommander.PlaynitePlugin.Views;
using LANCommander.PlaynitePlugin.Models;
using LANCommander.PlaynitePlugin.Controls;
using System.Threading.Tasks;
using System.Net;
using Windows.Media.Capture;

namespace LANCommander.PlaynitePlugin
{
    public class LANCommanderLibraryPlugin : LibraryPlugin
    {
        public static readonly ILogger Logger = LogManager.GetLogger();
        internal LANCommanderSettingsViewModel Settings { get; set; }
        internal LANCommander.SDK.Client LANCommanderClient { get; set; }
        internal LANCommanderSaveController SaveController { get; set; }

        public override Guid Id { get; } = Guid.Parse("48e1bac7-e0a0-45d7-ba83-36f5e9e959fc");
        public override string Name => "LANCommander";

        public DownloadQueueController DownloadQueue { get; set; }
        public SidebarItem DownloadQueueSidebarItem { get; set; }

        public TopPanelItem OfflineModeTopPanelItem { get; set; }
        public TopPanelItem ProfileTopPanelItem { get; set; }

        public LANCommanderLibraryPlugin(IPlayniteAPI api) : base(api)
        {
            Properties = new LibraryPluginProperties
            {
                HasSettings = true,
                HasCustomizedGameImport = true,
            };

            Settings = new LANCommanderSettingsViewModel(this);
            LANCommanderClient = new SDK.Client(Settings.ServerAddress, Settings.InstallDirectory, new PlayniteLogger(Logger));
            LANCommanderClient.UseToken(new SDK.Models.AuthToken()
            {
                AccessToken = Settings.AccessToken,
                RefreshToken = Settings.RefreshToken,
            });

            #region Initialize Top Bar Items
            OfflineModeTopPanelItem = new TopPanelItem
            {
                Title = "Go Online",
                Icon = new TextBlock
                {
                    Text = char.ConvertFromUtf32(0xef3e),
                    FontSize = 16,
                    FontFamily = ResourceProvider.GetResource("FontIcoFont") as FontFamily,
                    Padding = new Thickness(10, 0, 10, 0),
                    Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#ff6b6b"),
                },
                Visible = Settings.OfflineModeEnabled,
                Activated = () =>
                {
                    ShowAuthenticationWindow();
                }
            };

            ProfileTopPanelItem = new TopPanelItem
            {
                Icon = new ProfileTopPanelItem(this)
            };
            #endregion

            ValidateConnection();

            Settings.Load();

            DownloadQueue = new DownloadQueueController(this);

            api.UriHandler.RegisterSource("lancommander", args =>
            {
                if (args.Arguments.Length == 0)
                    return;

                Guid gameId;

                switch (args.Arguments[0].ToLower())
                {
                    case "install":
                        if (args.Arguments.Length == 1)
                            break;

                        if (Guid.TryParse(args.Arguments[1], out gameId))
                            PlayniteApi.InstallGame(gameId);
                        break;

                    case "run":
                        if (args.Arguments.Length == 1)
                            break;

                        if (Guid.TryParse(args.Arguments[1], out gameId))
                            PlayniteApi.StartGame(gameId);
                        break;

                    case "connect":
                        if (args.Arguments.Length == 1)
                        {
                            ShowAuthenticationWindow();
                            break;
                        }

                        ShowAuthenticationWindow(HttpUtility.UrlDecode(args.Arguments[1]));
                        break;
                }

            });
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            Migrate();
        }

        public bool ValidateConnection()
        {
            LANCommanderClient.ValidateToken();

            if (LANCommanderClient.IsConnected())
            {
                OfflineModeTopPanelItem.Visible = false;
            }

            return LANCommanderClient.IsConnected();
        }

        public async Task Logout()
        {
            await LANCommanderClient.LogoutAsync();

            Settings.AccessToken = null;
            Settings.RefreshToken = null;
            Settings.PlayerAlias = null;
            Settings.PlayerName = null;
            Settings.PlayerAvatarUrl = null;

            SavePluginSettings(Settings);

            ShowAuthenticationWindow();
        }

        public override IEnumerable<Game> ImportGames(LibraryImportGamesArgs args)
        {
            if (Settings.OfflineModeEnabled)
                yield break;

            if (!ValidateConnection() && !Settings.OfflineModeEnabled)
            {
                Logger.Trace("Authentication invalid, showing auth window...");
                ShowAuthenticationWindow();

                if (!ValidateConnection() && !Settings.OfflineModeEnabled)
                {
                    Logger.Trace("User cancelled authentication.");

                    throw new Exception("You must set up a valid connection to a LANCommander server.");
                }
            }

            if (Settings.OfflineModeEnabled)
                yield break;

            var playSessions = LANCommanderClient.Profile.GetPlaySessions().Where(ps => ps.Start != null && ps.End != null).OrderByDescending(ps => ps.End);

            var manifests = LANCommanderClient.Games.Get();

            var genres = ImportGenres(manifests.Where(m => m.Genre != null).SelectMany(m => m.Genre).Distinct());
            var tags = ImportTags(manifests.Where(m => m.Tags != null).SelectMany(m => m.Tags).Distinct());
            var publishers = ImportCompanies(manifests.Where(m => m.Publishers != null).SelectMany(m => m.Publishers).Distinct());
            var developers = ImportCompanies(manifests.Where(m => m.Developers != null).SelectMany(m => m.Developers).Distinct());
            var collections = ImportCollections(manifests.Where(m => m.Collections != null).SelectMany(m => m.Collections).Distinct());

            foreach (var manifest in manifests)
            {
                bool exists = false;
                var game = PlayniteApi.Database.Games.Get(manifest.Id);

                if (game == null)
                    game = new Game();
                else
                    exists = true;

                game.Id = manifest.Id;
                game.GameId = manifest.Id.ToString();
                game.PluginId = this.Id;
                game.Name = manifest.Title;
                game.SortingName = manifest.SortTitle;
                game.Description = manifest.Description;
                game.ReleaseDate = new ReleaseDate(manifest.ReleasedOn);

                if (game.IsInstalled && game.Version != manifest.Version)
                {
                    if (!game.Name.EndsWith(" - Update Available"))
                        game.Name += " - Update Available";
                }

                #region Play Sessions
                var gamePlaySessions = playSessions.Where(ps => ps.GameId == game.Id);

                if (gamePlaySessions.Count() > 0)
                {
                    game.LastActivity = gamePlaySessions.First().End;
                    game.PlayCount = (ulong)gamePlaySessions.Count();
                    game.Playtime = (ulong)gamePlaySessions.Sum(ps => ps.End.Value.Subtract(ps.Start.Value).TotalSeconds);
                }
                #endregion

                #region Actions
                if (game.GameActions == null)
                    game.GameActions = new ObservableCollection<PN.SDK.Models.GameAction>();
                else
                    game.GameActions.Clear();

                if (manifest.Actions == null)
                {
                    Logger?.Warn($"Game {manifest.Title} does not have any actions defined and may not be playable");
                    continue;
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

                        game.GameActions.Add(new PN.SDK.Models.GameAction()
                        {
                            Name = action.Name,
                            Arguments = action.Arguments,
                            Path = actionPath,
                            WorkingDir = actionArguments,
                            IsPlayAction = false
                        });
                    }
                #endregion

                // Genres
                if (manifest.Genre != null)
                    game.GenreIds = genres.Where(g => manifest.Genre.Contains(g.Name)).Select(g => g.Id).ToList();

                // Tags
                if (manifest.Tags != null)
                    game.TagIds = tags.Where(t => manifest.Tags.Contains(t.Name)).Select(t => t.Id).ToList();

                // Publishers
                if (manifest.Publishers != null)
                    game.PublisherIds = publishers.Where(p => manifest.Publishers.Contains(p.Name)).Select(p => p.Id).ToList();

                // Developers
                if (manifest.Developers != null)
                    game.DeveloperIds = developers.Where(d => manifest.Developers.Contains(d.Name)).Select(d => d.Id).ToList();

                // Collections
                if (manifest.Collections != null)
                    game.CategoryIds = collections.Where(c => manifest.Collections.Contains(c.Name)).Select(c => c.Id).ToList();

                // Media
                if (manifest.Media != null && manifest.Media.Any(m => m.Type == SDK.Enums.MediaType.Icon))
                    game.Icon = LANCommanderClient.GetMediaUrl(manifest.Media.First(m => m.Type == SDK.Enums.MediaType.Icon));

                if (manifest.Media != null && manifest.Media.Any(m => m.Type == SDK.Enums.MediaType.Cover))
                    game.CoverImage = LANCommanderClient.GetMediaUrl(manifest.Media.First(m => m.Type == SDK.Enums.MediaType.Cover));

                if (manifest.Media != null && manifest.Media.Any(m => m.Type == SDK.Enums.MediaType.Background))
                    game.BackgroundImage = LANCommanderClient.GetMediaUrl(manifest.Media.First(m => m.Type == SDK.Enums.MediaType.Background));

                // Features
                var features = ImportFeatures(manifest);

                game.FeatureIds = features.Select(f => f.Id).ToList();

                if (exists)
                    PlayniteApi.Database.Games.Update(game);
                else
                    PlayniteApi.Database.Games.Add(game);

                yield return game;
            }

            #region Cleanup
            // Clean up any games we don't have access to
            var gamesToRemove = PlayniteApi.Database.Games.Where(g => g.PluginId == Id && !manifests.Any(lg => lg.Id.ToString() == g.GameId)).ToList();

            PlayniteApi.Database.Games.Remove(gamesToRemove);
            #endregion
        }

        private IEnumerable<Genre> ImportGenres(IEnumerable<string> genreNames)
        {
            foreach (var genreName in genreNames)
            {
                var genre = PlayniteApi.Database.Genres.FirstOrDefault(g => g.Name == genreName);

                if (genre == null)
                {
                    genre = new Genre(genreName);

                    PlayniteApi.Database.Genres.Add(genre);
                }

                yield return genre;
            }
        }

        private IEnumerable<Tag> ImportTags(IEnumerable<string> tagNames)
        {
            foreach (var tagName in tagNames)
            {
                var tag = PlayniteApi.Database.Tags.FirstOrDefault(t => t.Name == tagName);

                if (tag == null)
                {
                    tag = new Tag(tagName);

                    PlayniteApi.Database.Tags.Add(tag);
                }

                yield return tag;
            }
        }

        private IEnumerable<Company> ImportCompanies(IEnumerable<string> companyNames)
        {
            foreach (var companyName in companyNames)
            {
                var company = PlayniteApi.Database.Companies.FirstOrDefault(c => c.Name == companyName);

                if (company == null)
                {
                    company = new Company(companyName);

                    PlayniteApi.Database.Companies.Add(company);
                }

                yield return company;
            }
        }

        private IEnumerable<Category> ImportCollections(IEnumerable<string> collectionNames)
        {
            foreach (var collectionName in collectionNames)
            {
                var category = PlayniteApi.Database.Categories.FirstOrDefault(c => c.Name == collectionName);

                if (category == null)
                {
                    category = new Category(collectionName);

                    PlayniteApi.Database.Categories.Add(category);
                }

                yield return category;
            }
        }

        private IEnumerable<GameFeature> ImportFeatures(GameManifest manifest)
        {
            if (manifest.Singleplayer)
            {
                var featureName = $"Singleplayer";
                var feature = PlayniteApi.Database.Features.FirstOrDefault(f => f.Name == featureName);

                if (feature == null)
                {
                    feature = new GameFeature(featureName);

                    PlayniteApi.Database.Features.Add(feature);
                }

                yield return feature;
            }

            if (manifest.LocalMultiplayer != null || manifest.LanMultiplayer != null || manifest.OnlineMultiplayer != null)
            {
                var featureName = $"Multiplayer";
                var feature = PlayniteApi.Database.Features.FirstOrDefault(f => f.Name == featureName);

                if (feature == null)
                {
                    feature = new GameFeature(featureName);

                    PlayniteApi.Database.Features.Add(feature);
                }

                yield return feature;
            }

            if (manifest.LocalMultiplayer != null)
            {
                var featureName = $"Local Multiplayer {manifest.LocalMultiplayer.GetPlayerCount()}".Trim();
                var feature = PlayniteApi.Database.Features.FirstOrDefault(f => f.Name == featureName);

                if (feature == null)
                {
                    feature = new GameFeature(featureName);

                    PlayniteApi.Database.Features.Add(feature);
                }

                yield return feature;
            }

            if (manifest.LanMultiplayer != null)
            {
                var featureName = $"LAN Multiplayer {manifest.LanMultiplayer.GetPlayerCount()}".Trim();
                var feature = PlayniteApi.Database.Features.FirstOrDefault(f => f.Name == featureName);

                if (feature == null)
                {
                    feature = new GameFeature(featureName);

                    PlayniteApi.Database.Features.Add(feature);
                }

                yield return feature;
            }

            if (manifest.OnlineMultiplayer != null)
            {
                var featureName = $"LAN Multiplayer {manifest.OnlineMultiplayer.GetPlayerCount()}".Trim();
                var feature = PlayniteApi.Database.Features.FirstOrDefault(f => f.Name == featureName);

                if (feature == null)
                {
                    feature = new GameFeature(featureName);

                    PlayniteApi.Database.Features.Add(feature);
                }

                yield return feature;
            }
        }

        public override IEnumerable<InstallController> GetInstallActions(GetInstallActionsArgs args)
        {
            if (args.Game.PluginId != Id)
                yield break;

            if (Settings.OfflineModeEnabled)
            {
                PlayniteApi.Dialogs.ShowErrorMessage("You must connect to a LANCommander server to install this game.", "Offline Mode Enabled");
            }

            yield return new LANCommanderInstallController(this, args.Game);
        }

        public override IEnumerable<UninstallController> GetUninstallActions(GetUninstallActionsArgs args)
        {
            if (args.Game.PluginId != Id)
                yield break;

            yield return new LANCommanderUninstallController(this, args.Game);
        }

        public override IEnumerable<PlayController> GetPlayActions(GetPlayActionsArgs args)
        {
            if (Guid.TryParse(args.Game.GameId, out var gameId))
            {
                var manifest = ManifestHelper.Read(args.Game.InstallDirectory, gameId);
                var primaryDisplay = System.Windows.Forms.Screen.AllScreens.FirstOrDefault(s => s.Primary);

                LANCommanderClient.Actions.AddVariable("DisplayWidth", primaryDisplay.Bounds.Width.ToString());
                LANCommanderClient.Actions.AddVariable("DisplayHeight", primaryDisplay.Bounds.Height.ToString());

                foreach (var action in manifest.Actions.Where(a => a.IsPrimaryAction).OrderBy(a => a.SortOrder))
                {
                    yield return new AutomaticPlayController(args.Game)
                    {
                        Arguments = LANCommanderClient.Actions.ExpandVariables(action.Arguments, args.Game.InstallDirectory, skipSlashes: true),
                        Name = action.Name,
                        Path = LANCommanderClient.Actions.ExpandVariables(action.Path, args.Game.InstallDirectory),
                        TrackingMode = TrackingMode.Default,
                        Type = AutomaticPlayActionType.File,
                        WorkingDir = LANCommanderClient.Actions.ExpandVariables(action.WorkingDirectory, args.Game.InstallDirectory)
                    };
                }

                if (!Settings.OfflineModeEnabled)
                {
                    var game = LANCommanderClient.Games.Get(gameId);

                    if (game.Servers != null)
                    foreach (var server in game.Servers.Where(s => s.Actions != null))
                    {
                        foreach (var action in server.Actions)
                        {
                            var serverHost = String.IsNullOrWhiteSpace(server.Host) ? new Uri(Settings.ServerAddress).Host : server.Host;
                            var serverIp = Dns.GetHostEntry(serverHost).AddressList.FirstOrDefault();

                            var variables = new Dictionary<string, string>()
                            {
                                { "ServerHost", serverHost },
                                { "ServerPort", server.Port.ToString() }
                            };

                            if (serverIp != null)
                                variables["ServerIP"] = serverIp.ToString();

                            yield return new AutomaticPlayController(args.Game)
                            {
                                Arguments = LANCommanderClient.Actions.ExpandVariables(action.Arguments, args.Game.InstallDirectory, variables, true),
                                Name = action.Name,
                                Path = LANCommanderClient.Actions.ExpandVariables(action.Path, args.Game.InstallDirectory, variables),
                                TrackingMode = TrackingMode.Default,
                                Type = AutomaticPlayActionType.File,
                                WorkingDir = LANCommanderClient.Actions.ExpandVariables(action.WorkingDirectory, args.Game.InstallDirectory, variables)
                            };
                        }
                    }
                }
            }
        }

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            Logger.Trace("Populating game menu items...");

            yield return new GameMenuItem
            {
                Description = "Add to Download Queue",
                Action = (args2) =>
                {
                    foreach (var game in args2.Games)
                        DownloadQueue.Add(game);
                }
            };

            if (args.Games.Count == 1 && args.Games.First().IsInstalled && !String.IsNullOrWhiteSpace(args.Games.First().InstallDirectory))
            {
                var game = args.Games.First();

                var nameChangeScriptPath = ScriptHelper.GetScriptFilePath(game.InstallDirectory, Guid.Parse(game.GameId), SDK.Enums.ScriptType.NameChange);
                var keyChangeScriptPath = ScriptHelper.GetScriptFilePath(game.InstallDirectory, Guid.Parse(game.GameId), SDK.Enums.ScriptType.KeyChange);
                var installScriptPath = ScriptHelper.GetScriptFilePath(game.InstallDirectory, Guid.Parse(game.GameId), SDK.Enums.ScriptType.Install);

                if (File.Exists(nameChangeScriptPath))
                {
                    Logger.Trace($"Name change script found at path {nameChangeScriptPath}");

                    yield return new GameMenuItem
                    {
                        Description = "Change Player Name",
                        Action = (nameChangeArgs) =>
                        {
                            var oldName = Settings.DisplayName;

                            var result = PlayniteApi.Dialogs.SelectString("Enter your player name", "Change Player Name", oldName);

                            if (result.Result == true)
                            {
                                var nameChangeGame = nameChangeArgs.Games.First();

                                if (Guid.TryParse(nameChangeGame.GameId, out var gameId))
                                {
                                    RunNameChangeScript(nameChangeGame.InstallDirectory, gameId, oldName, result.SelectedString);

                                    var alias = LANCommanderClient.Profile.ChangeAlias(result.SelectedString);

                                    Settings.DisplayName = alias;

                                    SavePluginSettings(Settings);
                                }
                            }
                        }
                    };
                }

                if (File.Exists(keyChangeScriptPath))
                {
                    Logger.Trace($"Key change script found at path {keyChangeScriptPath}");

                    yield return new GameMenuItem
                    {
                        Description = "Change Game Key",
                        Action = (keyChangeArgs) =>
                        {
                            Guid gameId;

                            if (Guid.TryParse(keyChangeArgs.Games.First().GameId, out gameId))
                            {
                                // NUKIEEEE
                                var newKey = LANCommanderClient.Games.GetNewKey(gameId);

                                if (String.IsNullOrEmpty(newKey))
                                    PlayniteApi.Dialogs.ShowErrorMessage("There are no more keys available on the server.", "No Keys Available");
                                else
                                    RunKeyChangeScript(keyChangeArgs.Games.First().InstallDirectory, gameId, newKey);
                            }
                            else
                            {
                                PlayniteApi.Dialogs.ShowErrorMessage("This game could not be found on the server. Your game may be corrupted.");
                            }
                        }
                    };
                }

                if (File.Exists(installScriptPath))
                {
                    Logger.Trace($"Install script found at path {installScriptPath}");

                    yield return new GameMenuItem
                    {
                        Description = "Run Install Script",
                        Action = (installArgs) =>
                        {
                            Guid gameId;

                            if (Guid.TryParse(installArgs.Games.First().GameId, out gameId))
                                RunInstallScript(installArgs.Games.First().InstallDirectory, gameId);
                            else
                                PlayniteApi.Dialogs.ShowErrorMessage("This game could not be found on the server. Your game may be corrupted.");
                        }
                    };
                }
            }
        }

        // To add new main menu items override GetMainMenuItems
        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            yield return new MainMenuItem
            {
                Description = "Change Player Name (All Games)",
                Action = (args2) =>
                {
                    ShowNameChangeWindow();
                }
            };
        }

        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
            if (args.Game.PluginId == Id)
            {
                var gameId = Guid.Parse(args.Game.GameId);
                var currentGamePlayerAlias = GameService.GetPlayerAlias(args.Game.InstallDirectory, gameId);
                var currentGameKey = GameService.GetCurrentKey(args.Game.InstallDirectory, gameId);

                if (currentGamePlayerAlias != Settings.DisplayName)
                {
                    RunNameChangeScript(args.Game.InstallDirectory, gameId, currentGamePlayerAlias, Settings.DisplayName);
                }

                if (!Settings.OfflineModeEnabled && LANCommanderClient.IsConnected())
                {
                    var allocatedKey = LANCommanderClient.Games.GetAllocatedKey(gameId);

                    if (currentGameKey != allocatedKey)
                        RunKeyChangeScript(args.Game.InstallDirectory, gameId, allocatedKey);
                }

                if (!Settings.OfflineModeEnabled && LANCommanderClient.IsConnected())
                {
                    // This would be better served by some metadata
                    if (args.Game.Name.EndsWith(" - Update Available"))
                    {
                        var title = args.Game.Name.Replace(" - Update Available", "");
                        var updateMessage = $"An update for {title} is available. Would you like to update now?";
                        var result = PlayniteApi.Dialogs.ShowMessage(updateMessage, "Update Available", MessageBoxButton.YesNo);

                        if (result == MessageBoxResult.Yes)
                        {
                            args.CancelStartup = true;

                            PlayniteApi.InstallGame(args.Game.Id);

                            return;
                        }
                    }

                    LANCommanderClient.Games.StartPlaySession(gameId);

                    try
                    {
                        var latestSave = LANCommanderClient.Saves.GetLatest(gameId);

                        if (latestSave == null || (latestSave.CreatedOn > args.Game.LastActivity && latestSave.CreatedOn > args.Game.Added))
                        {
                            SaveController = new LANCommanderSaveController(this, args.Game);
                            SaveController.Download(gameId);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger?.Error(ex, "Could not download save");
                    }
                }
            }

        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            if (args.Game.PluginId == Id && !Settings.OfflineModeEnabled)
            {
                var gameId = Guid.Parse(args.Game.GameId);

                LANCommanderClient.Games.EndPlaySession(gameId);

                try
                {
                    SaveController = new LANCommanderSaveController(this, args.Game);
                    SaveController.Upload(gameId);
                }
                catch (Exception ex)
                {
                    Logger?.Error(ex, "Could not upload save");
                }
            }
        }

        public override IEnumerable<TopPanelItem> GetTopPanelItems()
        {
            yield return OfflineModeTopPanelItem;
            yield return ProfileTopPanelItem;
        }

        public override IEnumerable<SidebarItem> GetSidebarItems()
        {
            DownloadQueueSidebarItem = new SidebarItem
            {
                Title = "Downloads",
                Icon = new TextBlock
                {
                    Text = char.ConvertFromUtf32(0xef08),
                    FontFamily = ResourceProvider.GetResource("FontIcoFont") as FontFamily,
                },
                Type = SiderbarItemType.View,
                Opened = () =>
                {
                    var view = new Views.DownloadQueue(this);

                    view.DataContext = DownloadQueue;

                    return view;
                }
            };

            yield return DownloadQueueSidebarItem;
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return Settings;
        }

        public override UserControl GetSettingsView(bool firstRunView)
        {
            return new LANCommanderSettingsView(this);
        }

        public void ShowNameChangeWindow()
        {
            Logger.Trace("Showing name change dialog!");

            var oldName = Settings.DisplayName;

            var result = PlayniteApi.Dialogs.SelectString("Enter your new player name", "Enter Name", oldName);

            if (result.Result == true)
            {
                Logger.Trace($"New name entered was \"{result.SelectedString}\"");

                // Check to make sure they're staying in ASCII encoding
                if (String.IsNullOrEmpty(result.SelectedString) || result.SelectedString.Any(c => c > sbyte.MaxValue))
                {
                    PlayniteApi.Dialogs.ShowErrorMessage("The name you supplied is invalid. Try again.");

                    Logger.Trace("An invalid name was specified. Showing the name dialog again...");

                    ShowNameChangeWindow();
                }
                else
                {
                    Settings.DisplayName = result.SelectedString;

                    Logger.Trace($"New player name of \"{Settings.DisplayName}\" has been set!");

                    Logger.Trace("Saving plugin settings!");
                    SavePluginSettings(Settings);

                    var games = PlayniteApi.Database.Games.Where(g => g.IsInstalled).ToList();

                    LANCommanderClient.Profile.ChangeAlias(result.SelectedString);
                }
            }
            else
                Logger.Trace("Name change was cancelled");
        }

        public Window ShowAuthenticationWindow(string serverAddress = null, EventHandler onClose = null)
        {
            Window window = null;
            Application.Current.Dispatcher.Invoke((Action)delegate
            {
                window = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions()
                {
                    ShowMinimizeButton = false,
                });

                window.Title = "Authenticate to LANCommander";
                window.SizeToContent = SizeToContent.WidthAndHeight;
                window.MinWidth = 400;
                window.Content = new Views.Authentication(this);
                window.DataContext = new ViewModels.Authentication()
                {
                    ServerAddress = serverAddress ?? Settings?.ServerAddress
                };

                window.Owner = PlayniteApi.Dialogs.GetCurrentAppWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ResizeMode = ResizeMode.NoResize;

                if (onClose != null)
                    window.Closed += onClose;

                window.ShowDialog();
            });

            return window;
        }

        public void UpdateGame(SDK.GameManifest manifest, string installDirectory, IEnumerable<SDK.Models.PlaySession> gamePlaySessions)
        {
            var game = PlayniteApi.Database.Games.FirstOrDefault(g => g.GameId == manifest?.Id.ToString());

            if (game == null)
                return;

            #region Basic Game Info
            game.Name = manifest.Title;
            game.SortingName = manifest.SortTitle;
            game.Description = manifest.Description;
            game.ReleaseDate = new ReleaseDate(manifest.ReleasedOn);
            game.Notes = manifest.Notes;
            #endregion

            #region Versioning
            if (game.IsInstalled && game.Version != manifest.Version)
                if (!game.Name.EndsWith(" - Update Available"))
                    game.Name += " - Update Available";
            else
                game.Name = manifest.Title;
            #endregion

            #region Play Sessions
            if (gamePlaySessions.Count() > 0)
            {
                game.LastActivity = gamePlaySessions.First().End;
                game.PlayCount = (ulong)gamePlaySessions.Count();
                game.Playtime = (ulong)gamePlaySessions.Sum(ps => ps.End.Value.Subtract(ps.Start.Value).TotalSeconds);
            }
            #endregion

            #region Actions
            if (game.GameActions == null)
                game.GameActions = new ObservableCollection<PN.SDK.Models.GameAction>();
            else
                game.GameActions.Clear();

            if (manifest.Actions == null)
                throw new Exception("The game has no actions defined.");

            foreach (var action in game.GameActions.Where(a => a.IsPlayAction))
                game.GameActions.Remove(action);

            if (game.IsInstalled || game.IsInstalling)
                foreach (var action in manifest.Actions.OrderBy(a => a.SortOrder).Where(a => !a.IsPrimaryAction))
                {
                    var actionPath = action.Path?.ExpandEnvironmentVariables(installDirectory);
                    var actionWorkingDir = String.IsNullOrWhiteSpace(action.WorkingDirectory) ? installDirectory : action.WorkingDirectory.ExpandEnvironmentVariables(installDirectory);
                    var actionArguments = action.Arguments?.ExpandEnvironmentVariables(installDirectory);

                    if (actionPath.StartsWith(actionWorkingDir))
                        actionPath = actionPath.Substring(actionWorkingDir.Length).TrimStart(Path.DirectorySeparatorChar);

                    game.GameActions.Add(new PN.SDK.Models.GameAction()
                    {
                        Name = action.Name,
                        Arguments = action.Arguments,
                        Path = actionPath,
                        WorkingDir = actionArguments,
                        IsPlayAction = false
                    });
                }
            #endregion

            #region Features
            var singlePlayerFeature = PlayniteApi.Database.Features.FirstOrDefault(f => f.Name == "Single Player");

            if (manifest.LanMultiplayer != null)
            {
                var multiplayerInfo = manifest.LanMultiplayer;

                string playerCount = multiplayerInfo.MinPlayers == multiplayerInfo.MaxPlayers ? $"({multiplayerInfo.MinPlayers} players)" : $"({multiplayerInfo.MinPlayers} - {multiplayerInfo.MaxPlayers} players)";
                string featureName = $"LAN Multiplayer {playerCount}";

                if (PlayniteApi.Database.Features.Any(f => f.Name == featureName))
                {
                    game.Features.Add(PlayniteApi.Database.Features.FirstOrDefault(f => f.Name == featureName));
                }
                else
                {
                    PlayniteApi.Database.Features.Add(new GameFeature()
                    {
                        Name = featureName
                    });

                    game.Features.Add(new GameFeature()
                    {
                        Name = $"LAN Multiplayer {playerCount}"
                    });
                }
            }

            if (manifest.LocalMultiplayer != null)
            {
                var multiplayerInfo = manifest.LocalMultiplayer;

                string playerCount = multiplayerInfo.MinPlayers == multiplayerInfo.MaxPlayers ? $"({multiplayerInfo.MinPlayers} players)" : $"({multiplayerInfo.MinPlayers} - {multiplayerInfo.MaxPlayers} players)";

                game.Features.Add(new GameFeature()
                {
                    Name = $"Local Multiplayer {playerCount}"
                });
            }

            if (manifest.OnlineMultiplayer != null)
            {
                var multiplayerInfo = manifest.OnlineMultiplayer;

                string playerCount = multiplayerInfo.MinPlayers == multiplayerInfo.MaxPlayers ? $"({multiplayerInfo.MinPlayers} players)" : $"({multiplayerInfo.MinPlayers} - {multiplayerInfo.MaxPlayers} players)";

                game.Features.Add(new GameFeature()
                {
                    Name = $"Online Multiplayer {playerCount}"
                });
            }
            #endregion

            PlayniteApi.Database.Games.Update(game);
        }

        private int RunInstallScript(string installDirectory, Guid gameId)
        {
            var manifest = ManifestHelper.Read(installDirectory, gameId);
            var path = ScriptHelper.GetScriptFilePath(installDirectory, gameId, SDK.Enums.ScriptType.Install);

            if (File.Exists(path))
            {
                var script = new PowerShellScript();

                script.AddVariable("InstallDirectory", installDirectory);
                script.AddVariable("GameManifest", manifest);
                script.AddVariable("DefaultInstallDirectory", Settings.InstallDirectory);
                script.AddVariable("ServerAddress", Settings.ServerAddress);

                script.UseFile(path);

                return script.Execute();
            }

            return 0;
        }

        private int RunNameChangeScript(string installDirectory, Guid gameId, string oldPlayerAlias, string newPlayerAlias)
        {
            var manifest = ManifestHelper.Read(installDirectory, gameId);
            var path = ScriptHelper.GetScriptFilePath(installDirectory, gameId, SDK.Enums.ScriptType.NameChange);

            if (File.Exists(path))
            {
                var script = new PowerShellScript();

                script.AddVariable("InstallDirectory", installDirectory);
                script.AddVariable("GameManifest", manifest);
                script.AddVariable("DefaultInstallDirectory", Settings.InstallDirectory);
                script.AddVariable("ServerAddress", Settings.ServerAddress);
                script.AddVariable("OldPlayerAlias", oldPlayerAlias);
                script.AddVariable("NewPlayerAlias", newPlayerAlias);

                script.UseFile(path);

                GameService.UpdatePlayerAlias(installDirectory, gameId, newPlayerAlias);

                return script.Execute();
            }

            return 0;
        }

        private int RunKeyChangeScript(string installDirectory, Guid gameId, string key = "")
        {
            var manifest = ManifestHelper.Read(installDirectory, gameId);
            var path = ScriptHelper.GetScriptFilePath(installDirectory, gameId, SDK.Enums.ScriptType.KeyChange);

            if (File.Exists(path))
            {
                var script = new PowerShellScript();

                if (String.IsNullOrEmpty(key))
                    key = LANCommanderClient.Games.GetAllocatedKey(manifest.Id);

                script.AddVariable("InstallDirectory", installDirectory);
                script.AddVariable("GameManifest", manifest);
                script.AddVariable("DefaultInstallDirectory", Settings.InstallDirectory);
                script.AddVariable("ServerAddress", Settings.ServerAddress);
                script.AddVariable("AllocatedKey", key);

                script.UseFile(path);

                GameService.UpdateCurrentKey(installDirectory, gameId, key);

                return script.Execute();
            }

            return 0;
        }

        void Migrate()
        {  
            #region Old Manifest Locations
            var installedGames = PlayniteApi.Database.Games.Where(g => g.IsInstalled && !String.IsNullOrWhiteSpace(g.InstallDirectory) && g.PluginId == Id).ToList();

            foreach (var game in installedGames)
            {
                var gameId = Guid.Parse(game.GameId);

                if (!Directory.Exists(GameService.GetMetadataDirectoryPath(game.InstallDirectory, gameId)))
                    Directory.CreateDirectory(GameService.GetMetadataDirectoryPath(game.InstallDirectory, gameId));

                var metaFiles = new Dictionary<string, string>()
                {
                    { "_manifest.yml", "Manifest.yml" },
                    { "_install.ps1", "Install.ps1" },
                    { "_uninstall.ps1", "Uninstall.ps1" },
                    { "_changename.ps1", "ChangeName.ps1" },
                    { "_changekey.ps1", "ChangeKey.ps1" },
                };

                foreach (var file in metaFiles)
                {
                    var originalPath = Path.Combine(game.InstallDirectory, file.Key);
                    var destinationPath = GameService.GetMetadataFilePath(game.InstallDirectory, gameId, file.Value);

                    if (File.Exists(originalPath) && !File.Exists(destinationPath))
                        File.Move(originalPath, destinationPath);
                }
            }
            #endregion
        }
    }
}
