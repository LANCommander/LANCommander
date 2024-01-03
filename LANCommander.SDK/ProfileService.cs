using LANCommander.SDK.Enums;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using LANCommander.SDK.PowerShell;
using Microsoft.Extensions.Logging;
using RestSharp;
using SharpCompress.Common;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.SDK
{
    public class ProfileService
    {
        private readonly ILogger Logger;
        private Client Client { get; set; }

        public ProfileService(Client client)
        {
            Client = client;
        }

        public ProfileService(Client client, ILogger logger)
        {
            Client = client;
            Logger = logger;
        }

        public User Get()
        {
            Logger?.LogTrace("Requesting player's profile...");

            return Client.GetRequest<User>("/api/Profile");
        }

        public string ChangeAlias(string alias)
        {
            Logger?.LogTrace("Requesting to change player alias...");

            var response = Client.PostRequest<object>("/api/Profile/ChangeAlias", alias);

            return alias;
        }
    }
}
