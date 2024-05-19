using LANCommander.SDK;
using LANCommander.SDK.Exceptions;
using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using RestSharp;
using Semver;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Management.Automation.Internal;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Threading.Tasks;

namespace LANCommander.SDK
{
    public class Client
    {
        private readonly ILogger Logger;

        private RestClient ApiClient;
        private AuthToken Token;

        private bool Connected = false;

        public Uri BaseUrl;
        public string DefaultInstallDirectory;

        public readonly GameService Games;
        public readonly SaveService Saves;
        public readonly RedistributableService Redistributables;
        public readonly ActionService Actions;
        public readonly ProfileService Profile;
        public readonly MediaService Media;

        private Settings _Settings { get; set; }
        public Settings Settings
        {
            get
            {
                if (_Settings == null)
                    _Settings = GetSettings();

                return _Settings;
            }
        }

        public Client(string baseUrl, string defaultInstallDirectory)
        {
            DefaultInstallDirectory = defaultInstallDirectory;

            Games = new GameService(this, DefaultInstallDirectory);
            Saves = new SaveService(this);
            Redistributables = new RedistributableService(this);
            Actions = new ActionService(this);
            Profile = new ProfileService(this);
            Media = new MediaService(this);

            ChangeServerAddress(baseUrl);
        }

        public Client(string baseUrl, string defaultInstallDirectory, ILogger logger)
        {
            ChangeServerAddress(baseUrl);

            DefaultInstallDirectory = defaultInstallDirectory;

            Games = new GameService(this, DefaultInstallDirectory, logger);
            Saves = new SaveService(this, logger);
            Redistributables = new RedistributableService(this, logger);
            Actions = new ActionService(this);
            Profile = new ProfileService(this, logger);
            Media = new MediaService(this, logger);

            Logger = logger;
        }

        public void ChangeServerAddress(string baseUrl)
        {
            if (!String.IsNullOrWhiteSpace(baseUrl))
            {
                BaseUrl = new Uri(baseUrl);
                ApiClient = new RestClient(BaseUrl);

                ApiClient.ThrowOnAnyError = true;
            }
        }

        public bool IsConnected()
        {
            return Connected;
        }

        public static SemVersion GetCurrentVersion()
        {
            return SemVersion.FromVersion(Assembly.GetExecutingAssembly().GetName().Version);
        }

        private void ValidateVersion(IRestResponse response)
        {
            var version = GetCurrentVersion();
            var header = response.Headers.FirstOrDefault(h => h.Name == "X-API-Version");

            if (header == null)
                throw new ApiVersionMismatchException(version, null, $"The server is out of date and does not support client version {version}.");

            var apiVersion = SemVersion.Parse((string)header.Value, SemVersionStyles.Any);

            if (version.Major != apiVersion.Major || version.Minor != apiVersion.Minor)
            {
                switch (version.ComparePrecedenceTo(apiVersion))
                {
                    case -1:
                        throw new ApiVersionMismatchException(version, apiVersion, $"Your client (v{version}) is out of date and is not supported by the server (v{apiVersion})");
                    case 1:
                        throw new ApiVersionMismatchException(version, apiVersion, $"Your client (v{version}) is on a version not supported by the server (v{apiVersion})");
                }
            }
        }

        internal T PostRequest<T>(string route, object body)
        {
            if (Token == null)
                return default;

            var request = new RestRequest(route)
                .AddJsonBody(body)
                .AddHeader("Authorization", $"Bearer {Token.AccessToken}");

            request.OnBeforeDeserialization += ValidateVersion;

            var response = ApiClient.Post<T>(request);

            return response.Data;
        }

        internal T PostRequest<T>(string route)
        {
            if (Token == null)
                return default;

            var request = new RestRequest(route)
                .AddHeader("Authorization", $"Bearer {Token.AccessToken}");

            request.OnBeforeDeserialization += ValidateVersion;

            var response = ApiClient.Post<T>(request);

            return response.Data;
        }

        internal async Task<T> PostRequestAsync<T>(string route, object body)
        {
            if (Token == null)
                return default;

            var request = new RestRequest(route)
                .AddJsonBody(body)
                .AddHeader("Authorization", $"Bearer {Token.AccessToken}");

            request.OnBeforeDeserialization += ValidateVersion;

            var response = await ApiClient.PostAsync<T>(request);

            return response;
        }

