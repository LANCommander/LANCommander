using LANCommander.Models;
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

            LoadSettings();
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            var gameMetadata = new List<GameMetadata>();

            try
            {
                var games = LANCommander
                    .GetGames()
                    .Where(g => g.Archives != null && g.Archives.Count() > 0);

                foreach (var game in games)
                {
                    var existingGame = PlayniteApi.Database.Games.FirstOrDefault(g => g.GameId == game.Id.ToString() && g.PluginId == Id && g.IsInstalled);

                    var iconUri = new Uri(new Uri(Settings.ServerAddress), $"Games/GetIcon/{game.Id}");

                    var metadata = new GameMetadata()
                    {
                        IsInstalled = existingGame != null,
                        Name = game.Title,
                        SortingName = game.SortTitle,
                        Description = game.Description,
                        GameId = game.Id.ToString(),
                        ReleaseDate = new ReleaseDate(game.ReleasedOn),
                        Version = game.Archives.OrderByDescending(a => a.CreatedOn).FirstOrDefault().Version,
                        Icon = new MetadataFile(iconUri.ToString())
                    };

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

        public void LoadSettings()
        {
            Settings = LoadPluginSettings<PlayniteSettingsViewModel>();

            try
            {
                if (LANCommander == null)
                    LANCommander = new LANCommanderClient(Settings.ServerAddress);

                LANCommander.Client.BaseUrl = new Uri(Settings.ServerAddress);

                var token = new SDK.Models.AuthToken()
                {
                    AccessToken = Settings.AccessToken,
                    RefreshToken = Settings.RefreshToken,
                };

                LANCommander.Token = token;
            }
            catch
            {

            }
        }

        public void SaveSettings()
        {
            SavePluginSettings(Settings);

            if (LANCommander == null)
                LANCommander = new LANCommanderClient(Settings.ServerAddress);

            if (Settings.ServerAddress != LANCommander.Client.BaseUrl.ToString())
            {
                LANCommander.Client.BaseUrl = new Uri(Settings.ServerAddress);

                var token = new SDK.Models.AuthToken()
                {
                    AccessToken = Settings.AccessToken,
                    RefreshToken = Settings.RefreshToken,
                };

                LANCommander.Token = token;
            }
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

                        if (game.GameActions == null)
                            game.GameActions = new System.Collections.ObjectModel.ObservableCollection<PN.SDK.Models.GameAction>();

                        foreach (var action in manifest.Actions)
                        {
                            bool isFirstAction = !manifest.Actions.Any(a => a.IsPrimaryAction) && manifest.Actions.First().Name == action.Name;

                            game.GameActions.AddMissing(new PN.SDK.Models.GameAction()
                            {
                                Name = action.Name,
                                Arguments = action.Arguments,
                                Path = PlayniteApi.ExpandGameVariables(game, action.Path),
                                WorkingDir = action.WorkingDirectory.Replace('/', Path.DirectorySeparatorChar) ?? game.InstallDirectory,
                                IsPlayAction = action.IsPrimaryAction || isFirstAction
                            });
                        }

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
