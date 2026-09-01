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
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Factories;
using Action = System.Action;

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

    public class GameClient(
        ILogger<GameClient> logger,
        ApiRequestFactory apiRequestFactory,
        ProcessExecutionContextFactory processExecutionContextFactory,
        INetworkInformationProvider networkInformationProvider,
        ISettingsProvider settingsProvider,
        IConnectionClient connectionClient,
        RedistributableClient redistributableClient,
        SaveClient saveClient,
        ScriptClient scriptClient,
        ProfileClient profileClient,
        LobbyClient lobbyClient,
        ToolClient toolClient)
    {
        public delegate void OnArchiveEntryExtractionProgressHandler(object sender, ArchiveEntryExtractionProgressArgs e);
        public event OnArchiveEntryExtractionProgressHandler OnArchiveEntryExtractionProgress;

        public delegate void OnArchiveExtractionProgressHandler(long position, long length, Game game);
        public event OnArchiveExtractionProgressHandler OnArchiveExtractionProgress;

        public delegate void OnInstallProgressUpdateHandler(InstallProgress e);
        public event OnInstallProgressUpdateHandler OnInstallProgressUpdate;

        public delegate void OnTaskProgressHandler(InstallTaskProgress progress);
        public event OnTaskProgressHandler OnTaskProgress;

        private const string PlayerAliasFilename = "PlayerAlias";
        private const string KeyFilename = "Key";

        private static readonly TimeSpan ServerNotificationTimeout = TimeSpan.FromSeconds(15);

        private TrackableStream _transferStream;
        private IAsyncReader _reader;

        private readonly InstallProgress _installProgress = new();

        private readonly Dictionary<Guid, CancellationTokenSource> _running = new();

        public async Task<IEnumerable<Game>> GetAsync()
        {
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute("/api/Games")
                .GetAsync<IEnumerable<Game>>();
        }

        public async Task<Game> GetAsync(Guid id)
        {
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Games/{id}")
                .GetAsync<Game>();
        }

        public async Task<Models.Manifest.Game> GetManifestAsync(Guid id, Guid? archiveId = null)
        {
            var route = archiveId.HasValue
                ? $"/api/Games/{id}/Manifest?archiveId={archiveId}"
                : $"/api/Games/{id}/Manifest";

            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute(route)
                .GetAsync<Models.Manifest.Game>();
        }

        /// <summary>
        /// Lists a game's selectable archives (version, changelog, sizes, CreatedOn, and
        /// explicit/effective default flags). Under the immutable-archive model every archive
        /// belonging to the game is a complete, installable snapshot, so all of them are selectable.
        /// </summary>
        public async Task<IEnumerable<Archive>> GetArchivesAsync(Guid gameId)
        {
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Games/{gameId}/Archives")
                .GetAsync<IEnumerable<Archive>>();
        }

        /// <summary>
        /// Resolves a single archive server-side: the explicit <paramref name="archiveId"/> when
        /// provided (validated to belong to the game), otherwise the game's effective default.
        /// Callers that will act on the result later (e.g. install-plan generation) should resolve
        /// exactly once through this method and record the returned archive's ID, rather than
        /// re-deriving "latest" themselves afterwards.
        /// </summary>
        public async Task<Archive> ResolveArchiveAsync(Guid gameId, Guid? archiveId = null)
        {
            var route = archiveId.HasValue
                ? $"/api/Games/{gameId}/Archives/Resolve?archiveId={archiveId}"
                : $"/api/Games/{gameId}/Archives/Resolve";

            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute(route)
                .GetAsync<Archive>();
        }

        /// <summary>
        /// Resolves an archive exactly like <see cref="ResolveArchiveAsync"/>, but returns null
        /// instead of throwing for the two "there is nothing to resolve" answers the server gives:
        /// 404 (the game has no archives at all, or does not exist) and 400 (the requested archive
        /// does not belong to this game — including a pinned archive an administrator has since
        /// deleted). Every other failure — auth, transport, server error — still propagates.
        ///
        /// Use this only where an unresolvable archive is a legitimate, non-fatal outcome for the
        /// operation at hand: skipping an add-on the server has no archive for, or modifying/moving
        /// an existing installation that never re-downloads its base archive. It deliberately does
        /// NOT fall back to the game's effective default — silently re-pointing an explicitly
        /// pinned request at a different archive is exactly what
        /// <c>ArchiveNotFoundForGameException</c> exists to prevent.
        /// </summary>
        public async Task<Archive> TryResolveArchiveAsync(Guid gameId, Guid? archiveId = null)
        {
            try
            {
                return await ResolveArchiveAsync(gameId, archiveId);
            }
            catch (HttpRequestException ex) when (
                ex.StatusCode == HttpStatusCode.NotFound || ex.StatusCode == HttpStatusCode.BadRequest)
            {
                logger?.LogWarning(
                    "Could not resolve archive {ArchiveId} for game {GameId} ({StatusCode}); treating it as unavailable",
                    archiveId, gameId, ex.StatusCode);

                return null;
            }
        }

        /// <summary>
        /// Sets (or, when <paramref name="archiveId"/> is null, clears) a game's explicit default
        /// archive. Requires an administrator role on the server.
        /// </summary>
        public async Task SetDefaultArchiveAsync(Guid gameId, Guid? archiveId)
        {
            await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Games/{gameId}/DefaultArchive")
                .AddBody(new SetDefaultArchiveRequest { ArchiveId = archiveId })
                .PostAsync();
        }

        public async Task<ICollection<Models.Manifest.Game>> GetManifestsAsync(string installDirectory, Guid id)
        {
            var manifests = new List<Models.Manifest.Game>();
            var mainManifest = await ManifestHelper.ReadAsync<Models.Manifest.Game>(installDirectory, id);

            if (mainManifest == null)
                return manifests;

            manifests.Add(mainManifest);

            if (mainManifest.Addons != null)
            {
                foreach (var addon in mainManifest.Addons)
                {
                    try
                    {
                        if (ManifestHelper.Exists(installDirectory, addon.Id))
                        {
                            var addonManifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Game>(installDirectory, addon.Id);

                            if (addonManifest?.Type == GameType.Expansion || addonManifest?.Type == GameType.Mod)
                                manifests.Add(addon);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, $"Could not load manifest from dependent game {addon.Id}");
                    }
                }
            }

            return manifests;
        }

        public async Task<IEnumerable<Models.Manifest.Action>> GetActionsAsync(string installDirectory, Guid id)
        {
            var actions = new List<Models.Manifest.Action>();

            var manifests = await GetManifestsAsync(installDirectory, id);
            var installedIds = manifests.Select(m => m.Id).ToHashSet();

            try
            {
                if (connectionClient.IsConnected() && !connectionClient.IsOfflineMode())
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

                    var serverActions = await apiRequestFactory
                        .Create()
                        .UseRoute($"/api/Games/{id}/Actions")
                        .UseAuthenticationToken()
                        .UseVersioning()
                        .UseCancellationToken(cts.Token)
                        .GetAsync<IEnumerable<SDK.Models.Action>>();

                    actions.AddRange(serverActions
                        .Where(a => installedIds.Contains(a.GameId))
                        .Select(a => new Models.Manifest.Action
                        {
                            Name = a.Name,
                            Arguments = a.Arguments,
                            Path = a.Path,
                            WorkingDirectory = a.WorkingDirectory,
                            IsPrimaryAction = a.IsPrimaryAction,
                            SortOrder = a.SortOrder,
                            Variables = a.Variables,
                            Platforms = a.Platforms
                        }));
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Could not get actions from server");
            }

            if (!actions.Any())
            {
                actions = manifests
                    .Where(m => m != null && m.Actions != null)
                    .SelectMany(m => m.Actions)
                    .OrderByDescending(a => a.IsPrimaryAction)
                    .ThenBy(a => a.SortOrder)
                    .ToList();
            }

            // Merge in actions from tools that are actually installed. Tool actions are persisted to
            // the game's install directory (its manifest) only when the tool is installed, so the
            // presence of the tool manifest on disk gates whether its actions appear.
            var mainManifest = manifests.FirstOrDefault(m => m.Id == id);

            if (mainManifest?.Tools != null)
            {
                foreach (var tool in mainManifest.Tools)
                {
                    if (!ManifestHelper.Exists(installDirectory, tool.Id))
                        continue;

                    try
                    {
                        var toolManifest = await ManifestHelper.ReadAsync<Models.Manifest.Tool>(installDirectory, tool.Id);

                        if (toolManifest?.Actions != null)
                            actions.AddRange(toolManifest.Actions);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Could not load actions from installed tool {ToolId}", tool.Id);
                    }
                }
            }

            var shims = mainManifest == null
                ? Array.Empty<ShimInfo>()
                : CompatibilityResolver.GetShims(mainManifest);

            actions = actions
                .Where(a => CompatibilityResolver.CanRunOnCurrentRuntime(a.Platforms, shims))
                .OrderByDescending(a => CompatibilityResolver.GetBridge(a.Platforms, shims) == null)
                .ThenByDescending(a => a.IsPrimaryAction)
                .ThenBy(a => a.SortOrder)
                .ToList();

            if (manifests.Any(m => m.MultiplayerModes?.Any(m => m.NetworkProtocol == NetworkProtocol.Lobby) ?? false))
            {
                var primaryAction = actions.FirstOrDefault(a => a.IsPrimaryAction);

                if (primaryAction != null)
                {
                    try
                    {
                        var lobbies = lobbyClient.GetSteamLobbies(installDirectory, id);

                        foreach (var lobby in lobbies)
                        {
                            var lobbyAction = new Models.Manifest.Action
                            {
                                Arguments = $"{primaryAction.Arguments} +connect_lobby {lobby.Id}",
                                IsPrimaryAction = true,
                                Name = $"Join {lobby.ExternalUsername}'s lobby",
                                SortOrder = actions.Count,
                                Path = primaryAction.Path,
                                WorkingDirectory = primaryAction.WorkingDirectory,
                                Platforms = primaryAction.Platforms
                            };

                            actions.Add(lobbyAction);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Could not get lobbies");
                    }
                }
            }

            return actions;
        }
        
        /// <summary>
        /// Returns the compatibility runtimes ("shims") attached to an installed game, used to label and
        /// disambiguate bridged actions in the UI.
        /// </summary>
        public async Task<IReadOnlyList<ShimInfo>> GetShimsAsync(string installDirectory, Guid id)
        {
            var manifests = await GetManifestsAsync(installDirectory, id);
            var mainManifest = manifests.FirstOrDefault(m => m.Id == id);

            return CompatibilityResolver.GetShims(mainManifest);
        }

        public async Task<IEnumerable<Game>> GetAddonsAsync(Guid id)
        {
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Games/{id}/Addons")
                .GetAsync<IEnumerable<Game>>();
        }

        public async Task<IEnumerable<Tool>> GetToolsAsync(Guid id)
        {
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Games/{id}/Tools")
                .GetAsync<IEnumerable<Tool>>();
        }

        public async Task<IEnumerable<Script>> GetScriptsAsync(Guid id)
        {
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Games/{id}/Scripts")
                .GetAsync<IEnumerable<Script>>();
        }

        public async Task<bool> CheckForUpdateAsync(Guid id, string currentVersion, Guid? archiveId = null)
        {
            var route = $"/api/Games/{id}/CheckForUpdate?version={currentVersion}";

            if (archiveId.HasValue)
                route += $"&archiveId={archiveId}";

            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute(route)
                .GetAsync<bool>();
        }

        public async Task<IEnumerable<Archive>> GetUpdatesAsync(Guid gameId, string version, Guid? archiveId = null)
        {
            var route = $"/api/Games/{gameId}/Updates?version={version}";

            if (archiveId.HasValue)
                route += $"&archiveId={archiveId}";

            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute(route)
                .GetAsync<IEnumerable<Archive>>();
        }

        /// <summary>
        /// Streams one exact archive of a game through the game-scoped download endpoint. This
        /// route — not the raw <c>/Download/Archive/{id}</c> one — is the only correct way to
        /// download a <em>game</em> archive: it is the endpoint that enforces
        /// <c>Server.Archives.AllowInsecureDownloads</c> and validates that the archive actually
        /// belongs to the requested game (rejecting a cross-game archive id outright rather than
        /// serving it). Because normal generated install plans always pin an ArchiveId, routing
        /// pinned downloads anywhere else would mean the policy gate applied to virtually no real
        /// download at all.
        /// </summary>
        private async Task<TrackableStream> StreamArchiveAsync(Guid gameId, Guid archiveId)
        {
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Games/{gameId}/Download?archiveId={archiveId}")
                .StreamAsync();
        }

        /// <summary>
        /// Downloads and extracts a specific archive for a game update.
        /// </summary>
        /// <returns>True if successful, false if canceled.</returns>
        public async Task<bool> ApplyUpdateArchiveAsync(Guid archiveId, Guid gameId, string destination, CancellationToken cancellationToken = default)
        {
            var game = await GetAsync(gameId);

            if (game == null)
                throw new InstallException($"Could not fetch game info for game {gameId}");

            _installProgress.Game = game;
            _installProgress.Title = game.Title;

            var result = await DownloadAndExtractArchiveAsync(archiveId, game, destination, cancellationToken);

            if (result.Canceled)
                return false;

            if (!result.Success)
                throw new InstallException("Could not extract the update archive. Retry the update or check your connection");

            return true;
        }

        internal async Task<ExtractionResult> DownloadAndExtractArchiveAsync(Guid archiveId, Game game, string destination, CancellationToken cancellationToken = default)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game), "No game was specified");

            logger?.LogTrace("Downloading archive {ArchiveId} and extracting {Game} to path {Destination}", archiveId, game.Title, destination);

            var extractionResult = new ExtractionResult
            {
                Canceled = false,
            };

            var fileManifest = new StringBuilder();
            var files = new List<ExtractionResult.FileEntry>();

            try
            {
                Directory.CreateDirectory(destination);

                var stream = await StreamArchiveAsync(game.Id, archiveId);

                var monitor = new FileTransferMonitor(stream.Length);
                var progress = new Progress<ProgressReport>(report =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _reader?.Cancel();
                        _installProgress.Status = InstallStatus.Canceled;
                        OnInstallProgressUpdate?.Invoke(_installProgress);
                        return;
                    }

                    if (monitor.CanUpdate())
                    {
                        monitor.Update(stream.Position);
                        _installProgress.BytesTransferred = monitor.GetBytesTransferred();
                        _installProgress.TotalBytes = stream.Length;
                        _installProgress.TransferSpeed = monitor.GetSpeed();
                        _installProgress.TimeRemaining = monitor.GetTimeRemaining();
                        OnInstallProgressUpdate?.Invoke(_installProgress);
                    }

                    OnArchiveEntryExtractionProgress?.Invoke(this, new ArchiveEntryExtractionProgressArgs
                    {
                        Progress = report,
                        Game = game,
                    });
                });

                _reader = await ReaderFactory.OpenAsyncReader(stream, new ReaderOptions { Progress = progress }, cancellationToken);

                _installProgress.Status = InstallStatus.Downloading;
                OnInstallProgressUpdate?.Invoke(_installProgress);

                while (await _reader.MoveToNextEntryAsync(cancellationToken))
                {
                    if (_reader.Cancelled)
                        break;

                    try
                    {
                        var entryKey = _reader.Entry.Key;
                        var localFile = Path.Combine(destination, entryKey);

                        fileManifest.AppendLine($"{entryKey} | {_reader.Entry.Crc.ToString("X")}");
                        files.Add(new ExtractionResult.FileEntry
                        {
                            EntryPath = entryKey,
                            LocalPath = localFile,
                        });

                        await _reader.WriteEntryToDirectoryAsync(destination, new ExtractionOptions()
                        {
                            ExtractFullPath = true,
                            Overwrite = true,
                            PreserveFileTime = true
                        }, cancellationToken);
                    }
                    catch (IOException ex)
                    {
                        var errorCode = ex.HResult & 0xFFFF;

                        if (errorCode == 87)
                            throw;
                        else
                            logger?.LogTrace("Not replacing existing file/folder on disk: {EntryKey} - {Message}", _reader.Entry.Key, ex.Message);

                        await using var es = await _reader.OpenEntryStreamAsync(cancellationToken);
                    }
                }

                await _reader.DisposeAsync();
                await stream.DisposeAsync();
            }
            catch (ReaderCancelledException ex)
            {
                logger?.LogTrace(ex, "User cancelled the download");
                extractionResult.Canceled = true;
            }
            catch (HttpRequestException ex)
            {
                // The download itself was refused (unauthorized, archive not found / not this
                // game's, ...). Surface that as-is rather than mislabeling it as a corrupt archive.
                logger?.LogError(ex, "Could not download archive {ArchiveId} for game {GameTitle} ({GameId}): {StatusCode}",
                    archiveId, game.Title, game.Id, ex.StatusCode);

                throw;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Could not extract archive {ArchiveId} to path {Destination}", archiveId, destination);
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
            }

            return extractionResult;
        }

        private async Task<bool> CanStreamLatestArchiveAsync(Guid id)
        {
            try
            {
                await apiRequestFactory
                    .Create()
                    .UseAuthenticationToken()
                    .UseVersioning()
                    .UseRoute($"/api/Games/{id}/Download")
                    .HeadAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<TrackableStream> StreamLatestArchiveAsync(Guid id)
        {
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Games/{id}/Download")
                .StreamAsync();
        }

        public async Task StartedAsync(Guid id)
        {
            if (!connectionClient.IsConnected())
                return;
            
            logger?.LogTrace("Signaling to the server that we started the game...");

            using var timeout = new CancellationTokenSource(ServerNotificationTimeout);

            try
            {
                await apiRequestFactory
                    .Create()
                    .UseAuthenticationToken()
                    .UseVersioning()
                    .UseRoute($"/api/Games/{id}/Started")
                    .UseCancellationToken(timeout.Token)
                    .GetAsync<object>();
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed sending start request to server");
            }
        }

        public async Task StoppedAsync(Guid id)
        {
            if (!connectionClient.IsConnected())
                return;
            
            logger?.LogTrace("Signaling to the server that we stopped the game...");

            using var timeout = new CancellationTokenSource(ServerNotificationTimeout);

            try
            {
                await apiRequestFactory
                    .Create()
                    .UseAuthenticationToken()
                    .UseVersioning()
                    .UseRoute($"/api/Games/{id}/Stopped")
                    .UseCancellationToken(timeout.Token)
                    .GetAsync<object>();
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed sending stop request to server");
            }
        }

        public async Task<string> GetAllocatedKeyAsync(Guid id)
        {
            logger?.LogTrace("Requesting allocated key...");
            
            var request = new KeyRequest()
            {
                GameId = id,
                MacAddress = networkInformationProvider.GetMacAddress(),
                ComputerName = Environment.MachineName,
                IpAddress = networkInformationProvider.GetIpAddress(),
            };

            var response = await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Keys/GetAllocated/{id}")
                .AddBody(request)
                .PostAsync<Key>();

            if (response == null)
                return string.Empty;

            return response.Value;
        }

        public async Task<string> GetNewKey(Guid id)
        {
            logger?.LogTrace("Requesting new key allocation...");

            var request = new KeyRequest()
            {
                GameId = id,
                MacAddress = networkInformationProvider.GetMacAddress(),
                ComputerName = Environment.MachineName,
                IpAddress = networkInformationProvider.GetIpAddress(),
            };

            var response = await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Keys/Allocate/{id}")
                .AddBody(request)
                .PostAsync<Key>();

            if (response == null)
                return string.Empty;

            return response.Value;
        }

        /// <summary>
        /// Returns the key currently tracked for this install. The locally tracked key
        /// (stored in .lancommander/{gameId}/Key) is authoritative: a new key is only
        /// requested from the server and persisted when the install has no key tracked yet.
        /// This prevents re-allocating a fresh key on every run/install (e.g. when the
        /// machine's detected MAC address is not stable between launches).
        /// </summary>
        public async Task<string> GetOrAllocateKeyAsync(string installDirectory, Guid gameId)
        {
            var currentKey = await GetCurrentKeyAsync(installDirectory, gameId);

            if (!string.IsNullOrWhiteSpace(currentKey))
            {
                logger?.LogTrace("Reusing key tracked for game {GameId}", gameId);
                return currentKey;
            }

            var allocatedKey = await GetAllocatedKeyAsync(gameId);

            if (!string.IsNullOrWhiteSpace(allocatedKey))
                await UpdateCurrentKeyAsync(installDirectory, gameId, allocatedKey);

            return allocatedKey;
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
        public async Task<InstallResult> InstallAsync(Guid gameId, string installDirectory = "", Guid[] addonIds = null, int maxAttempts = 10, CancellationToken cancellationToken = default)
        {
            var installResult = new InstallResult(installDirectory, gameId);
            var gameFileList = installResult.FileList;
            SDK.Models.Manifest.Game manifest = null;

            if (string.IsNullOrWhiteSpace(installDirectory))
                installDirectory = settingsProvider.CurrentValue.Games.InstallDirectories.First();

            var game = await GetAsync(gameId);
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
                var baseGame = await GetAsync(game.BaseGameId);

                destination = await GetInstallDirectory(baseGame, installDirectory);

                if (!Directory.Exists(destination))
                {
                    var baseGameFileList = await InstallAsync(game.BaseGameId, installDirectory, null, maxAttempts, cancellationToken);
                    destination = installResult.InstallDirectory;
                }
            }

            try
            {
                if (ManifestHelper.Exists(destination, game.Id))
                    manifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Game>(destination, game.Id);
            }
            catch (Exception ex)
            {
                logger?.LogTrace(ex, "Error reading manifest before install");
            }

            logger?.LogTrace("Installing game {GameTitle} ({GameId})", game.Title, game.Id);

            // An overlay (expansion/mod/standalone mod) extracts into its base game's directory,
            // which it does not own — a failed or canceled download there must never be allowed to
            // delete the base game's installation. Everything else gets its own directory.
            var destinationOwnership = IsOverlayInstallType(game)
                ? InstallDestinationOwnership.ExistingInstallation
                : InstallDestinationOwnership.Fresh;

            // Download and extract
            var result = await RetryHelper.RetryOnExceptionAsync(maxAttempts, TimeSpan.FromMilliseconds(500), new ExtractionResult(), async () =>
            {
                logger?.LogTrace("Attempting to download and extract game");

                return await Task.Run(async () => await DownloadAndExtractAsync(game, destination, cancellationToken, skipFiles: null, archiveId: null, destinationOwnership));
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
                logger?.LogTrace("Attempting to get game manifest");
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
                logger?.LogTrace("Installing redistributables");

                await redistributableClient.InstallAsync(game);
            }
            #endregion

            #region Download Latest Save
            logger?.LogInformation("Downloading latest save for game {GameTitle} ({GameId}) during install", game.Title, game.Id);

            _installProgress.Status = InstallStatus.DownloadingSaves;

            OnInstallProgressUpdate?.Invoke(_installProgress);

            await saveClient.DownloadAsync(game.InstallDirectory, game.Id);
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
            var game = await GetAsync(baseGameId);

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
                        addons.Add(await GetAsync(addonId));
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Could not get information for addon with ID {AddonId}, skipping install", addonId);
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
                        logger?.LogError(ex, "Could not install expansion with ID {AddonId}", expansion.Id);
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
                        logger?.LogError(ex, "Could not install mod with ID {AddonId}", mod.Id);
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
                logger?.LogDebug("Install canceled");

                _installProgress.Status = InstallStatus.Canceled;
                OnInstallProgressUpdate?.Invoke(_installProgress);

                throw;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to install addon {AddonTitle} ({AddonId})", addon.Title, addon.Id);

                _installProgress.Status = InstallStatus.Failed;
                OnInstallProgressUpdate?.Invoke(_installProgress);

                throw;
            }

            await RunPostInstallScripts(addon);
            return installResult;
        }

        /// <summary>
        /// Builds the standard ordered task list for a full base-game/standalone-mod install or
        /// in-place update: verify local files, download+extract the exact
        /// <paramref name="archiveId"/> snapshot, write the manifest, save scripts, download
        /// saves, and (when the game has scripts) run install/key-change/name-change scripts,
        /// plus a manual-download task when the game has manuals. Pure — takes only the
        /// already-resolved <paramref name="game"/>/archive and makes no network calls of its
        /// own — so both a fresh install (<see cref="GenerateInstallPlanAsync"/>) and an explicit
        /// in-place version change (the launcher's InstallService.ChangeVersionAsync) share
        /// exactly the same task shape. This guarantees an in-place transition can never silently
        /// "succeed" having skipped the actual download/extract/manifest work a fresh install
        /// always performs, and that any future change to that task shape can't accidentally
        /// drift the two flows apart.
        /// </summary>
        public static List<InstallTaskDefinition> BuildGameInstallTasks(Game game, Guid? archiveId, string archiveVersion)
        {
            var tasks = new List<InstallTaskDefinition>();
            int taskOrder = 0;

            tasks.Add(new InstallTaskDefinition
            {
                Type = InstallTaskType.VerifyFiles,
                Title = "Verify local files",
                Order = taskOrder++,
                TargetId = game.Id,
                TargetName = game.Title,
                IsCritical = false,
            });

            tasks.Add(new InstallTaskDefinition
            {
                Type = InstallTaskType.DownloadAndExtract,
                Title = $"Download {game.Title}",
                Order = taskOrder++,
                TargetId = game.Id,
                TargetName = game.Title,
                IsCritical = true,
                ReportsProgress = true,
                Parameters = new Dictionary<string, string>
                {
                    ["ArchiveId"] = archiveId?.ToString() ?? string.Empty,
                    ["ArchiveVersion"] = archiveVersion ?? string.Empty,
                },
            });

            tasks.Add(new InstallTaskDefinition
            {
                Type = InstallTaskType.WriteManifest,
                Title = "Write manifest",
                Order = taskOrder++,
                TargetId = game.Id,
                TargetName = game.Title,
                IsCritical = true,
            });

            tasks.Add(new InstallTaskDefinition
            {
                Type = InstallTaskType.WriteScripts,
                Title = "Save scripts",
                Order = taskOrder++,
                TargetId = game.Id,
                TargetName = game.Title,
                IsCritical = false,
            });

            tasks.Add(new InstallTaskDefinition
            {
                Type = InstallTaskType.DownloadSaves,
                Title = "Download saves",
                Order = taskOrder++,
                TargetId = game.Id,
                TargetName = game.Title,
                IsCritical = false,
                ReportsProgress = true,
            });

            if (game.Scripts != null && game.Scripts.Any())
            {
                tasks.Add(new InstallTaskDefinition
                {
                    Type = InstallTaskType.RunInstallScript,
                    Title = "Run install script",
                    Order = taskOrder++,
                    TargetId = game.Id,
                    TargetName = game.Title,
                    IsCritical = false,
                });

                tasks.Add(new InstallTaskDefinition
                {
                    Type = InstallTaskType.RunKeyChangeScript,
                    Title = "Apply key",
                    Order = taskOrder++,
                    TargetId = game.Id,
                    TargetName = game.Title,
                    IsCritical = false,
                });

                tasks.Add(new InstallTaskDefinition
                {
                    Type = InstallTaskType.RunNameChangeScript,
                    Title = "Apply player name",
                    Order = taskOrder++,
                    TargetId = game.Id,
                    TargetName = game.Title,
                    IsCritical = false,
                });
            }

            if (game.Media != null && game.Media.Any(m => m.Type == MediaType.Manual))
            {
                var manualIds = game.Media
                    .Where(m => m.Type == MediaType.Manual)
                    .Select(m => m.Id.ToString());

                tasks.Add(new InstallTaskDefinition
                {
                    Type = InstallTaskType.DownloadManual,
                    Title = "Download manuals",
                    Order = taskOrder++,
                    TargetId = game.Id,
                    TargetName = game.Title,
                    IsCritical = false,
                    Parameters = new Dictionary<string, string>
                    {
                        ["ManualIds"] = string.Join(",", manualIds),
                    },
                });
            }

            return tasks;
        }

        /// <summary>
        /// Generates an install plan for a game, producing a list of queue items and their tasks
        /// without executing anything.
        /// </summary>
        /// <param name="requireResolvableArchive">
        /// Whether the base game's archive target must actually resolve server-side. True (the
        /// default) for any operation that will really install files — a fresh install or an
        /// explicit version change — so an unresolvable target fails loudly instead of producing a
        /// plan that cannot install anything. Pass false for operations against an
        /// already-installed, archive-pinned installation that never re-download the base archive
        /// (modify, move): those must keep working even when an administrator has deleted the
        /// archive the installation is pinned to. In that case <paramref name="archiveId"/> is
        /// carried onto the plan verbatim — the pinned archive is never reinterpreted as, or
        /// silently replaced by, the game's current effective default.
        /// </param>
        /// <param name="destinationOwnership">
        /// Whether the resolved destination belongs to this install (a brand-new side-by-side or
        /// first install) or to an existing installation it is being applied on top of (an in-place
        /// version change, a legacy exact-directory update, an overlay add-on's shared base game
        /// directory). Only the caller that resolved the destination knows this, and it decides
        /// whether a canceled/failed download is allowed to recursively delete that directory, so
        /// it defaults to the safe <see cref="InstallDestinationOwnership.ExistingInstallation"/>.
        /// </param>
        public async Task<InstallPlan> GenerateInstallPlanAsync(Guid gameId, string installDirectory, Guid[] addonIds = null, Guid[] toolIds = null, Guid? archiveId = null, bool useExactInstallDirectory = false, bool requireResolvableArchive = true, InstallDestinationOwnership destinationOwnership = InstallDestinationOwnership.ExistingInstallation)
        {
            logger?.LogInformation("[InstallQueue] GenerateInstallPlan: gameId={GameId}, installDir={InstallDir}, addonIds={AddonIds}",
                gameId, installDirectory, addonIds != null ? string.Join(",", addonIds) : "none");

            var plan = new InstallPlan();
            var game = await GetAsync(gameId);

            logger?.LogInformation("[InstallQueue] GenerateInstallPlan: Fetched game {Title} ({Id}), type={Type}, baseGameId={BaseGameId}, redistCount={RedistCount}, scriptCount={ScriptCount}",
                game?.Title, game?.Id, game?.Type, game?.BaseGameId, game?.Redistributables?.Count() ?? 0, game?.Scripts?.Count() ?? 0);

            // Resolve the base game's archive exactly once, here, and pin it onto the plan item so
            // execution downloads this exact archive later rather than re-resolving "latest" and
            // potentially picking up an archive uploaded after the plan was generated.
            var resolvedArchive = requireResolvableArchive
                ? await ResolveArchiveAsync(gameId, archiveId)
                : await TryResolveArchiveAsync(gameId, archiveId);

            // Falling back to the requested id (rather than to the effective default) keeps a
            // pinned-but-deleted archive pinned: modify/move don't need it, and anything that
            // genuinely has to download will fail against that exact id instead of quietly
            // installing a different version than the one the installation is pinned to.
            var resolvedArchiveId = resolvedArchive?.Id ?? archiveId;
            var resolvedArchiveVersion = resolvedArchive?.Version;

            if (string.IsNullOrWhiteSpace(installDirectory))
                installDirectory = settingsProvider.CurrentValue.Games.InstallDirectories.First();

            // Callers that have already resolved a collision-safe, exact destination (e.g. the
            // launcher picking a versioned sibling directory for a side-by-side installation) pass
            // useExactInstallDirectory so it is used verbatim instead of being re-suffixed with the
            // game's title, which is only correct for the "installDirectory is a parent folder" case.
            var destination = useExactInstallDirectory
                ? installDirectory
                : await GetInstallDirectory(game, installDirectory);

            logger?.LogInformation("[InstallQueue] GenerateInstallPlan: Resolved install directory to {Destination}", destination);

            // Handle standalone mods — the base game must be installed first, and the
            // standalone mod's archive extracts into the base game's directory. The mod is
            // still a separate library entity with an independent lifecycle from the base game.
            if (!useExactInstallDirectory && game.Type == GameType.StandaloneMod && game.BaseGameId != Guid.Empty)
            {
                var baseGame = await GetAsync(game.BaseGameId);
                var baseDestination = await GetInstallDirectory(baseGame, installDirectory);

                if (!Directory.Exists(baseDestination))
                {
                    var basePlan = await GenerateInstallPlanAsync(game.BaseGameId, installDirectory, destinationOwnership: InstallDestinationOwnership.Fresh);
                    plan.Items.AddRange(basePlan.Items);
                }

                destination = baseDestination;

                // The mod extracts into the base game's directory — whether the base game was just
                // planned above or was already installed, this item never owns that directory.
                destinationOwnership = InstallDestinationOwnership.ExistingInstallation;
            }

            // Base game item
            var gameItem = new InstallPlanItem
            {
                EntityId = game.Id,
                Title = game.Title,
                Type = InstallPlanItemType.Game,
                InstallDirectory = destination,
                Order = plan.Items.Count,
                ArchiveId = resolvedArchiveId,
                ArchiveVersion = resolvedArchiveVersion,
                DestinationOwnership = destinationOwnership,
                Tasks = BuildGameInstallTasks(game, resolvedArchiveId, resolvedArchiveVersion),
            };

            plan.Items.Add(gameItem);

            // Addon items
            if (addonIds != null)
            {
                foreach (var addonId in addonIds)
                {
                    var addon = await GetAsync(addonId);

                    if (addon == null)
                    {
                        logger?.LogWarning("[InstallQueue] GenerateInstallPlan: Addon {AddonId} could not be fetched, skipping it", addonId);
                        continue;
                    }

                    // Resolve each addon's own effective default archive exactly once so its plan
                    // item, like the base game's, stays pinned to the archive chosen at
                    // generation time. An addon the server has no archive for cannot contribute
                    // anything to this install: its plan item's DownloadAndExtract task is
                    // critical, so including it would guarantee a failure, and letting the
                    // resolve call throw would abort the entire base game plan over one
                    // unavailable addon. Skip just that addon instead — and never substitute a
                    // different archive for an explicitly selected one.
                    var resolvedAddonArchive = await TryResolveArchiveAsync(addonId);

                    if (resolvedAddonArchive == null)
                    {
                        logger?.LogWarning("[InstallQueue] GenerateInstallPlan: Addon {AddonTitle} ({AddonId}) has no installable archive on the server, skipping it", addon.Title, addonId);
                        continue;
                    }

                    var addonItem = new InstallPlanItem
                    {
                        EntityId = addon.Id,
                        Title = addon.Title,
                        Type = InstallPlanItemType.Addon,
                        InstallDirectory = destination,
                        Order = plan.Items.Count,
                        // References the base game item's plan-scoped identity, not its raw
                        // EntityId — two concurrently queued plans for different versions of the
                        // same game each have their own gameItem.PlanItemId, so an addon item
                        // always resolves back to the correct sibling base game item.
                        DependsOnId = gameItem.PlanItemId,
                        ArchiveId = resolvedAddonArchive.Id,
                        ArchiveVersion = resolvedAddonArchive.Version,
                        // An add-on always overlays the base game's directory, which the base game
                        // item owns. A failed/canceled add-on download must leave the base game's
                        // files (and any sibling add-on's) completely intact.
                        DestinationOwnership = InstallDestinationOwnership.ExistingInstallation,
                    };

                    int addonTaskOrder = 0;

                    addonItem.Tasks.Add(new InstallTaskDefinition
                    {
                        Type = InstallTaskType.DownloadAndExtract,
                        Title = $"Download {addon.Title}",
                        Order = addonTaskOrder++,
                        TargetId = addon.Id,
                        TargetName = addon.Title,
                        IsCritical = true,
                        ReportsProgress = true,
                        Parameters = new Dictionary<string, string>
                        {
                            ["ArchiveId"] = resolvedAddonArchive.Id.ToString(),
                            ["ArchiveVersion"] = resolvedAddonArchive.Version ?? string.Empty,
                        },
                    });

                    addonItem.Tasks.Add(new InstallTaskDefinition
                    {
                        Type = InstallTaskType.WriteManifest,
                        Title = "Write manifest",
                        Order = addonTaskOrder++,
                        TargetId = addon.Id,
                        TargetName = addon.Title,
                        IsCritical = true,
                    });

                    addonItem.Tasks.Add(new InstallTaskDefinition
                    {
                        Type = InstallTaskType.WriteScripts,
                        Title = "Save scripts",
                        Order = addonTaskOrder++,
                        TargetId = addon.Id,
                        TargetName = addon.Title,
                        IsCritical = false,
                    });

                    if (addon.Scripts != null && addon.Scripts.Any())
                    {
                        addonItem.Tasks.Add(new InstallTaskDefinition
                        {
                            Type = InstallTaskType.RunInstallScript,
                            Title = "Run install script",
                            Order = addonTaskOrder++,
                            TargetId = addon.Id,
                            TargetName = addon.Title,
                            IsCritical = false,
                        });

                        addonItem.Tasks.Add(new InstallTaskDefinition
                        {
                            Type = InstallTaskType.RunKeyChangeScript,
                            Title = "Apply key",
                            Order = addonTaskOrder++,
                            TargetId = addon.Id,
                            TargetName = addon.Title,
                            IsCritical = false,
                        });

                        addonItem.Tasks.Add(new InstallTaskDefinition
                        {
                            Type = InstallTaskType.RunNameChangeScript,
                            Title = "Apply player name",
                            Order = addonTaskOrder++,
                            TargetId = addon.Id,
                            TargetName = addon.Title,
                            IsCritical = false,
                        });
                    }

                    plan.Items.Add(addonItem);
                }
            }

            // Tool items
            var toolIdSet = new HashSet<Guid>(toolIds ?? Array.Empty<Guid>());

            // Always-install tools are installed alongside the game regardless of user selection
            try
            {
                var gameTools = await GetToolsAsync(game.Id);

                if (gameTools != null)
                {
                    foreach (var alwaysInstallTool in gameTools.Where(t => t.AlwaysInstall))
                        toolIdSet.Add(alwaysInstallTool.Id);
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "[InstallQueue] GenerateInstallPlan: Could not resolve always-install tools for game {GameId}", game.Id);
            }

            foreach (var toolId in toolIdSet)
            {
                var tool = await toolClient.GetAsync(toolId);
                var toolPlan = await toolClient.GenerateInstallPlanAsync(tool, destination);

                foreach (var toolPlanItem in toolPlan.Items)
                {
                    toolPlanItem.Order = plan.Items.Count;
                    // References the base game's plan-scoped identity (see the addon items above)
                    // so tool items resolve to the correct base game item when two versions of the
                    // same game are queued at once.
                    toolPlanItem.DependsOnId = gameItem.PlanItemId;
                    plan.Items.Add(toolPlanItem);
                }
            }

            // Redistributable items
            if (game.Redistributables != null)
            {
                foreach (var redist in game.Redistributables)
                {
                    var redistItem = new InstallPlanItem
                    {
                        EntityId = redist.Id,
                        Title = redist.Name,
                        Type = InstallPlanItemType.Redistributable,
                        InstallDirectory = destination,
                        Order = plan.Items.Count,
                        DependsOnId = gameItem.PlanItemId,
                    };

                    redistItem.Tasks.Add(new InstallTaskDefinition
                    {
                        Type = InstallTaskType.DownloadAndExtract,
                        Title = $"Download {redist.Name}",
                        Order = 0,
                        TargetId = redist.Id,
                        TargetName = redist.Name,
                        IsCritical = true,
                        ReportsProgress = true,
                        Parameters = new Dictionary<string, string>
                        {
                            ["ParentGameId"] = game.Id.ToString(),
                        },
                    });

                    redistItem.Tasks.Add(new InstallTaskDefinition
                    {
                        Type = InstallTaskType.RunRedistributableInstallScript,
                        Title = $"Install {redist.Name}",
                        Order = 1,
                        TargetId = redist.Id,
                        TargetName = redist.Name,
                        IsCritical = false,
                        Parameters = new Dictionary<string, string>
                        {
                            ["ParentGameId"] = game.Id.ToString(),
                        },
                    });

                    plan.Items.Add(redistItem);
                }
            }

            return plan;
        }

        /// <summary>
        /// Executes a single install plan item's tasks in order, firing OnTaskProgress events for each.
        /// </summary>
        public async Task<InstallResult> ExecuteInstallPlanItemAsync(InstallPlanItem planItem, CancellationToken cancellationToken = default)
        {
            var installResult = new InstallResult(planItem.InstallDirectory, planItem.EntityId);

            switch (planItem.Type)
            {
                case InstallPlanItemType.Game:
                case InstallPlanItemType.Addon:
                    await ExecuteGamePlanItemAsync(planItem, installResult, cancellationToken);
                    break;

                case InstallPlanItemType.Redistributable:
                    await ExecuteRedistributablePlanItemAsync(planItem, installResult, cancellationToken);
                    break;

                case InstallPlanItemType.Tool:
                    var toolResult = await toolClient.ExecuteInstallPlanItemAsync(planItem, cancellationToken);
                    installResult.InstallDirectory = toolResult.InstallDirectory;
                    break;
            }

            return installResult;
        }

        private async Task ExecuteGamePlanItemAsync(InstallPlanItem planItem, InstallResult installResult, CancellationToken cancellationToken)
        {
            logger?.LogInformation("[InstallQueue] ExecuteGamePlanItem: Starting for {Title} ({EntityId}), type={Type}, installDir={InstallDir}, taskCount={TaskCount}",
                planItem.Title, planItem.EntityId, planItem.Type, planItem.InstallDirectory, planItem.Tasks?.Count ?? 0);

            var game = await GetAsync(planItem.EntityId);

            if (game == null)
            {
                logger?.LogInformation("[InstallQueue] ExecuteGamePlanItem: ERROR - Could not fetch game {EntityId} from server", planItem.EntityId);
                throw new InstallException($"Could not fetch game info for {planItem.Title}");
            }

            // Set the progress context so OnInstallProgressUpdate events carry the game reference
            _installProgress.Game = game;
            _installProgress.Title = game.Title;

            var gameFileList = installResult.FileList;
            SDK.Models.Manifest.Game manifest = null;

            // Files confirmed to exist locally and match FileList.txt — skip during extraction
            HashSet<string> verifiedFiles = null;

            foreach (var taskDef in planItem.Tasks.OrderBy(t => t.Order))
            {
                cancellationToken.ThrowIfCancellationRequested();

                logger?.LogInformation("[InstallQueue] ExecuteGamePlanItem: Running task [{Order}] {Type}: {Title} (critical={IsCritical})",
                    taskDef.Order, taskDef.Type, taskDef.Title, taskDef.IsCritical);

                var taskProgress = new InstallTaskProgress
                {
                    // PlanItemId, not EntityId: two concurrently queued installs of different
                    // versions of the same game share an EntityId but never a PlanItemId, so this
                    // is what lets the launcher's queue match progress to the right queue item.
                    QueueItemId = planItem.PlanItemId,
                    TaskId = taskDef.Id,
                    TaskType = taskDef.Type,
                    TaskTitle = taskDef.Title,
                    TaskStatus = InstallTaskStatus.Running,
                };

                OnTaskProgress?.Invoke(taskProgress);

                try
                {
                    switch (taskDef.Type)
                    {
                        case InstallTaskType.VerifyFiles:
                            verifiedFiles = await VerifyLocalFilesAsync(planItem.InstallDirectory, game.Id, cancellationToken);
                            logger?.LogInformation("[InstallQueue] VerifyFiles: {Count} files verified as present", verifiedFiles?.Count ?? 0);
                            break;

                        case InstallTaskType.DownloadAndExtract:
                            var skipFiles = verifiedFiles;
                            var maxAttempts = Math.Max(1, settingsProvider.CurrentValue.Games.MaxInstallAttempts);
                            var result = await RetryHelper.RetryOnExceptionAsync(maxAttempts, TimeSpan.FromMilliseconds(500), new ExtractionResult(), async () =>
                            {
                                return await Task.Run(async () => await DownloadAndExtractAsync(game, planItem.InstallDirectory, cancellationToken, skipFiles, planItem.ArchiveId, planItem.DestinationOwnership));
                            });

                            if (!result.Success && !result.Canceled)
                                throw new InstallException("Could not extract the installer. Retry the install or check your connection");
                            
                            if (result.Canceled)
                                throw new InstallCanceledException("Game install was canceled");

                            game.InstallDirectory = result.Directory;
                            installResult.InstallDirectory = result.Directory;
                            planItem.InstallDirectory = result.Directory;

                            gameFileList.BaseGame.AddFiles(result.Files?
                                .Where(x => !x.EntryPath.EndsWith("/"))
                                .Select(x => new GameInstallationFileListEntry.FileEntry
                                {
                                    EntryPath = x.EntryPath,
                                    LocalPath = x.LocalPath,
                                }) ?? []);
                            break;

                        case InstallTaskType.WriteManifest:
                            manifest = await RetryHelper.RetryOnExceptionAsync(10, TimeSpan.FromSeconds(1), (SDK.Models.Manifest.Game)null, async () =>
                            {
                                return await WriteManifestAsync(planItem.InstallDirectory, game, planItem.ArchiveId);
                            });

                            if (manifest == null)
                                throw new InstallException("Could not grab the manifest file. Retry the install or check your connection");

                            gameFileList.BaseGame.Manifest = manifest;
                            break;

                        case InstallTaskType.WriteScripts:
                            await WriteScriptsAsync(planItem.InstallDirectory, game);
                            break;

                        case InstallTaskType.DownloadSaves:
                            await saveClient.DownloadAsync(planItem.InstallDirectory, game.Id);
                            break;

                        case InstallTaskType.RunInstallScript:
                            await scriptClient.Game_RunInstallScriptAsync(planItem.InstallDirectory, game.Id);
                            break;

                        case InstallTaskType.RunKeyChangeScript:
                            var allocatedKey = await GetOrAllocateKeyAsync(planItem.InstallDirectory, game.Id);
                            await scriptClient.Game_RunKeyChangeScriptAsync(planItem.InstallDirectory, game.Id, allocatedKey);
                            break;

                        case InstallTaskType.RunNameChangeScript:
                            var alias = await profileClient.GetAliasAsync();
                            await scriptClient.Game_RunNameChangeScriptAsync(planItem.InstallDirectory, game.Id, alias);
                            break;

                        case InstallTaskType.DownloadManual:
                            // Manual download handled by caller (InstallService) since it needs MediaClient
                            break;
                    }

                    taskProgress.TaskStatus = InstallTaskStatus.Completed;
                    taskProgress.Progress = 1.0f;
                    OnTaskProgress?.Invoke(taskProgress);
                }
                catch (InstallCanceledException)
                {
                    taskProgress.TaskStatus = InstallTaskStatus.Canceled;
                    OnTaskProgress?.Invoke(taskProgress);
                    throw;
                }
                catch (Exception ex) when (!taskDef.IsCritical)
                {
                    logger?.LogError(ex, "Non-critical task {TaskTitle} failed for {GameTitle} ({GameId})", taskDef.Title, game.Title, game.Id);
                    taskProgress.TaskStatus = InstallTaskStatus.Failed;
                    taskProgress.ErrorMessage = ex.Message;
                    OnTaskProgress?.Invoke(taskProgress);
                }
            }
        }

        private async Task ExecuteRedistributablePlanItemAsync(InstallPlanItem planItem, InstallResult installResult, CancellationToken cancellationToken)
        {
            // RedistributableClient.InstallAsync bundles download + install into one operation.
            // We fire task progress for both tasks but execute them as one call.
            var firstTask = planItem.Tasks.OrderBy(t => t.Order).FirstOrDefault();
            
            if (firstTask == null)
                return;

            // Get parent game context from task parameters
            var parentGameId = Guid.Empty;
            
            if (firstTask.Parameters.TryGetValue("ParentGameId", out var parentGameIdStr))
                Guid.TryParse(parentGameIdStr, out parentGameId);

            var taskProgress = new InstallTaskProgress
            {
                QueueItemId = planItem.PlanItemId,
                TaskId = firstTask.Id,
                TaskType = firstTask.Type,
                TaskTitle = firstTask.Title,
                TaskStatus = InstallTaskStatus.Running,
            };

            OnTaskProgress?.Invoke(taskProgress);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var game = parentGameId != Guid.Empty ? await GetAsync(parentGameId) : null;

                if (game != null)
                {
                    game.InstallDirectory = planItem.InstallDirectory;

                    var redist = game.Redistributables?.FirstOrDefault(r => r.Id == planItem.EntityId);

                    if (redist != null)
                        await redistributableClient.InstallAsync(redist, game);
                }

                // Mark all tasks as completed
                foreach (var taskDef in planItem.Tasks.OrderBy(t => t.Order))
                {
                    OnTaskProgress?.Invoke(new InstallTaskProgress
                    {
                        QueueItemId = planItem.PlanItemId,
                        TaskId = taskDef.Id,
                        TaskType = taskDef.Type,
                        TaskTitle = taskDef.Title,
                        TaskStatus = InstallTaskStatus.Completed,
                        Progress = 1.0f,
                    });
                }
            }
            catch (InstallCanceledException)
            {
                taskProgress.TaskStatus = InstallTaskStatus.Canceled;
                OnTaskProgress?.Invoke(taskProgress);
                throw;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Redistributable {RedistName} failed to install", planItem.Title);
                taskProgress.TaskStatus = InstallTaskStatus.Failed;
                taskProgress.ErrorMessage = ex.Message;
                OnTaskProgress?.Invoke(taskProgress);
            }
        }

        public async Task<InstallResult> UninstallAsync(string installDirectory, Guid gameId)
        {
            var installResult = new InstallResult(installDirectory, gameId);
            var gameFileList = installResult.FileList;

            var manifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Game>(installDirectory, gameId);
            if (manifest == null)
            {
                logger?.LogInformation("Unable to read or find manifest for game with ID {GameId}. Skip uninstallation!", gameId);
                return installResult;
            }

            // store manifest for current game (could be base game, or any dependent game as this point due to recursive call)
            gameFileList.BaseGame.Manifest = manifest;
            var baseFileList = gameFileList.BaseGame;

            #region Uninstall Addons
            if (manifest.Addons != null)
            {
                foreach (var addon in manifest.Addons)
                {
                    try
                    {
                        if (ManifestHelper.Exists(installDirectory, addon.Id))
                        {
                            var dependentResult = await UninstallAsync(installDirectory, addon.Id);
                            gameFileList.MergeDependentGames(dependentResult.FileList);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning("Could not uninstall dependent game with ID {GameId}. Assuming it's already uninstalled or never installed...", gameId);
                    }
                }
            }
            #endregion

            #region Delete Redistributable Files
            if (manifest.Redistributables != null)
            {
                foreach (var redistributable in manifest.Redistributables)
                {
                    try
                    {
                        await scriptClient.Redistributable_RunUninstallScriptAsync(installDirectory, gameId, redistributable.Id);

                        var redistFileListPath = GetMetadataFilePath(installDirectory, redistributable.Id, "FileList.txt");

                        if (File.Exists(redistFileListPath))
                        {
                            var redistFiles = await File.ReadAllLinesAsync(redistFileListPath);

                            foreach (var file in redistFiles.Where(f => !string.IsNullOrWhiteSpace(f)))
                            {
                                var localPath = Path.Combine(installDirectory, file);

                                try
                                {
                                    if (File.Exists(localPath))
                                        File.Delete(localPath);

                                    logger?.LogTrace("Deleted redistributable file {LocalPath}", localPath);
                                }
                                catch (Exception ex)
                                {
                                    logger?.LogWarning(ex, "Could not remove redistributable file {LocalPath}", localPath);
                                }
                            }
                        }

                        var redistMetadataPath = GetMetadataDirectoryPath(installDirectory, redistributable.Id);

                        if (Directory.Exists(redistMetadataPath))
                            Directory.Delete(redistMetadataPath, true);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "Could not clean up redistributable {RedistributableId}", redistributable.Id);
                    }
                }
            }
            #endregion

            #region Delete Tool Files
            if (manifest.Tools != null)
            {
                foreach (var tool in manifest.Tools)
                {
                    try
                    {
                        if (ManifestHelper.Exists(installDirectory, tool.Id))
                            await toolClient.UninstallAsync(installDirectory, tool.Id);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "Could not clean up tool {ToolId}", tool.Id);
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

                logger?.LogDebug("Attempting to delete the install files");

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

                        logger?.LogTrace("Deleted file {LocalPath}", localPath);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "Could not remove file {LocalPath}", localPath);
                    }
                }

                logger?.LogDebug("Attempting to delete any empty directories");

                DirectoryHelper.DeleteEmptyDirectories(installDirectory);

                if (!Directory.Exists(installDirectory))
                    logger?.LogDebug("Deleted install directory {InstallDirectory}", installDirectory);
                else
                    logger?.LogTrace("Removed game files for {GameTitle} ({GameId})", manifest.Title, gameId);
            }
            else
            {
                Directory.Delete(installDirectory, true);
            }
            #endregion

            await scriptClient.Game_RunUninstallScriptAsync(installDirectory, gameId);

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

            var baseManifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Game>(installDirectory, baseGameId);
            
            if (baseManifest == null)
            {
                logger?.LogInformation("Unable to read or find manifest for addon game with ID {GameId}. Skip uninstallation!", baseGameId);
                return installResult;
            }

            // store manifest for current addon game, skip any files
            gameFileList.BaseGame.Manifest = baseManifest;
            gameFileList.InstallDirectory = installDirectory;

            addonIds ??= [];
            
            foreach (var addon in baseManifest.Addons)
            {
                if (!addonIds.Contains(addon.Id))
                    continue;

                try
                {
                    var dependentResult = await UninstallAddonAsync(installDirectory, addon.Id);
                    gameFileList.MergeBaseAsDependentGame(addon.Id, dependentResult.FileList);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, $"Could not uninstall dependent game {addon} of base game {baseGameId}. Assuming it's already uninstalled or never installed...");
                }
            }

            return installResult;
        }

        public async Task<InstallResult> UninstallAddonAsync(string installDirectory, Guid addonGameId)
        {
            var installResult = new InstallResult(installDirectory, addonGameId);
            var gameFileList = installResult.FileList;

            var manifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Game>(installDirectory, addonGameId);

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
            // Defense in depth: a caller that mis-resolves the destination (e.g. re-deriving it
            // from an already-installed directory instead of a fresh parent-folder hint) could
            // otherwise ask to "move" a directory into itself/a subdirectory of itself. Since the
            // implementation below copies to newInstallDirectory and then recursively deletes
            // oldInstallDirectory, a destination equal to or nested under the source would delete
            // the copies it just made along with the source — total data loss. Reject that
            // outright rather than ever attempting it.
            if (IsSameOrNestedPath(oldInstallDirectory, newInstallDirectory))
                throw new InvalidOperationException(
                    $"Cannot move install directory '{oldInstallDirectory}' to '{newInstallDirectory}': the destination is the same as, or nested under, the source directory.");

            var gameAndAddons = new List<Game>();

            _installProgress.Game = game;
            _installProgress.Status = InstallStatus.EnumeratingFiles;
            _installProgress.Indeterminate = true;
            _installProgress.Progress = 0;
            OnInstallProgressUpdate?.Invoke(_installProgress);

            gameAndAddons.Add(game);

            foreach (var dependentGameId in game.DependentGames)
            {
                var dependentGame = await GetAsync(dependentGameId);

                if (dependentGame.IsAddon)
                    gameAndAddons.Add(dependentGame);
            }

            foreach (var entry in gameAndAddons)
            {
                if (await IsInstalled(oldInstallDirectory, game, entry.Id))
                    await saveClient.UploadAsync(oldInstallDirectory, entry.Id);
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
                    
                    await saveClient.DownloadAsync(newInstallDirectory, entry.Id);
                }
            }

            _installProgress.Status = InstallStatus.Complete;
            OnInstallProgressUpdate?.Invoke(_installProgress);

            return newInstallDirectory;
        }

        /// <summary>
        /// True when <paramref name="candidatePath"/> is the same directory as
        /// <paramref name="basePath"/>, or a subdirectory nested anywhere under it. Used to guard
        /// destructive directory operations (e.g. <see cref="MoveAsync(Game, string, string)"/>)
        /// that delete <paramref name="basePath"/> after copying into <paramref name="candidatePath"/>
        /// — if the candidate were nested under the base, that delete would also destroy the
        /// copies it just made.
        /// </summary>
        public static bool IsSameOrNestedPath(string basePath, string candidatePath)
        {
            var normalizedBase = Path.GetFullPath(basePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedCandidate = Path.GetFullPath(candidatePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (string.Equals(normalizedBase, normalizedCandidate, StringComparison.OrdinalIgnoreCase))
                return true;

            return normalizedCandidate.StartsWith(
                normalizedBase + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
        }

        public async Task<bool> IsInstalled(string installDirectory, Game game, Guid? addonId = null)
        {
            installDirectory = await GetInstallDirectory(game, installDirectory);

            var metadataPath = ManifestHelper.GetPath(installDirectory, addonId ?? game.Id);

            return File.Exists(metadataPath);
        }

        public async Task UpdateGameInstallationAsync(string installDirectory, Game game, Guid? archiveId = null)
        {
            // update game and scripts locally
            await WriteManifestAsync(installDirectory, game, archiveId);
            await WriteScriptsAsync(installDirectory, game);
        }

        /// <summary>
        /// Refreshes an installation's on-disk manifest and scripts for its own pinned
        /// <paramref name="archiveId"/>, but leaves the existing manifest exactly as it is on disk
        /// when — and only when — the server tells us that exact archive is no longer available
        /// (404/`ArchiveNotFoundForGameException` → 400). An administrator deleting an archive some
        /// installation is still pinned to must not break operations that never re-download it
        /// (add-on/tool changes), and it must certainly not cause that installation to be silently
        /// re-identified as the game's current effective default: the manifest on disk is the only
        /// remaining record of what is actually installed there, so preserving it verbatim is the
        /// only non-destructive answer. Every other failure (auth, transport, server error) still
        /// propagates so genuine problems are never mistaken for a deleted archive.
        /// </summary>
        /// <returns>
        /// True when the manifest was refreshed from the server; false when the pinned archive was
        /// unavailable and the existing on-disk manifest was preserved instead.
        /// </returns>
        public async Task<bool> TryUpdateGameInstallationAsync(string installDirectory, Game game, Guid? archiveId = null)
        {
            var refreshed = true;

            try
            {
                await WriteManifestAsync(installDirectory, game, archiveId);
            }
            catch (HttpRequestException ex) when (archiveId.HasValue && (
                ex.StatusCode == HttpStatusCode.NotFound || ex.StatusCode == HttpStatusCode.BadRequest))
            {
                logger?.LogWarning(ex,
                    "Archive {ArchiveId} is no longer available for game {GameTitle} ({GameId}); preserving the existing manifest in {InstallDirectory} instead of refreshing it",
                    archiveId, game?.Title, game?.Id, installDirectory);

                refreshed = false;
            }

            await WriteScriptsAsync(installDirectory, game);

            return refreshed;
        }

        /// <summary>
        /// Refreshes the on-disk manifest and scripts for an installed game by fetching the latest
        /// versions from the server and writing them to the game's install directory. When
        /// <paramref name="archiveId"/> is supplied (the installation's own pinned archive), the
        /// manifest is written for that exact archive rather than the server's effective default,
        /// so refreshing metadata for a pinned installation never silently reports a different
        /// version than what is actually on disk.
        /// </summary>
        public async Task RefreshManifestAndScriptsAsync(string installDirectory, Guid gameId, Guid? archiveId = null)
        {
            logger?.LogTrace("Refreshing manifest and scripts for game {GameId} in {InstallDirectory}", gameId, installDirectory);

            var manifest = await GetManifestAsync(gameId, archiveId);
            await ManifestHelper.WriteAsync(manifest, installDirectory);

            var scripts = await GetScriptsAsync(gameId);

            if (scripts != null && scripts.Any())
            {
                var game = new Game { Id = gameId };

                foreach (var script in scripts)
                    await ScriptHelper.SaveScriptAsync(game, script, installDirectory);
            }
        }

        private async Task<Models.Manifest.Game> WriteManifestAsync(string installDirectory, Game game, Guid? archiveId = null)
        {
            logger?.LogTrace($"Retrieving game manifest for game {game.Title} with id {game.Id}");
            
            var manifest = await GetManifestAsync(game.Id, archiveId);
            
            logger?.LogTrace($"Saving Manifest for game {game.Id} into {installDirectory}");
            
            await ManifestHelper.WriteAsync(manifest, installDirectory);
            
            return manifest;
        }

        private async Task WriteScriptsAsync(string installDirectory, Game game)
        {
            var scripts = await GetScriptsAsync(game.Id);

            if (scripts != null && scripts.Any())
            {
                logger?.LogTrace($"Saving scripts for game {game.Title} with id {game.Id} into {installDirectory}");
                
                foreach (var script in scripts)
                    await ScriptHelper.SaveScriptAsync(game, script, installDirectory);
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
                    var allocatedKey = await GetOrAllocateKeyAsync(game.InstallDirectory, game.Id);

                    await scriptClient.Game_RunInstallScriptAsync(game.InstallDirectory, game.Id);
                    await scriptClient.Game_RunKeyChangeScriptAsync(game.InstallDirectory, game.Id, allocatedKey);
                    await scriptClient.Game_RunNameChangeScriptAsync(game.InstallDirectory, game.Id, await profileClient.GetAliasAsync());
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Scripts failed to execute for game/addon {GameTitle} ({GameId})", game.Title, game.Id);
                }
            }
        }

        /// <summary>
        /// Reads the existing FileList.txt and checks which files are present on disk.
        /// Returns a set of entry paths (relative) that exist locally and can be skipped during extraction.
        /// </summary>
        private async Task<HashSet<string>> VerifyLocalFilesAsync(string installDirectory, Guid gameId, CancellationToken cancellationToken)
        {
            var verified = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var fileListPath = GetMetadataFilePath(installDirectory, gameId, "FileList.txt");

            if (!File.Exists(fileListPath))
                return verified;

            var lines = await File.ReadAllLinesAsync(fileListPath, cancellationToken);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // Format: "path/to/file | CRC32HEX"
                var separatorIndex = line.IndexOf('|');
                var entryPath = separatorIndex >= 0
                    ? line.Substring(0, separatorIndex).Trim()
                    : line.Trim();

                if (string.IsNullOrEmpty(entryPath) || entryPath.EndsWith("/"))
                    continue;

                var localPath = Path.Combine(installDirectory, entryPath);

                if (File.Exists(localPath))
                    verified.Add(entryPath);
            }

            return verified;
        }

        private async Task<ExtractionResult> DownloadAndExtractAsync(Game game, string destination, CancellationToken cancellationToken = default, HashSet<string> skipFiles = null, Guid? archiveId = null, InstallDestinationOwnership destinationOwnership = InstallDestinationOwnership.ExistingInstallation)
        {
            if (game == null)
            {
                logger?.LogTrace("Game failed to download, no game was specified");

                throw new ArgumentNullException("No game was specified");
            }

            logger?.LogTrace("Downloading and extracting {Game} to path {Destination}", game.Title, destination);

            // Decide, *before* anything is written, whether failure cleanup is allowed to delete
            // this destination. Cleanup is recursive, so getting this wrong destroys an existing
            // installation (or, for an overlay add-on, the base game's shared directory) on a
            // cancel, a dropped connection, or a corrupt archive. Both conditions must hold: the
            // caller has to declare the destination is a fresh install's own, and the directory
            // must not already contain anything this extraction did not put there. A fresh
            // destination may legitimately be pre-created empty, which is why the declared intent
            // is threaded through rather than inferred solely from Directory.Exists.
            var ownsDestination = destinationOwnership == InstallDestinationOwnership.Fresh
                && !DirectoryHasContent(destination);

            if (!ownsDestination)
                logger?.LogTrace("Destination {Destination} is not owned by this install ({Ownership}); it will never be recursively deleted during cleanup", destination, destinationOwnership);

            var extractionResult = new ExtractionResult
            {
                Canceled = false,
            };

            // Both branches below go through the game-scoped, policy-gated download endpoint. Only
            // the unpinned "latest" case pre-checks availability with a HEAD: a pinned archive is
            // requested by exact id, so a failure there is a real error worth surfacing rather
            // than something to quietly downgrade into a cancellation.
            if (!archiveId.HasValue && !await CanStreamLatestArchiveAsync(game.Id))
            {
                extractionResult.Success = false;
                extractionResult.Canceled = true;

                return extractionResult;
            }

            var fileManifest = new StringBuilder();
            var files = new List<ExtractionResult.FileEntry>();

            // Tracked outside the try so the catch blocks can report exactly where extraction failed
            TrackableStream stream = null;
            string currentEntryKey = null;
            var entriesProcessed = 0;

            try
            {
                Directory.CreateDirectory(destination);

                stream = archiveId.HasValue
                    ? await StreamArchiveAsync(game.Id, archiveId.Value)
                    : await StreamLatestArchiveAsync(game.Id);

                var monitor = new FileTransferMonitor(stream.Length);
                var progress = new Progress<ProgressReport>(report =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _reader?.Cancel();

                        _installProgress.Status = InstallStatus.Canceled;

                        OnInstallProgressUpdate?.Invoke(_installProgress);

                        return;
                    }

                    if (monitor.CanUpdate())
                    {
                        monitor.Update(stream.Position);

                        _installProgress.BytesTransferred = monitor.GetBytesTransferred();
                        _installProgress.TotalBytes = stream.Length;
                        _installProgress.TransferSpeed = monitor.GetSpeed();
                        _installProgress.TimeRemaining = monitor.GetTimeRemaining();

                        OnInstallProgressUpdate?.Invoke(_installProgress);
                    }

                    OnArchiveEntryExtractionProgress?.Invoke(this, new ArchiveEntryExtractionProgressArgs
                    {
                        Progress = report,
                        Game = game,
                    });
                });

                _reader = await ReaderFactory.OpenAsyncReader(stream, new ReaderOptions { Progress = progress }, cancellationToken);

                _installProgress.Status = InstallStatus.Downloading;
                OnInstallProgressUpdate?.Invoke(_installProgress);

                while (await _reader.MoveToNextEntryAsync(cancellationToken))
                {
                    if (_reader.Cancelled)
                    {
                        // Bailing out of the entry loop mid-archive leaves a partial extraction
                        // behind, so it has to be reported as a cancellation. Falling through as a
                        // success would write a FileList.txt describing a half-extracted install
                        // and let the caller persist it as a completed installation.
                        logger?.LogTrace("The reader was cancelled after {EntriesProcessed} entries for game {GameTitle} ({GameId})", entriesProcessed, game.Title, game.Id);

                        extractionResult.Canceled = true;

                        break;
                    }

                    try
                    {
                        var entryKey = _reader.Entry.Key;
                        currentEntryKey = entryKey;
                        var localFile = Path.Combine(destination, entryKey);

                        fileManifest.AppendLine($"{entryKey} | {_reader.Entry.Crc.ToString("X")}");
                        files.Add(new ExtractionResult.FileEntry
                        {
                            EntryPath = entryKey,
                            LocalPath = localFile,
                        });

                        // If pre-flight verification confirmed this file exists locally, skip it
                        bool shouldSkip = skipFiles != null && skipFiles.Contains(entryKey);

                        if (!shouldSkip)
                            await _reader.WriteEntryToDirectoryAsync(destination, new ExtractionOptions()
                            {
                                ExtractFullPath = true,
                                Overwrite = true,
                                PreserveFileTime = true
                            }, cancellationToken);
                        else // Skip to next entry
                            try
                            {
                                await using var es = await _reader.OpenEntryStreamAsync(cancellationToken);
                            }
                            catch
                            {
                                logger?.LogError("Could not skip to next entry in archive: {EntryKey}", entryKey);
                            }

                        entriesProcessed++;
                    }
                    catch (IOException ex)
                    {
                        var errorCode = ex.HResult & 0xFFFF;

                        if (errorCode == 87)
                        {
                            logger?.LogError(ex, "Fatal IO error (HResult 0x{HResult:X8}, Win32 {ErrorCode}) writing entry {EntryKey} for game {GameTitle} ({GameId}) after {EntriesProcessed} entries at {Position}/{Length} bytes",
                                ex.HResult, errorCode, currentEntryKey, game.Title, game.Id, entriesProcessed, stream?.Position, stream?.Length);

                            throw ex;
                        }

                        logger?.LogTrace("Not replacing existing file/folder on disk: {EntryKey} (HResult 0x{HResult:X8}) - {Message}", currentEntryKey, ex.HResult, ex.Message);

                        // Skip to next entry
                        await using var es = await _reader.OpenEntryStreamAsync(cancellationToken);
                    }
                }

                await _reader.DisposeAsync();
                await stream.DisposeAsync();
                // _transferStream.Dispose();
            }
            catch (ReaderCancelledException ex)
            {
                logger?.LogTrace(ex, "User cancelled the download");

                extractionResult.Canceled = true;
            }
            catch (HttpRequestException ex)
            {
                // The download itself was refused (unauthorized, archive not found / not this
                // game's, ...). Surface that as-is rather than mislabeling it as a corrupt archive.
                logger?.LogError(ex, "Could not download archive {ArchiveId} for game {GameTitle} ({GameId}) to {Destination}: {StatusCode}",
                    archiveId, game.Title, game.Id, destination, ex.StatusCode);

                CleanUpFailedExtraction(destination, ownsDestination, game, "failed download");

                throw;
            }
            catch (OperationCanceledException ex)
            {
                // A cancellation observed through the CancellationToken rather than through the
                // reader's own Cancel(). It must stay classified as a cancellation: rethrowing it
                // as a generic extraction failure would make RetryHelper retry a download the user
                // just canceled, and would surface "is the archive corrupted?" to the caller
                // instead of a cancellation.
                logger?.LogTrace(ex, "The download was canceled for game {GameTitle} ({GameId})", game.Title, game.Id);

                extractionResult.Canceled = true;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Could not extract game {GameTitle} ({GameId}) to {Destination}. Failed on entry {EntryKey} (entry #{EntriesProcessed}) at {Position}/{Length} bytes with {ExceptionType} (HResult 0x{HResult:X8})",
                    game.Title, game.Id, destination, currentEntryKey, entriesProcessed, stream?.Position, stream?.Length, ex.GetType().Name, ex.HResult);

                CleanUpFailedExtraction(destination, ownsDestination, game, "bad install");

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

                logger?.LogTrace("Game {Game} successfully downloaded and extracted to {Destination}", game.Title, destination);
            }
            else
            {
                // Covers every way this extraction can be canceled — the reader throwing, the
                // cancellation token being observed, and the reader reporting cancellation from
                // inside the entry loop — all of which leave a partial extraction behind.
                CleanUpFailedExtraction(destination, ownsDestination, game, "canceled install");
            }

            return extractionResult;
        }

        /// <summary>
        /// True when <paramref name="directory"/> exists and already contains anything at all.
        /// Deliberately treats an unreadable/erroring directory as "has content" so a failure to
        /// inspect it can never be what authorizes a recursive delete.
        /// </summary>
        private bool DirectoryHasContent(string directory)
        {
            try
            {
                return Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any();
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Could not inspect {Directory} to determine install destination ownership; treating it as pre-existing", directory);

                return true;
            }
        }

        /// <summary>
        /// Removes the partial results of a canceled/failed extraction, but only from a destination
        /// this extraction actually owns (see <see cref="InstallDestinationOwnership"/>). A
        /// destination that belongs to an existing installation — an in-place version change, a
        /// legacy exact-directory update, or an overlay add-on sharing its base game's folder — is
        /// left completely untouched: a canceled download or a corrupt archive must never be able
        /// to delete a working installation. Partial files may be left behind in that case, which
        /// the next install/repair overwrites.
        /// </summary>
        private void CleanUpFailedExtraction(string destination, bool ownsDestination, Game game, string reason)
        {
            if (!ownsDestination)
            {
                logger?.LogWarning("Not cleaning up {Destination} after {Reason} for game {GameTitle} ({GameId}): the directory belongs to an existing installation and may only be overwritten, never deleted",
                    destination, reason, game?.Title, game?.Id);

                return;
            }

            try
            {
                if (Directory.Exists(destination))
                {
                    logger?.LogTrace("Cleaning up orphaned files in {Destination} after {Reason}", destination, reason);

                    Directory.Delete(destination, true);
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Could not clean up {Destination} after {Reason}", destination, reason);
            }
        }

        /// <summary>
        /// True when this game type installs as an overlay into its base game's existing
        /// directory (Expansion/Mod/StandaloneMod with a real <see cref="Game.BaseGameId"/>)
        /// rather than getting its own independent install directory. This is the single source
        /// of truth for that sharing rule — mirrored exactly by <see cref="GetInstallDirectory"/>
        /// below — so callers deciding whether to divert a destination to a collision-safe
        /// sibling path (see <c>InstallService.Add</c>) can never disagree with how the
        /// destination is actually resolved.
        /// </summary>
        public static bool IsOverlayInstallType(Game game) =>
            game != null
            && game.BaseGameId != Guid.Empty
            && (game.Type == GameType.Expansion || game.Type == GameType.Mod || game.Type == GameType.StandaloneMod);

        public async Task<string> GetInstallDirectory(Game game, string installDirectory)
        {
            if (string.IsNullOrWhiteSpace(installDirectory))
                installDirectory = settingsProvider.CurrentValue.Games.InstallDirectories.First();

            if (IsOverlayInstallType(game))
            {
                // modify installation passes the original installation of the game including the game folder, use the existing folder,
                // otherwise a name change could lead to installing files into differnt folder
                if (Path.Exists(installDirectory) && Path.Exists(Path.Combine(installDirectory, ".lancommander")))
                {
                    return installDirectory;
                }
                else
                {
                    var baseGame = await GetAsync(game.BaseGameId);

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

        public async Task<ICollection<SDK.Models.Manifest.Game>> ReadManifestsAsync(string installDirectory, Guid gameId)
        {
            var manifests = new List<SDK.Models.Manifest.Game>();
            var mainManifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Game>(installDirectory, gameId);

            if (mainManifest == null)
                return manifests;

            manifests.Add(mainManifest);

            if (mainManifest.Addons != null)
            {
                foreach (var addon in mainManifest.Addons)
                {
                    try
                    {
                        var dependentGameManifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Game>(installDirectory, addon.Id);

                        if (dependentGameManifest.Type == GameType.Expansion || dependentGameManifest.Type == GameType.Mod)
                            manifests.Add(dependentGameManifest);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Could not load manifest from dependent game {AddonId}", addon.Id);
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
        protected async Task<IEnumerable<ArchiveEntry>> GetGameInstallationArchiveEntries(Guid gameId, Models.Manifest.Game manifest)
        {
            var entries = await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Archives/Contents/{manifest.Id}/{manifest.Version}")
                .GetAsync<IEnumerable<ArchiveEntry>>();

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

                var savePathEntries = baseManifest.SavePaths?.SelectMany(p => saveClient.GetFileSavePathEntries(p, installDirectory)).ToList() ?? [];
                gameArchives.BaseGame.SavePaths = savePathEntries;
            }

            // Processes dependent game manifests and their corresponding archive entries.
            foreach (var depManifest in manifests ?? [])
            {
                var depEntries = await GetGameInstallationArchiveEntries(gameId, depManifest);

                if (!gameArchives.Addons.TryGetValue(depManifest.Id, out var depArchiveInfo))
                {
                    depArchiveInfo = new();
                    gameArchives.Addons.Add(depManifest.Id, depArchiveInfo);
                }

                depArchiveInfo.Manifest = depManifest;
                depArchiveInfo.Entries.AddRange(depEntries);

                var savePathEntries = depManifest.SavePaths?.SelectMany(p => saveClient.GetFileSavePathEntries(p, installDirectory)).ToList() ?? [];
                
                depArchiveInfo.SavePaths = savePathEntries;
            }

            return gameArchives;
        }

        public async Task RunAsync(string installDirectory, Guid gameId, Models.Manifest.Action action, DateTime? lastRun, string args = "")
        {
            var screen = DisplayHelper.GetScreen();

            using (var context = processExecutionContextFactory.Create())
            {
                context.AddVariable("ServerAddress", connectionClient.GetServerAddress().ToString());
                
                try
                {
                    context.AddVariable("DisplayWidth", screen.Width.ToString());
                    context.AddVariable("DisplayHeight", screen.Height.ToString());
                    context.AddVariable("DisplayRefreshRate", screen.RefreshRate.ToString());
                    context.AddVariable("DisplayBitDepth", screen.BitsPerPixel.ToString());
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Could not get display information for execution context variables");
                }

                try
                {
                    if (connectionClient.IsConnected() && !String.IsNullOrWhiteSpace(settingsProvider.CurrentValue.IPXRelay.Host))
                    {
                        context.AddVariable("IPXRelayHost", settingsProvider.CurrentValue.IPXRelay.Host);
                        context.AddVariable("IPXRelayPort", settingsProvider.CurrentValue.IPXRelay.Port.ToString());
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Could not connect to IPXRelay host");
                }

                if (action.Variables != null)
                {
                    foreach (var variable in action.Variables)
                        context.AddVariable(variable.Key, variable.Value);
                }

                // When an action references {ServerHost} but the game server didn't specify a host,
                // fall back to the host of the LANCommander server the launcher is connected to.
                if (action.Variables == null
                    || !action.Variables.TryGetValue("ServerHost", out var serverHost)
                    || String.IsNullOrWhiteSpace(serverHost))
                {
                    var serverAddress = connectionClient.GetServerAddress();

                    if (serverAddress != null)
                        context.AddVariable("ServerHost", serverAddress.Host);
                }

                #region Run Scripts
                var manifests = await ReadManifestsAsync(installDirectory, gameId);

                foreach (var manifest in manifests)
                {
                    //manifest.Actions
                    var currentGamePlayerAlias = await GetPlayerAliasAsync(installDirectory, manifest.Id);
                    var currentGameKey = await GetCurrentKeyAsync(installDirectory, manifest.Id);

                    #region Check Game's Player Name
                    if (connectionClient.IsConnected())
                    {
                        var alias = await profileClient.GetAliasAsync();

                        if (currentGamePlayerAlias != alias)
                        {
                            await scriptClient.Game_RunNameChangeScriptAsync(installDirectory, gameId, alias);

                            if (manifest.Redistributables != null)
                            {
                                foreach (var redistributable in manifest.Redistributables.Where(r => r.Scripts != null))
                                {
                                    await scriptClient.Redistributable_RunNameChangeScriptAsync(installDirectory, gameId, redistributable.Id, alias);
                                }
                            }
                        }
                    }
                    #endregion

                    #region Check Key Allocation
                    if (connectionClient.IsConnected())
                    {
                        // The locally tracked key is authoritative: only allocate and apply a
                        // key when this install doesn't already have one tracked. This avoids
                        // requesting a fresh allocation on every launch.
                        if (string.IsNullOrWhiteSpace(currentGameKey))
                        {
                            var newKey = await GetOrAllocateKeyAsync(installDirectory, manifest.Id);

                            if (!string.IsNullOrWhiteSpace(newKey))
                                await scriptClient.Game_RunKeyChangeScriptAsync(installDirectory, manifest.Id, newKey);
                        }
                    }
                    #endregion

                    #region Download Latest Saves
                    if (connectionClient.IsConnected())
                    {
                        await RetryHelper.RetryOnExceptionAsync(10, TimeSpan.FromSeconds(1), false, async () =>
                        {
                            logger?.LogTrace("Checking for latest save for game {GameId}", manifest.Id);

                            try
                            {
                                var latestSave = await saveClient.GetLatestAsync(manifest.Id);

                                if (latestSave == null)
                                {
                                    logger?.LogDebug("No saves found on server for game {GameId}", manifest.Id);
                                }
                                else if (lastRun == null)
                                {
                                    logger?.LogInformation("Downloading save for game {GameId} (first run, save date: {SaveDate})", manifest.Id, latestSave.CreatedOn);
                                    await saveClient.DownloadAsync(installDirectory, manifest.Id);
                                }
                                else if (latestSave.CreatedOn > lastRun)
                                {
                                    logger?.LogInformation("Downloading newer save for game {GameId} (save date: {SaveDate}, last run: {LastRun})", manifest.Id, latestSave.CreatedOn, lastRun);
                                    await saveClient.DownloadAsync(installDirectory, manifest.Id);
                                }
                                else
                                {
                                    logger?.LogDebug("Save for game {GameId} is up to date (save date: {SaveDate}, last run: {LastRun})", manifest.Id, latestSave.CreatedOn, lastRun);
                                }
                            }
                            catch (HttpRequestException ex)
                            {
                                if (ex.StatusCode == HttpStatusCode.NotFound)
                                {
                                    logger?.LogDebug("No saves found on server for game {GameId} (404)", manifest.Id);
                                    return true;
                                }

                                throw;
                            }

                            return true;
                        });
                    }
                    else
                    {
                        logger?.LogDebug("Skipping save download for game {GameId}, not connected to server", manifest.Id);
                    }
                    #endregion

                    #region Run Before Start Script
                    await scriptClient.Game_RunBeforeStartScriptAsync(installDirectory, manifest.Id);
                    
                    if (manifest.Redistributables != null)
                    {
                        foreach (var redistributable in manifest.Redistributables.Where(r => r.Scripts != null))
                        {
                            await scriptClient.Redistributable_RunBeforeStartScriptAsync(installDirectory, gameId, redistributable.Id);
                        }
                    }
                    #endregion
                }
                #endregion

                Task heartbeatTask = null;

                try
                {
                    var cancellationTokenSource = new CancellationTokenSource();
                    _running[gameId] = cancellationTokenSource;

                    heartbeatTask = SendKeepAlivesAsync(gameId, cancellationTokenSource.Token);

                    #region Run Wrapper Scripts
                    bool runWrapperHandled = false;

                    var gameManifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Game>(installDirectory, gameId);
                    var resolvedAction = action ?? gameManifest.Actions.FirstOrDefault(a => a.IsPrimaryAction);

                    if (resolvedAction != null && gameManifest.Redistributables != null)
                    {
                        var wrapperRedistributables = gameManifest.Redistributables
                            .Where(r => r.Scripts != null && r.Scripts.Any(s => s.Type == Enums.ScriptType.RunWrapper))
                            .ToList();

                        if (wrapperRedistributables.Any())
                        {
                            if (gameManifest.CustomFields != null && gameManifest.CustomFields.Any())
                            {
                                foreach (var customField in gameManifest.CustomFields)
                                {
                                    context.AddVariable(customField.Name, customField.Value);
                                }
                            }

                            var executablePath = context.ExpandVariables(resolvedAction.Path, installDirectory);
                            var arguments = context.ExpandVariables(resolvedAction.Arguments, installDirectory, skipSlashes: true);
                            var workingDirectory = context.ExpandVariables(resolvedAction.WorkingDirectory, installDirectory);

                            if (string.IsNullOrWhiteSpace(workingDirectory))
                                workingDirectory = installDirectory;

                            if (!string.IsNullOrWhiteSpace(args))
                                arguments = string.IsNullOrWhiteSpace(arguments) ? args : arguments + " " + args;

                            foreach (var redistributable in wrapperRedistributables)
                            {
                                runWrapperHandled = await scriptClient.Redistributable_RunRunWrapperScriptAsync(installDirectory, gameId, redistributable.Id, executablePath, arguments, workingDirectory, cancellationTokenSource.Token);

                                if (runWrapperHandled)
                                    break;
                            }
                        }
                    }
                    #endregion

                    if (!runWrapperHandled)
                        await context.ExecuteGameActionAsync(installDirectory, gameId, action, args, cancellationTokenSource.Token);

                    _running.Remove(gameId);
                    
                    await StopHeartbeatAsync(cancellationTokenSource, heartbeatTask);
                    
                    cancellationTokenSource.Dispose();

                    await UploadSavesAsync(manifests, installDirectory);
                }
                catch (Exception ex)
                {
                    if (_running.TryGetValue(gameId, out var cts))
                    {
                        _running.Remove(gameId);
                        await StopHeartbeatAsync(cts, heartbeatTask);
                        cts.Dispose();
                    }
                    logger?.LogError(ex, "Game failed to run");
                    throw;
                }

                foreach (var manifest in manifests)
                {
                    #region Run After Stop Script
                    await scriptClient.Game_RunAfterStopScriptAsync(installDirectory, gameId);
                    
                    if (manifest.Redistributables != null)
                    {
                        foreach (var redistributable in manifest.Redistributables.Where(r => r.Scripts != null))
                        {
                            await scriptClient.Redistributable_RunAfterStopScriptAsync(installDirectory, gameId, redistributable.Id);
                        }
                    }
                    #endregion
                }
            }
        }

        private async Task UploadSavesAsync(ICollection<SDK.Models.Manifest.Game> manifests, string installDirectory)
        {
            if (connectionClient.IsConnected())
            {
                foreach (var manifest in manifests)
                {
                    await RetryHelper.RetryOnExceptionAsync(10, TimeSpan.FromSeconds(1), false, async () =>
                    {
                        logger?.LogDebug("Uploading save for game {GameId}", manifest.Id);

                        try
                        {
                            await saveClient.UploadAsync(installDirectory, manifest.Id);
                        }
                        catch (Exception ex)
                        {
                            logger?.LogError(ex, "Save upload attempt failed for game {GameId}", manifest.Id);
                            throw;
                        }

                        logger?.LogInformation("Save uploaded successfully for game {GameId}", manifest.Id);

                        return true;
                    });
                }
            }
            else
            {
                logger?.LogDebug("Skipping save upload, not connected to server");
            }
        }

        // Heartbeat interval while a game is running. Must stay well below the server's
        // KeepAliveTimeout so a session isn't reaped between beats.
        private const int KeepAliveIntervalSeconds = 30;

        private async Task SendKeepAlivesAsync(Guid gameId, CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(KeepAliveIntervalSeconds), token);

                    if (token.IsCancellationRequested)
                        break;

                    if (!connectionClient.IsConnected() || RpcClient.Hub == null)
                        continue;

                    try
                    {
                        await RpcClient.Hub.GameKeepAliveAsync(gameId);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogTrace(ex, "Keepalive send failed for {GameId}", gameId);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when the game exits and the token is cancelled.
            }
        }

        private static async Task StopHeartbeatAsync(CancellationTokenSource cancellationTokenSource, Task heartbeatTask)
        {
            cancellationTokenSource.Cancel();

            if (heartbeatTask != null)
            {
                try
                {
                    await heartbeatTask;
                }
                catch (OperationCanceledException)
                {
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
                var objectKey = await apiRequestFactory
                    .Create()
                    .UseAuthenticationToken()
                    .UseVersioning()
                    .UploadInChunksAsync(settingsProvider.CurrentValue.Archives.UploadChunkSize, fs);

                if (objectKey != Guid.Empty)
                    await apiRequestFactory
                        .Create()
                        .UseAuthenticationToken()
                        .UseVersioning()
                        .UseRoute($"/api/Games/Import/{objectKey}")
                        .PostAsync();
            }
        }

        [Obsolete("Servers no longer do \"Full\" exports")]
        public async Task ExportAsync(string destinationPath, Guid gameId)
        {
            await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Games/Export/Full")
                .DownloadAsync(destinationPath);
        }

        public async Task UploadArchiveAsync(string archivePath, Guid gameId, string version, string changelog = "")
        {
            using (var fs = new FileStream(archivePath, FileMode.Open, FileAccess.Read))
            {
                var objectKey = await apiRequestFactory
                    .Create()
                    .UseAuthenticationToken()
                    .UseVersioning()
                    .UploadInChunksAsync(settingsProvider.CurrentValue.Archives.UploadChunkSize, fs);

                if (objectKey != Guid.Empty)
                    await apiRequestFactory
                        .Create()
                        .UseAuthenticationToken()
                        .UseVersioning()
                        .UseRoute("/api/Games/UploadArchive")
                        .AddBody(new UploadArchiveRequest
                        {
                            Id = gameId,
                            ObjectKey = objectKey,
                            Version = version,
                            Changelog = changelog
                        })
                        .PostAsync();
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
            var entries = archives?.BaseGame?.Entries?.ToList() ?? [];

            foreach ((var dependentGameId, var dependentGameInfo) in archives?.Addons ?? [])
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
            var lookupEntry = archives?.Addons?
                .SelectMany(dep => dep.Value?.Entries?.Select(entry => new { GameId = (Guid?)dep.Key, ArchiveEntry = entry }) ?? [])
                .ToLookup(tentry => tentry.ArchiveEntry, tentry => tentry.GameId) ?? Enumerable.Empty<Guid?>().ToLookup(x => default(ArchiveEntry));

            var conflictedEntries = new List<ArchiveValidationConflict>();

            var savePathEntries = archives?.BaseGame?.SavePaths.ToList() ?? [];
            var depSavePathEntries = archives?.Addons?.SelectMany(dep => dep.Value?.SavePaths ?? []).ToList() ?? [];
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
        /// Downloads the specified files for multiple games (base game, mods, expansions) from
        /// each game's effective default archive. Back-compat overload — prefer the
        /// archive-aware overload so files belonging to an installation pinned to a
        /// non-default archive are repaired from that exact archive.
        /// </summary>
        /// <param name="installDirectory">The directory where the games are installed.</param>
        /// <param name="entries">
        /// A collection of tuples containing the game ID and the corresponding file path.
        /// </param>
        public Task DownloadFilesAsync(string installDirectory, IEnumerable<(Guid GameId, string FilePath)> entries)
        {
            return DownloadFilesAsync(
                installDirectory,
                entries.Select(x => (x.GameId, (Guid?)null, x.FilePath)));
        }

        /// <summary>
        /// Downloads the specified files for multiple games (base game, mods, expansions), each
        /// from the exact archive supplied alongside it. A null ArchiveId means "archive identity
        /// is not known for this game here" and falls back to its effective default archive.
        /// </summary>
        /// <param name="installDirectory">The directory where the games are installed.</param>
        /// <param name="entries">
        /// A collection of tuples containing the game ID, the archive to pull the file from (or
        /// null for the game's effective default), and the corresponding file path.
        /// </param>
        public async Task DownloadFilesAsync(string installDirectory, IEnumerable<(Guid GameId, Guid? ArchiveId, string FilePath)> entries, CancellationToken cancellationToken = default)
        {
            var groups = entries.GroupBy(x => (x.GameId, x.ArchiveId));

            foreach (var group in groups)
            {
                await DownloadFilesAsync(
                    installDirectory,
                    group.Key.GameId,
                    group.Select(x => x.FilePath).ToList(),
                    group.Key.ArchiveId,
                    cancellationToken);
            }
        }

        /// <summary>
        /// Downloads the specified files for a single game from its effective default archive.
        /// Back-compat overload — prefer the archive-aware overload for anything repairing an
        /// installation that may be pinned to a non-default archive.
        /// </summary>
        /// <param name="installDirectory">The directory where the game is installed.</param>
        /// <param name="gameId">The unique identifier of the game.</param>
        /// <param name="entries">A collection of file paths to download.</param>
        public Task DownloadFilesAsync(string installDirectory, Guid gameId, ICollection<string> entries, CancellationToken cancellationToken = default)
        {
            return DownloadFilesAsync(installDirectory, gameId, entries, archiveId: null, cancellationToken);
        }

        /// <summary>
        /// Downloads the specified files for a single game out of one exact archive.
        /// </summary>
        /// <param name="installDirectory">The directory where the game is installed.</param>
        /// <param name="gameId">The unique identifier of the game.</param>
        /// <param name="entries">A collection of file paths to download.</param>
        /// <param name="archiveId">
        /// The exact archive to extract the files from — normally the installation's own pinned
        /// archive. Pulling from the game's effective default instead (what a null here falls back
        /// to) would repair a non-default installation with files from a completely different
        /// version, silently corrupting it. Routed through the game-scoped, policy-gated download
        /// endpoint (see <see cref="StreamArchiveAsync"/>), exactly like a pinned install.
        /// </param>
        public async Task DownloadFilesAsync(string installDirectory, Guid gameId, ICollection<string> entries, Guid? archiveId, CancellationToken cancellationToken = default)
        {
            var manifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Game>(installDirectory, gameId);

            try
            {
                var stream = archiveId.HasValue
                    ? await StreamArchiveAsync(gameId, archiveId.Value)
                    : await StreamLatestArchiveAsync(gameId);
                _reader = await ReaderFactory.OpenAsyncReader(stream, new ReaderOptions(), cancellationToken);

                while (await _reader.MoveToNextEntryAsync(cancellationToken))
                {
                    if (_reader.Cancelled)
                        break;

                    try
                    {
                        if (entries.Contains(_reader.Entry.Key))
                        {
                            await _reader.WriteEntryToDirectoryAsync(installDirectory, new ExtractionOptions
                            {
                                ExtractFullPath = true,
                                Overwrite = true,
                                PreserveFileTime = true,
                            }, cancellationToken);
                        }
                        else // Skip to next entry
                            try
                            {
                                await using var es = await _reader.OpenEntryStreamAsync(cancellationToken);
                            }
                            catch (Exception ex)
                            {
                                logger?.LogError(ex, "Could not skip to the next entry in the archive: {EntryKey}", _reader.Entry.Key);
                            }
                    }
                    catch (IOException ex)
                    {
                        var errorCode = ex.HResult & 0xFFFF;

                        if (errorCode == 87)
                            throw;
                        else
                            logger?.LogTrace("Not replacing existing file/folder on disk: {EntryKey} - {Message}", _reader.Entry.Key, ex.Message);

                        // Skip to next entry
                        await using var es = await _reader.OpenEntryStreamAsync(cancellationToken);
                    }
                }

                await _reader.DisposeAsync();
                await stream.DisposeAsync();
            }
            catch (Exception ex)
            {
                throw new Exception("The game archive could not be extracted, is it corrupted? Please try again");
            }
        }

        /// <summary>
        /// Restores base-game/add-on files that an add-on install/uninstall removed or overwrote,
        /// pulling each game's replacements from its effective default archive. Back-compat
        /// overload — prefer the archive-aware overload.
        /// </summary>
        public Task RestoreFilesAsync(string installDirectory, Guid gameId, GameInstallationFileList fileListRemoved, GameInstallationFileList fileListAdded)
        {
            return RestoreFilesAsync(installDirectory, gameId, fileListRemoved, fileListAdded, archiveId: null);
        }

        /// <summary>
        /// Restores base-game/add-on files that an add-on install/uninstall removed or overwrote.
        /// </summary>
        /// <param name="archiveId">
        /// The installation's own pinned archive, used for the base game's own files. See the
        /// <see cref="RestoreFilesAsync(string, Guid, IEnumerable{string}, Guid?)"/> overload.
        /// </param>
        public Task RestoreFilesAsync(string installDirectory, Guid gameId, GameInstallationFileList fileListRemoved, GameInstallationFileList fileListAdded, Guid? archiveId)
        {
            var listRemoved = fileListRemoved?.ToFlatDistinctFileEntries() ?? [];
            var listAdded = fileListAdded?.ToFlatDistinctFileEntries() ?? [];

            var uniqueList = listRemoved.ExceptBy(listAdded.Select(x => x.EntryPath), x => x.EntryPath, StringComparer.OrdinalIgnoreCase);
            var possibleRestoreEntries = uniqueList.Select(x => x.EntryPath).ToArray();
            
            return RestoreFilesAsync(installDirectory, gameId, possibleRestoreEntries, archiveId);
        }

        /// <summary>
        /// Restores invalidated files matching the specified files, pulling the base game's own
        /// files from its effective default archive. Back-compat overload — prefer the
        /// archive-aware overload.
        /// </summary>
        /// <param name="installDirectory">The directory where the game is installed.</param>
        /// <param name="gameId">The unique identifier of the game.</param>
        /// <param name="entries">A collection of file paths to check and compare with invalidated files.</param>
        public Task RestoreFilesAsync(string installDirectory, Guid gameId, IEnumerable<string> entries)
        {
            return RestoreFilesAsync(installDirectory, gameId, entries, archiveId: null);
        }

        /// <summary>
        /// Restores invalidated files matching the specified files.
        /// </summary>
        /// <param name="installDirectory">The directory where the game is installed.</param>
        /// <param name="gameId">The unique identifier of the game.</param>
        /// <param name="entries">A collection of file paths to check and compare with invalidated files.</param>
        /// <param name="archiveId">
        /// The archive <paramref name="gameId"/> is actually installed from — i.e. the
        /// installation's own pinned archive. Only conflicts belonging to the base game itself can
        /// use it: repairing them from the game's *effective default* archive instead would
        /// overwrite a pinned, non-default installation with files from a different version.
        /// Conflicts owned by another game (an add-on overlaying the same directory) keep the
        /// previous effective-default behavior, since their own archive identity is not knowable
        /// from here.
        /// </param>
        public async Task RestoreFilesAsync(string installDirectory, Guid gameId, IEnumerable<string> entries, Guid? archiveId)
        {
            // early out if no files were removed which would require checking
            if (entries == null || !entries.Any())
                return;

            // validate files, which takes addons into account
            var conflicts = await ValidateFilesAsync(installDirectory, gameId) ?? [];
            
            // build list of files to download by matching up removed files with conflicting files, split by game/addon
            var downloadEntries = conflicts
                .IntersectBy(entries, x => x.FullName, StringComparer.OrdinalIgnoreCase)
                .Select(x =>
                {
                    var conflictGameId = x.GameId ?? gameId;

                    return (
                        GameId: conflictGameId,
                        ArchiveId: conflictGameId == gameId ? archiveId : null,
                        FilePath: x.FullName);
                })
                .ToArray();

            await DownloadFilesAsync(installDirectory, downloadEntries);
        }

        /// <summary>
        /// Whether every archive listing a post-removal file restore of this installation will need
        /// is still retrievable from the server.
        ///
        /// <see cref="RestoreFilesAsync(string, Guid, IEnumerable{string}, Guid?)"/> puts back files
        /// an add-on removal deleted or overwrote, and it can only do so by listing (and then
        /// downloading from) the exact archives named by the on-disk manifests. It does that through
        /// <see cref="ValidateFilesAsync"/>, which queries the archive contents of the base game
        /// *and of every add-on manifest still on disk* — so a single missing listing anywhere in
        /// that set makes the restore throw. Once an administrator deletes one of those archives
        /// there is no safe source for the affected files at all — the game's current default
        /// archive is a different version and would silently corrupt the installation — so callers
        /// that are about to mutate the installation on disk must check this *first* and refuse,
        /// rather than deleting files they then cannot restore.
        ///
        /// <paramref name="removingAddonIds"/> lists add-ons this operation is about to uninstall.
        /// Their manifests are deleted by the uninstall before the restore runs, so the restore
        /// never queries their archives — a deleted archive belonging to an add-on that is on its
        /// way out must not block the very removal that gets rid of it.
        ///
        /// Returns true when nothing is installed to restore (no manifest on disk), since restoring
        /// is a no-op in that case. Only the server's two "that exact archive is gone" answers
        /// (404, and 400 for <c>ArchiveNotFoundForGameException</c>) count as unavailable; every
        /// other failure — auth, transport, server error — propagates, so a transient outage is
        /// never mistaken for a deleted archive.
        /// </summary>
        public async Task<bool> CanRestoreInstallationFilesAsync(string installDirectory, Guid gameId, IEnumerable<Guid> removingAddonIds = null)
        {
            // Deliberately the same enumeration ValidateFilesAsync/GetGameInstallationArchivesEntries
            // use, so the probed set is exactly the set the restore will query — no more, no less.
            var manifests = await GetManifestsAsync(installDirectory, gameId);

            if (manifests == null || manifests.Count == 0)
                return true;

            var removing = new HashSet<Guid>(removingAddonIds ?? Enumerable.Empty<Guid>());

            foreach (var manifest in manifests)
            {
                if (manifest == null)
                    continue;

                if (manifest.Id != gameId && removing.Contains(manifest.Id))
                    continue;

                if (string.IsNullOrWhiteSpace(manifest.Version))
                {
                    logger?.LogWarning("Installed manifest {ManifestId} in {InstallDirectory} has no version recorded, so its archive contents cannot be identified for a file restore", manifest.Id, installDirectory);

                    return false;
                }

                try
                {
                    await GetGameInstallationArchiveEntries(gameId, manifest);
                }
                catch (HttpRequestException ex) when (
                    ex.StatusCode == HttpStatusCode.NotFound || ex.StatusCode == HttpStatusCode.BadRequest)
                {
                    logger?.LogWarning(ex, "Archive contents for version {Version} of game {ManifestId} are no longer available ({StatusCode}); files of installation {InstallDirectory} could not be restored from it", manifest.Version, manifest.Id, ex.StatusCode, installDirectory);

                    return false;
                }
            }

            return true;
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
            
            return string.Empty;
        }

        public static async Task<string> GetPlayerAliasAsync(string installDirectory, Guid gameId)
        {
            var aliasFilePath = GetMetadataFilePath(installDirectory, gameId, PlayerAliasFilename);

            if (File.Exists(aliasFilePath))
                return await File.ReadAllTextAsync(aliasFilePath);
            
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
            
            return string.Empty;
        }

        public static async Task<string> GetCurrentKeyAsync(string installDirectory, Guid gameId)
        {
            var keyFilePath = GetMetadataFilePath(installDirectory, gameId, KeyFilename);

            if (File.Exists(keyFilePath))
                return await File.ReadAllTextAsync(keyFilePath);
            
            return string.Empty;
        }

        public static void UpdateCurrentKey(string installDirectory, Guid gameId, string newKey)
        {
            Directory.CreateDirectory(GetMetadataDirectoryPath(installDirectory, gameId));
            File.WriteAllText(GetMetadataFilePath(installDirectory, gameId, KeyFilename), newKey);
        }

        public static async Task UpdateCurrentKeyAsync(string installDirectory, Guid gameId, string newKey)
        {
            Directory.CreateDirectory(GetMetadataDirectoryPath(installDirectory, gameId));
            await File.WriteAllTextAsync(GetMetadataFilePath(installDirectory, gameId, KeyFilename), newKey);
        }
    }
}
