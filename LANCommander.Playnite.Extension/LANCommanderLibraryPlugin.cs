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

        internal Dictionary<Guid, string> DownloadCache = new Dictionary<Guid, string>();

        public LANCommanderLibraryPlugin(IPlayniteAPI api) : base(api)
        {
            Properties = new LibraryPluginProperties
            {
                HasSettings = true,
            };

            Settings = new LANCommanderSettingsViewModel(this);

            LANCommanderClient = new SDK.Client(Settings.ServerAddress, new PlayniteLogger(Logger));
            LANCommanderClient.UseToken(new SDK.Models.AuthToken()
            {
                AccessToken = Settings.AccessToken,
                RefreshToken = Settings.RefreshToken,
            });

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

        public bool ValidateConnection()
        {
            return LANCommanderClient.ValidateToken();
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            var gameMetadata = new List<GameMetadata>();

            if (!ValidateConnection())
            {
                Logger.Trace("Authentication invalid, showing auth window...");
                ShowAuthenticationWindow();

                if (!ValidateConnection())
                {
                    Logger.Trace("User cancelled authentication.");

                    throw new Exception("You must set up a valid connection to a LANCommander server.");
                }
            }

            var games = LANCommanderClient
                .GetGames()
                .Where(g => g != null && g.Archives != null && g.Archives.Count() > 0);

            foreach (var game in games)
            {
                try
                {
                    Logger.Trace($"Importing/updating metadata for game \"{game.Title}\"...");

                    var manifest = LANCommanderClient.GetGameManifest(game.Id);
                    Logger.Trace("Successfully grabbed game manifest");

                    var existingGame = PlayniteApi.Database.Games.FirstOrDefault(g => g.GameId == game.Id.ToString() && g.PluginId == Id && g.IsInstalled);

                    if (existingGame != null)
                    {
                        Logger.Trace("Game already exists in library, updating metadata...");

                        UpdateGame(manifest);

                        continue;
                    }

                    Logger.Trace("Game does not exist in the library, importing metadata...");

                    var metadata = new GameMetadata()
                    {
                        IsInstalled = false,
                        Name = manifest.Title,
                        SortingName = manifest.SortTitle,
                        Description = manifest.Description,
                        GameId = game.Id.ToString(),
                        ReleaseDate = new ReleaseDate(manifest.ReleasedOn),
                        //Version = game.Archives.OrderByDescending(a => a.CreatedOn).FirstOrDefault().Version,
                        GameActions = game.Actions.OrderBy(a => a.SortOrder).Select(a => new PN.SDK.Models.GameAction()
                        {
                            Name = a.Name,
                            Arguments = a.Arguments,
                            Path = a.Path,
                            WorkingDir = a.WorkingDirectory,
                            IsPlayAction = a.PrimaryAction
                        }).ToList()
                    };

                    if (manifest.Genre != null && manifest.Genre.Count() > 0)
                        metadata.Genres = new HashSet<MetadataProperty>(manifest.Genre.Select(g => new MetadataNameProperty(g)));

                    if (manifest.Developers != null && manifest.Developers.Count() > 0)
                        metadata.Developers = new HashSet<MetadataProperty>(manifest.Developers.Select(d => new MetadataNameProperty(d)));

                    if (manifest.Publishers != null && manifest.Publishers.Count() > 0)
                        metadata.Publishers = new HashSet<MetadataProperty>(manifest.Publishers.Select(p => new MetadataNameProperty(p)));

                    if (manifest.Tags != null && manifest.Tags.Count() > 0)
                        metadata.Tags = new HashSet<MetadataProperty>(manifest.Tags.Select(t => new MetadataNameProperty(t)));

                    metadata.Features = new HashSet<MetadataProperty>();

                    if (manifest.Singleplayer)
                        metadata.Features.Add(new MetadataNameProperty("Singleplayer"));

                    if (manifest.LocalMultiplayer != null)
                        metadata.Features.Add(new MetadataNameProperty($"Local Multiplayer {manifest.LocalMultiplayer.GetPlayerCount()}".Trim()));

                    if (manifest.LanMultiplayer != null)
                        metadata.Features.Add(new MetadataNameProperty($"LAN Multiplayer {manifest.LanMultiplayer.GetPlayerCount()}".Trim()));

                    if (manifest.OnlineMultiplayer != null)
                        metadata.Features.Add(new MetadataNameProperty($"Online Multiplayer {manifest.OnlineMultiplayer.GetPlayerCount()}".Trim()));

                    if (game.Media.Any(m => m.Type == SDK.Enums.MediaType.Icon))
                        metadata.Icon = new MetadataFile(LANCommanderClient.GetMediaUrl(game.Media.First(m => m.Type == SDK.Enums.MediaType.Icon)));

                    if (game.Media.Any(m => m.Type == SDK.Enums.MediaType.Cover))
                        metadata.CoverImage = new MetadataFile(LANCommanderClient.GetMediaUrl(game.Media.First(m => m.Type == SDK.Enums.MediaType.Cover)));

                    if (game.Media.Any(m => m.Type == SDK.Enums.MediaType.Background))
                        metadata.BackgroundImage = new MetadataFile(LANCommanderClient.GetMediaUrl(game.Media.First(m => m.Type == SDK.Enums.MediaType.Background)));

                    gameMetadata.Add(metadata);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Could not update game \"{game.Title}\" in library");
                }
            };

            // Clean up any games we don't have access to
            var gamesToRemove = PlayniteApi.Database.Games.Where(g => g.PluginId == Id && !games.Any(lg => lg.Id.ToString() == g.GameId)).ToList();

            PlayniteApi.Database.Games.Remove(gamesToRemove);

            return gameMetadata;
        }

        public override IEnumerable<InstallController> GetInstallActions(GetInstallActionsArgs args)
        {
            if (args.Game.PluginId != Id)
                yield break;

            yield return new LANCommanderInstallController(this, args.Game);
        }

        public override IEnumerable<UninstallController> GetUninstallActions(GetUninstallActionsArgs args)
        {
            if (args.Game.PluginId != Id)
                yield break;

            yield return new LANCommanderUninstallController(this, args.Game);
        }

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            Logger.Trace("Populating game menu items...");

            if (args.Games.Count == 1 && args.Games.First().IsInstalled && !String.IsNullOrWhiteSpace(args.Games.First().InstallDirectory))
            {
                var nameChangeScriptPath = ScriptHelper.GetScriptFilePath(args.Games.First().InstallDirectory, SDK.Enums.ScriptType.NameChange);
                var keyChangeScriptPath = ScriptHelper.GetScriptFilePath(args.Games.First().InstallDirectory, SDK.Enums.ScriptType.KeyChange);
                var installScriptPath = ScriptHelper.GetScriptFilePath(args.Games.First().InstallDirectory, SDK.Enums.ScriptType.Install);

                if (File.Exists(nameChangeScriptPath))
                {
                    Logger.Trace($"Name change script found at path {nameChangeScriptPath}");

                    yield return new GameMenuItem
                    {
                        Description = "Change Player Name",
                        Action = (nameChangeArgs) =>
                        {
                            var oldName = Settings.PlayerName;

                            var result = PlayniteApi.Dialogs.SelectString("Enter your player name", "Change Player Name", Settings.PlayerName);

                            if (result.Result == true)
                            {
                                var game = nameChangeArgs.Games.First();

                                RunNameChangeScript(game.InstallDirectory, oldName, result.SelectedString);

                                LANCommanderClient.ChangeAlias(result.SelectedString);
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
                                var newKey = LANCommanderClient.GetNewKey(gameId);

                                if (String.IsNullOrEmpty(newKey))
                                    PlayniteApi.Dialogs.ShowErrorMessage("There are no more keys available on the server.", "No Keys Available");
                                else
                                    RunKeyChangeScript(keyChangeArgs.Games.First().InstallDirectory, newKey);
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
                                RunInstallScript(installArgs.Games.First().InstallDirectory);
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

            yield return new MainMenuItem
            {
                Description = "Clear Download Cache",
                Action = (args2) =>
                {
                    foreach (var gameId in DownloadCache.Keys)
                    {
                        File.Delete(DownloadCache[gameId]);
                        DownloadCache.Remove(gameId);
                    }

                    PlayniteApi.Dialogs.ShowMessage("The download cache has been cleared and any temporary files have been deleted.", "Cache Cleared!", MessageBoxButton.OK);
                }
            };
        }

        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
            if (args.Game.PluginId == Id)
            {
                var gameId = Guid.Parse(args.Game.GameId);

                LANCommanderClient.StartPlaySession(gameId);

                try
                {
                    SaveController = new LANCommanderSaveController(this, args.Game);
                    SaveController.Download(args.Game);
                }
                catch (Exception ex)
                {
                    Logger?.Error(ex, "Could not download save");
                }
            }
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            if (args.Game.PluginId == Id)
            {
                var gameId = Guid.Parse(args.Game.GameId);

                LANCommanderClient.EndPlaySession(gameId);

                try
                {
                    SaveController = new LANCommanderSaveController(this, args.Game);
                    SaveController.Upload(args.Game);
                }
                catch (Exception ex)
                {
                    Logger?.Error(ex, "Could not upload save");
                }
            }
        }

        public override IEnumerable<TopPanelItem> GetTopPanelItems()
        {
            yield return new TopPanelItem
            {
                Title = "Click to change your name (All Games)",
                Icon = new TextBlock
                {
                    Text = char.ConvertFromUtf32(0xeded),
                    FontSize = 16,
                    FontFamily = ResourceProvider.GetResource("FontIcoFont") as FontFamily,
                    Padding = new Thickness(10, 0, 10, 0),

                },
                Activated = () =>
                {
                    ShowNameChangeWindow();
                }
            };
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

            var result = PlayniteApi.Dialogs.SelectString("Enter your new player name. This will change your name across all installed games!", "Enter Name", Settings.PlayerName);

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
                    var oldName = Settings.PlayerName;

                    Settings.PlayerName = result.SelectedString;

                    Logger.Trace($"New player name of \"{Settings.PlayerName}\" has been set!");

                    Logger.Trace("Saving plugin settings!");
                    SavePluginSettings(Settings);

                    var games = PlayniteApi.Database.Games.Where(g => g.IsInstalled).ToList();

                    LANCommanderClient.ChangeAlias(result.SelectedString);

                    Logger.Trace($"Running name change scripts across {games.Count} installed game(s)");

                    foreach (var game in games)
                    {
                        var script = new PowerShellScript();

                        script.AddVariable("OldName", oldName);
                        script.AddVariable("NewName", Settings.PlayerName);

                        script.UseFile(ScriptHelper.GetScriptFilePath(game.InstallDirectory, SDK.Enums.ScriptType.NameChange));

                        script.Execute();
                    }
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
                window.Width = 400;
                window.Height = 230;
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

        public void UpdateGame(SDK.GameManifest manifest)
        {
            var game = PlayniteApi.Database.Games.FirstOrDefault(g => g.GameId == manifest?.Id.ToString());

            if (game == null)
                return;

            if (game.GameActions == null)
                game.GameActions = new ObservableCollection<PN.SDK.Models.GameAction>();
            else
                game.GameActions.Clear();

            if (manifest.Actions == null)
                throw new Exception("The game has no actions defined.");

            if (game.IsInstalled || game.IsInstalling)
                foreach (var action in manifest.Actions.OrderBy(a => a.SortOrder))
                {
                    bool isFirstAction = !manifest.Actions.Any(a => a.IsPrimaryAction) && manifest.Actions.First().Name == action.Name;

                    var actionPath = action.Path?.ExpandEnvironmentVariables(game.InstallDirectory);
                    var actionWorkingDir = String.IsNullOrWhiteSpace(action.WorkingDirectory) ? game.InstallDirectory : action.WorkingDirectory.ExpandEnvironmentVariables(game.InstallDirectory);

                    if (actionPath.StartsWith(actionWorkingDir))
                        actionPath = actionPath.Substring(actionWorkingDir.Length).TrimStart(Path.DirectorySeparatorChar);

                    game.GameActions.Add(new PN.SDK.Models.GameAction()
                    {
                        Name = action.Name,
                        Arguments = action.Arguments,
                        Path = actionPath,
                        WorkingDir = actionWorkingDir,
                        IsPlayAction = action.IsPrimaryAction || isFirstAction
                    });
                }

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

            if (game.Notes != manifest.Notes)
                game.Notes = manifest.Notes;

            PlayniteApi.Database.Games.Update(game);
        }

        private int RunInstallScript(string installDirectory)
        {
            var manifest = ManifestHelper.Read(installDirectory);
            var path = ScriptHelper.GetScriptFilePath(installDirectory, SDK.Enums.ScriptType.Install);

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

        private int RunNameChangeScript(string installDirectory, string oldPlayerAlias, string newPlayerAlias)
        {
            var manifest = ManifestHelper.Read(installDirectory);
            var path = ScriptHelper.GetScriptFilePath(installDirectory, SDK.Enums.ScriptType.NameChange);

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

                return script.Execute();
            }

            return 0;
        }

        private int RunKeyChangeScript(string installDirectory, string key = "")
        {
            var manifest = ManifestHelper.Read(installDirectory);
            var path = ScriptHelper.GetScriptFilePath(installDirectory, SDK.Enums.ScriptType.KeyChange);

            if (File.Exists(path))
            {
                var script = new PowerShellScript();

                if (String.IsNullOrEmpty(key))
                    key = LANCommanderClient.GetAllocatedKey(manifest.Id);

                script.AddVariable("InstallDirectory", installDirectory);
                script.AddVariable("GameManifest", manifest);
                script.AddVariable("DefaultInstallDirectory", Settings.InstallDirectory);
                script.AddVariable("ServerAddress", Settings.ServerAddress);
                script.AddVariable("AllocatedKey", key);

                script.UseFile(path);

                return script.Execute();
            }

            return 0;
        }
    }
}
