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
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public sealed class UpdateService(
        ILogger<UpdateService> logger,
        IHostApplicationLifetime applicationLifetime,
        ServerService serverService,
        ServerProcessService serverProcessService) : BaseService(logger)
    {
        private GitHubClient _gitHub;

        public override void Initialize()
        {
            _gitHub = new GitHubClient(new ProductHeaderValue("LANCommander"));
        }

        public static SemVersion GetCurrentVersion()
        {
            return SemVersion.FromVersion(Assembly.GetExecutingAssembly().GetName().Version);
        }

        public async Task<SemVersion> GetLatestVersionAsync()
        {
            var release = await _gitHub.Repository.Release.GetLatest("LANCommander", "LANCommander");

            SemVersion version = null;

            SemVersion.TryParse(release.TagName.TrimStart('v'), SemVersionStyles.Strict, out version);

            return version;
        }

        public async Task<bool> UpdateAvailableAsync()
        {
            var latestVersion = await GetLatestVersionAsync();

            var sortOrder = GetCurrentVersion().ComparePrecedenceTo(latestVersion);

            return sortOrder < 0;
        }

        public async Task<IEnumerable<Release>> GetReleasesAsync(int count)
        {
            return await _gitHub.Repository.Release.GetAll("LANCommander", "LANCommander", new ApiOptions
            {
                PageSize = count,
                PageCount = 1,
            });
        }

        public async Task<Release?> GetReleaseAsync(SemVersion version)
        {
            var releases = await GetReleasesAsync(10);

            return releases.FirstOrDefault(r => r.TagName == $"v{version}");
        }

        public async Task DownloadServerReleaseAsync(Release release)
        {
            string releaseFile = release.Assets.FirstOrDefault(a => a.Name.StartsWith($"LANCommander.Server-{GetOS()}-{GetArchitecture()}-"))?.BrowserDownloadUrl ?? String.Empty;

            if (String.IsNullOrWhiteSpace(releaseFile))
                throw new NotImplementedException("Your platform is not supported");

            _logger?.LogInformation("Stopping all servers");

            var servers = await serverService.GetAsync();

            foreach (var server in servers)
            {
                if (serverProcessService.GetStatus(server) == ServerProcessStatus.Running)
                    serverProcessService.StopServerAsync(server.Id);
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
    }
}
