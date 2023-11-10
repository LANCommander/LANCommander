using LANCommander.SDK.Enums;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.IO;

namespace LANCommander.PlaynitePlugin
{
    public class LANCommanderUninstallController : UninstallController
    {
        public static readonly ILogger Logger = LogManager.GetLogger();

        private LANCommanderLibraryPlugin Plugin;

        public LANCommanderUninstallController(LANCommanderLibraryPlugin plugin, Game game) : base(game)
        {
            Name = "Uninstall LANCommander Game";
            Plugin = plugin;
        }

        public override void Uninstall(UninstallActionArgs args)
        {
            try
            {
                var gameManager = new LANCommander.SDK.GameManager(Plugin.LANCommanderClient);

                gameManager.Uninstall(Game.InstallDirectory);
            }
            catch (Exception ex)
            {

            }

            InvokeOnUninstalled(new GameUninstalledEventArgs());
        }
    }
}
