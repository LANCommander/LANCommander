using LANCommander.PlaynitePlugin.Models;
using LANCommander.SDK;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using LANCommander.SDK.PowerShell;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

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
            if (Plugin.Settings.OfflineModeEnabled)
            {
                var dbGame = Plugin.PlayniteApi.Database.Games.Get(Game.Id);

                dbGame.IsInstalling = false;
                dbGame.IsInstalled = false;

                Plugin.PlayniteApi.Database.Games.Update(dbGame);

                Logger.Trace("Offline mode enabled, skipping installation");

                return;
            }

            Logger.Trace("Game download triggered, checking connection...");

            while (!Plugin.ValidateConnection())
            {
                Logger.Trace("User not authenticated, opening auth window!");

                Plugin.ShowAuthenticationWindow();
            }

            Plugin.DownloadQueue.Add(Game);
            Plugin.DownloadQueue.OnInstallComplete += MarkInstalled;
            Plugin.DownloadQueue.OnInstallFail += OnInstallFail;
            Plugin.DownloadQueue.OnInstallCancelled += OnInstallCancelled;
        }

        private void OnInstallFail(Playnite.SDK.Models.Game game)
        {
            game.IsInstalling = false;
            game.IsInstalled = false;

            Plugin.PlayniteApi.Database.Games.Update(game);
        }

        private void OnInstallCancelled(Playnite.SDK.Models.Game game)
        {
            Plugin.LANCommanderClient.Games.CancelInstall();

            game.IsInstalling = false;
            game.IsInstalled = false;

            Plugin.PlayniteApi.Database.Games.Update(game);
        }

        public void MarkInstalled(Playnite.SDK.Models.Game game, string installDirectory)
        {
            if (game.Id == Game.Id)
            {
                InvokeOnInstalled(new GameInstalledEventArgs(new GameInstallationData
                {
                    InstallDirectory = installDirectory
                }));
            }
        }

        public override void Dispose()
        {
            Plugin.DownloadQueue.OnInstallComplete -= MarkInstalled;
        }
    }
}
