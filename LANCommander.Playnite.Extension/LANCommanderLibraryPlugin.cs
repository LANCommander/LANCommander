using LANCommander.PlaynitePlugin.Extensions;
using LANCommander.SDK;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using PN = Playnite;

namespace LANCommander.PlaynitePlugin
{
    public class LANCommanderLibraryPlugin : LibraryPlugin
    {
        public static readonly ILogger Logger = LogManager.GetLogger();
        internal LANCommanderSettingsViewModel Settings { get; set; }
        internal LANCommanderClient LANCommander { get; set; }
        internal PowerShellRuntime PowerShellRuntime { get; set; }

        public override Guid Id { get; } = Guid.Parse("48e1bac7-e0a0-45d7-ba83-36f5e9e959fc");
        public override string Name => "LANCommander";
        public override LibraryClient Client { get; } = new LANCommanderLibraryClient();

        public LANCommanderLibraryPlugin(IPlayniteAPI api) : base(api)
        {
            Properties = new LibraryPluginProperties
            {
                HasSettings = true,
            };

            Settings = new LANCommanderSettingsViewModel(this);
            LANCommander = new LANCommanderClient(Settings.ServerAddress);
            LANCommander.Token = new SDK.Models.AuthToken()
            {
                AccessToken = Settings.AccessToken,
                RefreshToken = Settings.RefreshToken,
            };

            PowerShellRuntime = new PowerShellRuntime();
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            var gameMetadata = new List<GameMetadata>();

            if (!LANCommander.ValidateToken(LANCommander.Token))
            {
                try
                {
                    var response = LANCommander.RefreshToken(LANCommander.Token);

                    LANCommander.Token.AccessToken = response.AccessToken;
                    LANCommander.Token.RefreshToken = response.RefreshToken;

                    if (!LANCommander.ValidateToken(LANCommander.Token))
                    {
                        throw new Exception();
                    }
                }
                catch
                {
                    ShowAuthenticationWindow();
                }
            }

            try
            {
                var games = LANCommander
                    .GetGames();

                foreach (var game in games)
                {
                    var manifest = LANCommander.GetGameManifest(game.Id);
                    var existingGame = PlayniteApi.Database.Games.FirstOrDefault(g => g.GameId == game.Id.ToString() && g.PluginId == Id && g.IsInstalled);

                    var iconUri = new Uri(new Uri(Settings.ServerAddress), $"Games/GetIcon/{game.Id}");

                    var metadata = new GameMetadata()
                    {
                        IsInstalled = existingGame != null,
                        Name = manifest.Title,
                        SortingName = manifest.SortTitle,
                        Description = manifest.Description,
                        GameId = game.Id.ToString(),
                        ReleaseDate = new ReleaseDate(manifest.ReleasedOn),
                        //Version = game.Archives.OrderByDescending(a => a.CreatedOn).FirstOrDefault().Version,
                        Icon = new MetadataFile(iconUri.ToString()),
                        Genres = new HashSet<MetadataProperty>()
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

                    gameMetadata.Add(metadata);
                };
            }
            catch (Exception ex)
            {
                
            }

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
            if (args.Games.Count == 1 && args.Games.First().IsInstalled && !String.IsNullOrWhiteSpace(args.Games.First().InstallDirectory))
            {
                var nameChangeScriptPath = PowerShellRuntime.GetScriptFilePath(args.Games.First(), SDK.Enums.ScriptType.NameChange);
                var keyChangeScriptPath = PowerShellRuntime.GetScriptFilePath(args.Games.First(), SDK.Enums.ScriptType.KeyChange);

                if (File.Exists(nameChangeScriptPath))
                    yield return new GameMenuItem
                    {
                        Description = "Change Player Name",
                        Action = (nameChangeArgs) =>
                        {
                            PowerShellRuntime.RunScript(nameChangeArgs.Games.First(), SDK.Enums.ScriptType.NameChange);
                        }
                    };

                if (File.Exists(keyChangeScriptPath))
                    yield return new GameMenuItem
                    {
                        Description = "Change Game Key",
                        Action = (keyChangeArgs) =>
                        {
                            Guid gameId;

                            if (Guid.TryParse(keyChangeArgs.Games.First().GameId, out gameId))
                            {
                                // NUKIEEEE
                                var newKey = LANCommander.GetNewKey(gameId);

                                if (String.IsNullOrEmpty(newKey))
                                    PlayniteApi.Dialogs.ShowErrorMessage("There are no more keys available on the server.", "No Keys Available");
                                else
                                    PowerShellRuntime.RunScript(keyChangeArgs.Games.First(), SDK.Enums.ScriptType.KeyChange, $@"""{newKey}""");
                            }
                            else
                            {
                                PlayniteApi.Dialogs.ShowErrorMessage("This game could not be found on the server. Your game may be corrupted.");
                            }
                        }
                    };
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
                    var result = PlayniteApi.Dialogs.SelectString("Enter your new player name. This will change your name across all installed games!", "Enter Name", "");

                    if (result.Result == true)
                    {
                        var games = PlayniteApi.Database.Games.Where(g => g.IsInstalled).ToList();

                        foreach (var game in games)
                        {
                            PowerShellRuntime.RunScript(game, SDK.Enums.ScriptType.NameChange);
                        }
                    }
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

        public Window ShowAuthenticationWindow()
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
                    ServerAddress = Settings.ServerAddress
                };

                window.Owner = PlayniteApi.Dialogs.GetCurrentAppWindow();
                window.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            });

