using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using LANCommander.SDK.Enums;
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
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using LANCommander.SDK.Models;

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
            var gameId = Guid.Parse(Game.GameId);

            var game = Plugin.LANCommander.GetGame(gameId);

            var tempFile = Download(game);

            var installDirectory = Extract(game, tempFile);

            var installInfo = new GameInstallationData()
            {
                InstallDirectory = installDirectory
            };

            PlayniteGame.InstallDirectory = installDirectory;

            File.WriteAllText(Path.Combine(installDirectory, "_manifest.yml"), GetManifest(gameId));

            SaveScript(game, installDirectory, ScriptType.Install);
            SaveScript(game, installDirectory, ScriptType.Uninstall);
            SaveScript(game, installDirectory, ScriptType.NameChange);
            SaveScript(game, installDirectory, ScriptType.KeyChange);

            try
            {
                PowerShellRuntime.RunScript(PlayniteGame, ScriptType.Install);
            }
            catch { }

            InvokeOnInstalled(new GameInstalledEventArgs(installInfo));

            Plugin.UpdateGamesFromManifest();
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
                ZipFile file = null;

                try
                {
                    FileStream fs = File.OpenRead(archivePath);

                    file = new ZipFile(fs);

                    progress.ProgressMaxValue = file.Count;

                    foreach (ZipEntry entry in file)
                    {
                        if (!entry.IsFile)
                            continue;

                        byte[] buffer = new byte[4096];
                        var zipStream = file.GetInputStream(entry);

                        var entryDestination = Path.Combine(destination, entry.Name);
                        var entryDirectory = Path.GetDirectoryName(entryDestination);

                        if (!String.IsNullOrWhiteSpace(entryDirectory))
                            Directory.CreateDirectory(entryDirectory);

                        using (FileStream streamWriter = File.Create(entryDestination))
                        {
                            StreamUtils.Copy(zipStream, streamWriter, buffer);
                        }

                        progress.CurrentProgressValue = entry.ZipFileIndex;
                    }
                }
                finally
                {
                    if (file != null)
                    {
                        file.IsStreamOwner = true;
                        file.Close();
                    }

                    File.Delete(archivePath);
                }
            },
            new GlobalProgressOptions($"Extracting {game.Title}...")
            {
                IsIndeterminate = false,
                Cancelable = false,
            });

            return destination;
        }

        private string GetManifest(Guid gameId)
        {
            var manifest = Plugin.LANCommander.GetGameManifest(gameId);

            var serializer = new SerializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .Build();

            var yaml = serializer.Serialize(manifest);

            return yaml;
        }

        private void SaveScript(LANCommander.SDK.Models.Game game, string installationDirectory, ScriptType type)
        {
            var script = game.Scripts.FirstOrDefault(s => s.Type == type);

            if (script == null)
                return;

            if (script.RequiresAdmin)
                script.Contents = "# Requires Admin" + "\r\n\r\n" + script.Contents;

            Dictionary<ScriptType, string> filenames = new Dictionary<ScriptType, string>() {
                { ScriptType.Install, "_install.ps1" },
                { ScriptType.Uninstall, "_uninstall.ps1" },
                { ScriptType.NameChange, "_changename.ps1" },
                { ScriptType.KeyChange, "_changekey.ps1" }
            };

            if (!filenames.ContainsKey(type))
                return;

            var filename = Path.Combine(installationDirectory, filenames[type]);

            if (File.Exists(filename))
                File.Delete(filename);

            File.WriteAllText(filename, script.Contents);
        }
    }
}
