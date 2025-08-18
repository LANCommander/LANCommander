using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using RestSharp;
using System;
using System.Threading.Tasks;

namespace LANCommander.SDK.Services
{
    public class LauncherService
    {
        private readonly ILogger _logger;
        private readonly Client _client;

        public LauncherService(Client client)
        {
            _client = client;
        }

        public LauncherService(Client client, ILogger logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<CheckForUpdateResponse> CheckForUpdateAsync()
        {
            try
            {
                var request = new RestRequest("/api/Launcher", Method.Get);

                return await _client.GetRequestAsync<CheckForUpdateResponse>("/api/Launcher/CheckForUpdate", true);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Could not check for updates from server");
            }

            return null;
        }

        public async Task<string> DownloadAsync(string destination)
        {
            _logger?.LogTrace("Downloading the launcher");

            return await _client.DownloadRequestAsync("/api/Launcher/Download", destination);
        }
    }
}
