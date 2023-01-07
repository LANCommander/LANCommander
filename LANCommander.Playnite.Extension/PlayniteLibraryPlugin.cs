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

namespace LANCommander.Playnite.Extension
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
            try
            {
                var syncedGames = PlayniteApi.Database.Games;

                var games = LANCommander
                    .GetGames()
                    .Where(g => g.Archives != null && g.Archives.Count() > 0)
                    .Select(g =>
                    {
                        return new GameMetadata()
                        {
                            IsInstalled = false,
                            Name = g.Title,
                            SortingName = g.SortTitle,
                            Description = g.Description,
                            GameId = g.Id.ToString(),
                            ReleaseDate = new ReleaseDate(g.ReleasedOn),
                            Version = g.Archives.OrderByDescending(a => a.CreatedOn).FirstOrDefault().Version,
                        };
                    });

                return games;
            }
            catch (Exception ex)
            {
                return new List<GameMetadata>();
            }
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

        private GameMetadata ParseManifest(string installDirectory)
        {
            var manifestContents = File.ReadAllText(Path.Combine(installDirectory, "_manifest.yml"));
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .Build();

            try
            {
                var manifest = deserializer.Deserialize<GameManifest>(manifestContents);

                var metadata = new GameMetadata()
                {
                    Name = manifest.Title,
                    SortingName = manifest.SortTitle,
                    Description = manifest.Description,
                    ReleaseDate = new ReleaseDate(manifest.ReleasedOn),
                    Version = manifest.Version,
                    GameActions = manifest.Actions.Select(a =>
                    {
                        return new PN.SDK.Models.GameAction()
                        {
                            Name = a.Name,
                            Arguments = a.Arguments,
                            Path = a.Path,
                            WorkingDir = a.WorkingDirectory,
                            IsPlayAction = a.IsPrimaryAction
                        };
                    }).ToList()
                };

                return metadata;
            }
            catch
            {
                throw new FileNotFoundException("The manifest file is invalid or corrupt.");
            }
        }
    }
}
