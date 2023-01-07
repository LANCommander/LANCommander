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

namespace LANCommander.Playnite.Extension
{
    public class LANCommanderUninstallController : UninstallController
    {
        private PlayniteLibraryPlugin Plugin;

        public LANCommanderUninstallController(PlayniteLibraryPlugin plugin, Game game) : base(game)
        {
            Name = "Uninstall LANCommander Game";
            Plugin = plugin;
        }

        public override void Uninstall(UninstallActionArgs args)
        {
            Directory.Delete(Game.InstallDirectory, true);

            InvokeOnUninstalled(new GameUninstalledEventArgs());
        }
    }
}
