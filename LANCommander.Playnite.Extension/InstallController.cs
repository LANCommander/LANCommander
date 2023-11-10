using LANCommander.SDK;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;

namespace LANCommander.PlaynitePlugin
{
    public class LANCommanderInstallController : InstallController
    {
        public static readonly ILogger Logger = LogManager.GetLogger();

        private LANCommanderLibraryPlugin Plugin;

        public LANCommanderInstallController(LANCommanderLibraryPlugin plugin, Playnite.SDK.Models.Game game) : base(game)
        {
            Name = "Install using LANCommander";
            Plugin = plugin;
        }

        public override void Install(InstallActionArgs args)
        {
            Logger.Trace("Game install triggered, checking connection...");

            while (!Plugin.ValidateConnection())
            {
                Logger.Trace("User not authenticated. Opening auth window...");

                Plugin.ShowAuthenticationWindow();
            }

            var gameId = Guid.Parse(Game.GameId);

            string installDirectory = null;

            var result = Plugin.PlayniteApi.Dialogs.ActivateGlobalProgress(progress =>
            {
                var gameManager = new GameManager(Plugin.LANCommanderClient);

                gameManager.OnArchiveExtractionProgress += (long pos, long len) =>
                {
                    progress.ProgressMaxValue = len;
                    progress.CurrentProgressValue = pos;
                };

                gameManager.OnArchiveEntryExtractionProgress += (object sender, ArchiveEntryExtractionProgressArgs e) =>
                {
                    if (progress.CancelToken != null && progress.CancelToken.IsCancellationRequested)
                    {
                        e.Reader.Cancel();
                        e.Reader.Dispose();
                        e.Stream.Dispose();

                        progress.IsIndeterminate = true;
                    }
                };

                installDirectory = gameManager.Install(gameId);
            },
            new GlobalProgressOptions($"Downloading {Game.Name}...")
            {
                IsIndeterminate = true,
                Cancelable = true,
            });

            if (!result.Canceled && result.Error == null && !String.IsNullOrWhiteSpace(installDirectory))
            {
                var manifest = ManifestHelper.Read(installDirectory);

                Plugin.UpdateGame(manifest);

                var installInfo = new GameInstallationData
                {
                    InstallDirectory = installDirectory,
                };

                InvokeOnInstalled(new GameInstalledEventArgs(installInfo));
            }
        }
    }
}
