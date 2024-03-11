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
using System.IO;
using System.Linq;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LANCommander.SDK;
using LANCommander.PlaynitePlugin.Controls;
using System.Threading.Tasks;
using System.Net;

namespace LANCommander.PlaynitePlugin
{
    public class LANCommanderLibraryPlugin : LibraryPlugin
    {
        public override Guid Id { get; } = Guid.Parse("48e1bac7-e0a0-45d7-ba83-36f5e9e959fc");
        public override string Name => "LANCommander";

        public static readonly ILogger Logger = LogManager.GetLogger();
        internal SettingsViewModel Settings { get; set; }
        internal Client LANCommanderClient { get; set; }

        internal LANCommanderSaveController SaveController { get; set; }
        internal ImportController ImportController { get; set; }
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

            Settings = new SettingsViewModel(this);
            LANCommanderClient = new SDK.Client(Settings.ServerAddress, Settings.InstallDirectory, new PlayniteLogger(Logger));
            LANCommanderClient.UseToken(new SDK.Models.AuthToken()
            {
                AccessToken = Settings.AccessToken,
                RefreshToken = Settings.RefreshToken,
            });

            #region Initialize Top Bar Items
            OfflineModeTopPanelItem = new TopPanelItem
            {
                Title = ResourceProvider.GetString("LOCLANCommanderGoOnline"),
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

            ImportController = new ImportController(this);
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
            if (!ValidateConnection() && !Settings.OfflineModeEnabled)
            {
                Logger.Trace("Authentication invalid, showing auth window...");
                ShowAuthenticationWindow();

                if (!ValidateConnection() && !Settings.OfflineModeEnabled)
                {
                    Logger.Trace("User cancelled authentication.");

                    throw new Exception(ResourceProvider.GetString("LOCLANCommanderImportGamesAuthenticationInvalidNotification"));
                }
            }

            if (Settings.OfflineModeEnabled)
                return new List<Game>();

            return ImportController.ImportGames();
        }

        public override IEnumerable<InstallController> GetInstallActions(GetInstallActionsArgs args)
        {
            if (args.Game.PluginId != Id)
                yield break;

            if (Settings.OfflineModeEnabled)
            {
                PlayniteApi.Dialogs.ShowErrorMessage(ResourceProvider.GetString("LOCLANCommanderOfflineModeInstallWarningMessage"), ResourceProvider.GetString("LOCLANCommanderOfflineModeInstallWarningCaption"));
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
            var manifest = ManifestHelper.Read(args.Game.InstallDirectory, args.Game.Id);
            var primaryDisplay = System.Windows.Forms.Screen.AllScreens.FirstOrDefault(s => s.Primary);

            LANCommanderClient.Actions.AddVariable("DisplayWidth", primaryDisplay.Bounds.Width.ToString());
            LANCommanderClient.Actions.AddVariable("DisplayHeight", primaryDisplay.Bounds.Height.ToString());

            foreach (var action in manifest.Actions.Where(a => a.IsPrimaryAction).OrderBy(a => a.SortOrder))
            {
                AutomaticPlayController automaticPlayController = null;

                try
                {
                    automaticPlayController = new AutomaticPlayController(args.Game)
                    {
                        Arguments = LANCommanderClient.Actions.ExpandVariables(action.Arguments, args.Game.InstallDirectory, skipSlashes: true),
                        Name = action.Name,
                        Path = LANCommanderClient.Actions.ExpandVariables(action.Path, args.Game.InstallDirectory),
                        TrackingMode = TrackingMode.Default,
                        Type = AutomaticPlayActionType.File,
                        WorkingDir = LANCommanderClient.Actions.ExpandVariables(action.WorkingDirectory, args.Game.InstallDirectory)
                    };
                }
                catch (Exception ex)
                {
                    Logger?.Error(ex, "Could not yield primary action");
                }

                if (automaticPlayController != null)
                    yield return automaticPlayController;
            }

            if (!Settings.OfflineModeEnabled)
            {
                var game = LANCommanderClient.Games.Get(args.Game.Id);

                if (game.Servers != null)
                foreach (var server in game.Servers.Where(s => s.Actions != null))
                {
                    foreach (var action in server.Actions)
                    {
                        AutomaticPlayController automaticPlayController = null;

                        try
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

                            automaticPlayController = new AutomaticPlayController(args.Game)
                            {
                                Arguments = LANCommanderClient.Actions.ExpandVariables(action.Arguments, args.Game.InstallDirectory, variables, true),
                                Name = action.Name,
                                Path = LANCommanderClient.Actions.ExpandVariables(action.Path, args.Game.InstallDirectory, variables),
                                TrackingMode = TrackingMode.Default,
                                Type = AutomaticPlayActionType.File,
                                WorkingDir = LANCommanderClient.Actions.ExpandVariables(action.WorkingDirectory, args.Game.InstallDirectory, variables)
                            };
                        }
                        catch (Exception ex)
                        {
                            Logger?.Error(ex, "Could not yield server action");
                        }

                        if (automaticPlayController != null)
                            yield return automaticPlayController;
                    }
                }
            }
        }

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            Logger.Trace("Populating game menu items...");

