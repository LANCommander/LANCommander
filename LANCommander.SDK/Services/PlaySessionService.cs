using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LANCommander.SDK.Factories;

namespace LANCommander.SDK.Services
{
    public class PlaySessionService(ApiRequestFactory apiRequestFactory)
    {
        public async Task<IEnumerable<EntityReference>> GetAsync()
        {
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute("/api/PlaySessions")
                .GetAsync<IEnumerable<EntityReference>>();
        }

        public async Task<IEnumerable<PlaySession>> GetAsync(Guid gameId)
        {
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/PlaySessions/{gameId}")
                .GetAsync<IEnumerable<PlaySession>>();
        }
    }
}
