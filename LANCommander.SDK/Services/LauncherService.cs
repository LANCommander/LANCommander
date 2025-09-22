using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using LANCommander.SDK.Factories;

namespace LANCommander.SDK.Services
{
    public class LauncherService(
        ILogger<LauncherService> logger,
        ApiRequestFactory apiRequestFactory)
    {
        public async Task<CheckForUpdateResponse> CheckForUpdateAsync()
        {
            try
            {
                return await apiRequestFactory
                    .Create()
                    .UseRoute("/api/Launcher/CheckForUpdate")
                    .GetAsync<CheckForUpdateResponse>();
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Could not check for updates from server");
            }

            return null;
        }

        public async Task<string> DownloadAsync(string destination)
        {
            logger?.LogTrace("Downloading the launcher");

            var result = await apiRequestFactory
                .Create()
                .UseRoute("/api/Launcher/Download")
                .DownloadAsync(destination);

            return result.FullName;
        }
    }
}
