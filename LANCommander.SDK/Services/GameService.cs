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
        public bool Indeterminate { get; set; }
        public float Progress
        {
            get
            {
                return BytesTransferred / (float)TotalBytes;
            }
            set { }
        }
        public long TransferSpeed { get; set; }
        public long BytesTransferred { get; set; }
        public long TotalBytes { get; set; }
        public TimeSpan TimeRemaining { get; set; }
    }

    public class InstallResult
    {
        public InstallResult()
        {
        }
        public InstallResult(string installDirectory, Guid gameId)
        {
            FileList = new GameInstallationFileList(installDirectory, gameId);
        }

        public string InstallDirectory 
        {
            get => FileList.InstallDirectory;
            internal set => FileList.InstallDirectory = value;
        }

        public GameInstallationFileList FileList { get; set; } = GameInstallationFileList.Empty;
    }

    public class GameService
    {
        private readonly ILogger _logger;
        private Client Client { get; set; }
        private string DefaultInstallDirectory { get; set; }

        public delegate void OnArchiveEntryExtractionProgressHandler(object sender, ArchiveEntryExtractionProgressArgs e);
        public event OnArchiveEntryExtractionProgressHandler OnArchiveEntryExtractionProgress;

        public delegate void OnArchiveExtractionProgressHandler(long position, long length, Game game);
        public event OnArchiveExtractionProgressHandler OnArchiveExtractionProgress;

        public delegate void OnInstallProgressUpdateHandler(InstallProgress e);
        public event OnInstallProgressUpdateHandler OnInstallProgressUpdate;

        private const string PlayerAliasFilename = "PlayerAlias";
        private const string KeyFilename = "Key";

        private TrackableStream _transferStream;
        private IReader _reader;

        private readonly InstallProgress _installProgress = new();

        private readonly Dictionary<Guid, CancellationTokenSource> _running = new();

        public GameService(Client client, string defaultInstallDirectory)
        {
            Client = client;
            DefaultInstallDirectory = defaultInstallDirectory;
        }

        public GameService(Client client, string defaultInstallDirectory, ILogger logger)
        {
            Client = client;
            DefaultInstallDirectory = defaultInstallDirectory;
            _logger = logger;
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

        public async Task<ICollection<GameManifest>> GetManifestsAsync(string installDirectory, Guid id)
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
                        if (ManifestHelper.Exists(installDirectory, dependentGameId))
                        {
                            var dependentGameManifest = await ManifestHelper.ReadAsync<GameManifest>(installDirectory, dependentGameId);

                            if (dependentGameManifest?.Type == GameType.Expansion || dependentGameManifest?.Type == GameType.Mod)
                                manifests.Add(dependentGameManifest);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"Could not load manifest from dependent game {dependentGameId}");
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
                _logger?.LogError(ex, "Could not get actions from server");
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
                    _logger?.LogError(ex, "Could not get lobbies");
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

        public async Task StartedAsync(Guid id)
        {
            _logger?.LogTrace("Signaling to the server that we started the game...");

            try
            {
                await Client.GetRequestAsync<object>($"/api/Games/{id}/Started");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed sending start request to server");
            }
        }

        public async Task StoppedAsync(Guid id)
        {
            _logger?.LogTrace("Signaling to the server that we stopped the game...");

            try
            { 
                await Client.GetRequestAsync<object>($"/api/Games/{id}/Stopped");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed sending stop request to server");
            }
        }

        public string GetAllocatedKey(Guid id)
        {
            _logger?.LogTrace("Requesting allocated key...");

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
            _logger?.LogTrace("Requesting allocated key...");

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
            _logger?.LogTrace("Requesting new key allocation...");

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
        /// <param name="gameId">Unique identifier of the game to install.</param>
        /// <param name="installDirectory">Optional custom installation directory.</param>
        /// <param name="addonIds">Optional list of add-on identifiers to install alongside the game.</param>
        /// <param name="maxAttempts">Maximum attempts in case of transmission error</param>
        /// <returns>
        /// An <see cref="InstallResult"/> containing details about the installation outcome such as  the final install path.
        /// </returns>
        /// <exception cref="Exception">
        /// Thrown if installation fails after the maximum retry attempts.
        /// </exception>
        public async Task<InstallResult> InstallAsync(Guid gameId, string installDirectory = "", Guid[] addonIds = null, int maxAttempts = 10)
        {
            var installResult = new InstallResult(installDirectory, gameId);
            var gameFileList = installResult.FileList;
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
            _installProgress.BytesTransferred = 0;

            OnInstallProgressUpdate?.Invoke(_installProgress);

            // Handle Standalone Mods
            if (game.Type == GameType.StandaloneMod && game.BaseGameId != Guid.Empty)
            {
                var baseGame = await Client.Games.GetAsync(game.BaseGameId);

                destination = await GetInstallDirectory(baseGame, installDirectory);

                if (!Directory.Exists(destination))
                {
                    var baseGameFileList = await InstallAsync(game.BaseGameId, installDirectory, null, maxAttempts);
                    destination = installResult.InstallDirectory;
                }
            }

            try
            {
                if (ManifestHelper.Exists(destination, game.Id))
                    manifest = await ManifestHelper.ReadAsync<GameManifest>(destination, game.Id);
            }
            catch (Exception ex)
            {
                _logger?.LogTrace(ex, "Error reading manifest before install");
            }

            _logger?.LogTrace("Installing game {GameTitle} ({GameId})", game.Title, game.Id);

            // Download and extract
            var result = await RetryHelper.RetryOnExceptionAsync(maxAttempts, TimeSpan.FromMilliseconds(500), new ExtractionResult(), async () =>
            {
                _logger?.LogTrace("Attempting to download and extract game");

                return await Task.Run(() => DownloadAndExtract(game, destination));
            });

            if (!result.Success && !result.Canceled)
                throw new InstallException("Could not extract the installer. Retry the install or check your connection");
            else if (result.Canceled)
                throw new InstallCanceledException("Game install was canceled");

            game.InstallDirectory = result.Directory;
            installResult.InstallDirectory = result.Directory;

            // Game is extracted, get metadata
            var writeManifestSuccess = await RetryHelper.RetryOnExceptionAsync(maxAttempts, TimeSpan.FromSeconds(1), false, async () =>
            {
                _logger?.LogTrace("Attempting to get game manifest");
                manifest = await WriteManifestAsync(game.InstallDirectory, game);

                return true;
            });

            if (!writeManifestSuccess)
                throw new InstallException("Could not grab the manifest file. Retry the install or check your connection");

            // store scripts locally
            await WriteScriptsAsync(game.InstallDirectory, game);


            // store manifest and files for current game (could be base game, or any dependent game as this point due to recursive call)
            gameFileList.BaseGame.Manifest = manifest;
            var gameFiles = result?.Files?.Where(x => !x.EntryPath.EndsWith("/")).Select(x => new GameInstallationFileListEntry.FileEntry
            {
                EntryPath = x.EntryPath,
                LocalPath = x.LocalPath,
            });
            gameFileList.BaseGame.AddFiles(gameFiles ?? []);


            _installProgress.Progress = 1;
            _installProgress.BytesTransferred = _installProgress.TotalBytes;
            _installProgress.Status = InstallStatus.InstallingRedistributables;

            OnInstallProgressUpdate?.Invoke(_installProgress);

            #region Install Redistributables
            if (game.Redistributables != null && game.Redistributables.Any())
            {
                _logger?.LogTrace("Installing redistributables");

                await Client.Redistributables.InstallAsync(game);
            }
            #endregion

            #region Download Latest Save
            _logger?.LogTrace("Attempting to download the latest save");

            _installProgress.Status = InstallStatus.DownloadingSaves;

            OnInstallProgressUpdate?.Invoke(_installProgress);

            await Client.Saves.DownloadAsync(game.InstallDirectory, game.Id);
            #endregion

            await RunPostInstallScripts(game);

            if (addonIds != null)
            {
                var addonsResult = await InstallAddonsAsync(installDirectory, game, addonIds);
                gameFileList.MergeDependentGames(addonsResult.FileList);
            }

            _installProgress.Status = InstallStatus.Complete;
            _installProgress.Progress = 1;
            _installProgress.BytesTransferred = _installProgress.TotalBytes;

            OnInstallProgressUpdate?.Invoke(_installProgress);

            return installResult;
        }

        public async Task<InstallResult> InstallAddonsAsync(string installDirectory, Guid baseGameId, IEnumerable<Guid> addonIds)
        {
            var game = await Client.Games.GetAsync(baseGameId);

            return await InstallAddonsAsync(installDirectory, game, addonIds);
        }

        public async Task<InstallResult> InstallAddonsAsync(string installDirectory, Game game, IEnumerable<Guid> addonIds)
        {
            var installResult = new InstallResult(installDirectory, game.Id);
            var gameFileList = installResult.FileList;

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
                        _logger?.LogError(ex, "Could not get information for addon with ID {AddonId}, skipping install", addonId);
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
                        _installProgress.BytesTransferred = 0;
                        _installProgress.TotalBytes = 1;
                        _installProgress.BytesTransferred = 0;

                        OnInstallProgressUpdate?.Invoke(_installProgress);

                        var expansionResult = await InstallAddonAsync(installDirectory, expansion);
                        gameFileList.MergeBaseAsDependentGame(expansion.Id, expansionResult.FileList);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Could not install expansion with ID {AddonId}", expansion.Id);
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
                        _installProgress.BytesTransferred = 0;
                        _installProgress.TotalBytes = 1;
                        _installProgress.BytesTransferred = 0;

                        OnInstallProgressUpdate?.Invoke(_installProgress);

                        var modResult = await InstallAddonAsync(installDirectory, mod);
                        gameFileList.MergeBaseAsDependentGame(mod.Id, modResult.FileList);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Could not install mod with ID {AddonId}", mod.Id);
                    }
                }
            }

            return installResult;
        }

        public async Task<InstallResult> InstallAddonAsync(string installDirectory, Game addon)
        {
            var installResult = new InstallResult(installDirectory, addon.Id);
            var gameFileList = installResult.FileList;

            if (!addon.IsAddon)
                return installResult;

            OnInstallProgressUpdate?.Invoke(_installProgress);

            try
            {
                var addonResult = await InstallAsync(addon.Id, installDirectory);
                gameFileList.Merge(addonResult.FileList);
            }
            catch (InstallCanceledException ex)
            {
                _logger?.LogDebug("Install canceled");

                _installProgress.Status = InstallStatus.Canceled;
                OnInstallProgressUpdate?.Invoke(_installProgress);

                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to install addon {AddonTitle} ({AddonId})", addon.Title, addon.Id);

                _installProgress.Status = InstallStatus.Failed;
                OnInstallProgressUpdate?.Invoke(_installProgress);

                throw;
            }

            await RunPostInstallScripts(addon);
            return installResult;
        }

        public async Task<InstallResult> UninstallAsync(string installDirectory, Guid gameId)
        {
            var installResult = new InstallResult(installDirectory, gameId);
            var gameFileList = installResult.FileList;

            var manifest = await ManifestHelper.ReadAsync<GameManifest>(installDirectory, gameId);
            if (manifest == null)
            {
                _logger?.LogInformation("Unable to read or find manifest for game with ID {GameId}. Skip uninstallation!", gameId);
                return installResult;
            }

            // store manifest for current game (could be base game, or any dependent game as this point due to recursive call)
            gameFileList.BaseGame.Manifest = manifest;
            var baseFileList = gameFileList.BaseGame;

            #region Uninstall Dependent Games
            if (manifest.DependentGames != null)
            {
                foreach (var dependentGame in manifest.DependentGames)
                {
                    try
                    {
                        if (ManifestHelper.Exists(installDirectory, dependentGame))
                        {
                            var dependentResult = await UninstallAsync(installDirectory, dependentGame);
                            gameFileList.MergeDependentGames(dependentResult.FileList);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning("Could not uninstall dependent game with ID {GameId}. Assuming it's already uninstalled or never installed...", gameId);
                    }
                }
            }
            #endregion

            #region Delete Files
            var fileListPath = GetMetadataFilePath(installDirectory, gameId, "FileList.txt");

            if (File.Exists(fileListPath))
            {
                var fileList = await File.ReadAllLinesAsync(fileListPath);
                var files = fileList.Select(l => l.Split('|').FirstOrDefault()?.Trim());

                _logger?.LogDebug("Attempting to delete the install files");

                foreach (var file in files.Where(f => f != null && !f.EndsWith("/")))
                {
                    var localPath = Path.Combine(installDirectory, file);
                    baseFileList.AddFile(new GameInstallationFileListEntry.FileEntry
                    {
                        EntryPath = file,
                        LocalPath = localPath,
                    });

                    try
                    {
                        if (File.Exists(localPath))
                            File.Delete(localPath);

                        _logger?.LogTrace("Deleted file {LocalPath}", localPath);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Could not remove file {LocalPath}", localPath);
                    }
                }

                _logger?.LogDebug("Attempting to delete any empty directories");

                DirectoryHelper.DeleteEmptyDirectories(installDirectory);

                if (!Directory.Exists(installDirectory))
                    _logger?.LogDebug("Deleted install directory {InstallDirectory}", installDirectory);
                else
                    _logger?.LogTrace("Removed game files for {GameTitle} ({GameId})", manifest.Title, gameId);
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

            return installResult;
        }

        public async Task<InstallResult> UninstallAddonsAsync(string installDirectory, Guid baseGameId, IEnumerable<Guid> addonIds)
        {
            var installResult = new InstallResult(installDirectory, baseGameId);
            var gameFileList = installResult.FileList;

            var baseManifest = await ManifestHelper.ReadAsync<GameManifest>(installDirectory, baseGameId);
            if (baseManifest == null)
            {
                _logger?.LogInformation("Unable to read or find manifest for addon game with ID {GameId}. Skip uninstallation!", baseGameId);
                return installResult;
            }

            // store manifest for current addon game, skip any files
            gameFileList.BaseGame.Manifest = baseManifest;
            gameFileList.InstallDirectory = installDirectory;

            addonIds ??= [];
            foreach (var dependentGame in baseManifest.DependentGames)
            {
                if (!addonIds.Contains(dependentGame))
                    continue;

                try
                {
                    var dependentResult = await UninstallAddonAsync(installDirectory, dependentGame);
                    gameFileList.MergeBaseAsDependentGame(dependentGame, dependentResult.FileList);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, $"Could not uninstall dependent game {dependentGame} of base game {baseGameId}. Assuming it's already uninstalled or never installed...");
                }
            }

            return installResult;
        }

        public async Task<InstallResult> UninstallAddonAsync(string installDirectory, Guid addonGameId)
        {
            var installResult = new InstallResult(installDirectory, addonGameId);
            var gameFileList = installResult.FileList;

            var manifest = await ManifestHelper.ReadAsync<GameManifest>(installDirectory, addonGameId);

            if (manifest != null)
            {
                var dependentResult = await UninstallAsync(installDirectory, manifest.Id);
                gameFileList.BaseGame.Manifest = manifest;
                gameFileList.Merge(dependentResult.FileList);
            }

            return installResult;
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
            _installProgress.Status = InstallStatus.EnumeratingFiles;
            _installProgress.Indeterminate = true;
            _installProgress.Progress = 0;
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
            {
                // Trigger notification eventually
                _installProgress.Status = InstallStatus.Failed;
                
                OnInstallProgressUpdate?.Invoke(_installProgress);
                
                return newInstallDirectory;
            }

            var directories = Directory.GetDirectories(oldInstallDirectory, "*", SearchOption.AllDirectories);
            var files = Directory.GetFiles(oldInstallDirectory, "*.*", SearchOption.AllDirectories);
            var fileInfos = files.Select(f => new FileInfo(f));
            var totalSize = fileInfos.Sum(fi => fi.Length);
            long totalPos = 0;

            _installProgress.Status = InstallStatus.Moving;
            _installProgress.Indeterminate = false;
            _installProgress.BytesTransferred = totalPos;
            _installProgress.TotalBytes = totalSize;

            foreach (var directory in directories)
            {
                Directory.CreateDirectory(directory.Replace(oldInstallDirectory, newInstallDirectory));
            }

            using (var fileTransferMonitor = new FileTransferMonitor(totalSize))
            {
                foreach (var fileInfo in fileInfos)
                {
                    using (FileStream sourceStream = File.Open(fileInfo.FullName, FileMode.Open))
                    using (FileStream destinationStream = File.Create(fileInfo.FullName.Replace(oldInstallDirectory, newInstallDirectory)))
                    {
                        _installProgress.TotalBytes = totalSize;
                        
                        var buffer = new byte[81920];
                        int bytesRead;
                        
                        while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await destinationStream.WriteAsync(buffer, 0, bytesRead);
                            totalPos += bytesRead;
                            
                            if (fileTransferMonitor.CanUpdate())
                            {
                                fileTransferMonitor.Update(totalPos);

                                _installProgress.TimeRemaining = fileTransferMonitor.GetTimeRemaining();
                                _installProgress.BytesTransferred = fileTransferMonitor.GetBytesTransferred();
                                _installProgress.TransferSpeed = fileTransferMonitor.GetSpeed();
                            
                                OnInstallProgressUpdate?.Invoke(_installProgress);
                            }
                        }
                    }
                }
            }

            _installProgress.BytesTransferred = totalSize;
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

        public async Task UpdateGameInstallationAsync(string installDirectory, Game game)
        {
            // update game and scripts locally
            await WriteManifestAsync(installDirectory, game);
            await WriteScriptsAsync(installDirectory, game);
        }

        private async Task<GameManifest> WriteManifestAsync(string installDirectory, Game game)
        {
            _logger?.LogTrace($"Retrieving game manifest for game {game.Title} with id {game.Id}");
            GameManifest manifest = GetManifest(game.Id);
            _logger?.LogTrace($"Saving Manifest for game {game.Id} into {installDirectory}");
            await ManifestHelper.WriteAsync(manifest, installDirectory);
            return manifest;
        }

        private async Task WriteScriptsAsync(string installDirectory, Game game)
        {
            if (game.Scripts != null)
            {
                _logger?.LogTrace($"Saving scripts for game {game.Title} with id {game.Id} into {installDirectory}");
                
                foreach (var script in game.Scripts)
                {
                    await ScriptHelper.SaveScriptAsync(game, script.Type, installDirectory);
                }
            }
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
                    _logger?.LogError(ex, "Scripts failed to execute for game/addon {GameTitle} ({GameId})", game.Title, game.Id);
                }
            }
        }

        private ExtractionResult DownloadAndExtract(Game game, string destination)
        {
            if (game == null)
            {
                _logger?.LogTrace("Game failed to download, no game was specified");

                throw new ArgumentNullException("No game was specified");
            }

            _logger?.LogTrace("Downloading and extracting {Game} to path {Destination}", game.Title, destination);

            var extractionResult = new ExtractionResult
            {
                Canceled = false,
            };

            var fileManifest = new StringBuilder();
            var files = new List<ExtractionResult.FileEntry>();

            try
            {
                Directory.CreateDirectory(destination);

                _transferStream = Stream(game.Id);
                _reader = ReaderFactory.Open(_transferStream);

                using (var monitor = new FileTransferMonitor(_transferStream.Length))
                {
                    _transferStream.OnProgress += (pos, len) =>
                    {
                        if (monitor.CanUpdate())
                        {
                            monitor.Update(pos);

                            _installProgress.BytesTransferred = monitor.GetBytesTransferred();
                            _installProgress.TotalBytes = len;
                            _installProgress.TransferSpeed = monitor.GetSpeed();
                            _installProgress.TimeRemaining = monitor.GetTimeRemaining();
                            
                            OnInstallProgressUpdate?.Invoke(_installProgress);
                        }
                    };
                }

                _reader.EntryExtractionProgress += (sender, e) =>
                {
                    // Do we need this granular of control? If so, invocations should be rate limited
                    OnArchiveEntryExtractionProgress?.Invoke(this, new ArchiveEntryExtractionProgressArgs
                    {
                        Entry = e.Item,
                        Progress = e.ReaderProgress,
                        Game = game,
                    });
                };

                while (_reader.MoveToNextEntry())
                {
                    if (_reader.Cancelled)
                        break;

                    try
                    {
                        var localFile = Path.Combine(destination, _reader.Entry.Key);

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

                        fileManifest.AppendLine($"{_reader.Entry.Key} | {_reader.Entry.Crc.ToString("X")}");
                        files.Add(new ExtractionResult.FileEntry
                        {
                            EntryPath = _reader.Entry.Key,
                            LocalPath = localFile,
                        });

                        if (crc == 0 || crc != _reader.Entry.Crc)
                            _reader.WriteEntryToDirectory(destination, new ExtractionOptions()
                            {
                                ExtractFullPath = true,
                                Overwrite = true,
                                PreserveFileTime = true
                            });
                        else // Skip to next entry
                            try
                            {
                                _reader.OpenEntryStream().Dispose();
                            }
                            catch
                            {
                                _logger?.LogError("Could not skip to next entry in archive");
                            }
                    }
                    catch (IOException ex)
                    {
                        var errorCode = ex.HResult & 0xFFFF;

                        if (errorCode == 87)
                            throw ex;
                        else
                            _logger?.LogTrace("Not replacing existing file/folder on disk: {Message}", ex.Message);

                        // Skip to next entry
                        _reader.OpenEntryStream().Dispose();
                    }
                }

                _reader.Dispose();
                _transferStream.Dispose();
            }
            catch (ReaderCancelledException ex)
            {
                _logger?.LogTrace(ex, "User cancelled the download");

                extractionResult.Canceled = true;

                if (Directory.Exists(destination))
                {
                    _logger?.LogTrace("Cleaning up orphaned files after cancelled install");

                    Directory.Delete(destination, true);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Could not extract to path {Destination}", destination);

                if (Directory.Exists(destination))
                {
                    _logger?.LogTrace("Cleaning up orphaned install files after bad install");

                    Directory.Delete(destination, true);
                }

                throw new Exception("The game archive could not be extracted, is it corrupted? Please try again");
            }

            if (!extractionResult.Canceled)
            {
                extractionResult.Success = true;
                extractionResult.Directory = destination;
                extractionResult.Files = files;

                var fileListDestination = Path.Combine(destination, ".lancommander", game.Id.ToString(), "FileList.txt");

                if (!Directory.Exists(Path.GetDirectoryName(fileListDestination)))
                    Directory.CreateDirectory(Path.GetDirectoryName(fileListDestination));

                File.WriteAllText(fileListDestination, fileManifest.ToString());

                _logger?.LogTrace("Game {Game} successfully downloaded and extracted to {Destination}", game.Title, destination);
            }

            return extractionResult;
        }

        public async Task<string> GetInstallDirectory(Game game, string installDirectory)
        {
            if (string.IsNullOrWhiteSpace(installDirectory))
                installDirectory = Client.DefaultInstallDirectory;

            if ((game.Type == GameType.Expansion || game.Type == GameType.Mod || game.Type == GameType.StandaloneMod) && game.BaseGameId != Guid.Empty)
            {
                // modify installation passes the original installation of the game including the game folder, use the existing folder,
                // otherwise a name change could lead to installing files into differnt folder
                if (Path.Exists(installDirectory) && Path.Exists(Path.Combine(installDirectory, ".lancommander")))
                {
                    return installDirectory;
                }
                else
                {
                    var baseGame = await Client.Games.GetAsync(game.BaseGameId);

                    return await GetInstallDirectory(baseGame, installDirectory);
                }
            }
            else
                return Path.Combine(installDirectory, game.Title.SanitizeFilename());
        }

        public void CancelInstall()
        {
            _reader?.Cancel();
        }

        public async Task<ICollection<GameManifest>> ReadManifestsAsync(string installDirectory, Guid gameId)
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
                        _logger?.LogError(ex, "Could not load manifest from dependent game {DependentGameId}", dependentGameId);
                    }
                }
            }

            return manifests;
        }

        /// <summary>
        /// Retrieves the archive entries of the current game installation from the server for the specified game
        /// </summary>
        /// <param name="gameId">The unique identifier of the game.</param>
        /// <param name="manifest">The manifest containing metadata of the game's installation.</param>
        /// <returns>
        /// A collection of <see cref="ArchiveEntry"/> representing the archive entries.
        /// Returns an empty list if no entries are found.
        /// </returns>
        /// <exception cref="Exception">
        /// Thrown if the request to retrieve archive entries encounters an error.
        /// </exception>
        protected async Task<IEnumerable<ArchiveEntry>> GetGameInstallationArchiveEntries(Guid gameId, GameManifest manifest)
        {
            var entries = await Client.GetRequestAsync<IEnumerable<ArchiveEntry>>($"/api/Archives/Contents/{manifest.Id}/{manifest.Version}");
            return entries ?? [];
        }

        /// <summary>
        /// Retrieves the archive entries for a game installation, including its base game and dependencies.
        /// </summary>
        /// <param name="installDirectory">The directory where the game is installed.</param>
        /// <param name="gameId">The unique identifier of the game.</param>
        /// <returns>
        /// An instance of <see cref="GameInstallationArchiveEntries"/> containing archive entries
        /// for the base game and any dependent games.
        /// </returns>
        protected async Task<GameInstallationArchiveEntries> GetGameInstallationArchivesEntries(string installDirectory, Guid gameId)
        {
            var gameArchives = new GameInstallationArchiveEntries();

            var manifests = await GetManifestsAsync(installDirectory, gameId);
            if (manifests == null || !manifests.Any())
                return gameArchives;

            // Retrieves and processes the base game manifest and its archive entries.
            var baseManifest = gameArchives.BaseGame.Manifest = manifests.FirstOrDefault(mf => mf.Type.ValueIsIn(GameType.MainGame, GameType.StandaloneExpansion, GameType.StandaloneMod));
            if (baseManifest != null)
            {
                var entries = await GetGameInstallationArchiveEntries(gameId, baseManifest);
                gameArchives.BaseGame.Entries.AddRange(entries);
                manifests = manifests.Except([baseManifest]).ToList();

                var savePathEntries = baseManifest.SavePaths?.SelectMany(p => Client.Saves.GetFileSavePathEntries(p, installDirectory)).ToList() ?? [];
                gameArchives.BaseGame.SavePaths = savePathEntries;
            }

            // Processes dependent game manifests and their corresponding archive entries.
            foreach (var depManifest in manifests ?? [])
            {
                var depEntries = await GetGameInstallationArchiveEntries(gameId, depManifest);

                if (!gameArchives.DependentGames.TryGetValue(depManifest.Id, out var depArchiveInfo))
                {
                    depArchiveInfo = new();
                    gameArchives.DependentGames.Add(depManifest.Id, depArchiveInfo);
                }

                depArchiveInfo.Manifest = depManifest;
                depArchiveInfo.Entries.AddRange(depEntries);

                var savePathEntries = depManifest.SavePaths?.SelectMany(p => Client.Saves.GetFileSavePathEntries(p, installDirectory)).ToList() ?? [];
                depArchiveInfo.SavePaths = savePathEntries;
            }

            return gameArchives;
        }

        public async Task RunAsync(string installDirectory, Guid gameId, Models.Action action, DateTime? lastRun, string args = "")
        {
            var screen = DisplayHelper.GetScreen();

            using (var context = new ProcessExecutionContext(Client, _logger))
            {
                context.AddVariable("ServerAddress", Client.GetServerAddress());
                
                try
                {
                    context.AddVariable("DisplayWidth", screen.Width.ToString());
                    context.AddVariable("DisplayHeight", screen.Height.ToString());
                    context.AddVariable("DisplayRefreshRate", screen.RefreshRate.ToString());
                    context.AddVariable("DisplayBitDepth", screen.BitsPerPixel.ToString());
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Could not get display information for execution context variables");
                }

                try
                {
                    if (Client.IsConnected() && !String.IsNullOrWhiteSpace(Client.Settings.IPXRelayHost))
                    {
                        context.AddVariable("IPXRelayHost", await Client.GetIPXRelayHostAsync());
                        context.AddVariable("IPXRelayPort", Client.Settings.IPXRelayPort.ToString());
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Could not connect to IPXRelay host");
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
                            _logger?.LogTrace("Attempting to download save");

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

                    _running[gameId] = cancellationTokenSource;

                    await task;

                    _running.Remove(gameId);

                    await UploadSavesAsync(manifests, installDirectory);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Game failed to run");
                }

                foreach (var manifest in manifests)
                {
                    #region Run After Stop Script
                    await Client.Scripts.RunAfterStopScriptAsync(installDirectory, gameId);
                    
                    if (manifest.Redistributables != null)
                    {
                        foreach (var redistributable in manifest.Redistributables.Where(r => r.Scripts != null))
                        {
                            await Client.Scripts.RunAfterStopScriptAsync(installDirectory, gameId, redistributable.Id);
                        }
                    }
                    #endregion
                }
            }
        }

        private async Task UploadSavesAsync(ICollection<GameManifest> manifests, string installDirectory)
        {
            if (Client.IsConnected())
            {
                foreach (var manifest in manifests)
                {
                    await RetryHelper.RetryOnExceptionAsync(10, TimeSpan.FromSeconds(1), false, async () =>
                    {
                        _logger?.LogTrace("Attempting to upload save");

                        await Client.Saves.UploadAsync(installDirectory, manifest.Id);

                        return true;
                    });
                }
            }
        }

        public async Task Stop(Guid gameId)
        {
            if (_running.ContainsKey(gameId))
            {
                await _running[gameId].CancelAsync();

                _running.Remove(gameId);
            }
        }

        public bool IsRunning(Guid gameId)
        {
            if (!_running.ContainsKey(gameId))
                return false;

            return !_running[gameId].IsCancellationRequested;
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
            var archives = await GetGameInstallationArchivesEntries(installDirectory, gameId);
            var manifest = archives?.BaseGame?.Entries;
            var entries = archives?.BaseGame?.Entries?.ToList() ?? [];

            foreach ((var dependentGameId, var dependentGameInfo) in archives?.DependentGames ?? [])
            {
                foreach (var depArchive in dependentGameInfo.Entries ?? [])
                {
                    if (depArchive.FullName.EndsWith('/'))
                        continue;

                    var archiveIndex = entries.FindLastIndex(archive => string.Equals(archive.FullName, depArchive.FullName));
                    if (archiveIndex < 0)
                    {
                        entries.Add(depArchive);
                        continue;
                    }

                    entries[archiveIndex] = depArchive;
                }
            }

            // lookup for dependent games
            var lookupEntry = archives?.DependentGames?
                .SelectMany(dep => dep.Value?.Entries?.Select(entry => new { GameId = (Guid?)dep.Key, ArchiveEntry = entry }) ?? [])
                .ToLookup(tentry => tentry.ArchiveEntry, tentry => tentry.GameId) ?? Enumerable.Empty<Guid?>().ToLookup(x => default(ArchiveEntry));

            var conflictedEntries = new List<ArchiveValidationConflict>();

            var savePathEntries = archives?.BaseGame?.SavePaths.ToList() ?? [];
            var depSavePathEntries = archives?.DependentGames?.SelectMany(dep => dep.Value?.SavePaths ?? []).ToList() ?? [];
            savePathEntries.AddRange(depSavePathEntries);

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
                        GameId = lookupEntry[entry]?.FirstOrDefault() ?? gameId,

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
                            GameId = lookupEntry[entry]?.FirstOrDefault() ?? gameId,

                            Name = entry.Name,
                            FullName = entry.FullName,
                            Crc32 = entry.Crc32,
                            LocalFileInfo = new FileInfo(localFile)
                        });
                }
            }

            return conflictedEntries;
        }

        /// <summary>
        /// Downloads the specified files for multiple games (base game, mods, expansions).
        /// </summary>
        /// <param name="installDirectory">The directory where the games are installed.</param>
        /// <param name="entries">
        /// A collection of tuples containing the game ID and the corresponding file path.
        /// </param>
        public async Task DownloadFilesAsync(string installDirectory, IEnumerable<(Guid GameId, string FilePath)> entries)
        {
            var groups = entries.GroupBy(x => x.GameId);
            foreach (var group in groups)
            {
                await DownloadFilesAsync(installDirectory, group.Key, group.Select(x => x.FilePath).ToList());
            }
        }

        /// <summary>
        /// Downloads the specified files for a single game.
        /// </summary>
        /// <param name="installDirectory">The directory where the game is installed.</param>
        /// <param name="gameId">The unique identifier of the game.</param>
        /// <param name="entries">A collection of file paths to download.</param>
        public async Task DownloadFilesAsync(string installDirectory, Guid gameId, ICollection<string> entries)
        {
            var manifest = await ManifestHelper.ReadAsync<GameManifest>(installDirectory, gameId);
            var archive = await Client.GetRequestAsync<Archive>($"/api/Archives/ByVersion/{manifest.Version}");

            await Task.Run(() =>
            {
                try
                {
                    _transferStream = Stream(gameId);
                    _reader = ReaderFactory.Open(_transferStream);

                    while (_reader.MoveToNextEntry())
                    {
                        if (_reader.Cancelled)
                            break;

                        try
                        {
                            if (entries.Contains(_reader.Entry.Key))
                            {
                                var destination = Path.Combine(installDirectory, _reader.Entry.Key?.Replace('/', Path.DirectorySeparatorChar) ?? string.Empty);

                                _reader.WriteEntryToFile(destination, new ExtractionOptions
                                {
                                    Overwrite = true,
                                    PreserveFileTime = true,
                                });
                            }
                            else // Skip to next entry
                                try
                                {
                                    _reader.OpenEntryStream().Dispose();
                                }
                                catch (Exception ex)
                                {
                                    _logger?.LogError(ex, "Could not skip to the next entry in the archive");
                                }
                        }
                        catch (IOException ex)
                        {
                            var errorCode = ex.HResult & 0xFFFF;

                            if (errorCode == 87)
                                throw;
                            else
                                _logger?.LogTrace("Not replacing existing file/folder on disk: {Message}", ex.Message);

                            // Skip to next entry
                            _reader.OpenEntryStream().Dispose();
                        }
                    }

                    _reader.Dispose();
                    _transferStream.Dispose();
                }
                catch (Exception ex)
                {
                    throw new Exception("The game archive could not be extracted, is it corrupted? Please try again");
                }
            });
        }

        public Task RestoreFilesAsync(string installDirectory, Guid gameId, GameInstallationFileList fileListRemoved, GameInstallationFileList fileListAdded)
        {
            var listRemoved = fileListRemoved?.ToFlatDistinctFileEntries() ?? [];
            var listAdded = fileListAdded?.ToFlatDistinctFileEntries() ?? [];

            var uniqueList = listRemoved.ExceptBy(listAdded.Select(x => x.EntryPath), x => x.EntryPath, StringComparer.OrdinalIgnoreCase);
            var possibleRestoreEntries = uniqueList.Select(x => x.EntryPath).ToArray();
            return RestoreFilesAsync(installDirectory, gameId, possibleRestoreEntries);
        }

        /// <summary>
        /// Restores invalidated files matching the specified files.
        /// </summary>
        /// <param name="installDirectory">The directory where the game is installed.</param>
        /// <param name="gameId">The unique identifier of the game.</param>
        /// <param name="entries">A collection of file paths to check and compare with invalidated files.</param>
        public async Task RestoreFilesAsync(string installDirectory, Guid gameId, IEnumerable<string> entries)
        {
            // early out if no files were removed which would require checking
            if (entries == null || !entries.Any())
                return;

            // validate files, which takes addons into account
            var conflicts = await ValidateFilesAsync(installDirectory, gameId) ?? [];
            
            // build list of files to download by matching up removed files with conflicting files, split by game/addon
            var downloadEntries = conflicts
                .IntersectBy(entries, x => x.FullName, StringComparer.OrdinalIgnoreCase)
                .Select(x => (x.GameId ?? gameId, x.FullName)).ToArray();

            await DownloadFilesAsync(installDirectory, downloadEntries);
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
