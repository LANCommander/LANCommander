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

namespace LANCommander.SDK.Services
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
            try
            {
                User = await GetAsync();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Could not get user alias from server");
            }

            return String.IsNullOrWhiteSpace(User.Alias) ? User.UserName : User.Alias;
        }

        public async Task<string> ChangeAliasAsync(string alias)
        {
            Logger?.LogTrace("Requesting to change player alias...");

            if (User == null)
                User = new User();

            User.Alias = alias;

            var response = await Client.PutRequestAsync<string>("/api/Profile/ChangeAlias", new
            {
                Alias = alias
            });

            return response;
        }

        public async Task<byte[]> GetAvatarAsync()
        {
            Logger?.LogTrace("Requesting avatar contents...");
            
            return await Client.GetRequestAsync<byte[]>("/api/Profile/Avatar");
        }

        public async Task<string> DownloadAvatar()
        {
            Logger?.LogTrace("Retrieving player's avatar...");

            var tempFile = Path.GetTempFileName();

            return await Client.DownloadRequestAsync("/api/Profile/Avatar", tempFile);
        }

        public async Task<string> GetCustomField(string name)
        {
            Logger?.LogTrace("Getting player custom field with name {CustomFieldName}...", name);

            return await Client.GetRequestAsync<string>($"/api/Profile/CustomField/{name}");
        }

        public async Task<string> UpdateCustomField(string name, string value)
        {
            Logger?.LogTrace("Updating player custom fields: {CustomFieldName} = {CustomFieldValue}", name, value);

            return await Client.PutRequestAsync<string>($"/api/Profile/CustomField/{name}", value);
        }
    }
}
