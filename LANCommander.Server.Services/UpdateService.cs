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
using LANCommander.Server.Services.Models;
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

        private const string _owner = "LANCommander";
        private const string _repository = "LANCommander";
        private const string _nightlyWorkflowFile = "LANCommander.Nightly.yml";

        public override void Initialize()
        {
            _gitHub = new GitHubClient(new ProductHeaderValue(_repository));
        }

        public static SemVersion GetCurrentVersion()
        {
            var productVersion = FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location).ProductVersion;
            
            return SemVersion.Parse(productVersion);
        }

        public async Task<IEnumerable<LauncherArtifact>> GetLauncherArtifactsAsync()
        {
            if (!_settings.Launcher.HostUpdates)
                return await GetLauncherArtifactsFromGitHubAsync().ToListAsync();

            return GetLauncherArtifactsFromLocalFiles();
        }

        public IEnumerable<LauncherArtifact> GetLauncherArtifactsFromLocalFiles()
        {
            var currentVersion = GetCurrentVersion();
            var downloadedLaunchers = Directory.GetFiles(_settings.Launcher.StoragePath, $"LANCommander.Launcher.*v{currentVersion.WithoutMetadata()}.zip");
            
            foreach (var downloadedLauncher in downloadedLaunchers)
                yield return GetArtifactFromName(downloadedLauncher);
        }

        public async IAsyncEnumerable<LauncherArtifact> GetLauncherArtifactsFromGitHubAsync()
        {
            var currentVersion = GetCurrentVersion();
            
            if (!String.IsNullOrWhiteSpace(_settings.Launcher.VersionOverride))
                currentVersion = SemVersion.Parse(_settings.Launcher.VersionOverride, SemVersionStyles.AllowV);
            
            if (_settings.Update.ReleaseChannel == ReleaseChannel.Stable ||
                _settings.Update.ReleaseChannel == ReleaseChannel.Prerelease)
            {
                var release = await _gitHub.Repository.Release.Get(_owner, _repository, "v" + currentVersion);

                var assets = release.Assets.Where(a => a.Name.Contains("LANCommander.Launcher")).ToList();
                
                foreach (var asset in assets)
                    yield return GetArtifactFromName(asset.Name, asset.BrowserDownloadUrl);
            }

            if (_settings.Update.ReleaseChannel == ReleaseChannel.Nightly)
            {
                if (!String.IsNullOrWhiteSpace(_settings.Launcher.VersionOverride))
                    currentVersion = SemVersion.Parse(_settings.Launcher.VersionOverride);
                
                var workflowRunsResponse = await _gitHub.Actions.Workflows.Runs.ListByWorkflow(_owner, _repository, _nightlyWorkflowFile, new WorkflowRunsRequest
                {
                    HeadSha = currentVersion.Metadata
                });

                if (workflowRunsResponse.WorkflowRuns.Any())
                {
                    var run = workflowRunsResponse.WorkflowRuns.FirstOrDefault();
                    var artifactsResponse = await _gitHub.Actions.Artifacts.ListWorkflowArtifacts(_owner, _repository, run.Id);

                    foreach (var artifact in artifactsResponse.Artifacts)
                        yield return GetArtifactFromName(artifact.Name, artifact.ArchiveDownloadUrl);
                }
            }
        }

        private LauncherArtifact GetArtifactFromName(string name, string url = "")
        {
            var platform = LauncherPlatform.Windows;
            var architecture = LauncherArchitecture.x64;

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
                url = $"/Launcher/{name}";

            return new LauncherArtifact
            {
                Name = name,
                Platform = platform,
                Architecture = architecture,
                Url = url,
            };
        }
        
        public async Task<SemVersion> GetLatestVersionAsync()
        {
            string version = "";
            
            if (_settings.Update.ReleaseChannel == ReleaseChannel.Stable)
            {
                var release = await _gitHub.Repository.Release.GetLatest(_owner, _repository);

                if (release.Prerelease)
                {
                    release = (await _gitHub.Repository.Release.GetAll(_owner, _repository))
                        .Where(r => !r.Prerelease)
                        .OrderByDescending(r => r.CreatedAt)
                        .FirstOrDefault();
                }

                version = release.TagName;
            }

            if (_settings.Update.ReleaseChannel == ReleaseChannel.Prerelease)
            {
                var release = await _gitHub.Repository.Release.GetLatest(_owner, _repository);
                
                version = release.TagName;
            }
            
            if (_settings.Update.ReleaseChannel == ReleaseChannel.Nightly)
            {
                var workflow = await _gitHub.Actions.Workflows.Get(_owner, _repository, _nightlyWorkflowFile);
                var runs = await _gitHub.Actions.Workflows.Runs.ListByWorkflow(_owner, _repository, workflow.Id);
                
                var latestRun = runs.WorkflowRuns
                    .Where(r => r.Conclusion == WorkflowRunConclusion.Success)
                    .OrderByDescending(r => r.CreatedAt)
                    .FirstOrDefault();
                
                var artifacts = await _gitHub.Actions.Artifacts.ListWorkflowArtifacts(_owner, _repository, latestRun.Id);

                var versionArtifact = artifacts.Artifacts.FirstOrDefault(a => a.Name.StartsWith("version."));

                if (versionArtifact != null)
                    version = versionArtifact.Name.Substring(0, versionArtifact.Name.Length - "version.".Length);
            }

            return SemVersion.Parse(version, SemVersionStyles.AllowV);
        }

        public async Task<bool> UpdateAvailableAsync()
        {
            var latestVersion = await GetLatestVersionAsync();

            var sortOrder = GetCurrentVersion().ComparePrecedenceTo(latestVersion);

            return sortOrder < 0;
        }

        public async Task<IEnumerable<Release>> GetReleasesAsync(int count)
        {
            return await _gitHub.Repository.Release.GetAll(_owner, _repository, new ApiOptions
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

    public class LauncherArtifact
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public LauncherPlatform Platform { get; set; }
        public LauncherArchitecture Architecture { get; set; }
    }
}
