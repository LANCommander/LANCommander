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

        public string BaseUrl;

        public Client(string baseUrl)
        {
            BaseUrl = baseUrl;

            if (!String.IsNullOrWhiteSpace(BaseUrl))
                ApiClient = new RestClient(BaseUrl);
        }

        public Client(string baseUrl, ILogger logger)
        {
            BaseUrl = baseUrl;

            if (!String.IsNullOrWhiteSpace(BaseUrl))
                ApiClient = new RestClient(BaseUrl);

            Logger = logger;
        }

        private T PostRequest<T>(string route, object body)
        {
            var request = new RestRequest(route)
                .AddJsonBody(body)
                .AddHeader("Authorization", $"Bearer {Token.AccessToken}");

            var response = ApiClient.Post<T>(request);

            return response.Data;
        }

        private T PostRequest<T>(string route)
        {
            var request = new RestRequest(route)
                .AddHeader("Authorization", $"Bearer {Token.AccessToken}");

            var response = ApiClient.Post<T>(request);

            return response.Data;
        }

        private T GetRequest<T>(string route)
        {
            var request = new RestRequest(route)
                .AddHeader("Authorization", $"Bearer {Token.AccessToken}");

            var response = ApiClient.Get<T>(request);

            return response.Data;
        }

        private string DownloadRequest(string route, Action<DownloadProgressChangedEventArgs> progressHandler, Action<AsyncCompletedEventArgs> completeHandler)
        {
            route = route.TrimStart('/');

            var client = new WebClient();
            var tempFile = Path.GetTempFileName();

            client.Headers.Add("Authorization", $"Bearer {Token.AccessToken}");
            client.DownloadProgressChanged += (s, e) => progressHandler(e);
            client.DownloadFileCompleted += (s, e) => completeHandler(e);

            client.DownloadFileAsync(new Uri($"{ApiClient.BaseUrl}{route}"), tempFile);

            return tempFile;
        }

        private TrackableStream StreamRequest(string route)
        {
            route = route.TrimStart('/');

            var client = new WebClient();
            var tempFile = Path.GetTempFileName();

            client.Headers.Add("Authorization", $"Bearer {Token.AccessToken}");

            var ws = client.OpenRead(new Uri($"{ApiClient.BaseUrl}{route}"));

            return new TrackableStream(ws, true, Convert.ToInt64(client.ResponseHeaders["Content-Length"]));
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
                    Token = new AuthToken
                    {
                        AccessToken = response.Data.AccessToken,
                        RefreshToken = response.Data.RefreshToken,
                        Expiration = response.Data.Expiration
                    };

                    return Token;

                case HttpStatusCode.Forbidden:
                case HttpStatusCode.BadRequest:
                case HttpStatusCode.Unauthorized:
                    throw new WebException("Invalid username or password");

                default:
                    throw new WebException("Could not communicate with the server");
            }
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

                    return Token;

                case HttpStatusCode.BadRequest:
                case HttpStatusCode.Forbidden:
                case HttpStatusCode.Unauthorized:
                    throw new WebException(response.Data.Message);

                default:
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

            return response.StatusCode == HttpStatusCode.OK;
        }

        public void UseToken(AuthToken token)
        {
            Token = token;
        }

        public void UseServerAddress(string address)
        {
            BaseUrl = address;
            ApiClient = new RestClient(BaseUrl);
        }

        public IEnumerable<Game> GetGames()
        {
            return GetRequest<IEnumerable<Game>>("/api/Games");
        }

        public Game GetGame(Guid id)
        {
            return GetRequest<Game>($"/api/Games/{id}");
        }

        public GameManifest GetGameManifest(Guid id)
        {
            return GetRequest<GameManifest>($"/api/Games/{id}/Manifest");
        }

        public string DownloadGame(Guid id, Action<DownloadProgressChangedEventArgs> progressHandler, Action<AsyncCompletedEventArgs> completeHandler)
        {
            return DownloadRequest($"/api/Games/{id}/Download", progressHandler, completeHandler);
        }

        public TrackableStream StreamGame(Guid id)
        {
            return StreamRequest($"/api/Games/{id}/Download");
        }

        public string DownloadArchive(Guid id, Action<DownloadProgressChangedEventArgs> progressHandler, Action<AsyncCompletedEventArgs> completeHandler)
        {
            return DownloadRequest($"/api/Archives/Download/{id}", progressHandler, completeHandler);
        }

        public TrackableStream StreamRedistributable(Guid id)
        {
            return StreamRequest($"/api/Redistributables/{id}/Download");
        }

        public string DownloadSave(Guid id, Action<DownloadProgressChangedEventArgs> progressHandler, Action<AsyncCompletedEventArgs> completeHandler)
        {
            return DownloadRequest($"/api/Saves/Download/{id}", progressHandler, completeHandler);
        }

        public string DownloadLatestSave(Guid gameId, Action<DownloadProgressChangedEventArgs> progressHandler, Action<AsyncCompletedEventArgs> completeHandler)
        {
            return DownloadRequest($"/api/Saves/DownloadLatest/{gameId}", progressHandler, completeHandler);
        }

        public GameSave UploadSave(string gameId, byte[] data)
        {
            Logger?.LogTrace("Uploading save...");

            var request = new RestRequest($"/api/Saves/Upload/{gameId}", Method.POST)
                .AddHeader("Authorization", $"Bearer {Token.AccessToken}");

            request.AddFile(gameId, data, gameId);

            var response = ApiClient.Post<GameSave>(request);

            return response.Data;
        }

        public string GetMediaUrl(Media media)
        {
            return (new Uri(ApiClient.BaseUrl, $"/api/Media/{media.Id}/Download?fileId={media.FileId}").ToString());
        }

        public string GetKey(Guid id)
        {
            Logger?.LogTrace("Requesting key allocation...");

            var macAddress = GetMacAddress();

            var request = new KeyRequest()
            {
                GameId = id,
                MacAddress = macAddress,
                ComputerName = Environment.MachineName,
                IpAddress = GetIpAddress(),
            };

            var response = PostRequest<Key>($"/api/Keys/Get", request);

            return response.Value;
        }

        public string GetAllocatedKey(Guid id)
        {
            Logger?.LogTrace("Requesting allocated key...");

            var macAddress = GetMacAddress();

            var request = new KeyRequest()
            {
                GameId = id,
                MacAddress = macAddress,
                ComputerName = Environment.MachineName,
                IpAddress = GetIpAddress(),
            };

            var response = PostRequest<Key>($"/api/Keys/GetAllocated/{id}", request);

            if (response == null)
                return String.Empty;

            return response.Value;
        }

        public string GetNewKey(Guid id)
        {
            Logger?.LogTrace("Requesting new key allocation...");

            var macAddress = GetMacAddress();

            var request = new KeyRequest()
            {
                GameId = id,
                MacAddress = macAddress,
                ComputerName = Environment.MachineName,
                IpAddress = GetIpAddress(),
            };

            var response = PostRequest<Key>($"/api/Keys/Allocate/{id}", request);

            if (response == null)
                return String.Empty;

            return response.Value;
        }

        public User GetProfile()
        {
            Logger?.LogTrace("Requesting player's profile...");

            return GetRequest<User>("/api/Profile");
        }

        public string ChangeAlias(string alias)
        {
            Logger?.LogTrace("Requesting to change player alias...");

            var response = PostRequest<object>("/api/Profile/ChangeAlias", alias);

            return alias;
        }

        public void StartPlaySession(Guid gameId)
        {
            Logger?.LogTrace("Starting a game session...");

            PostRequest<object>($"/api/PlaySession/Start/{gameId}");
        }

        public void EndPlaySession(Guid gameId)
        {
            Logger?.LogTrace("Ending a game session...");

            PostRequest<object>($"/api/PlaySession/End/{gameId}");
        }

        private string GetMacAddress()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Select(nic => nic.GetPhysicalAddress().ToString())
                .FirstOrDefault();
        }

        private string GetIpAddress()
        {
            return Dns.GetHostEntry(Dns.GetHostName()).AddressList[0].ToString();
        }
    }
}
