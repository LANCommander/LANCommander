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

        private User User { get; set; }

        public ProfileService(Client client)
        {
            Client = client;
        }

        public ProfileService(Client client, ILogger logger)
        {
            Client = client;
            Logger = logger;
        }

        public User Get(bool forceLoad = false)
        {
            Logger?.LogTrace("Requesting player's profile...");

            if (User == null || forceLoad)
                User = Client.GetRequest<User>("/api/Profile");

            return User;
        }

        public async Task<User> GetAsync(bool forceLoad = false)
        {
            Logger?.LogTrace("Requesting player's profile...");

            if (User == null || forceLoad)
                User = await Client.GetRequestAsync<User>("/api/Profile");

            return User;
        }

        public async Task<string> GetAliasAsync()
        {
            var user = await GetAsync();

            return user.Alias;
        }

        public async Task<string> ChangeAliasAsync(string alias)
        {
            Logger?.LogTrace("Requesting to change player alias...");

            var response = await Client.PostRequestAsync<string>("/api/Profile/ChangeAlias", new
            {
                Alias = alias
            });

            return response;
        }

        public async Task<string> DownloadAvatar()
        {
            Logger?.LogTrace("Retrieving player's avatar...");

            var tempFile = Path.GetTempFileName();

            return await Client.DownloadRequestAsync("/api/Profile/Avatar", tempFile);
        }
    }
}
