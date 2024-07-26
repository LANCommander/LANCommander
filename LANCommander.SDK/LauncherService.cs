using LANCommander.SDK.Enums;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using LANCommander.SDK.PowerShell;
using Microsoft.Extensions.Logging;
using RestSharp;
using Semver;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.SDK
{
    public class LauncherService
    {
        private readonly ILogger Logger;
        private Client Client { get; set; }

        public LauncherService(Client client)
        {
            Client = client;
        }

        public LauncherService(Client client, ILogger logger)
        {
            Client = client;
            Logger = logger;
        }

        public async Task<SemVersion> CheckForUpdateAsync()
        {
            Logger?.LogTrace("Checking the server to see if we're on a matching launcher version...");

            try
            {
                var versionAvailable = await Client.GetRequestAsync<string>("/api/Launcher/CheckForUpdate", true);

                if (SemVersion.TryParse(versionAvailable, SemVersionStyles.Any, out var version))
                    return version;
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Could not check for updates from server");
            }

            return null;
        }

        public async Task<string> DownloadAsync(string destination)
        {
            Logger?.LogTrace("Downloading the launcher...");

            return await Client.DownloadRequestAsync("/api/Launcher/Download", destination);
        }
    }
}
