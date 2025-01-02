using Octokit;
using Semver;
using System.Diagnostics;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;

namespace LANCommander.Server.Services
{
    public class UpdateService(
        ILogger<UpdateService> logger,
        IHostApplicationLifetime applicationLifetime,
        ServerProcessService serverProcessService,
        ServerService serverService,
        IGitHubClient githubClient,
        IPath path) : BaseService(logger)
    {
        public static SemVersion GetCurrentVersion()
        {
            return SemVersion.FromVersion(Assembly.GetExecutingAssembly().GetName().Version);
        }

        public async Task<SemVersion> GetLatestVersion()
        {
            var release = await githubClient.Repository.Release.GetLatest("LANCommander", "LANCommander");

            SemVersion.TryParse(release.TagName.TrimStart('v'), SemVersionStyles.Strict, out SemVersion? version);

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
            return await githubClient.Repository.Release.GetAll("LANCommander", "LANCommander", new ApiOptions
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

            if (string.IsNullOrWhiteSpace(releaseFile))
                throw new NotImplementedException("Your platform is not supported");

            Logger?.LogInformation("Stopping all servers");

            var servers = await serverService.Get();

            foreach (var server in servers)
            {
                if (serverProcessService.GetStatus(server) == ServerProcessStatus.Running)
                    serverProcessService.StopServer(server.Id);
            }

            Logger?.LogInformation("Servers stopped");
            Logger?.LogInformation("Downloading release version {Version}", release.TagName);

            if (!string.IsNullOrWhiteSpace(releaseFile))
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

                if (!string.IsNullOrWhiteSpace(releaseFile))
                {
                    WebClient client = new WebClient();

                    var uri = new Uri(releaseFile);

                    client.QueryString.Add("Version", release.TagName);
                    await client.DownloadFileTaskAsync(uri, Path.Combine(Settings.Update.StoragePath, $"{release.TagName}.zip"));
                }
            }
        }

        private static string GetOS()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "Windows";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return "Linux";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "macOS";

            return string.Empty;
        }

        private static string GetArchitecture()
        {
            return RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "x64",
                Architecture.Arm64 => "arm64",
                _ => string.Empty,
            };
        }

        private void ReleaseDownloaded(object? sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            string version = ((WebClient)sender).QueryString["Version"];
            string zipPath = path.Combine(Settings.Update.StoragePath, $"{version}.zip");

            Logger?.LogInformation("Update version {Version} has been downloaded", version);

            Logger?.LogInformation("New autoupdater is being extracted");

            string processExecutable = string.Empty;

            if (File.Exists("LANCommander.AutoUpdater.exe"))
                processExecutable = "LANCommander.AutoUpdater.exe";
            else if (File.Exists("LANCommander.AutoUpdater"))
                processExecutable = "LANCommander.AutoUpdater";

            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries.Where(e => e.FullName == processExecutable))
                {
                    entry.ExtractToFile(path.Combine(processExecutable, entry.FullName));
                }
            }

            Logger?.LogInformation("Running auto updater application");

            var process = new ProcessStartInfo();

            process.FileName = processExecutable;
            process.Arguments = $"-Version {version} -Path \"{Settings.Update.StoragePath}\" -Executable {Process.GetCurrentProcess().MainModule.FileName}";
            process.UseShellExecute = true;

            Process.Start(process);

            Logger?.LogInformation("Shutting down to get out of the way");

            applicationLifetime.StopApplication();
        }
    }
}
