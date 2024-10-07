using LANCommander.Server.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Octokit;
using Semver;
using System;
using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;

namespace LANCommander.Server.Services
{
    public class UpdateService : BaseService
    {
        private GitHubClient GitHub;
        private IHostApplicationLifetime ApplicationLifetime;
        private ServerService ServerService;
        private ServerProcessService ServerProcessService;

        public UpdateService(
            ILogger<UpdateService> logger,
            IHostApplicationLifetime applicationLifetime,
            ServerProcessService serverProcessService,
            ServerService serverService) : base(logger)
        {
            GitHub = new GitHubClient(new ProductHeaderValue("LANCommander"));
            ApplicationLifetime = applicationLifetime;
            ServerService = serverService;
            ServerProcessService = serverProcessService;
        }

        public static SemVersion GetCurrentVersion()
        {
            return SemVersion.FromVersion(Assembly.GetExecutingAssembly().GetName().Version);
        }

        public async Task<SemVersion> GetLatestVersion()
        {
            var release = await GitHub.Repository.Release.GetLatest("LANCommander", "LANCommander");

            SemVersion version = null;

            SemVersion.TryParse(release.TagName.TrimStart('v'), SemVersionStyles.Strict, out version);

            return version;
        }

        public async Task<bool> UpdateAvailable()
        {
            var latestVersion = await GetLatestVersion();

            var sortOrder = GetCurrentVersion().ComparePrecedenceTo(latestVersion);

            return sortOrder < 0;
        }

        public async Task<IEnumerable<Release>> GetReleases(int count)
        {
            return await GitHub.Repository.Release.GetAll("LANCommander", "LANCommander", new ApiOptions
            {
                PageSize = count,
                PageCount = 1,
            });
        }

        public async Task<Release?> GetRelease(SemVersion version)
        {
            var releases = await GetReleases(10);

            return releases.FirstOrDefault(r => r.TagName == $"v{version}");
        }

        public async Task DownloadServerRelease(Release release)
        {
            string releaseFile = release.Assets.FirstOrDefault(a => a.Name.StartsWith($"LANCommander.Server-{GetOS()}-{GetArchitecture()}-"))?.BrowserDownloadUrl ?? String.Empty;

            if (String.IsNullOrWhiteSpace(releaseFile))
                throw new NotImplementedException("Your platform is not supported");

            Logger?.LogInformation("Stopping all servers");

            var servers = await ServerService.Get();

            foreach (var server in servers)
            {
                if (ServerProcessService.GetStatus(server) == ServerProcessStatus.Running)
                    ServerProcessService.StopServer(server.Id);
            }

            Logger?.LogInformation("Servers stopped");
            Logger?.LogInformation("Downloading release version {Version}", release.TagName);

            if (!String.IsNullOrWhiteSpace(releaseFile))
            {
                WebClient client = new WebClient();

                client.DownloadFileCompleted += ReleaseDownloaded;
                client.QueryString.Add("Version", release.TagName);

                await client.DownloadFileTaskAsync(new Uri(releaseFile), Path.Combine(Settings.Update.StoragePath, $"{release.TagName}.zip"));
            }
        }

        public async Task DownloadLauncherRelease(Release release)
        {
            var releaseFiles = release.Assets.Where(a => a.Name.StartsWith("LANCommander.Launcher-")).Select(a => a.BrowserDownloadUrl);

            Logger?.LogInformation("Downloading release version {Version}", release.TagName);

            foreach (var releaseFile in releaseFiles)
            {
                Logger?.LogInformation($"Downloading launcher from {releaseFile}");

                if (!String.IsNullOrWhiteSpace(releaseFile))
                {
                    WebClient client = new WebClient();

                    var uri = new Uri(releaseFile);

                    client.QueryString.Add("Version", release.TagName);
                    await client.DownloadFileTaskAsync(uri, Path.Combine(Settings.Update.StoragePath, $"{release.TagName}.zip"));
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
            string path = Path.Combine(Settings.Update.StoragePath, $"{version}.zip");

            Logger?.LogInformation("Update version {Version} has been downloaded", version);

            Logger?.LogInformation("New autoupdater is being extracted");

            string processExecutable = String.Empty;

            if (File.Exists("LANCommander.AutoUpdater.exe"))
                processExecutable = "LANCommander.AutoUpdater.exe";
            else if (File.Exists("LANCommander.AutoUpdater"))
                processExecutable = "LANCommander.AutoUpdater";

            using (ZipArchive archive = ZipFile.OpenRead(path))
            {
                foreach (ZipArchiveEntry entry in archive.Entries.Where(e => e.FullName == processExecutable))
                {
                    entry.ExtractToFile(Path.Combine(processExecutable, entry.FullName));
                }
            }

            Logger?.LogInformation("Running auto updater application");

            var process = new ProcessStartInfo();

            process.FileName = processExecutable;
            process.Arguments = $"-Version {version} -Path \"{Settings.Update.StoragePath}\" -Executable {Process.GetCurrentProcess().MainModule.FileName}";
            process.UseShellExecute = true;

            Process.Start(process);

            Logger?.LogInformation("Shutting down to get out of the way");

            ApplicationLifetime.StopApplication();
        }
    }
}
