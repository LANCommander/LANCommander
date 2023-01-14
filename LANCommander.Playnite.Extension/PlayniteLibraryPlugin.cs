using LANCommander.PlaynitePlugin.Extensions;
using LANCommander.SDK;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using PN = Playnite;

namespace LANCommander.PlaynitePlugin
{
    public class PlayniteLibraryPlugin : LibraryPlugin
    {
        public static readonly ILogger Logger = LogManager.GetLogger();
        internal PlayniteSettingsViewModel Settings { get; set; }
        internal LANCommanderClient LANCommander { get; set; }

        public override Guid Id { get; } = Guid.Parse("48e1bac7-e0a0-45d7-ba83-36f5e9e959fc");
        public override string Name => "LANCommander";
        public override LibraryClient Client { get; } = new PlayniteClient();

        public PlayniteLibraryPlugin(IPlayniteAPI api) : base(api)
        {
            Properties = new LibraryPluginProperties
            {
                HasSettings = true,
            };

            Settings = new PlayniteSettingsViewModel(this);
            LANCommander = new LANCommanderClient(Settings.ServerAddress);
            LANCommander.Token = new SDK.Models.AuthToken()
            {
                AccessToken = Settings.AccessToken,
                RefreshToken = Settings.RefreshToken,
            };
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            var gameMetadata = new List<GameMetadata>();

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

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return Settings;
        }

        public override UserControl GetSettingsView(bool firstRunView)
        {
            return new PlayniteSettingsView(this);
        }

        public System.Windows.Window ShowAuthenticationWindow()
        {
            var window = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions()
            {
                ShowMinimizeButton = false,
            });

            window.Title = "Authenticate to LANCommander";

            window.Content = new Views.Authentication(this);
            window.DataContext = new ViewModels.Authentication();

            window.Owner = PlayniteApi.Dialogs.GetCurrentAppWindow();
            window.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
            window.ShowDialog();

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
