using LANCommander.SDK.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LANCommander.SDK.Factories;

namespace LANCommander.SDK.Services
{
    public class LibraryClient(ApiRequestFactory apiRequestFactory)
    {
        public async Task<IEnumerable<EntityReference>> GetAsync()
        {
            var results = await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute("/api/Library")
                .GetAsync<IEnumerable<EntityReference>>();

            return results ?? [];
        }

        public async Task<bool> AddToLibrary(Guid gameId)
        {
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Library/AddToLibrary/{gameId}")
                .PostAsync<bool>();
        }

        public async Task<bool> RemoveFromLibrary(Guid gameId)
        {
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Library/RemoveFromLibrary/{gameId}")
                .PostAsync<bool>();
        }

        public async Task<bool> RemoveFromLibrary(Guid gameId, Guid[] addonIds)
        {
            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Library/RemoveFromLibrary/{gameId}/addons")
                .AddBody(new GenericGuidsRequest
                {
                    Guids = addonIds
                })
                .PostAsync<bool>();
        }
    }
}
