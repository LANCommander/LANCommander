using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LANCommander.SDK.Services
{
    public class LibraryService
    {
        private readonly ILogger Logger;
        private Client Client { get; set; }

        public LibraryService(Client client)
        {
            Client = client;
        }

        public LibraryService(Client client, ILogger logger)
        {
            Client = client;
            Logger = logger;
        }

        public async Task<IEnumerable<EntityReference>> GetAsync()
        {
            return await Client.GetRequestAsync<IEnumerable<EntityReference>>("/api/Library");
        }

        public async Task<bool> AddToLibrary(Guid gameId)
        {
            return await Client.PostRequestAsync<bool>($"/api/Library/AddToLibrary/{gameId}");
        }

        public async Task<bool> RemoveFromLibrary(Guid gameId)
        {
            return await Client.PostRequestAsync<bool>($"/api/Library/RemoveFromLibrary/{gameId}");
        }

        public async Task<bool> RemoveFromLibrary(Guid gameId, Guid[] addonIds)
        {
            var requestBody = new GenericGuidsRequest { Guids = addonIds };
            return await Client.PostRequestAsync<bool>($"/api/Library/RemoveFromLibrary/{gameId}/addons", requestBody);
        }
    }
}
