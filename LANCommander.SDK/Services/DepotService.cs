using Force.Crc32;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

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
            return await Client.GetRequestAsync<DepotResults>("/api/Depot");
        }

        public async Task<DepotGame> GetGameAsync(Guid gameId)
        {
            return await Client.GetRequestAsync<DepotGame>($"/api/Depot/Games/{gameId}");
        }
    }
}
