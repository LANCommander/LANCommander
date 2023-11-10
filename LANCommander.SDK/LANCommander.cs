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
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LANCommander.SDK
{
    public class ArchiveExtractionProgressArgs : EventArgs
    {
        public long Position { get; set; }
        public long Length { get; set; }
    }

    public class ArchiveEntryExtractionProgressArgs : EventArgs
    {
        public IReader Reader { get; set; }
        public TrackableStream Stream { get; set; }
        public ReaderProgress Progress { get; set; }
        public IEntry Entry { get; set; }
    }

    public class LANCommander
    {
        public static readonly ILogger Logger;

        private const string ManifestFilename = "_manifest.yml";

        private string DefaultInstallDirectory { get; set; }
        public Client Client { get; set; }
        private PowerShellRuntime PowerShellRuntime;

        public delegate void OnArchiveEntryExtractionProgressHandler(object sender, ArchiveEntryExtractionProgressArgs e);
        public event OnArchiveEntryExtractionProgressHandler OnArchiveEntryExtractionProgress;

        public delegate void OnArchiveExtractionProgressHandler(long position, long length);
        public event OnArchiveExtractionProgressHandler OnArchiveExtractionProgress;

        public LANCommander(string baseUrl)
        {
            Client = new Client(baseUrl);
        }

        /// <summary>
        /// Downloads, extracts, and runs post-install scripts for the specified game
        /// </summary>
        /// <param name="game">Game to install</param>
        /// <param name="maxAttempts">Maximum attempts in case of transmission error</param>
        /// <returns>Final install path</returns>
        /// <exception cref="Exception"></exception>
        public string InstallGame(Guid gameId, int maxAttempts = 10)
        {
            var game = Client.GetGame(gameId);

            Logger.LogTrace("Installing game {GameTitle} (GameId)", game.Title, game.Id);

            var result = RetryHelper.RetryOnException<ExtractionResult>(maxAttempts, TimeSpan.FromMilliseconds(500), new ExtractionResult(), () =>
            {
                Logger.LogTrace("Attempting to download and extract game");

                return DownloadAndExtractGame(game);
            });

            if (!result.Success && !result.Canceled)
                throw new Exception("Could not extract the installer. Retry the install or check your connection");
            else if (result.Canceled)
                throw new Exception("Game install was canceled");

            GameManifest manifest = null;

            game.InstallDirectory = result.Directory;

            var writeManifestSuccess = RetryHelper.RetryOnException(maxAttempts, TimeSpan.FromSeconds(1), false, () =>
            {
                Logger.LogTrace("Attempting to get game manifest");

                manifest = Client.GetGameManifest(game.Id);

                WriteManifest(manifest, game.InstallDirectory);

                return true;
            });

            if (!writeManifestSuccess)
                throw new Exception("Could not grab the manifest file. Retry the install or check your connection");

            Logger.LogTrace("Saving scripts");

            SaveScript(game, ScriptType.Install);
            SaveScript(game, ScriptType.Uninstall);
            SaveScript(game, ScriptType.NameChange);
            SaveScript(game, ScriptType.KeyChange);

            if (game.Redistributables != null && game.Redistributables.Count() > 0)
            {
                Logger.LogTrace("Installing required redistributables");
                InstallRedistributables(game);
            }

            try
            {
                PowerShellRuntime.RunScript(game, ScriptType.Install);
                PowerShellRuntime.RunScript(game, ScriptType.NameChange, /* Plugin.Settings.PlayerName */ "");

                var key = Client.GetAllocatedKey(game.Id);

                PowerShellRuntime.RunScript(game, ScriptType.KeyChange, $"\"{key}\"");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Could not execute post-install scripts");
            }

            // Plugin.UpdateGame(manifest, gameId)

            // Plugin.DownloadCache.Remove(gameId);

            return result.Directory;
        }

        private ExtractionResult DownloadAndExtractGame(Game game, string installDirectory = "")
        {
            if (game == null)
            {
                Logger.LogTrace("Game failed to download, no game was specified");

                throw new ArgumentNullException("No game was specified");
            }

            if (String.IsNullOrWhiteSpace(installDirectory))
                installDirectory = DefaultInstallDirectory;

            var destination = Path.Combine(installDirectory, game.Title.SanitizeFilename());

            Logger.LogTrace("Downloading and extracting {Game} to path {Destination}", game.Title, destination);

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
                    Logger.LogError(ex, "Could not extract to path {Destination}", destination);

                    if (Directory.Exists(destination))
                    {
                        Logger.LogTrace("Cleaning up orphaned install files after bad install");

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

                Logger.LogTrace("Game {Game} successfully downloaded and extracted to {Destination}", game.Title, destination);
            }

            return extractionResult;
        }

        private void InstallRedistributables(Game game)
        {
            foreach (var redistributable in game.Redistributables)
            {
                InstallRedistributable(redistributable);
            }
        }

        private void InstallRedistributable(Redistributable redistributable)
        {
            string installScriptTempFile = null;
            string detectionScriptTempFile = null;
            string extractTempPath = null;

            try
            {
                var installScript = redistributable.Scripts.FirstOrDefault(s => s.Type == ScriptType.Install);
                installScriptTempFile = SaveTempScript(installScript);

                var detectionScript = redistributable.Scripts.FirstOrDefault(s => s.Type == ScriptType.DetectInstall);
                detectionScriptTempFile = SaveTempScript(detectionScript);

                var detectionResult = PowerShellRuntime.RunScript(detectionScriptTempFile, detectionScript.RequiresAdmin);

                // Redistributable is not installed
                if (detectionResult == 0)
                {
                    if (redistributable.Archives.Count() > 0)
                    {
                        var extractionResult = DownloadAndExtractRedistributable(redistributable);

                        if (extractionResult.Success)
                        {
                            extractTempPath = extractionResult.Directory;

                            PowerShellRuntime.RunScript(installScriptTempFile, installScript.RequiresAdmin, null, extractTempPath);
                        }
                    }
                    else
                    {
                        PowerShellRuntime.RunScript(installScriptTempFile, installScript.RequiresAdmin, null, extractTempPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Redistributable {Redistributable} failed to install", redistributable.Name);
            }
            finally
            {
                if (File.Exists(installScriptTempFile))
                    File.Delete(installScriptTempFile);

                if (File.Exists(detectionScriptTempFile))
                    File.Delete(detectionScriptTempFile);

                if (Directory.Exists(extractTempPath))
                    Directory.Delete(extractTempPath);
            }
        }

        private ExtractionResult DownloadAndExtractRedistributable(Redistributable redistributable)
        {
            if (redistributable == null)
            {
                Logger.LogTrace("Redistributable failed to download! No redistributable was specified");
                throw new ArgumentNullException("No redistributable was specified");
            }

            var destination = Path.Combine(Path.GetTempPath(), redistributable.Name.SanitizeFilename());

            Logger.LogTrace("Downloading and extracting {Redistributable} to path {Destination}", redistributable.Name, destination);

            try
            {
                Directory.CreateDirectory(destination);

                using (var redistributableStream = Client.StreamRedistributable(redistributable.Id))
                using (var reader = ReaderFactory.Open(redistributableStream))
                {
                    redistributableStream.OnProgress += (pos, len) =>
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
                            Stream = redistributableStream
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
                Logger.LogError(ex, "Could not extract to path {Destination}", destination);

                if (Directory.Exists(destination))
                {
                    Logger.LogTrace("Cleaning up orphaned files after bad install");

                    Directory.Delete(destination, true);
                }

                throw new Exception("The redistributable archive could not be extracted, is it corrupted? Please try again");
            }

            var extractionResult = new ExtractionResult
            {
                Canceled = false
            };

            if (!extractionResult.Canceled)
            {
                extractionResult.Success = true;
                extractionResult.Directory = destination;
                Logger.LogTrace("Redistributable {Redistributable} successfully downloaded and extracted to {Destination}", redistributable.Name, destination);
            }

            return extractionResult;
        }

        public void WriteManifest(GameManifest manifest, string installDirectory)
        {
            var destination = Path.Combine(installDirectory, ManifestFilename);

            Logger.LogTrace("Attempting to write manifest to path {Destination}", destination);

            var serializer = new SerializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .Build();

            Logger.LogTrace("Serializing manifest");

            var yaml = serializer.Serialize(manifest);

            Logger.LogTrace("Writing manifest file");

            File.WriteAllText(destination, yaml);
        }

        private string SaveTempScript(Script script)
        {
            var tempPath = Path.GetTempFileName();

            // PowerShell will only run scripts with the .ps1 file extension
            File.Move(tempPath, tempPath + ".ps1");

            Logger.LogTrace("Writing script {Script} to {Destination}", script.Name, tempPath);

            File.WriteAllText(tempPath, script.Contents);

            return tempPath;
        }

        private void SaveScript(Game game, ScriptType type)
        {
            var script = game.Scripts.FirstOrDefault(s => s.Type == type);

            if (script == null)
                return;

            if (script.RequiresAdmin)
                script.Contents = "# Requires Admin" + "\r\n\r\n" + script.Contents;

            var filename = PowerShellRuntime.GetScriptFilePath(game, type);

            if (File.Exists(filename))
                File.Delete(filename);

            Logger.LogTrace("Writing {ScriptType} script to {Destination}", type, filename);

            File.WriteAllText(filename, script.Contents);
        }

        public void ChangeAlias(string alias)
        {

        }
    }
}
