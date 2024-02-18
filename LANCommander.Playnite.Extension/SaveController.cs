using LANCommander.SDK;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Linq;

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

        public void Download(Guid gameId, string installDirectory = "")
        {
            var game = Plugin.PlayniteApi.Database.Games.FirstOrDefault(g => g.Id == gameId);

            if (installDirectory == "")
                installDirectory = game.InstallDirectory;

            if (game != null)
            {
                Plugin.PlayniteApi.Dialogs.ActivateGlobalProgress(progress =>
                {
                    progress.ProgressMaxValue = 100;
                    progress.CurrentProgressValue = 0;

                    Plugin.LANCommanderClient.Saves.OnDownloadProgress += (downloadProgress) =>
                    {
                        progress.CurrentProgressValue = downloadProgress.ProgressPercentage;
                    };

                    Plugin.LANCommanderClient.Saves.OnDownloadComplete += (downloadComplete) =>
                    {
                        progress.CurrentProgressValue = 100;
                    };

                    Plugin.LANCommanderClient.Saves.Download(installDirectory, gameId).Wait();

                    // Lock the thread until the download is done
                    while (progress.CurrentProgressValue != 100) { }
                },
                new GlobalProgressOptions(ResourceProvider.GetString("LOCLANCommanderSaveDownloadProgressDialogMessage"))
                {
                    IsIndeterminate = false,
                    Cancelable = false
                });
            }
        }

        public void Upload(Guid gameId)
        {
            var game = Plugin.PlayniteApi.Database.Games.FirstOrDefault(g => g.Id == gameId);

            if (game != null)
            {
                Plugin.LANCommanderClient.Saves.Upload(game.InstallDirectory, gameId);
            }
        }
    }
}
