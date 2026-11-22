using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Octokit;
using Semver;
using System.Collections.Concurrent;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Exceptions;
using LANCommander.Server.Settings.Enums;
using LANCommander.SDK;
using LANCommander.SDK.Updating;

namespace LANCommander.Server.Services
{
    public sealed class UpdateService(
        ILogger<UpdateService> logger,
        SettingsProvider<Settings.Settings> settingsProvider,
        IHostApplicationLifetime applicationLifetime,
        IVersionProvider versionProvider,
        IGitHubService gitHubService,
        ServerManager serverManager) : BaseService(logger, settingsProvider)
    {
        public const string ArtifactUrlBase = "/download/Launcher/";

        private static readonly Regex LauncherReleasePattern = new(
            @"^LANCommander\.Launcher-(?<Platform>[^-]+)-(?<Architecture>[^-]+)-v(?<Version>.+)\.(?<Extension>zip|AppImage)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly TimeSpan LauncherArtifactCacheLifetime = TimeSpan.FromMinutes(5);
        private static readonly SemaphoreSlim LauncherArtifactCacheLock = new(1, 1);
        private static (DateTime CachedOn, List<LauncherArtifact> Artifacts)? _launcherArtifactCache;

        private static readonly ConcurrentDictionary<(string Path, long Length, DateTime LastWriteTimeUtc), string> FileHashes = new();

        public async Task<IEnumerable<LauncherArtifact>> GetLauncherArtifactsAsync()
        {
            List<LauncherArtifact> launchers = [];

            if (_settingsProvider.CurrentValue.Server.Launcher.HostUpdates)
                launchers.AddRange(GetLauncherArtifactsFromLocalFiles());

            if (launchers.Count == 0 || _settingsProvider.CurrentValue.Server.Launcher.IncludeOnlineUpdates)
            {
                var githubLaunchers = await GetLauncherArtifactsFromGitHubAsync().ToListAsync();
                launchers.AddRange(githubLaunchers);
            }

            return launchers;
        }

        public IEnumerable<LauncherArtifact> GetLauncherArtifactsFromLocalFiles()
        {
            var currentVersion = versionProvider.GetCurrentVersion();
            var launcherStoragePath = AppPaths.ResolveStorageLocationPath(_settingsProvider.CurrentValue.Server.Launcher.StoragePath);
            var downloadedLaunchers = Directory.GetFiles(launcherStoragePath, $"LANCommander.Launcher*v{currentVersion.WithoutMetadata()}.*");
            var downloadedInstallers = Directory.GetFiles(launcherStoragePath, $"LANCommander.Launcher-{currentVersion.WithoutMetadata()}*Setup*.*");

            var downloads = downloadedLaunchers.Concat(downloadedInstallers).Distinct();

            foreach (var downloadedLauncher in downloads)
                yield return GetArtifactFromName(downloadedLauncher);
        }

        public async IAsyncEnumerable<LauncherArtifact> GetLauncherArtifactsFromGitHubAsync()
        {
            var currentVersion = versionProvider.GetCurrentVersion();
            
            if (!String.IsNullOrWhiteSpace(_settingsProvider.CurrentValue.Server.Launcher.VersionOverride))
                currentVersion = SemVersion.Parse(_settingsProvider.CurrentValue.Server.Launcher.VersionOverride, SemVersionStyles.AllowV);

            var releaseChannel = versionProvider.GetReleaseChannel(currentVersion);
            
            if (releaseChannel != _settingsProvider.CurrentValue.Server.Update.ReleaseChannel)
                releaseChannel = _settingsProvider.CurrentValue.Server.Update.ReleaseChannel;
            
            var tag = $"v{currentVersion.WithoutMetadata()}";

            if (releaseChannel == ReleaseChannel.Nightly)
                tag = "nightly";
            
            if (!String.IsNullOrWhiteSpace(_settingsProvider.CurrentValue.Server.Launcher.VersionOverride))
                tag = _settingsProvider.CurrentValue.Server.Launcher.VersionOverride;
            
            logger.LogInformation($"Searching for artifacts for v{currentVersion.WithoutMetadata()}");

            var release = await gitHubService.GetReleaseAsync(tag);

            // Development and unpublished builds have no matching release
            if (release == null)
            {
                logger.LogWarning("No release found with the tag {Tag}", tag);
                yield break;
            }

            var assets = release.Assets.Where(a => a.Name.Contains("LANCommander.Launcher")).ToList();
            
            if (assets.Any())
                logger.LogInformation($"Found the following assets:\n{String.Join("\n\t - ", assets.Select(a => a.Name))}");
            else
                logger.LogError($"No assets found for v{currentVersion.WithoutMetadata()}!");
            
            foreach (var asset in assets)
                yield return GetArtifactFromName(asset.Name, asset.BrowserDownloadUrl, isOnline: true, isPrerelease: release.Prerelease, isNightly: tag == "nightly");
        }

        private LauncherArtifact GetArtifactFromName(string name, string url = "", bool isOnline = false, bool isPrerelease = false, bool isNightly = false)
        {
            var platform = LauncherPlatform.Windows;
            var architecture = LauncherArchitecture.x64;
            var assetName = Path.GetFileName(name);

            if (name.Contains("Windows"))
                platform = LauncherPlatform.Windows;
            else if (name.Contains("Linux"))
                platform = LauncherPlatform.Linux;
            else if (name.Contains("macOS"))
                platform = LauncherPlatform.macOS;
            
            if (name.Contains("x64"))
                architecture = LauncherArchitecture.x64;
            else if (name.Contains("arm64"))
                architecture = LauncherArchitecture.arm64;

            if (String.IsNullOrWhiteSpace(url))
                url = $"{ArtifactUrlBase}{assetName}";

            return new LauncherArtifact
            {
                Name = name,
                Platform = platform,
                Architecture = architecture,
                Url = url,
                IsOnline = isOnline,
                IsPrerelease = isPrerelease,
                IsNightly = isNightly,
            };
        }

        /// <summary>
        /// Finds the newest launcher release for a platform, architecture and package kind among the
        /// launchers this server offers, preferring a locally hosted copy over GitHub's.
        /// </summary>
        public async Task<LauncherUpdate?> FindLauncherUpdateAsync(string platform, string architecture, UpdatePackageKind packageKind)
        {
            var artifacts = await GetCachedLauncherArtifactsAsync();

            var match = artifacts
                .Select(a => (Artifact: a, Release: ParseLauncherRelease(Path.GetFileName(a.Name))))
                .Where(m => m.Release is { } release
                    && release.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase)
                    && release.Architecture.Equals(architecture, StringComparison.OrdinalIgnoreCase)
                    && release.PackageKind == packageKind)
                .OrderByDescending(m => m.Release!.Version, SemVersion.PrecedenceComparer)
                .ThenBy(m => m.Artifact.IsOnline)
                .FirstOrDefault();

            if (match.Artifact == null)
                return null;

            var localPath = match.Artifact.IsOnline ? null : GetLauncherFileLocation(Path.GetFileName(match.Artifact.Name));

            if (localPath != null && !File.Exists(localPath))
                return null;

            return new LauncherUpdate(match.Artifact, match.Release!.Version, Path.GetFileName(match.Artifact.Name), localPath);
        }

        /// <summary>Lowercase hex SHA-256 of a hosted launcher file, cached until the file changes.</summary>
        public async Task<string> GetFileHashAsync(string path)
        {
            var file = new FileInfo(path);
            var key = (file.FullName, file.Length, file.LastWriteTimeUtc);

            if (FileHashes.TryGetValue(key, out var hash))
                return hash;

            hash = await UpdatePackage.ComputeSha256Async(file.FullName);

            FileHashes[key] = hash;

            return hash;
        }

        public static LauncherRelease? ParseLauncherRelease(string fileName)
        {
            var match = LauncherReleasePattern.Match(fileName);

            if (!match.Success || !SemVersion.TryParse(match.Groups["Version"].Value, SemVersionStyles.Any, out var version))
                return null;

            var packageKind = match.Groups["Extension"].Value.Equals("AppImage", StringComparison.OrdinalIgnoreCase)
                ? UpdatePackageKind.AppImage
                : UpdatePackageKind.Archive;

            return new LauncherRelease(match.Groups["Platform"].Value, match.Groups["Architecture"].Value, version, packageKind);
        }

        // Every launcher checks for updates when it starts; without a cache each check would query GitHub
        private async Task<List<LauncherArtifact>> GetCachedLauncherArtifactsAsync()
        {
            await LauncherArtifactCacheLock.WaitAsync();

            try
            {
                if (_launcherArtifactCache is { } cache && DateTime.UtcNow - cache.CachedOn < LauncherArtifactCacheLifetime)
                    return cache.Artifacts;

                var artifacts = (await GetLauncherArtifactsAsync()).ToList();

                _launcherArtifactCache = (DateTime.UtcNow, artifacts);

                return artifacts;
            }
            finally
            {
                LauncherArtifactCacheLock.Release();
            }
        }

        public async Task<bool> UpdateAvailableAsync()
        {
            var latestVersion = await gitHubService.GetLatestVersionAsync(_settingsProvider.CurrentValue.Server.Update.ReleaseChannel);

            var sortOrder = versionProvider.GetCurrentVersion().ComparePrecedenceTo(latestVersion);

            return sortOrder < 0;
        }

        /// <summary>
        /// Downloads and stages a server release, then stops the server. Once it has stopped, the
        /// staged files are swapped in and the updated server started; see <see cref="ServerUpdateHandover"/>.
        /// </summary>
        public async Task DownloadServerReleaseAsync(Release release)
        {
            var installation = ServerUpdateHandover.DetectInstallation()
                ?? throw new NotImplementedException("This server can't update itself. Update its container image or package instead");

            string releaseFile = release.Assets.FirstOrDefault(a => a.Name.StartsWith($"LANCommander.Server-{GetOS()}-{GetArchitecture()}-"))?.BrowserDownloadUrl ?? String.Empty;

            if (String.IsNullOrWhiteSpace(releaseFile))
                throw new NotImplementedException("Your platform is not supported");

            _logger?.LogInformation("Stopping all servers");

            await serverManager.StopAllAsync();

            _logger?.LogInformation("Servers stopped");
            _logger?.LogInformation("Downloading release version {Version}", release.TagName);

            var journalDirectory = GetUpdateJournalDirectory();
            var packagePath = Path.Combine(journalDirectory, $"{release.TagName}.zip");

            Directory.CreateDirectory(journalDirectory);

            using (var client = new WebClient())
                await client.DownloadFileTaskAsync(new Uri(releaseFile), packagePath);

            _logger?.LogInformation("Update version {Version} has been downloaded", release.TagName);

            var journal = SelfUpdater.Begin(journalDirectory, release.TagName.TrimStart('v'), installation, _logger);

            journal.PreviousVersion = versionProvider.GetCurrentVersion().ToString();

            UpdatePackage.Stage(journal, packagePath, UpdatePackageKind.Archive);
            journal.Save();

            File.Delete(packagePath);

            _logger?.LogInformation("Update version {Version} is staged, stopping to apply it", release.TagName);

            ServerUpdateHandover.Request(journal.Directory);

            applicationLifetime.StopApplication();
        }

        /// <summary>Where the server keeps its update journal and staged files.</summary>
        public string GetUpdateJournalDirectory() =>
            AppPaths.ResolveStorageLocationPath(_settingsProvider.CurrentValue.Server.Update.StoragePath);

        public async Task DownloadLatestLauncherReleaseAsync()
        {
            var currentVersion = versionProvider.GetCurrentVersion();
            var currentRelease = await gitHubService.GetReleaseAsync(currentVersion);

            if (currentRelease != null)
                await DownloadLauncherReleaseAsync(currentRelease);
            else
                throw new ReleaseNotFoundException(currentVersion);
        }

        public async Task DownloadLauncherReleaseAsync(Release release)
        {
            var releaseFiles = release.Assets.Where(a => a.Name.StartsWith("LANCommander.Launcher-")).Select(a => a.BrowserDownloadUrl);

            _logger?.LogInformation("Downloading release version {Version}", release.TagName);

            foreach (var releaseFile in releaseFiles)
            {
                _logger?.LogInformation($"Downloading launcher from {releaseFile}");

                if (!String.IsNullOrWhiteSpace(releaseFile))
                {
                    WebClient client = new WebClient();

                    var uri = new Uri(releaseFile);

                    client.QueryString.Add("Version", release.TagName);
                    await client.DownloadFileTaskAsync(uri, AppPaths.ResolveStorageLocationPath(_settingsProvider.CurrentValue.Server.Update.StoragePath, $"{release.TagName}.zip"));
                }
            }
        }

        private string GetOS()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "Windows";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return "Linux";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "macOS";

            return String.Empty;
        }

