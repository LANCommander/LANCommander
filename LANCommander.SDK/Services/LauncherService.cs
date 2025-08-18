using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using RestSharp;
using System;
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
