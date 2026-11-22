using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Factories;
using LANCommander.SDK.Updating;

namespace LANCommander.SDK.Services
{
    public class LauncherClient(
        ILogger<LauncherClient> logger,
        ApiRequestFactory apiRequestFactory)
    {
        /// <summary>Asks the server whether it has a newer launcher for this platform, architecture and package kind.</summary>
        public async Task<CheckForUpdateResponse> CheckForUpdateAsync(UpdatePackageKind packageKind = UpdatePackageKind.Archive, CancellationToken cancellationToken = default)
        {
            try
            {
                return await apiRequestFactory
                    .Create()
                    .UseAuthenticationToken()
                    .UseVersioning()
                    .UseCancellationToken(cancellationToken)
                    .UseRoute($"/api/Launcher/CheckForUpdate?{PackageQuery(packageKind)}")
                    .GetAsync<CheckForUpdateResponse>();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Could not check for updates from server");
            }

            return null;
        }

        /// <summary>Downloads the launcher release for this platform, architecture and package kind.</summary>
        public async Task<string> DownloadAsync(
            string destination,
            UpdatePackageKind packageKind = UpdatePackageKind.Archive,
            Action<DownloadProgressChangedEventArgs> progress = null,
            CancellationToken cancellationToken = default)
        {
            logger?.LogTrace("Downloading the launcher");

            var request = apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseCancellationToken(cancellationToken)
                .UseRoute($"/api/Launcher/Download?{PackageQuery(packageKind)}");

            if (progress != null)
                request.OnProgress(progress);

            var result = await request.DownloadAsync(destination);

            return result.FullName;
        }

        private static string PackageQuery(UpdatePackageKind packageKind) =>
            $"platform={UpdateInstallation.Platform}&architecture={UpdateInstallation.Architecture}&package={packageKind}";
    }
}
