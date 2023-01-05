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
        private SettingsViewModel Settings { get; set; }
        private LANCommanderClient LANCommander { get; set; }

        public override Guid Id { get; } = Guid.Parse("48e1bac7-e0a0-45d7-ba83-36f5e9e959fc");
        public override string Name => "LANCommander";
        public override LibraryClient Client { get; } = new PlayniteClient();

        public PlayniteLibraryPlugin(IPlayniteAPI api) : base(api)
        {
            LANCommander = new LANCommanderClient();
            Settings = new SettingsViewModel(this);
            Properties = new LibraryPluginProperties
            {
                HasSettings = true,
            };
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            // Implement LANCommander client here
            var games = LANCommander.GetGames().Select(g => new GameMetadata()
            {
                Name = g.Title,
                Description = g.Description,
                GameId = g.Id.ToString(),
                ReleaseDate = new ReleaseDate(g.ReleasedOn),
                SortingName = g.SortTitle,
                Version = g.Archives != null && g.Archives.Count() > 0 ? g.Archives.OrderByDescending(a => a.CreatedOn).FirstOrDefault().Version : null,
            });

            return games;
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return base.GetSettings(firstRunSettings);
        }

        public override UserControl GetSettingsView(bool firstRunView)
        {
            return base.GetSettingsView(firstRunView);
        }
    }
}
