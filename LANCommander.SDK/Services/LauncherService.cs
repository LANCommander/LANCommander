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

namespace LANCommander.SDK.Services
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

        public async Task<CheckForUpdateResponse> CheckForUpdateAsync()
        {
            try
            {
                var request = new RestRequest("/api/Launcher", Method.Get);

                return await Client.GetRequestAsync<CheckForUpdateResponse>("/api/Launcher/CheckForUpdate", true);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Could not check for updates from server");
            }

            return null;
        }

        public async Task<string> DownloadAsync(string destination)
        {
            Logger?.LogTrace("Downloading the launcher");

            return await Client.DownloadRequestAsync("/api/Launcher/Download", destination);
        }
    }
}
