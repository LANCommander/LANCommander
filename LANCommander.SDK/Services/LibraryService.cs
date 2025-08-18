using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LANCommander.SDK.Services
{
    public class LibraryService
    {
        private readonly ILogger _logger;
        private readonly Client _client;

        public LibraryService(Client client)
        {
            _client = client;
        }

        public LibraryService(Client client, ILogger logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task<IEnumerable<EntityReference>> GetAsync()
        {
            return await _client.GetRequestAsync<IEnumerable<EntityReference>>("/api/Library");
        }

        public async Task<bool> AddToLibrary(Guid gameId)
        {
            return await _client.PostRequestAsync<bool>($"/api/Library/AddToLibrary/{gameId}");
        }

        public async Task<bool> RemoveFromLibrary(Guid gameId)
        {
            return await _client.PostRequestAsync<bool>($"/api/Library/RemoveFromLibrary/{gameId}");
        }

        public async Task<bool> RemoveFromLibrary(Guid gameId, Guid[] addonIds)
        {
            var requestBody = new GenericGuidsRequest { Guids = addonIds };
            return await _client.PostRequestAsync<bool>($"/api/Library/RemoveFromLibrary/{gameId}/addons", requestBody);
        }
    }
}