            return window;
        }

        public void UpdateGamesFromManifest()
        {
            var games = PlayniteApi.Database.Games;

            foreach (var game in games.Where(g => g.PluginId == Id && g.IsInstalled))
            {
                if (!Directory.Exists(game.InstallDirectory))
                    continue;

                var manifestPath = Path.Combine(game.InstallDirectory, "_manifest.yml");

                if (File.Exists(manifestPath))
                {
                    try
                    {
                        var manifestContents = File.ReadAllText(manifestPath);
                        var deserializer = new DeserializerBuilder()
                            .IgnoreUnmatchedProperties()
                            .WithNamingConvention(PascalCaseNamingConvention.Instance)
                            .Build();

                        var manifest = deserializer.Deserialize<GameManifest>(manifestContents);

                        #region Actions
                        if (game.GameActions == null)
                            game.GameActions = new System.Collections.ObjectModel.ObservableCollection<PN.SDK.Models.GameAction>();

                        foreach (var action in manifest.Actions)
                        {
                            bool isFirstAction = !manifest.Actions.Any(a => a.IsPrimaryAction) && manifest.Actions.First().Name == action.Name;

                            foreach (var existingAction in game.GameActions)
                                if (action.Name == existingAction.Name)
                                    game.GameActions.Remove(existingAction);

                            game.GameActions.AddMissing(new PN.SDK.Models.GameAction()
                            {
                                Name = action.Name,
                                Arguments = action.Arguments,
                                Path = PlayniteApi.ExpandGameVariables(game, action.Path?.Replace('/', Path.DirectorySeparatorChar)),
                                WorkingDir = action.WorkingDirectory?.Replace('/', Path.DirectorySeparatorChar) ?? game.InstallDirectory,
                                IsPlayAction = action.IsPrimaryAction || isFirstAction
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
                                PlayniteApi.Database.Features.Add(new PN.SDK.Models.GameFeature()
                                {
                                    Name = featureName
                                });

                                game.Features.Add(new PN.SDK.Models.GameFeature()
                                {
                                    Name = $"LAN Multiplayer {playerCount}"
                                });
                            }
                        }

                        if (manifest.LocalMultiplayer != null)
                        {
                            var multiplayerInfo = manifest.LocalMultiplayer;

                            string playerCount = multiplayerInfo.MinPlayers == multiplayerInfo.MaxPlayers ? $"({multiplayerInfo.MinPlayers} players)" : $"({multiplayerInfo.MinPlayers} - {multiplayerInfo.MaxPlayers} players)";

                            game.Features.Add(new PN.SDK.Models.GameFeature()
                            {
                                Name = $"Local Multiplayer {playerCount}"
                            });
                        }

                        if (manifest.OnlineMultiplayer != null)
                        {
                            var multiplayerInfo = manifest.OnlineMultiplayer;

                            string playerCount = multiplayerInfo.MinPlayers == multiplayerInfo.MaxPlayers ? $"({multiplayerInfo.MinPlayers} players)" : $"({multiplayerInfo.MinPlayers} - {multiplayerInfo.MaxPlayers} players)";

                            game.Features.Add(new PN.SDK.Models.GameFeature()
                            {
                                Name = $"Online Multiplayer {playerCount}"
                            });
                        }
                        #endregion

                        PlayniteApi.Database.Games.Update(game);
                    }
                    catch (Exception ex)
                    {

                    }
                }
            }
        }
    }
}
