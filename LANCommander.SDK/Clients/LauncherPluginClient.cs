using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Factories;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.Services;

/// <summary>The launcher plugins the server keeps for launchers to install.</summary>
public class LauncherPluginClient(ApiRequestFactory apiRequestFactory)
{
    public virtual async Task<IReadOnlyList<LauncherPluginPackage>> GetAsync(CancellationToken cancellationToken = default)
    {
        var packages = await apiRequestFactory.Create()
            .UseAuthenticationToken()
            .UseVersioning()
            .UseCancellationToken(cancellationToken)
            .UseRoute("/api/Launcher/Plugins")
            .GetAsync<List<LauncherPluginPackage>>();

        return packages ?? [];
    }

    /// <summary>
    /// Downloads a package to <paramref name="destination"/> and checks it is the one the server
    /// described, deleting it and throwing when it isn't.
    /// </summary>
    public virtual async Task DownloadAsync(LauncherPluginPackage package, string destination, CancellationToken cancellationToken = default)
    {
        await apiRequestFactory.Create()
            .UseAuthenticationToken()
            .UseCancellationToken(cancellationToken)
            .UseRoute(package.DownloadPath)
            .DownloadAsync(destination);

        if (string.IsNullOrEmpty(package.Sha512))
            return;

        byte[] hash;

        using (var file = File.OpenRead(destination))
        using (var sha512 = SHA512.Create())
            hash = sha512.ComputeHash(file);

        if (!hash.SequenceEqual(Convert.FromBase64String(package.Sha512)))
        {
            File.Delete(destination);

            throw new InvalidDataException($"{package.Id} {package.Version} did not download intact.");
        }
    }
}
