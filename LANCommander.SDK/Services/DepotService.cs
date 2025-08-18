using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using LANCommander.SDK.Exceptions;

namespace LANCommander.SDK.Services
{
    public class DepotService
    {
        private readonly ILogger _logger;

        private readonly Client _client;

        public DepotService(Client client)
        {
            _client = client;
        }

        public DepotService(Client client, ILogger logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<DepotResults> GetAsync()
        {
            var results = await _client.GetRequestAsync<DepotResults>("/api/Depot");

            if (results == null)
            {
                _logger?.LogDebug("Could not find any depot results");
                throw new DepotNoResultsException("Did not find any depot results");
            }
                

            return results;
        }

        public async Task<DepotGame> GetGameAsync(Guid gameId)
        {
            return await _client.GetRequestAsync<DepotGame>($"/api/Depot/Games/{gameId}");
        }
    }
}
