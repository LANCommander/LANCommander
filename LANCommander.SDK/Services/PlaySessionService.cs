using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LANCommander.SDK.Services
{
    public class PlaySessionService
    {
        private readonly ILogger _logger;
        private readonly Client _client;

        public PlaySessionService(Client client)
        {
            _client = client;
        }

        public PlaySessionService(Client client, ILogger logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<IEnumerable<EntityReference>> GetAsync()
        {
            return await _client.GetRequestAsync<IEnumerable<EntityReference>>("/api/PlaySessions");
        }

        public async Task<IEnumerable<PlaySession>> GetAsync(Guid gameId)
        {
            return await _client.PostRequestAsync<IEnumerable<PlaySession>>($"/api/PlaySessions/{gameId}");
        }
    }
}
