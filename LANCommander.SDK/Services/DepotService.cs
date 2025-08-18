using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using LANCommander.SDK.Exceptions;

namespace LANCommander.SDK.Services
{
    public class DepotService
    {
        private readonly ILogger Logger;

        private readonly Client Client;

        public DepotService(Client client)
        {
            Client = client;
        }

        public DepotService(Client client, ILogger logger)
        {
            Client = client;
            Logger = logger;
        }

        public async Task<DepotResults> GetAsync()
        {
            var results = await Client.GetRequestAsync<DepotResults>("/api/Depot");

            if (results == null)
                throw new DepotNoResultsException("Did not find any depot results");

            return results;
        }

        public async Task<DepotGame> GetGameAsync(Guid gameId)
        {
            return await Client.GetRequestAsync<DepotGame>($"/api/Depot/Games/{gameId}");
        }
    }
}
