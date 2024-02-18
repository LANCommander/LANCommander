using LANCommander.SDK;
using LANCommander.SDK.Models;
using Microsoft.Extensions.Logging;
using RestSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
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
            BaseUrl = new Uri(baseUrl);
            DefaultInstallDirectory = defaultInstallDirectory;

            Games = new GameService(this, DefaultInstallDirectory);
            Saves = new SaveService(this);
            Redistributables = new RedistributableService(this);
            Actions = new ActionService(this);
            Profile = new ProfileService(this);

            if (!String.IsNullOrWhiteSpace(baseUrl))
                ApiClient = new RestClient(BaseUrl);
        }

        public Client(string baseUrl, string defaultInstallDirectory, ILogger logger)
        {
            if (!String.IsNullOrWhiteSpace(baseUrl))
            {
                BaseUrl = new Uri(baseUrl);
                ApiClient = new RestClient(BaseUrl);
            }

            DefaultInstallDirectory = defaultInstallDirectory;

            Games = new GameService(this, DefaultInstallDirectory, logger);
            Saves = new SaveService(this, logger);
            Redistributables = new RedistributableService(this, logger);
            Actions = new ActionService(this);
            Profile = new ProfileService(this, logger);

            Logger = logger;
        }

        public bool IsConnected()
        {
            return Connected;
        }

        internal T PostRequest<T>(string route, object body)
        {
            if (Token == null)
                return default;

            var request = new RestRequest(route)
                .AddJsonBody(body)
                .AddHeader("Authorization", $"Bearer {Token.AccessToken}");

            var response = ApiClient.Post<T>(request);

            return response.Data;
        }

        internal T PostRequest<T>(string route)
        {
            if (Token == null)
                return default;

            var request = new RestRequest(route)
                .AddHeader("Authorization", $"Bearer {Token.AccessToken}");

            var response = ApiClient.Post<T>(request);

            return response.Data;
        }

        internal T GetRequest<T>(string route)
        {
            if (Token == null)
                return default;

            var request = new RestRequest(route)
                .AddHeader("Authorization", $"Bearer {Token.AccessToken}");

            var response = ApiClient.Get<T>(request);

            return response.Data;
        }

        internal async Task<string> DownloadRequest(string route, Action<DownloadProgressChangedEventArgs> progressHandler, Action<AsyncCompletedEventArgs> completeHandler)
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

            request.AddFile(fileName, data, fileName);

            var response = ApiClient.Post<T>(request);

            return response.Data;
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

        public string GetMediaUrl(Media media)
        {
            return (new Uri(BaseUrl, $"/api/Media/{media.Id}/Download?fileId={media.FileId}").ToString());
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
