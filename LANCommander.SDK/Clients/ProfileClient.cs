using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Factories;

namespace LANCommander.SDK.Services
{
    public class ProfileClient(
        ApiRequestFactory apiRequestFactory, 
        ILogger<ProfileClient> logger,
        IConnectionClient connectionClient)
    {
        private User _user;
        
        public async Task<User> GetAsync(bool forceLoad = false)
        {
            logger?.LogTrace("Requesting player's profile...");

            if (_user == null || forceLoad)
            {
                _user = await apiRequestFactory
                    .Create()
                    .UseAuthenticationToken()
                    .UseVersioning()
                    .UseRoute("/api/Profile")
                    .GetAsync<User>();
            }

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
                logger?.LogError(ex, "Could not get user alias from server");
            }

            return String.IsNullOrWhiteSpace(_user.Alias) ? _user.UserName : _user.Alias;
        }

        public async Task<string> ChangeAliasAsync(string alias)
        {
            logger?.LogTrace("Requesting to change player alias...");

            if (_user == null)
                _user = new User();

            _user.Alias = alias;

            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute("/api/Profile/ChangeAlias")
                .AddBody(new
                {
                    Alias = alias
                })
                .PutAsync<string>();
        }

        public async Task<byte[]> GetAvatarAsync()
        {
            logger?.LogTrace("Requesting avatar contents...");

            using (var ms = new MemoryStream())
            {
                var stream = await apiRequestFactory
                    .Create()
                    .UseAuthenticationToken()
                    .UseVersioning()
                    .UseRoute("/api/Profile/Avatar")
                    .StreamAsync();
                
                await stream.CopyToAsync(ms);
                
                return ms.ToArray();
            }
        }

        public async Task<string> DownloadAvatar()
        {
            logger?.LogTrace("Retrieving player's avatar...");

            var tempFile = Path.GetTempFileName();

            var result = await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute("/api/Profile/Avatar")
                .DownloadAsync(tempFile);

            return result.FullName;
        }

        public Uri GetAvatarUri(string userName) 
            => connectionClient.GetServerAddress().Join("api", "Profile", userName, "Avatar");

        public async Task<string> GetCustomFieldAsync(string name)
        {
            logger?.LogTrace("Getting player custom field with name {CustomFieldName}...", name);

            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Profile/CustomField/{name}")
                .GetAsync<string>();
        }

        public async Task<string> UpdateCustomFieldAsync(string name, string value)
        {
            logger?.LogTrace("Updating player custom fields: {CustomFieldName} = {CustomFieldValue}", name, value);

            return await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute($"/api/Profile/CustomField/{name}")
                .AddBody(value)
                .PutAsync<string>();
        }
    }
}
