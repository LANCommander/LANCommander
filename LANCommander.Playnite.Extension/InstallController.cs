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
        public static readonly ILogger Logger = LogManager.GetLogger();

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
            Logger.Trace("Game install triggered, checking connection...");

            while (!Plugin.ValidateConnection())
            {
                Logger.Trace("User not authenticated. Opening auth window...");

                Plugin.ShowAuthenticationWindow();
            }

            var gameId = Guid.Parse(Game.GameId);
            var game = Plugin.LANCommander.GetGame(gameId);

            Logger.Trace($"Installing game {game.Title} ({game.Id})...");

            var result = RetryHelper.RetryOnException<ExtractionResult>(10, TimeSpan.FromMilliseconds(500), new ExtractionResult(), () =>
            {
                Logger.Trace("Attempting to download and extract game...");
                return DownloadAndExtractGame(game);
            });

            if (!result.Success && !result.Canceled)
                throw new Exception("Could not extract the install archive. Retry the install or check your connection.");
            else if (result.Canceled)
                throw new Exception("Install was canceled");

            var installInfo = new GameInstallationData()
            {
                InstallDirectory = result.Directory
            };

            PlayniteGame.InstallDirectory = result.Directory;

            SDK.GameManifest manifest = null;

            var writeManifestSuccess = RetryHelper.RetryOnException(10, TimeSpan.FromSeconds(1), false, () =>
            {
                Logger.Trace("Attempting to get game manifest...");

                manifest = Plugin.LANCommander.GetGameManifest(gameId);

                WriteManifest(manifest, result.Directory);

                return true;
            });

            if (!writeManifestSuccess)
                throw new Exception("Could not get or write the manifest file. Retry the install or check your connection.");

            Logger.Trace("Saving scripts...");

            SaveScript(game, result.Directory, ScriptType.Install);
            SaveScript(game, result.Directory, ScriptType.Uninstall);
            SaveScript(game, result.Directory, ScriptType.NameChange);
            SaveScript(game, result.Directory, ScriptType.KeyChange);

            if (game.Redistributables != null && game.Redistributables.Count() > 0)
            {
                Logger.Trace("Installing required redistributables...");
                InstallRedistributables(game);
            }

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

            InvokeOnInstalled(new GameInstalledEventArgs(installInfo));
        }

        private ExtractionResult DownloadAndExtractGame(LANCommander.SDK.Models.Game game)
        {
            if (game == null)
            {
                Logger.Trace("Game failed to download! No game was specified!");

                throw new Exception("Game failed to download!");
            }

            var destination = Path.Combine(Plugin.Settings.InstallDirectory, game.Title.SanitizeFilename());

            Logger.Trace($"Downloading and extracting \"{game.Title}\" to path {destination}");
            var result = Plugin.PlayniteApi.Dialogs.ActivateGlobalProgress(progress =>
            {
                try
                {
                    Directory.CreateDirectory(destination);
                    progress.ProgressMaxValue = 100;
                    progress.CurrentProgressValue = 0;

                    using (var gameStream = Plugin.LANCommander.StreamGame(game.Id))
                    using (var reader = ReaderFactory.Open(gameStream))
                    {
                        progress.ProgressMaxValue = gameStream.Length;

                        gameStream.OnProgress += (pos, len) =>
                        {
                            progress.CurrentProgressValue = pos;
                        };

                        reader.EntryExtractionProgress += (object sender, ReaderExtractionEventArgs<IEntry> e) =>
                        {
                            if (progress.CancelToken != null && progress.CancelToken.IsCancellationRequested)
                            {
                                reader.Cancel();
                                progress.IsIndeterminate = true;

                                reader.Dispose();
                                gameStream.Dispose();
                            }
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
                    if (progress.CancelToken != null && progress.CancelToken.IsCancellationRequested)
                    {
                        Logger.Trace("User cancelled the download");

                        if (Directory.Exists(destination))
                        {
                            Logger.Trace("Cleaning up orphaned install files after cancelled install...");

                            Directory.Delete(destination, true);
                        }
                    }
                    else
                    {
                        Logger.Error(ex, $"Could not extract to path {destination}");

                        if (Directory.Exists(destination))
                        {
                            Logger.Trace("Cleaning up orphaned install files after bad install...");

                            Directory.Delete(destination, true);
                        }

                        throw new Exception("The game archive could not be extracted. Please try again or fix the archive!");
                    }
                }
            },
            new GlobalProgressOptions($"Downloading {game.Title}...")
            {
                IsIndeterminate = false,
                Cancelable = true,
            });

            var extractionResult = new ExtractionResult
            {
                Canceled = result.Canceled
            };

            if (!result.Canceled)
            {
                extractionResult.Success = true;
                extractionResult.Directory = destination;
                Logger.Trace($"Game successfully downloaded and extracted to {destination}");
            }

            return extractionResult;
        }

        private void InstallRedistributables(LANCommander.SDK.Models.Game game)
        {
            foreach (var redistributable in game.Redistributables)
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
                        var extractionResult = DownloadAndExtractRedistributable(redistributable);
                        
                        if (extractionResult.Success)
                        {
                            extractTempPath = extractionResult.Directory;

                            PowerShellRuntime.RunScript(installScriptTempFile, installScript.RequiresAdmin, null, extractTempPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Redistributable {redistributable.Name} failed to install");
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
        }

        private ExtractionResult DownloadAndExtractRedistributable(LANCommander.SDK.Models.Redistributable redistributable)
        {
            if (redistributable == null)
            {
                Logger.Trace("Redistributable failed to download! No redistributable was specified!");

                throw new Exception("Redistributable failed to download!");
            }

            var destination = Path.Combine(Path.GetTempPath(), redistributable.Name.SanitizeFilename());

            Logger.Trace($"Downloading and extracting \"{redistributable.Name}\" to path {destination}");
            var result = Plugin.PlayniteApi.Dialogs.ActivateGlobalProgress(progress =>
            {
                try
                {
                    Directory.CreateDirectory(destination);
                    progress.ProgressMaxValue = 100;
                    progress.CurrentProgressValue = 0;

                    using (var redistributableStream = Plugin.LANCommander.StreamRedistributable(redistributable.Id))
                    using (var reader = ReaderFactory.Open(redistributableStream))
                    {
                        progress.ProgressMaxValue = redistributableStream.Length;

                        redistributableStream.OnProgress += (pos, len) =>
                        {
                            progress.CurrentProgressValue = pos;
                        };

                        reader.EntryExtractionProgress += (object sender, ReaderExtractionEventArgs<IEntry> e) =>
                        {
                            if (progress.CancelToken != null && progress.CancelToken.IsCancellationRequested)
                            {
                                reader.Cancel();
                                progress.IsIndeterminate = true;

                                reader.Dispose();
                                redistributableStream.Dispose();
                            }
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
                    if (progress.CancelToken != null && progress.CancelToken.IsCancellationRequested)
                    {
                        Logger.Trace("User cancelled the download");

                        if (Directory.Exists(destination))
                        {
                            Logger.Trace("Cleaning up orphaned install files after cancelled install...");

                            Directory.Delete(destination, true);
                        }
                    }
                    else
                    {
                        Logger.Error(ex, $"Could not extract to path {destination}");

                        if (Directory.Exists(destination))
                        {
                            Logger.Trace("Cleaning up orphaned install files after bad install...");

                            Directory.Delete(destination, true);
                        }

                        throw new Exception("The redistributable archive could not be extracted. Please try again or fix the archive!");
                    }
                }
            },
            new GlobalProgressOptions($"Downloading {redistributable.Name}...")
            {
                IsIndeterminate = false,
                Cancelable = true,
            });

            var extractionResult = new ExtractionResult
            {
                Canceled = result.Canceled
            };

            if (!result.Canceled)
            {
                extractionResult.Success = true;
                extractionResult.Directory = destination;
                Logger.Trace($"Redistributable successfully downloaded and extracted to {destination}");
            }

            return extractionResult;
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
            var destination = Path.Combine(installDirectory, "_manifest.yml");

            Logger.Trace($"Attempting to write manifest to path {destination}");

            var serializer = new SerializerBuilder()
                .WithNamingConvention(new PascalCaseNamingConvention())
                .Build();

            Logger.Trace("Serializing manifest...");
            var yaml = serializer.Serialize(manifest);

            Logger.Trace("Writing manifest file...");
            File.WriteAllText(destination, yaml);
        }

        private string SaveTempScript(LANCommander.SDK.Models.Script script)
        {
            var tempPath = Path.GetTempFileName();

            File.Move(tempPath, tempPath + ".ps1");

            tempPath = tempPath + ".ps1";

            Logger.Trace($"Writing script {script.Name} to {tempPath}");

            File.WriteAllText(tempPath, script.Contents);

            return tempPath;
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

            Logger.Trace($"Writing {type} script to {filename}");

            File.WriteAllText(filename, script.Contents);
        }
    }
}