        private string GetArchitecture()
        {
            switch (RuntimeInformation.ProcessArchitecture)
            {
                case Architecture.X64:
                    return "x64";

                case Architecture.Arm64:
                    return "arm64";

                default:
                    return String.Empty;
            }
        }

        public string GetLauncherFileLocation(LauncherArtifact artifact) =>
            GetLauncherFileLocation(artifact.Name);

        public string GetLauncherFileLocation(string objectKey) =>
            AppPaths.ResolveStorageLocationPath(_settingsProvider.CurrentValue.Server.Launcher.StoragePath, objectKey);

        public LauncherArtifact GetLauncherArtifact(string objectKey)
        {
            string name = AppPaths.ResolveStorageLocationPath(_settingsProvider.CurrentValue.Server.Launcher.StoragePath, objectKey);
            return GetArtifactFromName(name);
        }
    }

    /// <summary>The platform, architecture, version and package kind in a launcher release's file name.</summary>
    public sealed record LauncherRelease(string Platform, string Architecture, SemVersion Version, UpdatePackageKind PackageKind);

    /// <summary>A launcher release a launcher can update to. <see cref="LocalPath"/> is set when the server hosts the file.</summary>
    public sealed record LauncherUpdate(LauncherArtifact Artifact, SemVersion Version, string FileName, string? LocalPath);

    public class LauncherArtifact
    {
        public required string Name { get; set; }
        public required string Url { get; set; }
        public LauncherPlatform Platform { get; set; }
        public LauncherArchitecture Architecture { get; set; }
        public bool IsOnline { get; set; }
        public bool IsPrerelease { get; set; }
        public bool IsNightly { get; set; }
    }
}
