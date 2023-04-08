using LANCommander.SDK.Enums;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.IO;

namespace LANCommander.PlaynitePlugin
{
    public class LANCommanderUninstallController : UninstallController
    {
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

            if (!String.IsNullOrWhiteSpace(Game.InstallDirectory) && Directory.Exists(Game.InstallDirectory))
                Directory.Delete(Game.InstallDirectory, true);

            InvokeOnUninstalled(new GameUninstalledEventArgs());
        }
    }
}
