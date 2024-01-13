using LANCommander.Models;
using Octokit;
using Semver;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;

namespace LANCommander.Services
{
    public class UpdateService : BaseService
    {
        private GitHubClient GitHub;
        private LANCommanderSettings Settings;

        public UpdateService() : base()
        {
            GitHub = new GitHubClient(new ProductHeaderValue("LANCommander"));
            Settings = SettingService.GetSettings();
        }

        public async Task<Semver.SemVersion> GetLatestVersion()
        {
            var release = await GitHub.Repository.Release.GetLatest("LANCommander", "LANCommander");

            SemVersion version = null;

            SemVersion.TryParse(release.TagName, SemVersionStyles.Strict, out version);

            return version;
        }

        public async Task<bool> UpdateAvailable()
        {
            var latestVersion = await GetLatestVersion();
            var assemblyVersion = SemVersion.FromVersion(Assembly.GetExecutingAssembly().GetName().Version);

            var sortOrder = assemblyVersion.ComparePrecedenceTo(latestVersion);

            return sortOrder > 0;
        }

        public async Task<IEnumerable<Release>> GetReleases(int count)
        {
            return await GitHub.Repository.Release.GetAll("LANCommander", "LANCommander", new ApiOptions
            {
                PageSize = count,
                PageCount = 1,
            });
        }

        public async Task DownloadRelease(Release release)
        {
            string releaseFile = String.Empty;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                releaseFile = release.Assets.FirstOrDefault(a => a.Name.Contains("Windows")).BrowserDownloadUrl;
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                releaseFile = release.Assets.FirstOrDefault(a => a.Name.Contains("Linux")).BrowserDownloadUrl;
            else
                throw new NotImplementedException("The current operating system is not supported.");

            if (!String.IsNullOrWhiteSpace(releaseFile))
            {
                WebClient client = new WebClient();

                client.DownloadFileCompleted += ReleaseDownloaded;

                client.DownloadFileAsync(new Uri(releaseFile), Path.Combine(Settings.Update.StoragePath, release.TagName + ".zip"));
            }
        }

        private void ReleaseDownloaded(object? sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            
        }
    }
}
