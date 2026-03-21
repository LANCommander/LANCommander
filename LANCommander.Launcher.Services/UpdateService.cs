using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using Semver;
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Services;

namespace LANCommander.Launcher.Services
{
    public class UpdateService(
        ILogger<UpdateService> logger,
        LauncherClient launcherClient,
        SettingsProvider<Settings.Settings> settingsProvider) : BaseService(logger)
    {
        public delegate Task OnUpdateAvailableHandler(CheckForUpdateResponse response);
        public event OnUpdateAvailableHandler OnUpdateAvailable;

        public async Task<CheckForUpdateResponse> CheckForUpdateAsync()
        {
            using (var op = Logger.BeginOperation("Checking for updates"))
            {
                var response = await launcherClient.CheckForUpdateAsync();

                if (response != null && response.UpdateAvailable)
                    OnUpdateAvailable?.Invoke(response);
                
                op.Complete();

                return response;
            }
        }

        public async Task UpdateAsync(SemVersion version)
        {
            using (var op = Logger.BeginDebugOperation("Updating launcher"))
            {
                op.Enrich("Version", version);
                
                Logger?.LogInformation("Downloading launcher v{Version}", version);

                string path = Path.Combine(settingsProvider.CurrentValue.Updates.StoragePath, $"{version}.zip");

                await launcherClient.DownloadAsync(path);

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
                process.Arguments = $"-Version {version} -Path \"{settingsProvider.CurrentValue.Updates.StoragePath}\" -Executable {Process.GetCurrentProcess().MainModule.FileName}";
                process.UseShellExecute = true;

                Process.Start(process);

                settingsProvider.Update(s => s.Launcher.LaunchCount = 0);

                Logger?.LogInformation("Shutting down to get out of the way");
                
                op.Complete();

                Environment.Exit(0);
            }
        }

        public static SemVersion GetCurrentVersion()
        {
            var productVersion = FileVersionInfo.GetVersionInfo(Assembly.GetEntryAssembly().Location).ProductVersion;
            
            return SemVersion.Parse(productVersion);
        }
    }
}
