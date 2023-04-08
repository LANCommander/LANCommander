using LANCommander.PlaynitePlugin.Helpers;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Models;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LANCommander.PlaynitePlugin
{
    public class LANCommanderInstallController : InstallController
    {
        private LANCommanderLibraryPlugin Plugin;
        private PowerShellRuntime PowerShellRuntime;
        private Playnite.SDK.Models.Game PlayniteGame;

        public LANCommanderInstallController(LANCommanderLibraryPlugin plugin, Playnite.SDK.Models.Game game) : base(game)
        {
            Name = "Install using LANCommander";
            Plugin = plugin;
            PlayniteGame = game;
            PowerShellRuntime = new PowerShellRuntime();
        }

        public override void Install(InstallActionArgs args)
        {
            while (!Plugin.ValidateConnection())
            {
                Plugin.ShowAuthenticationWindow();
            }

            var gameId = Guid.Parse(Game.GameId);
            var game = Plugin.LANCommander.GetGame(gameId);

            string tempDownloadLocation;

            if (Plugin.DownloadCache.ContainsKey(gameId))
                tempDownloadLocation = Plugin.DownloadCache[gameId];
            else
            {
                tempDownloadLocation = Download(game);
                Plugin.DownloadCache[gameId] = tempDownloadLocation;
            }

            var installDirectory = RetryHelper.RetryOnException(10, TimeSpan.FromMilliseconds(500), "", () =>
            {
                return Extract(game, tempDownloadLocation);
            });

            if (installDirectory == "")
                throw new Exception("Could not extract the install archive. Retry the install or check your connection.");

            var installInfo = new GameInstallationData()
            {
                InstallDirectory = installDirectory
            };

            PlayniteGame.InstallDirectory = installDirectory;

            SDK.GameManifest manifest = null;

            var writeManifestSuccess = RetryHelper.RetryOnException(10, TimeSpan.FromSeconds(1), false, () =>
            {
                manifest = Plugin.LANCommander.GetGameManifest(gameId);

                WriteManifest(manifest, installDirectory);

                return true;
            });

            if (!writeManifestSuccess)
                throw new Exception("Could not get or write the manifest file. Retry the install or check your connection.");

            SaveScript(game, installDirectory, ScriptType.Install);
            SaveScript(game, installDirectory, ScriptType.Uninstall);
            SaveScript(game, installDirectory, ScriptType.NameChange);
            SaveScript(game, installDirectory, ScriptType.KeyChange);

            try
            {
                PowerShellRuntime.RunScript(PlayniteGame, ScriptType.Install);
                PowerShellRuntime.RunScript(PlayniteGame, ScriptType.NameChange, Plugin.Settings.PlayerName);

                var key = Plugin.LANCommander.GetAllocatedKey(game.Id);

                PowerShellRuntime.RunScript(PlayniteGame, ScriptType.KeyChange, $"\"{key}\"");
            }
            catch { }

            Plugin.UpdateGame(manifest, gameId);

            Plugin.DownloadCache.Remove(gameId);
            File.Delete(tempDownloadLocation);

            InvokeOnInstalled(new GameInstalledEventArgs(installInfo));
        }

        private string Download(LANCommander.SDK.Models.Game game)
        {
            string tempFile = String.Empty;

            if (game != null)
            {
                Plugin.PlayniteApi.Dialogs.ActivateGlobalProgress(progress =>
                {
                    progress.ProgressMaxValue = 100;
                    progress.CurrentProgressValue = 0;

                    var destination = Plugin.LANCommander.DownloadGame(game.Id, (changed) =>
                    {
                        progress.CurrentProgressValue = changed.ProgressPercentage;
                    }, (complete) =>
                    {
                        progress.CurrentProgressValue = 100;
                    });

                    // Lock the thread until download is done
                    while (progress.CurrentProgressValue != 100)
                    {

                    }

                    tempFile = destination;
                },
                new GlobalProgressOptions($"Downloading {game.Title}...")
                {
                    IsIndeterminate = false,
                    Cancelable = false,
                });

                return tempFile;
            }
            else
                throw new Exception("Game failed to download!");
        }

        private string Extract(LANCommander.SDK.Models.Game game, string archivePath)
        {
            var destination = Path.Combine(Plugin.Settings.InstallDirectory, game.Title.SanitizeFilename());

            Plugin.PlayniteApi.Dialogs.ActivateGlobalProgress(progress =>
            {
                Directory.CreateDirectory(destination);

                using (var fs = File.OpenRead(archivePath))
                using (var ts = new TrackableStream(fs))
                using (var reader = ReaderFactory.Open(ts))
                {
                    progress.ProgressMaxValue = ts.Length;
                    ts.OnProgress += (pos, len) =>
                    {
                        progress.CurrentProgressValue = pos;
                    };

                    reader.WriteAllToDirectory(destination, new ExtractionOptions()
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });
                }
            },
            new GlobalProgressOptions($"Extracting {game.Title}...")
            {
                IsIndeterminate = false,
                Cancelable = false,
            });

            return destination;
        }

        private void WriteManifest(SDK.GameManifest manifest, string installDirectory)
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(new PascalCaseNamingConvention())
                .Build();

            var yaml = serializer.Serialize(manifest);

            File.WriteAllText(Path.Combine(installDirectory, "_manifest.yml"), yaml);
        }

        private void SaveScript(LANCommander.SDK.Models.Game game, string installationDirectory, ScriptType type)
        {
            var script = game.Scripts.FirstOrDefault(s => s.Type == type);

            if (script == null)
                return;

            if (script.RequiresAdmin)
                script.Contents = "# Requires Admin" + "\r\n\r\n" + script.Contents;

            var filename = PowerShellRuntime.GetScriptFilePath(PlayniteGame, type);

            if (File.Exists(filename))
                File.Delete(filename);

            File.WriteAllText(filename, script.Contents);
        }
    }
}
