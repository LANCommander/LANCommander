﻿using Force.Crc32;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Exceptions;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation.Remoting;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace LANCommander.SDK.Services
{
    public class GameInstallProgress
    {
        public Game Game { get; set; }
        public GameInstallStatus Status { get; set; }
        public float Progress
        {
            get
            {
                return BytesDownloaded / (float)TotalBytes;
            }
            set { }
        }
        public double TransferSpeed { get; set; }
        public long BytesDownloaded { get; set; }
        public long TotalBytes { get; set; }
    }

    public class GameService
    {
        private readonly ILogger Logger;
        private Client Client { get; set; }
        private string DefaultInstallDirectory { get; set; }

        public delegate void OnArchiveEntryExtractionProgressHandler(object sender, ArchiveEntryExtractionProgressArgs e);
        public event OnArchiveEntryExtractionProgressHandler OnArchiveEntryExtractionProgress;

        public delegate void OnArchiveExtractionProgressHandler(long position, long length, Game game);
        public event OnArchiveExtractionProgressHandler OnArchiveExtractionProgress;

        public delegate void OnGameInstallProgressUpdateHandler(GameInstallProgress e);
        public event OnGameInstallProgressUpdateHandler OnGameInstallProgressUpdate;

        public const string PlayerAliasFilename = "PlayerAlias";
        public const string KeyFilename = "Key";

        private TrackableStream DownloadStream;
        private IReader Reader;

        private GameInstallProgress GameInstallProgress = new GameInstallProgress();

        private Dictionary<Guid, Process> RunningProcesses = new Dictionary<Guid, Process>();

        public GameService(Client client, string defaultInstallDirectory)
        {
            Client = client;
            DefaultInstallDirectory = defaultInstallDirectory;
        }

        public GameService(Client client, string defaultInstallDirectory, ILogger logger)
        {
            Client = client;
            DefaultInstallDirectory = defaultInstallDirectory;
            Logger = logger;
        }

        public async Task<IEnumerable<Game>> GetAsync()
        {
            return await Client.GetRequestAsync<IEnumerable<Game>>("/api/Games");
        }

        public Game Get(Guid id)
        {
            return Client.GetRequest<Game>($"/api/Games/{id}");
        }

        public async Task<Game> GetAsync(Guid id)
        {
            return await Client.GetRequestAsync<Game>($"/api/Games/{id}");
        }

        public GameManifest GetManifest(Guid id)
        {
            return Client.GetRequest<GameManifest>($"/api/Games/{id}/Manifest");
        }

        public async Task<IEnumerable<GameManifest>> GetManifestsAsync(string installDirectory, Guid id)
        {
            var manifests = new List<GameManifest>();
            var mainManifest = await ManifestHelper.ReadAsync(installDirectory, id);

            if (mainManifest == null)
                return manifests;

            manifests.Add(mainManifest);

            if (mainManifest.DependentGames != null)
            {
                foreach (var dependentGameId in mainManifest.DependentGames)
                {
                    try
                    {
                        var dependentGameManifest = await ManifestHelper.ReadAsync(installDirectory, dependentGameId);

                        if (dependentGameManifest.Type == GameType.Expansion || dependentGameManifest.Type == GameType.Mod)
                            manifests.Add(dependentGameManifest);
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogError(ex, $"Could not load manifest from dependent game {dependentGameId}");
                    }
                }
            }

            return manifests;
        }

        public async Task<IEnumerable<Models.Action>> GetActionsAsync(string installDirectory, Guid id)
        {
            var actions = new List<Models.Action>();
            var manifests = await GetManifestsAsync(installDirectory, id);

            foreach (var manifest in manifests.Where(m => m != null && m.Actions != null))
            {
                actions.AddRange(manifest.Actions.OrderBy(a => a.SortOrder).ToList());
            }

            if (Client.IsConnected())
            {
                var remoteGame = await Client.Games.GetAsync(id);

                if (remoteGame != null && remoteGame.Servers != null)
                    actions.AddRange(remoteGame.Servers.Where(s => s.Actions != null).SelectMany(s => s.Actions));
            }

            if (manifests.Any(m => m.OnlineMultiplayer != null && m.OnlineMultiplayer.NetworkProtocol == NetworkProtocol.Lobby || m.LanMultiplayer != null && m.LanMultiplayer.NetworkProtocol == NetworkProtocol.Lobby))
            {
                var primaryAction = actions.First();
                var lobbies = Client.Lobbies.GetSteamLobbies(installDirectory, id);

                foreach (var lobby in lobbies)
                {
                    var lobbyAction = new Models.Action
                    {
                        Arguments = $"{primaryAction.Arguments} +connect_lobby {lobby.Id}",
                        IsPrimaryAction = true,
                        Name = $"Join {lobby.ExternalUsername}'s lobby",
                        SortOrder = actions.Count,
                        Variables = primaryAction.Variables,
                        Path = primaryAction.Path,
                        WorkingDirectory = primaryAction.WorkingDirectory
                    };

                    actions.Add(lobbyAction);
                }
            }

            return actions;
        }

        private TrackableStream Stream(Guid id)
        {
            return Client.StreamRequest($"/api/Games/{id}/Download");
        }

        public async Task StartPlaySessionAsync(Guid id)
        {
            Logger?.LogTrace("Starting a game session...");

            await Client.PostRequestAsync<object>($"/api/PlaySessions/Start/{id}");
        }

        public async Task EndPlaySessionAsync(Guid id)
        {
            Logger?.LogTrace("Ending a game session...");

            try
            {
                await Client.PostRequestAsync<object>($"/api/PlaySessions/End/{id}");
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Failed sending end session request to server");
            }
        }

        public string GetAllocatedKey(Guid id)
        {
            Logger?.LogTrace("Requesting allocated key...");

            var macAddress = Client.GetMacAddress();

            var request = new KeyRequest()
            {
                GameId = id,
                MacAddress = macAddress,
                ComputerName = Environment.MachineName,
                IpAddress = Client.GetIpAddress(),
            };

            var response = Client.PostRequest<Key>($"/api/Keys/GetAllocated/{id}", request);

            if (response == null)
                return string.Empty;

            return response.Value;
        }

        public async Task<string> GetAllocatedKeyAsync(Guid id)
        {
            Logger?.LogTrace("Requesting allocated key...");

            var macAddress = Client.GetMacAddress();

            var request = new KeyRequest()
            {
                GameId = id,
                MacAddress = macAddress,
                ComputerName = Environment.MachineName,
                IpAddress = Client.GetIpAddress(),
            };

            var response = await Client.PostRequestAsync<Key>($"/api/Keys/GetAllocated/{id}", request);

            if (response == null)
                return string.Empty;

            return response.Value;
        }

        public string GetNewKey(Guid id)
        {
            Logger?.LogTrace("Requesting new key allocation...");

            var macAddress = Client.GetMacAddress();

            var request = new KeyRequest()
            {
                GameId = id,
                MacAddress = macAddress,
                ComputerName = Environment.MachineName,
                IpAddress = Client.GetIpAddress(),
            };

            var response = Client.PostRequest<Key>($"/api/Keys/Allocate/{id}", request);

            if (response == null)
                return string.Empty;

            return response.Value;
        }

        /// <summary>
        /// Downloads, extracts, and runs post-install scripts for the specified game
        /// </summary>
        /// <param name="game">Game to install</param>
        /// <param name="maxAttempts">Maximum attempts in case of transmission error</param>
        /// <returns>Final install path</returns>
        /// <exception cref="Exception"></exception>
        public async Task<string> InstallAsync(Guid gameId, string installDirectory = "", int maxAttempts = 10)
        {
            GameManifest manifest = null;

            if (string.IsNullOrWhiteSpace(installDirectory))
                installDirectory = Client.DefaultInstallDirectory;

            var game = Get(gameId);
            var destination = GetInstallDirectory(game, installDirectory);

            GameInstallProgress.Game = game;
            GameInstallProgress.Status = GameInstallStatus.Downloading;
            GameInstallProgress.Progress = 0;
            GameInstallProgress.TransferSpeed = 0;
            GameInstallProgress.TotalBytes = 1;
            GameInstallProgress.BytesDownloaded = 0;

            OnGameInstallProgressUpdate?.Invoke(GameInstallProgress);

            // Handle Standalone Mods
            if (game.Type == GameType.StandaloneMod && game.BaseGame != null)
            {
                destination = GetInstallDirectory(game.BaseGame, installDirectory);

                if (!Directory.Exists(destination))
                    destination = await InstallAsync(game.BaseGame.Id, installDirectory, maxAttempts);
            }

            try
            {
                if (ManifestHelper.Exists(destination, game.Id))
                    manifest = ManifestHelper.Read(destination, game.Id);
            }
            catch (Exception ex)
            {
                Logger?.LogTrace(ex, "Error reading manifest before install");
            }

            Logger?.LogTrace("Installing game {GameTitle} ({GameId})", game.Title, game.Id);

            // Download and extract
            var result = await RetryHelper.RetryOnExceptionAsync(maxAttempts, TimeSpan.FromMilliseconds(500), new ExtractionResult(), async () =>
            {
                Logger?.LogTrace("Attempting to download and extract game");

                return await Task.Run(() => DownloadAndExtract(game, destination));
            });

            if (!result.Success && !result.Canceled)
                throw new InstallException("Could not extract the installer. Retry the install or check your connection");
            else if (result.Canceled)
                throw new InstallCanceledException("Game install was canceled");

            game.InstallDirectory = result.Directory;

            // Game is extracted, get metadata
            var writeManifestSuccess = RetryHelper.RetryOnException(maxAttempts, TimeSpan.FromSeconds(1), false, () =>
            {
                Logger?.LogTrace("Attempting to get game manifest");

                manifest = GetManifest(game.Id);

                ManifestHelper.Write(manifest, game.InstallDirectory);

                return true;
            });

            if (!writeManifestSuccess)
                throw new InstallException("Could not grab the manifest file. Retry the install or check your connection");

            Logger?.LogTrace("Saving scripts");

            foreach (var script in game.Scripts)
            {
                ScriptHelper.SaveScriptAsync(game, script.Type);
            }

            GameInstallProgress.Progress = 1;
            GameInstallProgress.BytesDownloaded = GameInstallProgress.TotalBytes;
            GameInstallProgress.Status = GameInstallStatus.InstallingRedistributables;

            OnGameInstallProgressUpdate?.Invoke(GameInstallProgress);

            #region Install Redistributables
            if (game.Redistributables != null && game.Redistributables.Any())
            {
                Logger?.LogTrace("Installing redistributables");

                await Client.Redistributables.InstallAsync(game);
            }
            #endregion

            #region Download Latest Save
            Logger?.LogTrace("Attempting to download the latest save");

            GameInstallProgress.Status = GameInstallStatus.DownloadingSaves;

            OnGameInstallProgressUpdate?.Invoke(GameInstallProgress);

            await Client.Saves.DownloadAsync(game.InstallDirectory, game.Id);
            #endregion

            #region Run Scripts
            if (game.Scripts != null && game.Scripts.Any())
            {
                GameInstallProgress.Status = GameInstallStatus.RunningScripts;

                OnGameInstallProgressUpdate?.Invoke(GameInstallProgress);

                try
                {
                    var allocatedKey = await GetAllocatedKeyAsync(game.Id);

                    await Client.Scripts.RunInstallScriptAsync(game.InstallDirectory, game.Id);
                    await Client.Scripts.RunKeyChangeScriptAsync(game.InstallDirectory, game.Id, allocatedKey);
                    await Client.Scripts.RunNameChangeScriptAsync(game.InstallDirectory, game.Id, await Client.Profile.GetAliasAsync());
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "Scripts failed to execute for mod/expansion {GameTitle} ({GameId})", game.Title, game.Id);
                }
            }
            #endregion

            #region Install Expansions/Mods
            foreach (var dependentGame in game.DependentGames.Where(g => g.Type == GameType.Expansion || g.Type == GameType.Mod))
            {
                if (dependentGame.Type == GameType.Expansion)
                    GameInstallProgress.Status = GameInstallStatus.InstallingExpansions;
                else if (dependentGame.Type == GameType.Mod)
                    GameInstallProgress.Status = GameInstallStatus.InstallingMods;

                OnGameInstallProgressUpdate?.Invoke(GameInstallProgress);

                try
                {
                    await InstallAsync(dependentGame.Id, installDirectory);
                }
                catch (InstallCanceledException ex)
                {
                    Logger?.LogDebug("Install canceled");

                    GameInstallProgress.Status = GameInstallStatus.Canceled;
                    OnGameInstallProgressUpdate?.Invoke(GameInstallProgress);

                    throw;
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "Failed to install dependent game {GameTitle} ({GameId})", dependentGame.Title, dependentGame.Id);

                    GameInstallProgress.Status = GameInstallStatus.Failed;
                    OnGameInstallProgressUpdate?.Invoke(GameInstallProgress);

                    throw;
                }

                try
                {
                    if (dependentGame.BaseGame == null)
                        dependentGame.BaseGame = game;

                    GameInstallProgress.Status = GameInstallStatus.RunningScripts;
                    OnGameInstallProgressUpdate?.Invoke(GameInstallProgress);

                    var key = await GetAllocatedKeyAsync(dependentGame.Id);

                    await Client.Scripts.RunInstallScriptAsync(game.InstallDirectory, dependentGame.Id);
                    await Client.Scripts.RunKeyChangeScriptAsync(game.InstallDirectory, dependentGame.Id, key);
                    await Client.Scripts.RunNameChangeScriptAsync(game.InstallDirectory, dependentGame.Id, await Client.Profile.GetAliasAsync());
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "Scripts failed to execute for mod/expansion {GameTitle} ({GameId})", dependentGame.Title, dependentGame.Id);
                }
            }
            #endregion

            GameInstallProgress.Status = GameInstallStatus.Complete;
            GameInstallProgress.Progress = 1;
            GameInstallProgress.BytesDownloaded = GameInstallProgress.TotalBytes;

            OnGameInstallProgressUpdate?.Invoke(GameInstallProgress);

            return game.InstallDirectory;
        }

        public async Task UninstallAsync(string installDirectory, Guid gameId)
        {
            var manifest = ManifestHelper.Read(installDirectory, gameId);

            #region Uninstall Dependent Games
            if (manifest.DependentGames != null)
            {
                foreach (var dependentGame in manifest.DependentGames)
                {
                    await UninstallAsync(installDirectory, dependentGame);
                }
            }
            #endregion

            #region Delete Files
            var fileListPath = GetMetadataFilePath(installDirectory, gameId, "FileList.txt");

            if (File.Exists(fileListPath))
            {
                var fileList = await File.ReadAllLinesAsync(fileListPath);
                var files = fileList.Select(l => l.Split('|').FirstOrDefault().Trim());

                Logger?.LogDebug("Attempting to delete the install files");

                foreach (var file in files.Where(f => !f.EndsWith("/")))
                {
                    var localPath = Path.Combine(installDirectory, file);

                    try
                    {
                        if (File.Exists(localPath))
                            File.Delete(localPath);

                        Logger?.LogTrace("Deleted file {LocalPath}", localPath);
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogWarning(ex, "Could not remove file {LocalPath}", localPath);
                    }
                }

                Logger?.LogDebug("Attempting to delete any empty directories");

                DirectoryHelper.DeleteEmptyDirectories(installDirectory);

                if (!Directory.Exists(installDirectory))
                    Logger?.LogDebug("Deleted install directory {InstallDirectory}", installDirectory);
                else
                    Logger?.LogTrace("Removed game files for {GameTitle} ({GameId})", manifest.Title, gameId);
            }
            else
            {
                Directory.Delete(installDirectory, true);
            }
            #endregion

            await Client.Scripts.RunUninstallScriptAsync(installDirectory, gameId);

            #region Cleanup Install Directory
            var metadataPath = GetMetadataDirectoryPath(installDirectory, gameId);

            if (Directory.Exists(metadataPath))
                Directory.Delete(metadataPath, true);

            DirectoryHelper.DeleteEmptyDirectories(installDirectory);
            #endregion
        }

        private ExtractionResult DownloadAndExtract(Game game, string destination)
        {
            var stopwatch = Stopwatch.StartNew();

            if (game == null)
            {
                Logger?.LogTrace("Game failed to download, no game was specified");

                throw new ArgumentNullException("No game was specified");
            }

            Logger?.LogTrace("Downloading and extracting {Game} to path {Destination}", game.Title, destination);

            var extractionResult = new ExtractionResult
            {
                Canceled = false,
            };

            var fileManifest = new StringBuilder();

            try
            {
                Directory.CreateDirectory(destination);

                DownloadStream = Stream(game.Id);
                Reader = ReaderFactory.Open(DownloadStream);

                long lastPosition = 0;

                DownloadStream.OnProgress += (pos, len) =>
                {
                    if (stopwatch.ElapsedMilliseconds > 500)
                    {
                        var bytesThisInterval = pos - lastPosition;

                        GameInstallProgress.BytesDownloaded = pos;
                        GameInstallProgress.TotalBytes = len;
                        GameInstallProgress.TransferSpeed = (double)(bytesThisInterval / (stopwatch.ElapsedMilliseconds / 1000d));

                        OnGameInstallProgressUpdate?.Invoke(GameInstallProgress);

                        lastPosition = pos;

                        stopwatch.Restart();
                    }
                };

                Reader.EntryExtractionProgress += (sender, e) =>
                {
                    // Do we need this granular of control? If so, invocations should be rate limited
                    OnArchiveEntryExtractionProgress?.Invoke(this, new ArchiveEntryExtractionProgressArgs
                    {
                        Entry = e.Item,
                        Progress = e.ReaderProgress,
                        Game = game,
                    });
                };

                while (Reader.MoveToNextEntry())
                {
                    if (Reader.Cancelled)
                        break;

                    try
                    {
                        var localFile = Path.Combine(destination, Reader.Entry.Key);

                        uint crc = 0;

                        if (File.Exists(localFile))
                        {
                            using (FileStream fs = File.Open(localFile, FileMode.Open))
                            {
                                var buffer = new byte[65536];

                                while (true)
                                {
                                    var count = fs.Read(buffer, 0, buffer.Length);

                                    if (count == 0)
                                        break;

                                    crc = Crc32Algorithm.Append(crc, buffer, 0, count);
                                }
                            }
                        }

                        fileManifest.AppendLine($"{Reader.Entry.Key} | {Reader.Entry.Crc.ToString("X")}");

                        if (crc == 0 || crc != Reader.Entry.Crc)
                            Reader.WriteEntryToDirectory(destination, new ExtractionOptions()
                            {
                                ExtractFullPath = true,
                                Overwrite = true,
                                PreserveFileTime = true
                            });
                        else // Skip to next entry
                            try
                            {
                                Reader.OpenEntryStream().Dispose();
                            }
                            catch { }
                    }
                    catch (IOException ex)
                    {
                        var errorCode = ex.HResult & 0xFFFF;

                        if (errorCode == 87)
                            throw ex;
                        else
                            Logger?.LogTrace("Not replacing existing file/folder on disk: {Message}", ex.Message);

                        // Skip to next entry
                        Reader.OpenEntryStream().Dispose();
                    }
                }

                Reader.Dispose();
                DownloadStream.Dispose();
            }
            catch (ReaderCancelledException ex)
            {
                Logger?.LogTrace("User cancelled the download");

                extractionResult.Canceled = true;

                if (Directory.Exists(destination))
                {
                    Logger?.LogTrace("Cleaning up orphaned files after cancelled install");

                    Directory.Delete(destination, true);
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Could not extract to path {Destination}", destination);

                if (Directory.Exists(destination))
                {
                    Logger?.LogTrace("Cleaning up orphaned install files after bad install");

                    Directory.Delete(destination, true);
                }

                throw new Exception("The game archive could not be extracted, is it corrupted? Please try again");
            }

            if (!extractionResult.Canceled)
            {
                extractionResult.Success = true;
                extractionResult.Directory = destination;

                var fileListDestination = Path.Combine(destination, ".lancommander", game.Id.ToString(), "FileList.txt");

                if (!Directory.Exists(Path.GetDirectoryName(fileListDestination)))
                    Directory.CreateDirectory(Path.GetDirectoryName(fileListDestination));

                File.WriteAllText(fileListDestination, fileManifest.ToString());

                Logger?.LogTrace("Game {Game} successfully downloaded and extracted to {Destination}", game.Title, destination);
            }

            return extractionResult;
        }

        public string GetInstallDirectory(Game game, string installDirectory)
        {
            if (string.IsNullOrWhiteSpace(installDirectory))
                installDirectory = Client.DefaultInstallDirectory;

            if ((game.Type == GameType.Expansion || game.Type == GameType.Mod || game.Type == GameType.StandaloneMod) && game.BaseGame != null)
                return GetInstallDirectory(game.BaseGame, installDirectory);
            else
                return Path.Combine(installDirectory, game.Title.SanitizeFilename());
        }

        public void CancelInstall()
        {
            Reader?.Cancel();
        }

        public async Task<GameManifest> ReadManifestAsync(string installDirectory, Guid gameId)
        {
            return await ReadManifestAsync(installDirectory, gameId);
        }

        public async Task<IEnumerable<GameManifest>> ReadManifestsAsync(string installDirectory, Guid gameId)
        {
            var manifests = new List<GameManifest>();
            var mainManifest = await ManifestHelper.ReadAsync(installDirectory, gameId);

            if (mainManifest == null)
                return manifests;

            manifests.Add(mainManifest);

            if (mainManifest.DependentGames != null)
            {
                foreach (var dependentGameId in mainManifest.DependentGames)
                {
                    try
                    {
                        var dependentGameManifest = await ManifestHelper.ReadAsync(installDirectory, dependentGameId);

                        if (dependentGameManifest.Type == GameType.Expansion || dependentGameManifest.Type == GameType.Mod)
                            manifests.Add(dependentGameManifest);
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogError(ex, "Could not load manifest from dependent game {DependentGameId}", dependentGameId);
                    }
                }
            }

            return manifests;
        }

        public async Task RunAsync(string installDirectory, Guid gameId, Models.Action action, DateTime? lastRun, string args = "")
        {
            var alias = await Client.Profile.GetAliasAsync();
            var screen = DisplayHelper.GetScreen();

            using (var context = new GameExecutionContext(Client, Logger))
            {
                context.AddVariable("ServerAddress", Client.GetServerAddress());
                context.AddVariable("IPXRelayHost", await Client.GetIPXRelayHostAsync());
                context.AddVariable("IPXRelayPort", Client.Settings.IPXRelayPort.ToString());
                context.AddVariable("DisplayWidth", screen.Width.ToString());
                context.AddVariable("DisplayHeight", screen.Height.ToString());
                context.AddVariable("DisplayRefreshRate", screen.RefreshRate.ToString());
                context.AddVariable("DisplayBitDepth", screen.BitsPerPixel.ToString());

                #region Run Scripts
                var manifests = await ReadManifestsAsync(installDirectory, gameId);

                foreach (var manifest in manifests)
                {
                    //manifest.Actions
                    var currentGamePlayerAlias = await GetPlayerAliasAsync(installDirectory, manifest.Id);
                    var currentGameKey = await GetCurrentKeyAsync(installDirectory, manifest.Id);

                    #region Check Game's Player Name
                    if (currentGamePlayerAlias != alias)
                        await Client.Scripts.RunNameChangeScriptAsync(installDirectory, gameId, alias);
                    #endregion

                    #region Check Key Allocation
                    if (Client.IsConnected())
                    {
                        var newKey = await Client.Games.GetAllocatedKeyAsync(manifest.Id);

                        if (currentGameKey != newKey)
                            await Client.Scripts.RunKeyChangeScriptAsync(installDirectory, manifest.Id, newKey);
                    }
                    #endregion

                    #region Download Latest Saves
                    if (Client.IsConnected())
                    {
                        await RetryHelper.RetryOnExceptionAsync(10, TimeSpan.FromSeconds(1), false, async () =>
                        {
                            Logger?.LogTrace("Attempting to download save");

                            var latestSave = await Client.Saves.GetLatestAsync(manifest.Id);

                            if (latestSave != null && (latestSave.CreatedOn > lastRun || lastRun == null))
                                await Client.Saves.DownloadAsync(installDirectory, manifest.Id);

                            return true;
                        });
                    }
                    #endregion

                    #region Run Before Start Script
                    await Client.Scripts.RunBeforeStartScriptAsync(installDirectory, manifest.Id);
                    #endregion
                }
                #endregion

                try
                {
                    var task = context.ExecuteAsync(installDirectory, gameId, action);

                    RunningProcesses[gameId] = context.Process;

                    await task;
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "Game failed to run");
                }

                foreach (var manifest in manifests)
                {
                    #region Upload Saves
                    await RetryHelper.RetryOnExceptionAsync(10, TimeSpan.FromSeconds(1), false, async () =>
                    {
                        Logger?.LogTrace("Attempting to upload save");

                        await Client.Saves.UploadAsync(installDirectory, manifest.Id);

                        return true;
                    });
                    #endregion

                    await Client.Scripts.RunAfterStopScriptAsync(installDirectory, gameId);
                }
            }
        }

        public async Task Stop(Guid gameId)
        {
            await Task.Run(() =>
            {
                if (RunningProcesses.ContainsKey(gameId))
                {
                    var process = RunningProcesses[gameId];

                    process.CloseMainWindow();

                    RunningProcesses.Remove(gameId);
                }
            });
        }

        public bool IsRunning(Guid gameId)
        {
            if (!RunningProcesses.ContainsKey(gameId))
                return false;

            var process = RunningProcesses[gameId];

            try
            {
                if (process.HasExited)
                {
                    RunningProcesses.Remove(gameId);
                    return false;
                }
                else
                    return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task ImportAsync(string archivePath)
        {
            using (var fs = new FileStream(archivePath, FileMode.Open, FileAccess.Read))
            {
                var objectKey = await Client.ChunkedUploadRequestAsync("", fs);

                if (objectKey != Guid.Empty)
                    await Client.PostRequestAsync<object>($"/api/Games/Import/{objectKey}");
            }
        }

        public async Task ExportAsync(string destinationPath, Guid gameId)
        {
            await Client.DownloadRequestAsync($"/Games/{gameId}/Export/Full", destinationPath);
        }

        public async Task UploadArchiveAsync(string archivePath, Guid gameId, string version, string changelog = "")
        {
            using (var fs = new FileStream(archivePath, FileMode.Open, FileAccess.Read))
            {
                var objectKey = await Client.ChunkedUploadRequestAsync("", fs);

                if (objectKey != Guid.Empty)
                    await Client.PostRequestAsync<object>($"/api/Games/UploadArchive", new UploadArchiveRequest
                    {
                        Id = gameId,
                        ObjectKey = objectKey,
                        Version = version,
                        Changelog = changelog,
                    });
            }
        }

        public static string GetMetadataDirectoryPath(string installDirectory, Guid gameId)
        {
            if (string.IsNullOrWhiteSpace(installDirectory))
                return "";

            return Path.Combine(installDirectory, ".lancommander", gameId.ToString());
        }

        public static string GetMetadataFilePath(string installDirectory, Guid gameId, string fileName)
        {
            return Path.Combine(GetMetadataDirectoryPath(installDirectory, gameId), fileName);
        }

        public static string GetPlayerAlias(string installDirectory, Guid gameId)
        {
            var aliasFilePath = GetMetadataFilePath(installDirectory, gameId, PlayerAliasFilename);

            if (File.Exists(aliasFilePath))
                return File.ReadAllText(aliasFilePath);
            else
                return string.Empty;
        }

        public static async Task<string> GetPlayerAliasAsync(string installDirectory, Guid gameId)
        {
            var aliasFilePath = GetMetadataFilePath(installDirectory, gameId, PlayerAliasFilename);

            if (File.Exists(aliasFilePath))
                return await File.ReadAllTextAsync(aliasFilePath);
            else
                return string.Empty;
        }

        public static void UpdatePlayerAlias(string installDirectory, Guid gameId, string newName)
        {
            File.WriteAllText(GetMetadataFilePath(installDirectory, gameId, PlayerAliasFilename), newName);
        }

        public static async Task UpdatePlayerAliasAsync(string installDirectory, Guid gameId, string newName)
        {
            await File.WriteAllTextAsync(GetMetadataFilePath(installDirectory, gameId, PlayerAliasFilename), newName);
        }

        public static string GetCurrentKey(string installDirectory, Guid gameId)
        {
            var keyFilePath = GetMetadataFilePath(installDirectory, gameId, KeyFilename);

            if (File.Exists(keyFilePath))
                return File.ReadAllText(keyFilePath);
            else
                return string.Empty;
        }

        public static async Task<string> GetCurrentKeyAsync(string installDirectory, Guid gameId)
        {
            var keyFilePath = GetMetadataFilePath(installDirectory, gameId, KeyFilename);

            if (File.Exists(keyFilePath))
                return await File.ReadAllTextAsync(keyFilePath);
            else
                return string.Empty;
        }

        public static void UpdateCurrentKey(string installDirectory, Guid gameId, string newKey)
        {
            File.WriteAllText(GetMetadataFilePath(installDirectory, gameId, KeyFilename), newKey);
        }

        public static async Task UpdateCurrentKeyAsync(string installDirectory, Guid gameId, string newKey)
        {
            await File.WriteAllTextAsync(GetMetadataFilePath(installDirectory, gameId, KeyFilename), newKey);
        }
    }
}
