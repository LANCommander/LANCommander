using LANCommander.SDK;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace LANCommander.PlaynitePlugin
{
    public class LANCommanderSaveController : ControllerBase
    {
        public static readonly ILogger Logger = LogManager.GetLogger();

        private LANCommanderLibraryPlugin Plugin;

        public LANCommanderSaveController(LANCommanderLibraryPlugin plugin, Game game) : base(game)
        {
            Name = "Download save using LANCommander";
            Plugin = plugin;
        }

        public void Download(Game game)
        {
            if (game != null)
            {
                var saveManager = new GameSaveManager(Plugin.LANCommanderClient, new PlayniteLogger(Logger));

                Plugin.PlayniteApi.Dialogs.ActivateGlobalProgress(progress =>
                {
                    progress.ProgressMaxValue = 100;
                    progress.CurrentProgressValue = 0;

                    saveManager.OnDownloadProgress += (downloadProgress) =>
                    {
                        progress.CurrentProgressValue = downloadProgress.ProgressPercentage;
                    };

                    saveManager.OnDownloadComplete += (downloadComplete) =>
                    {
                        progress.CurrentProgressValue = 100;
                    };

                    saveManager.Download(game.InstallDirectory);

                    // Lock the thread until the download is done
                    while (progress.CurrentProgressValue != 100) { }
                },
                new GlobalProgressOptions("Downloading latest save...")
                {
                    IsIndeterminate = false,
                    Cancelable = false
                });
            }
        }

        public void Upload(Game game)
        {
            var saveManager = new GameSaveManager(Plugin.LANCommanderClient, new PlayniteLogger(Logger));

            saveManager.Upload(game.InstallDirectory);
        }
    }
}
