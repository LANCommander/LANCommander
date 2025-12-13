using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using LANCommander.SDK.Exceptions;
using LANCommander.SDK.Factories;

namespace LANCommander.SDK.Services
{
    public class DepotClient(
        ILogger<DepotClient> logger,
        ApiRequestFactory apiRequestFactory)
    {
        public async Task<DepotResults> GetAsync()
        {
            var results = await apiRequestFactory.Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute("/api/Depot")
                .GetAsync<DepotResults>();

            if (results == null)
            {
                logger?.LogDebug("Could not find any depot results");
                throw new DepotNoResultsException("Did not find any depot results");
            }

            return results;
        }

        public async Task<DepotGame> GetGameAsync(Guid gameId)
        {
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Depot/Games/{gameId}")
                .GetAsync<DepotGame>();
        }
    }
}
