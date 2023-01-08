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
    public class LANCommanderInstallController : InstallController
    {
        private PlayniteLibraryPlugin Plugin;

        public LANCommanderInstallController(PlayniteLibraryPlugin plugin, Game game) : base(game)
        {
            Name = "Install using LANCommander";
            Plugin = plugin;
            
        }

        public override void Install(InstallActionArgs args)
        {
            var tempPath = System.IO.Path.GetTempFileName();
            var gameId = Guid.Parse(Game.GameId);

            var game = Plugin.LANCommander.GetGame(gameId);

            var tempFile = Download(game);

            var installDirectory = Extract(game, tempFile);
            var installInfo = new GameInstallationData()
            {
                InstallDirectory = installDirectory
            };

            InvokeOnInstalled(new GameInstalledEventArgs(installInfo));

            Plugin.UpdateGamesFromManifest();
        }

        private string Download(LANCommander.SDK.Models.Game game)
        {
            string tempFile = String.Empty;

            var archive = game.Archives.OrderByDescending(a => a.CreatedOn).FirstOrDefault();

            if (archive != null)
            {
                Plugin.PlayniteApi.Dialogs.ActivateGlobalProgress(progress =>
                {
                    progress.ProgressMaxValue = 100;
                    progress.CurrentProgressValue = 0;

                    var destination = Plugin.LANCommander.DownloadArchive(archive.Id, (changed) =>
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
                throw new Exception("Game failed to download");
        }

        private string Extract(LANCommander.SDK.Models.Game game, string archivePath)
        {
            var destination = $"C:\\Games\\{game.Title.SanitizeFilename()}";

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
    }
}
