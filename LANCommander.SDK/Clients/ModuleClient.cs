using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Factories;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.Services
{
    public class ModuleClient(
        ILogger<ModuleClient> logger,
        ApiRequestFactory apiRequestFactory,
        ISettingsProvider settingsProvider)
    {
        public async Task<IEnumerable<string>> GetAsync()
        {
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute("/api/Modules")
                .GetAsync<IEnumerable<string>>();
        }

        public string GetLocalPath()
            => settingsProvider.CurrentValue.Modules.StoragePath;

        public async Task<FileInfo> DownloadAsync(string destination)
        {
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute("/api/Modules/Download")
                .DownloadAsync(destination);
        }

        public async Task SyncAsync()
        {
            var destination = GetLocalPath();
            var archivePath = Path.Combine(Path.GetTempPath(), $"LANCommander.Modules.{Guid.NewGuid()}.zip");

            try
            {
                await DownloadAsync(archivePath);

                if (Directory.Exists(destination))
                    Directory.Delete(destination, true);

                Directory.CreateDirectory(destination);

                ZipFile.ExtractToDirectory(archivePath, destination, true);

                logger?.LogInformation("Synced PowerShell modules to {Destination}", destination);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Could not sync PowerShell modules");
            }
            finally
            {
                if (File.Exists(archivePath))
                    File.Delete(archivePath);
            }
        }
    }
}
