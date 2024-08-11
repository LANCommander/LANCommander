using Force.Crc32;
using LANCommander.SDK;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using LANCommander.SDK.PowerShell;
using Microsoft.Extensions.Logging;
using RestSharp;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LANCommander.SDK
{
    public class IssueService
    {
        private readonly ILogger Logger;

        private readonly Client Client;

        public IssueService(Client client)
        {
            Client = client;
        }

        public IssueService(Client client, ILogger logger)
        {
            Client = client;
            Logger = logger;
        }

        public async Task<bool> Open(string description, Guid gameId)
        {
            return await Client.PostRequestAsync<bool>("/Issue/Open", new Issue
            {
                Description = description,
                GameId = gameId
            });
        }
    }
}
