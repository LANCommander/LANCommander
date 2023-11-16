using LANCommander.SDK;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using LANCommander.SDK.PowerShell;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

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
                var gameManager = new GameManager(Plugin.LANCommanderClient, Plugin.Settings.InstallDirectory);

                Stopwatch stopwatch = new Stopwatch();

                stopwatch.Start();

                var lastTotalSize = 0d;
                var speed = 0d;

                gameManager.OnArchiveExtractionProgress += (long pos, long len) =>
                {
                    if (stopwatch.ElapsedMilliseconds > 500)
                    {
                        var percent = Math.Ceiling((pos / (decimal)len) * 100);

                        progress.ProgressMaxValue = len;
                        progress.CurrentProgressValue = pos;

                        speed = (double)(progress.CurrentProgressValue - lastTotalSize) / (stopwatch.ElapsedMilliseconds / 1000d);

                        progress.Text = $"Downloading {Game.Name} ({percent}%) | {ByteSizeLib.ByteSize.FromBytes(speed).ToString("#.#")}/s";

                        lastTotalSize = pos;

                        stopwatch.Restart();
                    }
                };

                gameManager.OnArchiveEntryExtractionProgress += (object sender, ArchiveEntryExtractionProgressArgs e) =>
                {
                    if (progress.CancelToken != null && progress.CancelToken.IsCancellationRequested)
                    {
                        gameManager.CancelInstall();

                        progress.IsIndeterminate = true;
                    }
                };

                installDirectory = gameManager.Install(gameId);

                stopwatch.Stop();
            },
            new GlobalProgressOptions($"Preparing to download {Game.Name}")
            {
                IsIndeterminate = false,
                Cancelable = true,
            });

            // Install any redistributables
            var game = Plugin.LANCommanderClient.GetGame(gameId);

            if (game.Redistributables != null && game.Redistributables.Count() > 0)
            {
                Plugin.PlayniteApi.Dialogs.ActivateGlobalProgress(progress =>
                {
                    var redistributableManager = new RedistributableManager(Plugin.LANCommanderClient);

                    redistributableManager.Install(game);
                },
                new GlobalProgressOptions("Installing redistributables...")
                {
                    IsIndeterminate = true,
                    Cancelable = false,
                });
            }

            if (!result.Canceled && result.Error == null && !String.IsNullOrWhiteSpace(installDirectory))
            {
                var manifest = ManifestHelper.Read(installDirectory);

                Plugin.UpdateGame(manifest);

                var installInfo = new GameInstallationData
                {
                    InstallDirectory = installDirectory,
                };

                RunInstallScript(installDirectory);
                RunNameChangeScript(installDirectory);
                RunKeyChangeScript(installDirectory);

                InvokeOnInstalled(new GameInstalledEventArgs(installInfo));
            }
            else if (result.Canceled)
            {
                var dbGame = Plugin.PlayniteApi.Database.Games.Get(Game.Id);

                dbGame.IsInstalling = false;
                dbGame.IsInstalled = false;

                Plugin.PlayniteApi.Database.Games.Update(dbGame);
            }
            else if (result.Error != null)
                throw result.Error;
        }

        private int RunInstallScript(string installDirectory)
        {
            var manifest = ManifestHelper.Read(installDirectory);
            var path = ScriptHelper.GetScriptFilePath(installDirectory, SDK.Enums.ScriptType.Install);

            if (File.Exists(path))
            {
                var script = new PowerShellScript();

                script.AddVariable("InstallDirectory", installDirectory);
                script.AddVariable("GameManifest", manifest);
                script.AddVariable("DefaultInstallDirectory", Plugin.Settings.InstallDirectory);
                script.AddVariable("ServerAddress", Plugin.Settings.ServerAddress);

                script.UseFile(ScriptHelper.GetScriptFilePath(installDirectory, SDK.Enums.ScriptType.Install));

                return script.Execute();
            }

            return 0;
        }

        private int RunNameChangeScript(string installDirectory)
        {
            var manifest = ManifestHelper.Read(installDirectory);
            var path = ScriptHelper.GetScriptFilePath(installDirectory, SDK.Enums.ScriptType.NameChange);

            if (File.Exists(path))
            {
                var script = new PowerShellScript();

                script.AddVariable("InstallDirectory", installDirectory);
                script.AddVariable("GameManifest", manifest);
                script.AddVariable("DefaultInstallDirectory", Plugin.Settings.InstallDirectory);
                script.AddVariable("ServerAddress", Plugin.Settings.ServerAddress);
                script.AddVariable("OldPlayerAlias", "");
                script.AddVariable("NewPlayerAlias", Plugin.Settings.PlayerName);

                script.UseFile(path);

                return script.Execute();
            }

            return 0;
        }

        private int RunKeyChangeScript(string installDirectory)
        {
            var manifest = ManifestHelper.Read(installDirectory);
            var path = ScriptHelper.GetScriptFilePath(installDirectory, SDK.Enums.ScriptType.KeyChange);

            if (File.Exists(path))
            {
                var script = new PowerShellScript();

                var key = Plugin.LANCommanderClient.GetAllocatedKey(manifest.Id);

                script.AddVariable("InstallDirectory", installDirectory);
                script.AddVariable("GameManifest", manifest);
                script.AddVariable("DefaultInstallDirectory", Plugin.Settings.InstallDirectory);
                script.AddVariable("ServerAddress", Plugin.Settings.ServerAddress);
                script.AddVariable("AllocatedKey", key);

                script.UseFile(path);

                return script.Execute();
            }

            return 0;
        }
    }
}
