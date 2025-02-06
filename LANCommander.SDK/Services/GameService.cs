using Force.Crc32;
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LANCommander.SDK.Services
{
    public class InstallProgress
    {
        public Game Game { get; set; }
        public string Title { get; set; }
        public Guid IconId { get; set; }
        public InstallStatus Status { get; set; }
        public float Progress
        {
            get
            {
                return BytesDownloaded / (float)TotalBytes;
            }
            set { }
        }
        public long TransferSpeed { get; set; }
        public long BytesDownloaded { get; set; }
        public long TotalBytes { get; set; }
        public TimeSpan TimeRemaining { get; set; }
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

        public delegate void OnInstallProgressUpdateHandler(InstallProgress e);
        public event OnInstallProgressUpdateHandler OnInstallProgressUpdate;

        public const string PlayerAliasFilename = "PlayerAlias";
        public const string KeyFilename = "Key";

        private TrackableStream TransferStream;
        private IReader Reader;

        private InstallProgress _installProgress = new();

        private Dictionary<Guid, CancellationTokenSource> Running = new();

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
            var mainManifest = await ManifestHelper.ReadAsync<GameManifest>(installDirectory, id);

            if (mainManifest == null)
                return manifests;

            manifests.Add(mainManifest);

            if (mainManifest.DependentGames != null)
            {
                foreach (var dependentGameId in mainManifest.DependentGames)
                {
                    try
                    {
                        var dependentGameManifest = await ManifestHelper.ReadAsync<GameManifest>(installDirectory, dependentGameId);

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

            try
            {
                if (Client.IsConnected())
                    actions.AddRange(await Client.GetRequestAsync<IEnumerable<Models.Action>>($"/api/Games/{id}/Actions"));
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Could not get actions from server");
            }
            
            var manifests = await GetManifestsAsync(installDirectory, id);

            if (!actions.Any())
            {
                actions = manifests
                    .Where(m => m != null && m.Actions != null)
                    .SelectMany(m => m.Actions)
                    .OrderByDescending(a => a.IsPrimaryAction)
                    .ThenBy(a => a.SortOrder)
                    .ToList();
            }

            if (manifests.Any(m => m.OnlineMultiplayer != null && m.OnlineMultiplayer.NetworkProtocol == NetworkProtocol.Lobby || m.LanMultiplayer != null && m.LanMultiplayer.NetworkProtocol == NetworkProtocol.Lobby))
            {
                var primaryAction = actions.Where(a => a.IsPrimaryAction).First();

                try
                {
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
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "Could not get lobbies");
                }
            }

            return actions;
        }

        public async Task<IEnumerable<Game>> GetAddonsAsync(Guid id)
        {
            return await Client.GetRequestAsync<IEnumerable<Game>>($"/api/Games/{id}/Addons");
        }

        public async Task<bool> CheckForUpdateAsync(Guid id, string currentVersion)
        {
            return await Client.GetRequestAsync<bool>($"/api/Games/{id}/CheckForUpdate?version={currentVersion}");
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
        public async Task<string> InstallAsync(Guid gameId, string installDirectory = "", Guid[] addonIds = null, int maxAttempts = 10)
        {
            GameManifest manifest = null;

            if (string.IsNullOrWhiteSpace(installDirectory))
                installDirectory = Client.DefaultInstallDirectory;

            var game = Get(gameId);
            var destination = await GetInstallDirectory(game, installDirectory);

            _installProgress.Game = game;
            _installProgress.Title = game.Title;
            _installProgress.Status = InstallStatus.Downloading;
            _installProgress.Progress = 0;
            _installProgress.TransferSpeed = 0;
            _installProgress.TotalBytes = 0;
            _installProgress.BytesDownloaded = 0;

            OnInstallProgressUpdate?.Invoke(_installProgress);

            // Handle Standalone Mods
            if (game.Type == GameType.StandaloneMod && game.BaseGameId != Guid.Empty)
            {
                var baseGame = await Client.Games.GetAsync(game.BaseGameId);

                destination = await GetInstallDirectory(baseGame, installDirectory);

                if (!Directory.Exists(destination))
                    destination = await InstallAsync(game.BaseGameId, installDirectory, null, maxAttempts);
            }

            try
            {
                if (ManifestHelper.Exists(destination, game.Id))
                    manifest = await ManifestHelper.ReadAsync<GameManifest>(destination, game.Id);
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
            var writeManifestSuccess = await RetryHelper.RetryOnExceptionAsync(maxAttempts, TimeSpan.FromSeconds(1), false, async () =>
            {
                Logger?.LogTrace("Attempting to get game manifest");

                manifest = GetManifest(game.Id);

                await ManifestHelper.WriteAsync(manifest, game.InstallDirectory);

                return true;
            });

            if (!writeManifestSuccess)
                throw new InstallException("Could not grab the manifest file. Retry the install or check your connection");

            Logger?.LogTrace("Saving scripts");

            foreach (var script in game.Scripts)
            {
                await ScriptHelper.SaveScriptAsync(game, script.Type);
            }

            _installProgress.Progress = 1;
            _installProgress.BytesDownloaded = _installProgress.TotalBytes;
            _installProgress.Status = InstallStatus.InstallingRedistributables;

            OnInstallProgressUpdate?.Invoke(_installProgress);

            #region Install Redistributables
            if (game.Redistributables != null && game.Redistributables.Any())
            {
                Logger?.LogTrace("Installing redistributables");

                await Client.Redistributables.InstallAsync(game);
            }
            #endregion

            #region Download Latest Save
            Logger?.LogTrace("Attempting to download the latest save");

            _installProgress.Status = InstallStatus.DownloadingSaves;

            OnInstallProgressUpdate?.Invoke(_installProgress);

            await Client.Saves.DownloadAsync(game.InstallDirectory, game.Id);
            #endregion

            await RunPostInstallScripts(game);

            if (addonIds != null)
                await InstallAddonsAsync(installDirectory, game, addonIds);

            _installProgress.Status = InstallStatus.Complete;
            _installProgress.Progress = 1;
            _installProgress.BytesDownloaded = _installProgress.TotalBytes;

            OnInstallProgressUpdate?.Invoke(_installProgress);

            return game.InstallDirectory;
        }

        public async Task InstallAddonsAsync(string installDirectory, Guid baseGameId, IEnumerable<Guid> addonIds)
        {
            var game = await Client.Games.GetAsync(baseGameId);

            await InstallAddonsAsync(installDirectory, game, addonIds);
        }

        public async Task InstallAddonsAsync(string installDirectory, Game game, IEnumerable<Guid> addonIds)
        {
            if (addonIds != null)
            {
                var addons = new List<Game>();
                
                foreach (var addonId in addonIds)
                {
                    try
                    {
                        addons.Add(await Client.Games.GetAsync(addonId));
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogError(ex, "Could not get information for addon with ID {AddonId}, skipping install", addonId);
                    }
                }

                var expansions = addons.Where(a => a?.Type == GameType.Expansion).ToList();
                

                foreach (var expansion in expansions)
                {
                    try
                    {
                        _installProgress.Status = InstallStatus.Downloading;
                        _installProgress.Game = expansion;
                        _installProgress.Progress = 0;
                        _installProgress.BytesDownloaded = 0;
                        _installProgress.TotalBytes = 1;
                        _installProgress.BytesDownloaded = 0;

                        OnInstallProgressUpdate?.Invoke(_installProgress);
                        
                        await InstallAddonAsync(installDirectory, expansion);
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogError(ex, "Could not install expansion with ID {AddonId}", expansion.Id);
                    }
                }
                
                var mods = addons.Where(a => a?.Type == GameType.Mod).ToList();

                foreach (var mod in mods)
                {
                    try
                    {
                        _installProgress.Status = InstallStatus.Downloading;
                        _installProgress.Game = mod;
                        _installProgress.Progress = 0;
                        _installProgress.BytesDownloaded = 0;
                        _installProgress.TotalBytes = 1;
                        _installProgress.BytesDownloaded = 0;

                        OnInstallProgressUpdate?.Invoke(_installProgress);
                        
                        await InstallAddonAsync(installDirectory, mod);
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogError(ex, "Could not install mod with ID {AddonId}", mod.Id);
                    }
                }
            }
        }

        public async Task InstallAddonAsync(string installDirectory, Game addon)
        {
            if (!addon.IsAddon)
                return;

            OnInstallProgressUpdate?.Invoke(_installProgress);

            try
            {
                await InstallAsync(addon.Id, installDirectory);
            }
            catch (InstallCanceledException ex)
            {
                Logger?.LogDebug("Install canceled");

                _installProgress.Status = InstallStatus.Canceled;
                OnInstallProgressUpdate?.Invoke(_installProgress);

                throw;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Failed to install addon {AddonTitle} ({AddonId})", addon.Title, addon.Id);

                _installProgress.Status = InstallStatus.Failed;
                OnInstallProgressUpdate?.Invoke(_installProgress);

                throw;
            }

            await RunPostInstallScripts(addon);
        }

        public async Task UninstallAsync(string installDirectory, Guid gameId)
        {
            var manifest = await ManifestHelper.ReadAsync<GameManifest>(installDirectory, gameId);

            #region Uninstall Dependent Games
            if (manifest.DependentGames != null)
            {
                foreach (var dependentGame in manifest.DependentGames)
                {
                    try
                    {
                        await UninstallAsync(installDirectory, dependentGame);
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogWarning("Could not uninstall dependent game with ID {GameId}. Assuming it's already uninstalled or never installed...", gameId);
                    }
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

        public async Task<string> MoveAsync(Guid gameId, string oldInstallDirectory, string newInstallDirectory)
        {
            var game = await GetAsync(gameId);

            return await MoveAsync(game, oldInstallDirectory, newInstallDirectory);
        }

        public async Task<string> MoveAsync(Game game, string oldInstallDirectory, string newInstallDirectory)
        {
            var gameAndAddons = new List<Game>();

            _installProgress.Game = game;
            _installProgress.Status = InstallStatus.Moving;
            OnInstallProgressUpdate?.Invoke(_installProgress);

            gameAndAddons.Add(game);

            foreach (var dependentGameId in game.DependentGames)
            {
                var dependentGame = await Client.Games.GetAsync(dependentGameId);

                if (dependentGame.IsAddon)
                    gameAndAddons.Add(dependentGame);
            }

            foreach (var entry in gameAndAddons)
            {
                if (await IsInstalled(oldInstallDirectory, game, entry.Id))
                    await Client.Saves.UploadAsync(oldInstallDirectory, entry.Id);
            }

            if (Directory.Exists(newInstallDirectory))
                throw new ArgumentException("Destination directory already exists");

            var directories = Directory.GetDirectories(oldInstallDirectory, "*", SearchOption.AllDirectories);
            var files = Directory.GetFiles(oldInstallDirectory, "*.*", SearchOption.AllDirectories);
            var fileInfos = files.Select(f => new FileInfo(f));
            var totalSize = fileInfos.Sum(fi => fi.Length);

            foreach (var directory in directories)
            {
                Directory.CreateDirectory(directory.Replace(oldInstallDirectory, newInstallDirectory));
            }

            foreach (var fileInfo in fileInfos)
            {
                using (FileStream sourceStream = File.Open(fileInfo.FullName, FileMode.Open))
                using (FileStream destinationStream = File.Create(fileInfo.FullName.Replace(oldInstallDirectory, newInstallDirectory)))
                {
                    _installProgress.TotalBytes = totalSize;

                    using (var fileTransferMonitor = new FileTransferMonitor(totalSize))
                    {
                        TransferStream = new TrackableStream(destinationStream, true, totalSize);
                        TransferStream.OnProgress += (pos, _) =>
                        {
                            if (fileTransferMonitor.CanUpdate())
                            {
                                fileTransferMonitor.Update(pos);

                                _installProgress.TimeRemaining = fileTransferMonitor.GetTimeRemaining();
                                _installProgress.BytesDownloaded = fileTransferMonitor.GetBytesTransferred();
                                _installProgress.TransferSpeed = fileTransferMonitor.GetSpeed();
                                
                                OnInstallProgressUpdate?.Invoke(_installProgress);
                            }
                        };

                        await sourceStream.CopyToAsync(TransferStream);
                    }
                }
            }

            _installProgress.BytesDownloaded = totalSize;
            _installProgress.Progress = 1;
            _installProgress.Status = InstallStatus.RunningScripts;
            OnInstallProgressUpdate?.Invoke(_installProgress);

            Directory.Delete(oldInstallDirectory, true);

            foreach (var entry in gameAndAddons)
            {
                if (await IsInstalled(newInstallDirectory, game, entry.Id))
                {
                    await RunPostInstallScripts(entry);
                    await Client.Saves.DownloadAsync(newInstallDirectory, entry.Id);
                }
            }

            _installProgress.Status = InstallStatus.Complete;
            OnInstallProgressUpdate?.Invoke(_installProgress);

            return newInstallDirectory;
        }

        public async Task<bool> IsInstalled(string installDirectory, Game game, Guid? addonId = null)
        {
            installDirectory = await GetInstallDirectory(game, installDirectory);

            var metadataPath = ManifestHelper.GetPath(installDirectory, addonId ?? game.Id);

            return File.Exists(metadataPath);
        }
         
        private async Task RunPostInstallScripts(Game game)
        {
            if (game.Scripts != null && game.Scripts.Any())
            {
                _installProgress.Status = InstallStatus.RunningScripts;

                OnInstallProgressUpdate?.Invoke(_installProgress);

                try
                {
                    var allocatedKey = await GetAllocatedKeyAsync(game.Id);

                    await Client.Scripts.RunInstallScriptAsync(game.InstallDirectory, game.Id);
                    await Client.Scripts.RunKeyChangeScriptAsync(game.InstallDirectory, game.Id, allocatedKey);
                    await Client.Scripts.RunNameChangeScriptAsync(game.InstallDirectory, game.Id, await Client.Profile.GetAliasAsync());
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "Scripts failed to execute for game/addon {GameTitle} ({GameId})", game.Title, game.Id);
                }
            }
        }

        private ExtractionResult DownloadAndExtract(Game game, string destination)
        {
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

                TransferStream = Stream(game.Id);
                Reader = ReaderFactory.Open(TransferStream);

                using (var monitor = new FileTransferMonitor(TransferStream.Length))
                {
                    TransferStream.OnProgress += (pos, len) =>
                    {
                        if (monitor.CanUpdate())
                        {
                            monitor.Update(pos);

                            _installProgress.BytesDownloaded = monitor.GetBytesTransferred();
                            _installProgress.TotalBytes = len;
                            _installProgress.TransferSpeed = monitor.GetSpeed();
                            _installProgress.TimeRemaining = monitor.GetTimeRemaining();
                            
                            OnInstallProgressUpdate?.Invoke(_installProgress);
                        }
                    };
                }

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
                TransferStream.Dispose();
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

        public async Task<string> GetInstallDirectory(Game game, string installDirectory)
        {
            if (string.IsNullOrWhiteSpace(installDirectory))
                installDirectory = Client.DefaultInstallDirectory;

            if ((game.Type == GameType.Expansion || game.Type == GameType.Mod || game.Type == GameType.StandaloneMod) && game.BaseGameId != Guid.Empty)
            {
                var baseGame = await Client.Games.GetAsync(game.BaseGameId);

                return await GetInstallDirectory(baseGame, installDirectory);
            }
            else
                return Path.Combine(installDirectory, game.Title.SanitizeFilename());
        }

        public void CancelInstall()
        {
            Reader?.Cancel();
        }

        public async Task<IEnumerable<GameManifest>> ReadManifestsAsync(string installDirectory, Guid gameId)
        {
            var manifests = new List<GameManifest>();
            var mainManifest = await ManifestHelper.ReadAsync<GameManifest>(installDirectory, gameId);

            if (mainManifest == null)
                return manifests;

            manifests.Add(mainManifest);

            if (mainManifest.DependentGames != null)
            {
                foreach (var dependentGameId in mainManifest.DependentGames)
                {
                    try
                    {
                        var dependentGameManifest = await ManifestHelper.ReadAsync<GameManifest>(installDirectory, dependentGameId);

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
            var screen = DisplayHelper.GetScreen();

            using (var context = new ProcessExecutionContext(Client, Logger))
            {
                context.AddVariable("ServerAddress", Client.GetServerAddress());
                context.AddVariable("DisplayWidth", screen.Width.ToString());
                context.AddVariable("DisplayHeight", screen.Height.ToString());
                context.AddVariable("DisplayRefreshRate", screen.RefreshRate.ToString());
                context.AddVariable("DisplayBitDepth", screen.BitsPerPixel.ToString());
                
                if (Client.IsConnected())
                {
                    context.AddVariable("IPXRelayHost", await Client.GetIPXRelayHostAsync());
                    context.AddVariable("IPXRelayPort", Client.Settings.IPXRelayPort.ToString());                    
                }

                #region Run Scripts
                var manifests = await ReadManifestsAsync(installDirectory, gameId);

                foreach (var manifest in manifests)
                {
                    //manifest.Actions
                    var currentGamePlayerAlias = await GetPlayerAliasAsync(installDirectory, manifest.Id);
                    var currentGameKey = await GetCurrentKeyAsync(installDirectory, manifest.Id);

                    #region Check Game's Player Name
                    if (Client.IsConnected())
                    {
                        var alias = await Client.Profile.GetAliasAsync();

                        if (currentGamePlayerAlias != alias)
                        {
                            await Client.Scripts.RunNameChangeScriptAsync(installDirectory, gameId, alias);

                            if (manifest.Redistributables != null)
                            {
                                foreach (var redistributable in manifest.Redistributables.Where(r => r.Scripts != null))
                                {
                                    await Client.Scripts.RunNameChangeScriptAsync(installDirectory, gameId, redistributable.Id, alias);
                                }
                            }
                        }
                    }
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
                    
                    if (manifest.Redistributables != null)
                    {
                        foreach (var redistributable in manifest.Redistributables.Where(r => r.Scripts != null))
                        {
                            await Client.Scripts.RunBeforeStartScriptAsync(installDirectory, gameId, redistributable.Id);
                        }
                    }
                    #endregion
                }
                #endregion

                try
                {
                    var cancellationTokenSource = new CancellationTokenSource();
                    var task = context.ExecuteGameActionAsync(installDirectory, gameId, action, "", cancellationTokenSource.Token);

                    Running[gameId] = cancellationTokenSource;

                    await task;

                    Running.Remove(gameId);
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

                    #region Run After Stop Script
                    await Client.Scripts.RunAfterStopScriptAsync(installDirectory, gameId);
                    
                    if (manifest.Redistributables != null)
                    {
                        foreach (var redistributable in manifest.Redistributables.Where(r => r.Scripts != null))
                        {
                            await Client.Scripts.RunBeforeStartScriptAsync(installDirectory, gameId, redistributable.Id);
                        }
                    }
                    #endregion
                }
            }
        }

        public async Task Stop(Guid gameId)
        {
            if (Running.ContainsKey(gameId))
            {
                await Running[gameId].CancelAsync();

                Running.Remove(gameId);
            }
        }

        public bool IsRunning(Guid gameId)
        {
            if (!Running.ContainsKey(gameId))
                return false;

            return !Running[gameId].IsCancellationRequested;
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

        /// <summary>
        /// Get the archive associated with the installed version of the game and return any non-matching files in the current install.
        /// </summary>
        /// <param name="installDirectory">The game's install directory</param>
        /// <param name="gameId">The game's ID</param>
        /// <returns>List of file conflicts</returns>
        public async Task<IEnumerable<ArchiveValidationConflict>> ValidateFilesAsync(string installDirectory, Guid gameId)
        {
            var manifest = await ManifestHelper.ReadAsync<GameManifest>(installDirectory, gameId);
            var entries = await Client.GetRequestAsync<IEnumerable<ArchiveEntry>>($"/api/Archives/Contents/{gameId}/{manifest.Version}");

            var conflictedEntries = new List<ArchiveValidationConflict>();

            var savePathEntries = manifest.SavePaths?.SelectMany(p => Client.Saves.GetFileSavePathEntries(p, installDirectory)) ?? new List<SavePathEntry>();

            foreach (var entry in entries)
            {
                if (savePathEntries.Any(e => e.ArchivePath.Equals(entry.FullName, StringComparison.OrdinalIgnoreCase)))
                    continue;

                if (entry.FullName.EndsWith('/'))
                    continue;

                var localFile = Path.Combine(installDirectory, entry.FullName.Replace('/', Path.DirectorySeparatorChar));

                if (!Path.Exists(localFile))
                    conflictedEntries.Add(new ArchiveValidationConflict
                    {
                        Name = entry.Name,
                        FullName = entry.FullName,
                        Crc32 = entry.Crc32,
                        Length = entry.Length,
                    });
                else
                {
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

                    if (crc == 0 || crc != entry.Crc32)
                        conflictedEntries.Add(new ArchiveValidationConflict
                        {
                            Name = entry.Name,
                            FullName = entry.FullName,
                            Crc32 = entry.Crc32,
                            LocalFileInfo = new FileInfo(localFile)
                        });
                }
            }

            return conflictedEntries;
        }

        public async Task DownloadFilesAsync(string installDirectory, Guid gameId, IEnumerable<string> entries)
        {
            var manifest = await ManifestHelper.ReadAsync<GameManifest>(installDirectory, gameId);
            var archive = await Client.GetRequestAsync<Archive>($"/api/Archives/ByVersion/{manifest.Version}");

            await Task.Run(() =>
            {
                try
                {
                    TransferStream = Stream(gameId);
                    Reader = ReaderFactory.Open(TransferStream);

                    while (Reader.MoveToNextEntry())
                    {
                        if (Reader.Cancelled)
                            break;

                        try
                        {
                            if (entries.Contains(Reader.Entry.Key))
                            {
                                var destination = Path.Combine(installDirectory, Reader.Entry.Key.Replace('/', Path.DirectorySeparatorChar));

                                Reader.WriteEntryToFile(destination, new ExtractionOptions
                                {
                                    Overwrite = true,
                                    PreserveFileTime = true,
                                });
                            }
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
                    TransferStream.Dispose();
                }
                catch (Exception ex)
                {
                    throw new Exception("The game archive could not be extracted, is it corrupted? Please try again");
                }
            });
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
