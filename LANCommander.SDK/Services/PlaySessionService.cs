using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LANCommander.SDK.Services
{
    public class PlaySessionService
    {
        private readonly ILogger Logger;
        private Client Client { get; set; }

        public PlaySessionService(Client client)
        {
            Client = client;
        }

        public PlaySessionService(Client client, ILogger logger)
        {
            Client = client;
            Logger = logger;
        }

        public async Task<IEnumerable<EntityReference>> GetAsync()
        {
            return await Client.GetRequestAsync<IEnumerable<EntityReference>>("/api/PlaySessions");
        }

        public async Task<IEnumerable<PlaySession>> GetAsync(Guid gameId)
        {
            return await Client.PostRequestAsync<IEnumerable<PlaySession>>($"/api/PlaySessions/{gameId}");
        }
    }
}
