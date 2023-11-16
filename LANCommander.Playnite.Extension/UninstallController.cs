using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.PowerShell;
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
                var gameManager = new LANCommander.SDK.GameManager(Plugin.LANCommanderClient, Plugin.Settings.InstallDirectory);

                try
                {
                    var scriptPath = ScriptHelper.GetScriptFilePath(Game.InstallDirectory, SDK.Enums.ScriptType.Uninstall);

                    if (!String.IsNullOrEmpty(scriptPath))
                    {
                        var manifest = ManifestHelper.Read(Game.InstallDirectory);
                        var script = new PowerShellScript();

                        script.AddVariable("InstallDirectory", Game.InstallDirectory);
                        script.AddVariable("GameManifest", manifest);
                        script.AddVariable("DefaultInstallDirectory", Plugin.Settings.InstallDirectory);
                        script.AddVariable("ServerAddress", Plugin.Settings.ServerAddress);

                        script.UseFile(scriptPath);

                        script.Execute();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "There was an error running the uninstall script");
                }

                gameManager.Uninstall(Game.InstallDirectory);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "There was an error uninstalling the game");
            }

            InvokeOnUninstalled(new GameUninstalledEventArgs());
        }
    }
}
