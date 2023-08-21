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
        private PowerShellRuntime PowerShellRuntime;

        public LANCommanderUninstallController(LANCommanderLibraryPlugin plugin, Game game) : base(game)
        {
            Name = "Uninstall LANCommander Game";
            Plugin = plugin;
            PowerShellRuntime = new PowerShellRuntime();
        }

        public override void Uninstall(UninstallActionArgs args)
        {
            try
            {
                PowerShellRuntime.RunScript(Game, ScriptType.Uninstall);
            }
            catch { }

            Logger.Trace("Attempting to delete install directory...");

            if (!String.IsNullOrWhiteSpace(Game.InstallDirectory) && Directory.Exists(Game.InstallDirectory))
                Directory.Delete(Game.InstallDirectory, true);

            Logger.Trace("Deleted!");

            InvokeOnUninstalled(new GameUninstalledEventArgs());
        }
    }
}
