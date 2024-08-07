using LANCommander.SDK.Models;
using Semver;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Services
{
    public class UpdateService : BaseService
    {
        private readonly SDK.Client Client;

        public delegate Task OnUpdateAvailableHandler(CheckForUpdateResponse response);
        public event OnUpdateAvailableHandler OnUpdateAvailable;

        public UpdateService(SDK.Client client)
        {
            Client = client;
        }

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

            Logger?.Info("Updating launcher to v{Version}", version);

            await Client.Launcher.DownloadAsync(Path.Combine(settings.Updates.StoragePath, $"{version}.zip"));

            string processExecutable = String.Empty;

            if (File.Exists("LANCommander.AutoUpdater.exe"))
                processExecutable = "LANCommander.AutoUpdater.exe";
            else if (File.Exists("LANCommander.AutoUpdater"))
                processExecutable = "LANCommander.AutoUpdater";

            var process = new ProcessStartInfo();

            process.FileName = processExecutable;
            process.Arguments = $"-Version {version} -Path \"{settings.Updates.StoragePath}\" -Executable {Process.GetCurrentProcess().MainModule.FileName}";
            process.UseShellExecute = true;

            Process.Start(process);

            settings.LaunchCount = 0;

            SettingService.SaveSettings(settings);

            Logger?.Info("Shutting down to get out of the way");

            Environment.Exit(0);
        }

        public static SemVersion GetCurrentVersion()
        {
            return SemVersion.FromVersion(Assembly.GetExecutingAssembly().GetName().Version);
        }
    }
}
