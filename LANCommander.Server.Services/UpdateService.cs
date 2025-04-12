using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Octokit;
using Semver;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using AutoMapper;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Enums;
using LANCommander.Server.Services.Exceptions;
using LANCommander.Server.Services.Models;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class UpdateService(
        ILogger<UpdateService> logger,
        IHostApplicationLifetime applicationLifetime,
        IServiceProvider serviceProvider,
        IVersionProvider versionProvider,
        IGitHubService gitHubService,
        ServerService serverService) : BaseService(logger)
    {
        public const string ArtifactUrlBase = "/download/Launcher/";

        public async Task<IEnumerable<LauncherArtifact>> GetLauncherArtifactsAsync()
        {
            if (!_settings.Launcher.HostUpdates)
                return await GetLauncherArtifactsFromGitHubAsync().ToListAsync();

            return GetLauncherArtifactsFromLocalFiles();
        }

        public IEnumerable<LauncherArtifact> GetLauncherArtifactsFromLocalFiles()
        {
            var currentVersion = versionProvider.GetCurrentVersion();
            var downloadedLaunchers = Directory.GetFiles(_settings.Launcher.StoragePath, $"LANCommander.Launcher*v{currentVersion.WithoutMetadata()}.*");
            var downloadedInstallers = Directory.GetFiles(_settings.Launcher.StoragePath, $"LANCommander.Launcher-{currentVersion.WithoutMetadata()}*Setup*.*");

            var downloads = downloadedLaunchers.Concat(downloadedInstallers).Distinct();

            foreach (var downloadedLauncher in downloads)
                yield return GetArtifactFromName(downloadedLauncher);
        }

        public async IAsyncEnumerable<LauncherArtifact> GetLauncherArtifactsFromGitHubAsync()
        {
            var currentVersion = versionProvider.GetCurrentVersion();
            
            if (!String.IsNullOrWhiteSpace(_settings.Launcher.VersionOverride))
                currentVersion = SemVersion.Parse(_settings.Launcher.VersionOverride, SemVersionStyles.AllowV);

            var releaseChannel = versionProvider.GetReleaseChannel(currentVersion);
            
            if (releaseChannel == ReleaseChannel.Stable || releaseChannel == ReleaseChannel.Prerelease)
            {
                logger.LogInformation($"Searching for artifacts for v{currentVersion.WithoutMetadata()}");

                var release = await gitHubService.GetReleaseAsync(currentVersion);
                
                var assets = release.Assets.Where(a => a.Name.Contains("LANCommander.Launcher")).ToList();
                
                if (assets.Any())
                    logger.LogInformation($"Found the following assets:\n{String.Join("\n\t - ", assets.Select(a => a.Name))}");
                else
                    logger.LogError($"No assets found for v{currentVersion.WithoutMetadata()}!");
                
                foreach (var asset in assets)
                    yield return GetArtifactFromName(asset.Name, asset.BrowserDownloadUrl);
            }

            if (releaseChannel == ReleaseChannel.Nightly)
            {
                var nightlyArtifacts = await gitHubService.GetNightlyArtifactsAsync(_settings.Launcher.VersionOverride);
                
                foreach (var artifact in nightlyArtifacts.Where(a => a.Name.Contains("LANCommander.Launcher")))
                    yield return GetArtifactFromName(artifact.Name, artifact.ArchiveDownloadUrl);
            }
        }

        private LauncherArtifact GetArtifactFromName(string name, string url = "")
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
            };
        }

        public async Task<bool> UpdateAvailableAsync()
        {
            var latestVersion = await gitHubService.GetLatestVersionAsync(_settings.Update.ReleaseChannel);

            var sortOrder = versionProvider.GetCurrentVersion().ComparePrecedenceTo(latestVersion);

            return sortOrder < 0;
        }

        public async Task DownloadServerReleaseAsync(Release release)
        {
            string releaseFile = release.Assets.FirstOrDefault(a => a.Name.StartsWith($"LANCommander.Server-{GetOS()}-{GetArchitecture()}-"))?.BrowserDownloadUrl ?? String.Empty;

            if (String.IsNullOrWhiteSpace(releaseFile))
                throw new NotImplementedException("Your platform is not supported");

            _logger?.LogInformation("Stopping all servers");

            var servers = await serverService.GetAsync();

            foreach (var engine in serviceProvider.GetServices<IServerEngine>())
            {
                foreach (var server in servers)
                {
                    if (engine.IsManaging(server.Id))
                        await engine.StopAsync(server.Id);
                }
            }

            _logger?.LogInformation("Servers stopped");
            _logger?.LogInformation("Downloading release version {Version}", release.TagName);

            if (!String.IsNullOrWhiteSpace(releaseFile))
            {
                WebClient client = new WebClient();

                client.DownloadFileCompleted += ReleaseDownloaded;
                client.QueryString.Add("Version", release.TagName);

                await client.DownloadFileTaskAsync(new Uri(releaseFile), Path.Combine(_settings.Update.StoragePath, $"{release.TagName}.zip"));
            }
        }
        
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
                    await client.DownloadFileTaskAsync(uri, Path.Combine(_settings.Update.StoragePath, $"{release.TagName}.zip"));
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

        private void ReleaseDownloaded(object? sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            string version = ((WebClient)sender).QueryString["Version"];
            string path = Path.Combine(_settings.Update.StoragePath, $"{version}.zip");

            _logger?.LogInformation("Update version {Version} has been downloaded", version);

            _logger?.LogInformation("New autoupdater is being extracted");

            string processExecutable = String.Empty;

            if (File.Exists("LANCommander.AutoUpdater.exe"))
                processExecutable = "LANCommander.AutoUpdater.exe";
            else if (File.Exists("LANCommander.AutoUpdater"))
                processExecutable = "LANCommander.AutoUpdater";

            using (ZipArchive archive = ZipFile.OpenRead(path))
            {
                foreach (ZipArchiveEntry entry in archive.Entries.Where(e => e.FullName == processExecutable))
                {
                    entry.ExtractToFile(processExecutable);
                }
            }

            _logger?.LogInformation("Running auto updater application");

            var process = new ProcessStartInfo();

            process.FileName = processExecutable;
            process.Arguments = $"-Version {version} -Path \"{_settings.Update.StoragePath}\" -Executable {Process.GetCurrentProcess().MainModule.FileName}";
            process.UseShellExecute = true;

            Process.Start(process);

            _logger?.LogInformation("Shutting down to get out of the way");

            applicationLifetime.StopApplication();
        }

        public string GetLauncherFileLocation(LauncherArtifact artifact)
        {
            return GetLauncherFileLocation(artifact.Name);
        }

        public string GetLauncherFileLocation(string objectKey)
        {
            return Path.Combine(_settings.Launcher.StoragePath, objectKey);
        }

        public LauncherArtifact GetLauncherArtifact(string objectKey)
        {
            string name = Path.Combine(_settings.Launcher.StoragePath, objectKey);
            return GetArtifactFromName(name);
        }
    }

    public class LauncherArtifact
    {
        public required string Name { get; set; }
        public required string Url { get; set; }
        public LauncherPlatform Platform { get; set; }
        public LauncherArchitecture Architecture { get; set; }
    }
}
