using LANCommander.SDK.Enums;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LANCommander.SDK
{
    public class GameManager
    {
        private static readonly ILogger Logger;
        private Client Client { get; set; }
        private string DefaultInstallDirectory { get; set; }

        public delegate void OnArchiveEntryExtractionProgressHandler(object sender, ArchiveEntryExtractionProgressArgs e);
        public event OnArchiveEntryExtractionProgressHandler OnArchiveEntryExtractionProgress;

        public delegate void OnArchiveExtractionProgressHandler(long position, long length);
        public event OnArchiveExtractionProgressHandler OnArchiveExtractionProgress;

        public GameManager(Client client)
        {
            Client = client;
        }

        /// <summary>
        /// Downloads, extracts, and runs post-install scripts for the specified game
        /// </summary>
        /// <param name="game">Game to install</param>
        /// <param name="maxAttempts">Maximum attempts in case of transmission error</param>
        /// <returns>Final install path</returns>
        /// <exception cref="Exception"></exception>
        public string Install(Guid gameId, int maxAttempts = 10)
        {
            var game = Client.GetGame(gameId);

            Logger?.LogTrace("Installing game {GameTitle} (GameId)", game.Title, game.Id);

            var result = RetryHelper.RetryOnException<ExtractionResult>(maxAttempts, TimeSpan.FromMilliseconds(500), new ExtractionResult(), () =>
            {
                Logger?.LogTrace("Attempting to download and extract game");

                return DownloadAndExtract(game);
            });

            if (!result.Success && !result.Canceled)
                throw new Exception("Could not extract the installer. Retry the install or check your connection");
            else if (result.Canceled)
                throw new Exception("Game install was canceled");

            GameManifest manifest = null;

            game.InstallDirectory = result.Directory;

            var writeManifestSuccess = RetryHelper.RetryOnException(maxAttempts, TimeSpan.FromSeconds(1), false, () =>
            {
                Logger?.LogTrace("Attempting to get game manifest");

                manifest = Client.GetGameManifest(game.Id);

                ManifestHelper.Write(manifest, game.InstallDirectory);

                return true;
            });

            if (!writeManifestSuccess)
                throw new Exception("Could not grab the manifest file. Retry the install or check your connection");

            Logger?.LogTrace("Saving scripts");

            ScriptHelper.SaveScript(game, ScriptType.Install);
            ScriptHelper.SaveScript(game, ScriptType.Uninstall);
            ScriptHelper.SaveScript(game, ScriptType.NameChange);
            ScriptHelper.SaveScript(game, ScriptType.KeyChange);

            try
            {
                PowerShellRuntime.RunScript(game, ScriptType.Install);
                PowerShellRuntime.RunScript(game, ScriptType.NameChange, /* Plugin.Settings.PlayerName */ "");

                var key = Client.GetAllocatedKey(game.Id);

                PowerShellRuntime.RunScript(game, ScriptType.KeyChange, $"\"{key}\"");
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Could not execute post-install scripts");
            }

            return result.Directory;
        }

        public void Uninstall(string installDirectory)
        {
            var manifest = ManifestHelper.Read(installDirectory);

            try
            {
                Logger?.LogTrace("Running uninstall script");
                PowerShellRuntime.RunScript(installDirectory, ScriptType.Uninstall);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Error running uninstall script");
            }

            Logger?.LogTrace("Attempting to delete the install directory");

            if (Directory.Exists(installDirectory))
                Directory.Delete(installDirectory, true);

            Logger?.LogTrace("Deleted install directory {InstallDirectory}", installDirectory);
        }

        private ExtractionResult DownloadAndExtract(Game game, string installDirectory = "")
        {
            if (game == null)
            {
                Logger?.LogTrace("Game failed to download, no game was specified");

                throw new ArgumentNullException("No game was specified");
            }

            if (String.IsNullOrWhiteSpace(installDirectory))
                installDirectory = DefaultInstallDirectory;

            var destination = Path.Combine(installDirectory, game.Title.SanitizeFilename());

            Logger?.LogTrace("Downloading and extracting {Game} to path {Destination}", game.Title, destination);

            try
            {
                Directory.CreateDirectory(destination);

                using (var gameStream = Client.StreamGame(game.Id))
                using (var reader = ReaderFactory.Open(gameStream))
                {
                    gameStream.OnProgress += (pos, len) =>
                    {
                        OnArchiveExtractionProgress?.Invoke(pos, len);
                    };

                    reader.EntryExtractionProgress += (object sender, ReaderExtractionEventArgs<IEntry> e) =>
                    {
                        OnArchiveEntryExtractionProgress?.Invoke(this, new ArchiveEntryExtractionProgressArgs
                        {
                            Entry = e.Item,
                            Progress = e.ReaderProgress,
                            Reader = reader,
                            Stream = gameStream
                        });
                    };

                    reader.WriteAllToDirectory(destination, new ExtractionOptions()
                    {
                        ExtractFullPath = true,
                        Overwrite = true
                    });
                }
            }
            catch (Exception ex)
            {
                if (false)
                {

                }
                else
                {
                    Logger?.LogError(ex, "Could not extract to path {Destination}", destination);

                    if (Directory.Exists(destination))
                    {
                        Logger?.LogTrace("Cleaning up orphaned install files after bad install");

                        Directory.Delete(destination, true);
                    }

                    throw new Exception("The game archive could not be extracted, is it corrupted? Please try again");
                }
            }

            var extractionResult = new ExtractionResult
            {
                Canceled = false,
            };

            if (!extractionResult.Canceled)
            {
                extractionResult.Success = true;
                extractionResult.Directory = destination;

                Logger?.LogTrace("Game {Game} successfully downloaded and extracted to {Destination}", game.Title, destination);
            }

            return extractionResult;
        }
    }
}
