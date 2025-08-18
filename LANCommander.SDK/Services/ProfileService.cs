using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;

namespace LANCommander.SDK.Services
{
    public class ProfileService
    {
        private readonly ILogger _logger;
        private readonly Client _client;

        private User _user;

        public ProfileService(Client client)
        {
            _client = client;
        }

        public ProfileService(Client client, ILogger logger)
        {
            _client = client;
            _logger = logger;
        }

        public User Get(bool forceLoad = false)
        {
            _logger?.LogTrace("Requesting player's profile...");

            if (_user == null || forceLoad)
                _user = _client.GetRequest<User>("/api/Profile");

            return _user;
        }

        public async Task<User> GetAsync(bool forceLoad = false)
        {
            _logger?.LogTrace("Requesting player's profile...");

            if (_user == null || forceLoad)
                _user = await _client.GetRequestAsync<User>("/api/Profile");

            return _user;
        }

        public async Task<string> GetAliasAsync()
        {
            try
            {
                _user = await GetAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Could not get user alias from server");
            }

            return String.IsNullOrWhiteSpace(_user.Alias) ? _user.UserName : _user.Alias;
        }

        public async Task<string> ChangeAliasAsync(string alias)
        {
            _logger?.LogTrace("Requesting to change player alias...");

            if (_user == null)
                _user = new User();

            _user.Alias = alias;

            var response = await _client.PutRequestAsync<string>("/api/Profile/ChangeAlias", new
            {
                Alias = alias
            });

            return response;
        }

        public async Task<byte[]> GetAvatarAsync()
        {
            _logger?.LogTrace("Requesting avatar contents...");

            using (var ms = new MemoryStream())
            {
                var stream = _client.StreamRequest("/api/Profile/Avatar");
                
                await stream.CopyToAsync(ms);
                
                return ms.ToArray();
            }
        }

        public async Task<string> DownloadAvatar()
        {
            _logger?.LogTrace("Retrieving player's avatar...");

            var tempFile = Path.GetTempFileName();

            return await _client.DownloadRequestAsync("/api/Profile/Avatar", tempFile);
        }

        public async Task<string> GetCustomField(string name)
        {
            _logger?.LogTrace("Getting player custom field with name {CustomFieldName}...", name);

            return await _client.GetRequestAsync<string>($"/api/Profile/CustomField/{name}");
        }

        public async Task<string> UpdateCustomField(string name, string value)
        {
            _logger?.LogTrace("Updating player custom fields: {CustomFieldName} = {CustomFieldValue}", name, value);

            return await _client.PutRequestAsync<string>($"/api/Profile/CustomField/{name}", value);
        }
    }
}