            yield return new GameMenuItem
            {
                Description = ResourceProvider.GetString("LOCLANCommanderAddToDownloadQueue"),
                Action = (args2) =>
                {
                    foreach (var game in args2.Games)
                        DownloadQueue.Add(game);
                }
            };

            if (args.Games.Count == 1 && args.Games.First().IsInstalled && !String.IsNullOrWhiteSpace(args.Games.First().InstallDirectory))
            {
                var game = args.Games.First();

                var nameChangeScriptPath = ScriptHelper.GetScriptFilePath(game.InstallDirectory, game.Id, SDK.Enums.ScriptType.NameChange);
                var keyChangeScriptPath = ScriptHelper.GetScriptFilePath(game.InstallDirectory, game.Id, SDK.Enums.ScriptType.KeyChange);
                var installScriptPath = ScriptHelper.GetScriptFilePath(game.InstallDirectory, game.Id, SDK.Enums.ScriptType.Install);

                if (File.Exists(nameChangeScriptPath))
                {
                    Logger.Trace($"Name change script found at path {nameChangeScriptPath}");

                    yield return new GameMenuItem
                    {
                        Description = ResourceProvider.GetString("LOCLANCommanderChangeNameContextMenuItem"),
                        Action = (nameChangeArgs) =>
                        {
                            var oldName = Settings.DisplayName;

                            var result = PlayniteApi.Dialogs.SelectString(ResourceProvider.GetString("LOCLANCommanderChangePlayerNameDialogMessage"), ResourceProvider.GetString("LOCLANCommanderChangePlayerNameDialogCaption"), oldName);

                            if (result.Result == true)
                            {
                                var nameChangeGame = nameChangeArgs.Games.First();

                                RunNameChangeScript(nameChangeGame.InstallDirectory, game.Id, oldName, result.SelectedString);

                                var alias = LANCommanderClient.Profile.ChangeAlias(result.SelectedString);

                                Settings.DisplayName = alias;

                                SavePluginSettings(Settings);
                            }
                        }
                    };
                }

                if (File.Exists(keyChangeScriptPath))
                {
                    Logger.Trace($"Key change script found at path {keyChangeScriptPath}");

                    yield return new GameMenuItem
                    {
                        Description = ResourceProvider.GetString("LOCLANCommanderChangeGameKeyContextMenuItem"),
                        Action = (keyChangeArgs) =>
                        {
                            // NUKIEEEE
                            var newKey = LANCommanderClient.Games.GetNewKey(keyChangeArgs.Games.First().Id);

                            if (String.IsNullOrEmpty(newKey))
                                PlayniteApi.Dialogs.ShowErrorMessage(ResourceProvider.GetString("LOCLANCommanderNoKeysAvailableMessage"), ResourceProvider.GetString("LOCLANCommanderNoKeysAvailableTitle"));
                            else
                                RunKeyChangeScript(keyChangeArgs.Games.First().InstallDirectory, keyChangeArgs.Games.First().Id, newKey);
                        }
                    };
                }

                if (File.Exists(installScriptPath))
                {
                    Logger.Trace($"Install script found at path {installScriptPath}");

                    yield return new GameMenuItem
                    {
                        Description = ResourceProvider.GetString("LOCLANCommanderManageRunInstallScriptContextMenuItem"),
                        Action = (installArgs) =>
                        {
                            RunInstallScript(installArgs.Games.First().InstallDirectory, installArgs.Games.First().Id);
                        }
                    };
                }

                yield return new GameMenuItem
                {
                    Description = ResourceProvider.GetString("LOCLANCommanderManageSavesContextMenuItem"),
                    Action = (args2) =>
                    {
                        ShowSaveManagerWindow(game);
                    }
                };
            }
        }

        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
            if (args.Game.PluginId == Id)
            {
                var currentGamePlayerAlias = GameService.GetPlayerAlias(args.Game.InstallDirectory, args.Game.Id);
                var currentGameKey = GameService.GetCurrentKey(args.Game.InstallDirectory, args.Game.Id);

                if (currentGamePlayerAlias != Settings.DisplayName)
                {
                    RunNameChangeScript(args.Game.InstallDirectory, args.Game.Id, currentGamePlayerAlias, Settings.DisplayName);
                }

                if (!Settings.OfflineModeEnabled && LANCommanderClient.IsConnected())
                {
                    var allocatedKey = LANCommanderClient.Games.GetAllocatedKey(args.Game.Id);

                    if (currentGameKey != allocatedKey)
                        RunKeyChangeScript(args.Game.InstallDirectory, args.Game.Id, allocatedKey);
                }

                if (!Settings.OfflineModeEnabled && LANCommanderClient.IsConnected())
                {
                    // This would be better served by some metadata
                    if (args.Game.Name.EndsWith(ResourceProvider.GetString("LOCLANCommanderUpdateAvailableSuffix")))
                    {
                        var title = args.Game.Name.Replace(ResourceProvider.GetString("LOCLANCommanderUpdateAvailableSuffix"), "");
                        var updateMessage = ResourceProvider.GetString("LOCLANCommanderUpdateAvailableMessage").Replace("{Title}", title);
                        var result = PlayniteApi.Dialogs.ShowMessage(updateMessage, ResourceProvider.GetString("LOCLANCommanderUpdateAvailableCaption"), MessageBoxButton.YesNo);

                        if (result == MessageBoxResult.Yes)
                        {
                            args.CancelStartup = true;

                            PlayniteApi.InstallGame(args.Game.Id);

                            return;
                        }
                    }

                    LANCommanderClient.Games.StartPlaySession(args.Game.Id);

                    try
                    {
                        var latestSave = LANCommanderClient.Saves.GetLatest(args.Game.Id);

                        if (latestSave == null || (latestSave.CreatedOn > args.Game.LastActivity && latestSave.CreatedOn > args.Game.Added))
                        {
                            SaveController = new LANCommanderSaveController(this, args.Game);
                            SaveController.Download(args.Game.Id);
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
                LANCommanderClient.Games.EndPlaySession(args.Game.Id);

                try
                {
                    SaveController = new LANCommanderSaveController(this, args.Game);
                    SaveController.Upload(args.Game.Id);
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
                Title = ResourceProvider.GetString("LOCLANCommanderDownloadQueueSidebarTitle"),
                Icon = new TextBlock
                {
                    Text = char.ConvertFromUtf32(0xef08),
                    FontFamily = ResourceProvider.GetResource("FontIcoFont") as FontFamily,
                },
                Type = SiderbarItemType.View,
                Opened = () =>
                {
                    var view = new Views.DownloadQueueView(this);

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
            return new SettingsView(this);
        }

        public void ShowNameChangeWindow()
        {
            Logger.Trace("Showing name change dialog!");

            var oldName = Settings.DisplayName;

            var result = PlayniteApi.Dialogs.SelectString(ResourceProvider.GetString("LOCLANCommanderChangePlayerNameDialogMessage"), ResourceProvider.GetString("LOCLANCommanderChangePlayerNameDialogCaption"), oldName);

            if (result.Result == true)
            {
                Logger.Trace($"New name entered was \"{result.SelectedString}\"");

                // Check to make sure they're staying in ASCII encoding
                if (String.IsNullOrEmpty(result.SelectedString) || result.SelectedString.Any(c => c > sbyte.MaxValue))
                {
                    PlayniteApi.Dialogs.ShowErrorMessage(ResourceProvider.GetString("LOCLANCommanderChangePlayerNameDialogInvalidMessage"));

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

                window.Title = ResourceProvider.GetString("LOCLANCommanderAuthenticationWindowTitle");
                window.SizeToContent = SizeToContent.WidthAndHeight;
                window.MinWidth = 400;
                window.Content = new Views.AuthenticationView(this);
                window.DataContext = new ViewModels.AuthenticationViewModel()
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

        public Window ShowSaveManagerWindow(Game game)
        {
            Window window = null;

            Application.Current.Dispatcher.Invoke((Action)delegate
            {
                window = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions()
                {
                    ShowMinimizeButton = false,
                    ShowMaximizeButton = false
                });

                window.Title = ResourceProvider.GetString("LOCLANCommanderSaveManagerWindowTitle") + $" - {game.Name}";
                window.SizeToContent = SizeToContent.WidthAndHeight;
                window.MinWidth = 300;
                window.Content = new Views.SaveManagerView(this, game);
                window.Owner = PlayniteApi.Dialogs.GetCurrentAppWindow();
                window.ResizeMode = ResizeMode.CanResizeWithGrip;

                window.ShowDialog();
            });

            return window;
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
                if (!Directory.Exists(GameService.GetMetadataDirectoryPath(game.InstallDirectory, game.Id)))
                    Directory.CreateDirectory(GameService.GetMetadataDirectoryPath(game.InstallDirectory, game.Id));

                var metaFiles = new Dictionary<string, string>()
                {
                    { "_manifest.yml", "Manifest.yml" },
                    { "_install.ps1", "Install.ps1" },
                    { "_uninstall.ps1", "Uninstall.ps1" },
                    { "_changename.ps1", "ChangeName.ps1" },
                    { "_changekey.ps1", "ChangeKey.ps1" },
                };

                // Move any old file names to the .lancommander metadata directory
                foreach (var file in metaFiles)
                {
                    var originalPath = Path.Combine(game.InstallDirectory, file.Key);
                    var destinationPath = GameService.GetMetadataFilePath(game.InstallDirectory, game.Id, file.Value);

                    if (File.Exists(originalPath) && !File.Exists(destinationPath))
                        File.Move(originalPath, destinationPath);
                }

                // Change the ID of any game installed pre-0.6.0 to match the GameId
                if (game.Id.ToString() != game.GameId)
                {
                    try
                    {
                        var originalId = game.Id;

                        game.Id = Guid.Parse(game.GameId);

                        PlayniteApi.Database.Games.Remove(originalId);
                        PlayniteApi.Database.Games.Add(game);
                    }
                    catch (Exception ex)
                    {
                        Logger?.Error($"Could not migrate ID for game {game.Name}");
                    }
                }

                // Set the current version of the game as recorded in the manifest
                if (String.IsNullOrWhiteSpace(game.Version))
                {
                    var manifest = ManifestHelper.Read(game.InstallDirectory, game.Id);

                    game.Version = manifest.Version;

                    PlayniteApi.Database.Games.Update(game);
                }
            }
            #endregion
        }
    }
}