        internal async Task<T> PostRequestAsync<T>(string route)
        {
            if (Token == null)
                return default;

            var request = new RestRequest(route)
                .AddHeader("Authorization", $"Bearer {Token.AccessToken}");

            request.OnBeforeDeserialization += ValidateVersion;

            var response = await ApiClient.PostAsync<T>(request);

            return response;
        }

        internal T GetRequest<T>(string route)
        {
            if (Token == null)
                return default;

            var request = new RestRequest(route)
                .AddHeader("Authorization", $"Bearer {Token.AccessToken}");

            request.OnBeforeDeserialization += ValidateVersion;

            var response = ApiClient.Get<T>(request);

            return response.Data;
        }

        internal async Task<T> GetRequestAsync<T>(string route)
        {
            if (Token == null)
                return default;

            var request = new RestRequest(route)
                .AddHeader("Authorization", $"Bearer {Token.AccessToken}");

            request.OnBeforeDeserialization += ValidateVersion;

            var response = await ApiClient.GetAsync<T>(request);

            return response;
        }

        internal async Task<string> DownloadRequestAsync(string route, Action<DownloadProgressChangedEventArgs> progressHandler, Action<AsyncCompletedEventArgs> completeHandler)
        {
            route = route.TrimStart('/');

            var client = new WebClient();
            var tempFile = Path.GetTempFileName();

            client.Headers.Add("Authorization", $"Bearer {Token.AccessToken}");
            client.DownloadProgressChanged += (s, e) => progressHandler(e);
            client.DownloadFileCompleted += (s, e) => completeHandler(e);

            try
            {
                await client.DownloadFileTaskAsync(new Uri(BaseUrl, route), tempFile);
            }
            catch (Exception ex)
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);

                tempFile = String.Empty;
            }

