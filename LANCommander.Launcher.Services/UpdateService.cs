using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using Semver;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Services
{
    public class UpdateService : BaseService
    {
        public delegate Task OnUpdateAvailableHandler(CheckForUpdateResponse response);
        public event OnUpdateAvailableHandler OnUpdateAvailable;

        public UpdateService(SDK.Client client, ILogger<UpdateService> logger) : base(client, logger) { }

        public async Task<CheckForUpdateResponse> CheckForUpdateAsync()
        {
            var response = await Client.Launcher.CheckForUpdateAsync();

            if (response != null && response.UpdateAvailable)
                OnUpdateAvailable?.Invoke(response);

            return response;
        }

        public async Task UpdateAsync(SemVersion version)
        {
            var settings = SettingService.GetSettings();

            Logger?.LogInformation("Downloading launcher v{Version}", version);

            string path = Path.Combine(settings.Updates.StoragePath, $"{version}.zip");

            await Client.Launcher.DownloadAsync(path);

            Logger?.LogInformation("Update version {Version} has been downloaded", version);

            string processExecutable = "LANCommander.AutoUpdater.exe";

            if (File.Exists("LANCommander.AutoUpdater"))
                processExecutable = "LANCommander.AutoUpdater";

            Logger?.LogInformation("New autoupdater is being extracted");

            using (ZipArchive archive = ZipFile.OpenRead(path))
            {
                Logger?.LogTrace("Looping entries");
                foreach (ZipArchiveEntry entry in archive.Entries.Where(e => e.FullName == processExecutable))
                {
                    entry.ExtractToFile(entry.FullName, true);
                    Logger?.LogTrace("Extracted {Entry}", entry);
                }
                Logger?.LogTrace("Entries loop over");
            }

            Logger?.LogInformation("Sarting Autoupdater");

            var process = new ProcessStartInfo();

            process.FileName = processExecutable;
            process.Arguments = $"-Version {version} -Path \"{settings.Updates.StoragePath}\" -Executable {Process.GetCurrentProcess().MainModule.FileName}";
            process.UseShellExecute = true;

            Process.Start(process);

            settings.LaunchCount = 0;

            SettingService.SaveSettings(settings);

            Logger?.LogInformation("Shutting down to get out of the way");

            Environment.Exit(0);
        }

        public static SemVersion GetCurrentVersion()
        {
            var productVersion = FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location).ProductVersion;
            
            return SemVersion.Parse(productVersion);
        }
    }
}
