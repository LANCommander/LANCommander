using LANCommander.SDK.Exceptions;
using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
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

        public void ClearCache()
        {
            _user = null;
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

        /// <summary>Changes the player's alias. A blank alias clears it.</summary>
        /// <returns>The alias as the server stored it; empty when cleared.</returns>
        /// <exception cref="ProfileUpdateException">The server refused the change.</exception>
        public async Task<string> ChangeAliasAsync(string alias)
        {
            logger?.LogTrace("Requesting to change player alias...");

            var result = await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute("/api/Profile/ChangeAlias")
                .UseMethod(HttpMethod.Put)
                .AddBody(new
                {
                    Alias = alias
                })
                .SendAsync<string>();

            await EnsureSuccessAsync(result.Response, "The alias could not be changed.");

            // Older servers echo the alias as sent, untrimmed
            var stored = (result.Data ?? alias)?.Trim() ?? String.Empty;

            if (_user != null)
                _user.Alias = stored;

            return stored;
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

        /// <summary>
        /// Asks the server which profile edits it allows. Servers from before these settings were
        /// published always took alias changes and had no way to upload an avatar, and are treated so.
        /// </summary>
        public async Task<ProfileEditingOptions> GetEditingOptionsAsync()
        {
            var settings = await apiRequestFactory
                .Create()
                .UseRoute("/api/Settings")
                .GetAsync<ServerProfileSettings>();

            return new ProfileEditingOptions
            {
                CanChangeAlias = settings?.Authentication?.AllowAliasChange ?? true,
                CanChangeAvatar = settings?.Authentication?.AllowAvatarUpload ?? false,
            };
        }

        /// <summary>Replaces the player's avatar with the image at <paramref name="path"/>.</summary>
        /// <exception cref="ProfileUpdateException">The server refused the image.</exception>
        public async Task UploadAvatarAsync(string path, CancellationToken cancellationToken = default)
        {
            logger?.LogTrace("Uploading avatar {Path}...", path);

            await using var stream = File.OpenRead(path);

            using var form = new MultipartFormDataContent();

            form.Add(new StreamContent(stream), "file", Path.GetFileName(path));

            var result = await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseCancellationToken(cancellationToken)
                .UseRoute("/api/Profile/Avatar")
                .UseMethod(HttpMethod.Post)
                .AddContent(form)
                .SendAsync<object>();

            await EnsureSuccessAsync(result.Response, "The avatar could not be uploaded.");
        }

        /// <exception cref="ProfileUpdateException">The server refused to remove it.</exception>
        public async Task RemoveAvatarAsync()
        {
            logger?.LogTrace("Removing avatar...");

            var result = await apiRequestFactory
                .Create()
                .UseAuthenticationToken()
                .UseVersioning()
                .UseRoute("/api/Profile/Avatar")
                .UseMethod(HttpMethod.Delete)
                .SendAsync<object>();

            await EnsureSuccessAsync(result.Response, "The avatar could not be removed.");
        }

        /// <summary>Throws with the server's own explanation, when it sent one as text.</summary>
        private static async Task EnsureSuccessAsync(HttpResponseMessage response, string fallbackMessage)
        {
            if (response.IsSuccessStatusCode)
                return;

            var message = response.StatusCode == HttpStatusCode.Forbidden
                ? "The server does not allow changing this."
                : fallbackMessage;

            if (response.Content.Headers.ContentType?.MediaType == "text/plain")
            {
                var body = await response.Content.ReadAsStringAsync();

                if (!String.IsNullOrWhiteSpace(body))
                    message = body;
            }

            throw new ProfileUpdateException(message, response.StatusCode);
        }

        // Only the slice of /api/Settings this client reads. Nullable, to tell a false setting
        // from one an older server doesn't publish.
        private class ServerProfileSettings
        {
            public AuthenticationFlags Authentication { get; set; }

            public class AuthenticationFlags
            {
                public bool? AllowAliasChange { get; set; }
                public bool? AllowAvatarUpload { get; set; }
            }
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
