using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using LANCommander.SDK.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.Core;

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
                PowerShellRuntime.RunUninstallScript(Game);
            }
            catch { }

            if (!String.IsNullOrWhiteSpace(Game.InstallDirectory) && Directory.Exists(Game.InstallDirectory))
                Directory.Delete(Game.InstallDirectory, true);

            InvokeOnUninstalled(new GameUninstalledEventArgs());
        }
    }
}
