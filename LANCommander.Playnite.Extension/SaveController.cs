using LANCommander.SDK;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace LANCommander.PlaynitePlugin
{
    public class LANCommanderSaveController : ControllerBase
    {
        private static readonly ILogger Logger;

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
                Plugin.PlayniteApi.Dialogs.ActivateGlobalProgress(progress =>
                {
                    progress.ProgressMaxValue = 100;
                    progress.CurrentProgressValue = 0;

                    var saveManager = new GameSaveManager(Plugin.LANCommanderClient);

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
            var saveManager = new GameSaveManager(Plugin.LANCommanderClient);

            saveManager.Upload(game.InstallDirectory);
        }
    }
}
