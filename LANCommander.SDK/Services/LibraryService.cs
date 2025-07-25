using LANCommander.SDK.Enums;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using LANCommander.SDK.PowerShell;
using Microsoft.Extensions.Logging;
using RestSharp;
using Semver;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
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