            return tempFile;
        }

        internal async Task<string> DownloadRequestAsync(string route, string destination)
        {
            route = route.TrimStart('/');

            var client = new WebClient();

            client.Headers.Add("Authorization", $"Bearer {Token.AccessToken}");

            try
            {
                await client.DownloadFileTaskAsync(new Uri(BaseUrl, route), destination);
            }
            catch (Exception ex)
            {
                if (File.Exists(destination))
                    File.Delete(destination);

                destination = String.Empty;
            }

            return destination;
        }

        internal TrackableStream StreamRequest(string route)
        {
            route = route.TrimStart('/');

            var client = new WebClient();

            client.Headers.Add("Authorization", $"Bearer {Token.AccessToken}");

            var ws = client.OpenRead(new Uri(BaseUrl, route));

            return new TrackableStream(ws, true, Convert.ToInt64(client.ResponseHeaders["Content-Length"]));
        }

        internal T UploadRequest<T>(string route, string fileName, byte[] data)
        {
            var request = new RestRequest(route, Method.POST)
                .AddHeader("Authorization", $"Bearer {Token.AccessToken}");

            request.OnBeforeDeserialization += ValidateVersion;

            request.AddFile(fileName, data, fileName);

            var response = ApiClient.Post<T>(request);

            return response.Data;
        }

        internal async Task<T> UploadRequestAsync<T>(string route, string fileName, byte[] data)
        {
            var request = new RestRequest(route, Method.POST)
                .AddHeader("Authorization", $"Bearer {Token.AccessToken}");

            request.OnBeforeDeserialization += ValidateVersion;

            request.AddFile(fileName, data, fileName);

            var response = await ApiClient.PostAsync<T>(request);

            return response;
        }

        public async Task<AuthToken> AuthenticateAsync(string username, string password)
        {
            var response = await ApiClient.ExecuteAsync<AuthResponse>(new RestRequest("/api/Auth", Method.POST).AddJsonBody(new AuthRequest()
            {
                UserName = username,
                Password = password
            }));

            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    var token = new AuthToken
                    {
                        AccessToken = response.Data.AccessToken,
                        RefreshToken = response.Data.RefreshToken,
                        Expiration = response.Data.Expiration
                    };

                    UseToken(token);

                    Connected = true;

                    return token;

                case HttpStatusCode.Forbidden:
                case HttpStatusCode.BadRequest:
                case HttpStatusCode.Unauthorized:
                    Connected = false;
                    throw new WebException("Invalid username or password");

                default:
                    Connected = false;
                    throw new WebException("Could not communicate with the server");
            }
        }

        public async Task LogoutAsync()
        {
            await ApiClient.ExecuteAsync(new RestRequest("/api/Auth/Logout", Method.POST));

            Connected = false;
            Token = null;
        }

        public async Task<AuthToken> RegisterAsync(string username, string password)
        {
            var response = await ApiClient.ExecuteAsync<AuthResponse>(new RestRequest("/api/auth/register", Method.POST).AddJsonBody(new AuthRequest()
            {
                UserName = username,
                Password = password
            }));

            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    Token = new AuthToken
                    {
                        AccessToken = response.Data.AccessToken,
                        RefreshToken = response.Data.RefreshToken,
                        Expiration = response.Data.Expiration
                    };

                    Connected = true;

                    return Token;

                case HttpStatusCode.BadRequest:
                case HttpStatusCode.Forbidden:
                case HttpStatusCode.Unauthorized:
                    Connected = false;
                    throw new WebException(response.Data.Message);

                default:
                    Connected = false;
                    throw new WebException("Could not communicate with the server");
            }
        }

        public async Task<bool> PingAsync()
        {
            var response = await ApiClient.ExecuteAsync(new RestRequest("/api/Ping", Method.GET));

            return response.StatusCode == HttpStatusCode.OK;
        }

        public AuthToken RefreshToken(AuthToken token)
        {
            Logger?.LogTrace("Refreshing token...");

            var request = new RestRequest("/api/Auth/Refresh")
                .AddJsonBody(token);

            request.OnBeforeDeserialization += ValidateVersion;

            var response = ApiClient.Post<AuthResponse>(request);

            if (response.StatusCode != HttpStatusCode.OK)
                throw new WebException(response.ErrorMessage);

            Token = new AuthToken
            {
                AccessToken = response.Data.AccessToken,
                RefreshToken = response.Data.RefreshToken,
                Expiration = response.Data.Expiration
            };

            Connected = true;

            return Token;
        }

        public bool ValidateToken()
        {
            return ValidateToken(Token);
        }

        public bool ValidateToken(AuthToken token)
        {
            Logger?.LogTrace("Validating token...");

            if (token == null)
            {
                Logger?.LogTrace("Token is null!");
                return false;
            }

            var request = new RestRequest("/api/Auth/Validate")
                .AddHeader("Authorization", $"Bearer {token.AccessToken}");

            request.OnBeforeDeserialization += ValidateVersion;

            if (String.IsNullOrEmpty(token.AccessToken) || String.IsNullOrEmpty(token.RefreshToken))
            {
                Logger?.LogTrace("Token is empty!");
                return false;
            }

            var response = ApiClient.Post(request);

            var valid = response.StatusCode == HttpStatusCode.OK;

            if (valid)
                Logger?.LogTrace("Token is valid!");
            else
                Logger?.LogTrace("Token is invalid!");

            Connected = valid;

            return response.StatusCode == HttpStatusCode.OK;
        }

        public async Task<bool> ValidateTokenAsync(AuthToken token)
        {
            Logger?.LogTrace("Validating token...");

            if (token == null)
            {
                Logger?.LogTrace("Token is null!");
                return false;
            }

            var request = new RestRequest("/api/Auth/Validate")
                .AddHeader("Authorization", $"Bearer {token.AccessToken}");

            request.OnBeforeDeserialization += ValidateVersion;

            if (String.IsNullOrEmpty(token.AccessToken) || String.IsNullOrEmpty(token.RefreshToken))
            {
                Logger?.LogTrace("Token is empty!");
                return false;
            }

            try
            {
                var response = await ApiClient.PostAsync<object>(request);

                Logger?.LogTrace("Token is valid!");

                Connected = true;
            }
            catch
            {
                Logger?.LogTrace("Token is invalid!");

                Connected = false;
            }

            return Connected;
        }

        public void UseToken(AuthToken token)
        {
            Token = token;
        }

        public void UseServerAddress(string address)
        {
            BaseUrl = new Uri(address);
            ApiClient = new RestClient(BaseUrl);
        }

        public string GetServerAddress()
        {
            return BaseUrl.ToString();
        }

        public Settings GetSettings()
        {
            return GetRequest<Settings>($"/api/Settings");
        }

        internal string GetMacAddress()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Select(nic => nic.GetPhysicalAddress().ToString())
                .FirstOrDefault();
        }

        internal string GetIpAddress()
        {
            return Dns.GetHostEntry(Dns.GetHostName()).AddressList[0].ToString();
        }
    }
}
