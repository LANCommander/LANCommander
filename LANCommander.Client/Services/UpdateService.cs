using Photino.Blazor;
using Semver;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Client.Services
{
    public class UpdateService : BaseService
    {
        private readonly SDK.Client Client;

        public delegate Task OnUpdateAvailableHandler(SemVersion version);
        public event OnUpdateAvailableHandler OnUpdateAvailable;

        public UpdateService(SDK.Client client)
        {
            Client = client;
        }

        public async Task<SemVersion> CheckForUpdateAsync()
        {
            var serverVersion = await Client.Launcher.CheckForUpdateAsync();

            OnUpdateAvailable?.Invoke(serverVersion);

            return serverVersion;
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
            process.Arguments = $"-Version {version} -Path \"{settings.Updates.StoragePath}\"";
            process.UseShellExecute = true;

            Process.Start(process);

            Logger?.Info("Shutting down to get out of the way");

            Environment.Exit(0);
        }
    }
}
