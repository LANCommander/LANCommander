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
