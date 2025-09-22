using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using LANCommander.SDK.Factories;

namespace LANCommander.SDK.Services
{
    public class IssueService(ApiRequestFactory apiRequestFactory)
    {
        public async Task<bool> Open(string description, Guid gameId)
        {
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute("/api/Issue/Open")
                .AddBody(new Issue
                {
                    Description = description,
                    GameId = gameId,
                })
                .PostAsync<bool>();
        }
    }
}
