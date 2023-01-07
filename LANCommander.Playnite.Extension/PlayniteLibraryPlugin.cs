using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace LANCommander.Playnite.Extension
{
    public class PlayniteLibraryPlugin : LibraryPlugin
    {
        public static readonly ILogger Logger = LogManager.GetLogger();
        private PlayniteSettingsViewModel Settings { get; set; }
        internal LANCommanderClient LANCommander { get; set; }

        public override Guid Id { get; } = Guid.Parse("48e1bac7-e0a0-45d7-ba83-36f5e9e959fc");
        public override string Name => "LANCommander";
        public override LibraryClient Client { get; } = new PlayniteClient();

        public PlayniteLibraryPlugin(IPlayniteAPI api) : base(api)
        {
            LANCommander = new LANCommanderClient();
            Settings = new PlayniteSettingsViewModel(this);
            Properties = new LibraryPluginProperties
            {
                HasSettings = true,
            };
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            try
            {
                var token = new SDK.Models.AuthToken()
                {
                    AccessToken = Settings.AccessToken,
                    RefreshToken = Settings.RefreshToken,
                };

                LANCommander.Token = token;

                var tokenIsValid = LANCommander.ValidateToken(token);

                if (!tokenIsValid)
                {
                    try
                    {
                        LANCommander.RefreshToken(token);
                    }
                    catch
                    {
                        ShowAuthenticationWindow();
                    }
                }

                LANCommander.Token = token;

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
            catch
            {
                return new List<GameMetadata>();
            }
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
    }
}
