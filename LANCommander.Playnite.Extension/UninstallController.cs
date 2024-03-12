using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.PowerShell;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.IO;
using System.Linq;

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
                var completedQueueItem = Plugin.DownloadQueue.DownloadQueue.Completed.FirstOrDefault(qi => qi.Game != null && qi.Game.Id == Game.Id);

                if (completedQueueItem != null)
                    Plugin.DownloadQueue.DownloadQueue.Completed.Remove(completedQueueItem);

                try
                {
                    var scriptPath = ScriptHelper.GetScriptFilePath(Game.InstallDirectory, Game.Id, SDK.Enums.ScriptType.Uninstall);

                    if (!String.IsNullOrEmpty(scriptPath) && File.Exists(scriptPath))
                    {
                        var manifest = ManifestHelper.Read(Game.InstallDirectory, Game.Id);
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
                    Logger.Error(ex, ResourceProvider.GetString("LOCLANCommanderUninstallScriptError"));
                }

                Plugin.PlayniteApi.Dialogs.ActivateGlobalProgress(progress =>
                {
                    Plugin.LANCommanderClient.Games.Uninstall(Game.InstallDirectory, Game.Id);

                    var metadataPath = SDK.GameService.GetMetadataDirectoryPath(Game.InstallDirectory, Game.Id);

                    if (Directory.Exists(metadataPath))
                        Directory.Delete(metadataPath, true);

                    DirectoryHelper.DeleteEmptyDirectories(Game.InstallDirectory);
                },
                new GlobalProgressOptions(ResourceProvider.GetString("LOCLANCommanderUninstallRemovingFilesMessage"))
                {
                    IsIndeterminate = true,
                    Cancelable = false,
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "There was an error uninstalling the game");
                throw new Exception(ResourceProvider.GetString("LOCLANCommanderUninstallGenericError"));
            }

            InvokeOnUninstalled(new GameUninstalledEventArgs());
        }
    }
}
