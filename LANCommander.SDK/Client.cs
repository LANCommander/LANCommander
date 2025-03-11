using LANCommander.SDK.Exceptions;
using LANCommander.SDK.Models;
using LANCommander.SDK.PowerShell.Cmdlets;
using LANCommander.SDK.Services;
using Microsoft.Extensions.Logging;
using RestSharp;
using RestSharp.Interceptors;
using Semver;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Management.Automation.Internal;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Threading.Tasks;
using LANCommander.SDK.Extensions;

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
        public readonly LibraryService Library;
        public readonly DepotService Depot;
        public readonly SaveService Saves;
        public readonly RedistributableService Redistributables;
        public readonly ScriptService Scripts;
        public readonly ProfileService Profile;
        public readonly MediaService Media;
        public readonly LauncherService Launcher;
        public readonly IssueService Issues;
        public readonly LobbyService Lobbies;
        public readonly ServerService Servers;
        public readonly PlaySessionService PlaySessions;

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

        public EventHandler<Exception> OnError;
        
        public delegate void OnInstallProgressUpdateHandler(InstallProgress e);
        public event OnInstallProgressUpdateHandler OnInstallProgressUpdate;

        public Client(string baseUrl, string defaultInstallDirectory)
        {
            DefaultInstallDirectory = defaultInstallDirectory;

            Games = new GameService(this, DefaultInstallDirectory);
            Library = new LibraryService(this);
            Depot = new DepotService(this);
            Saves = new SaveService(this);
            Redistributables = new RedistributableService(this);
            Scripts = new ScriptService(this);
            Profile = new ProfileService(this);
            Media = new MediaService(this);
            Launcher = new LauncherService(this);
            Issues = new IssueService(this);
            Lobbies = new LobbyService(this);
            Servers = new ServerService(this);
            PlaySessions = new PlaySessionService(this);

            BaseCmdlet.Client = this;

            ChangeServerAddress(baseUrl);
        }

        public Client(string baseUrl, string defaultInstallDirectory, ILogger logger)
        {
            ChangeServerAddress(baseUrl);

            DefaultInstallDirectory = defaultInstallDirectory;

            Games = new GameService(this, DefaultInstallDirectory, logger);
            Library = new LibraryService(this, logger);
            Depot = new DepotService(this, logger);
            Saves = new SaveService(this, logger);
            Redistributables = new RedistributableService(this, logger);
            Scripts = new ScriptService(this, logger);
            Profile = new ProfileService(this, logger);
            Media = new MediaService(this, logger);
            Launcher = new LauncherService(this);
            Issues = new IssueService(this);
            Lobbies = new LobbyService(this, logger);
            Servers = new ServerService(this, logger);
            PlaySessions = new PlaySessionService(this, logger);

            BaseCmdlet.Client = this;

            Logger = logger;
        }

        public void ChangeServerAddress(string baseUrl)
        {
            if (!String.IsNullOrWhiteSpace(baseUrl))
            {
                var urisToTry = baseUrl.SuggestValidUris();

                foreach (var uri in urisToTry)
                {
                    Logger?.LogInformation("Attempting to find server at {ServerAddress}", uri.ToString());
                    
                    try
                    {
                        ApiClient = new RestClient(uri);

                        if (Ping())
                        {
                            BaseUrl = uri;

                            // Successful! Found our service
                            Logger?.LogInformation("Using server address {ServerAddress}", uri.ToString());

                            return;
                        }
                    }
                    catch
                    {
                        Logger?.LogError("Did not find server at {ServerAddress}", uri.ToString());
                    }
                }
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

        internal T PostRequest<T>(string route, object body, bool ignoreVersion = false)
        {
            try
            {
                if (Token == null)
                    return default;

                var request = new RestRequest(route)
                    .AddJsonBody(body)
                    .AddHeader("Authorization", $"Bearer {Token.AccessToken}")
                    .AddHeader("X-API-Version", GetCurrentVersion().ToString());

                if (!ignoreVersion)
                    request.Interceptors = new List<Interceptor>() { new VersionInterceptor() };

                var response = ApiClient.Post<T>(request);

                return response;
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex);

                return default;
            }
        }

        internal T PostRequest<T>(string route, bool ignoreVersion = false)
        {
            try
            {
                if (Token == null)
                    return default;

                var request = new RestRequest(route)
                    .AddHeader("Authorization", $"Bearer {Token.AccessToken}")
                    .AddHeader("X-API-Version", GetCurrentVersion().ToString());

                if (!ignoreVersion)
                    request.Interceptors = new List<Interceptor>() { new VersionInterceptor() };

                var response = ApiClient.Post<T>(request);

                return response;
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex);
                
                return default;
            }
        }

        internal async Task<T> PostRequestAsync<T>(string route, object body, bool ignoreVersion = false)
        {
            try
            {
                if (Token == null)
                    return default;

                var request = new RestRequest(route)
                    .AddJsonBody(body)
                    .AddHeader("Authorization", $"Bearer {Token.AccessToken}")
                    .AddHeader("X-API-Version", GetCurrentVersion().ToString());

                if (!ignoreVersion)
                    request.Interceptors = new List<Interceptor>() { new VersionInterceptor() };

                var response = await ApiClient.PostAsync<T>(request);

                return response;
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex);
                
                return default;
            }
        }

        internal async Task<T> PostRequestAsync<T>(string route, bool ignoreVersion = false)
        {
            try
            {
                if (Token == null)
                    return default;

                var request = new RestRequest(route)
                    .AddHeader("Authorization", $"Bearer {Token.AccessToken}")
                    .AddHeader("X-API-Version", GetCurrentVersion().ToString());

                if (!ignoreVersion)
                    request.Interceptors = new List<Interceptor>() { new VersionInterceptor() };

                var response = await ApiClient.PostAsync<T>(request);

                return response;
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex);
                
                return default;
            }
        }

        internal async Task<T> PutRequestAsync<T>(string route, object body, bool ignoreVersion = false)
        {
            try
            {
                if (Token == null)
                    return default;

                var request = new RestRequest(route)
                    .AddHeader("Authorization", $"Bearer {Token.AccessToken}")
                    .AddHeader("X-API-Version", GetCurrentVersion().ToString());

                if (!ignoreVersion)
                    request.Interceptors = new List<Interceptor>() { new VersionInterceptor() };

                var response = await ApiClient.PutAsync<T>(request);

                return response;
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex);
                
                return default;
            }
        }

        internal T GetRequest<T>(string route, bool ignoreVersion = false)
        {
            try
            {
                if (Token == null)
                    return default;

                var request = new RestRequest(route)
                    .AddHeader("Authorization", $"Bearer {Token.AccessToken}")
                    .AddHeader("X-API-Version", GetCurrentVersion().ToString());

                if (!ignoreVersion)
                    request.Interceptors = new List<Interceptor>() { new VersionInterceptor() };

                var response = ApiClient.Get<T>(request);

                return response;
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex);
                
                return default;
            }
        }

        internal async Task<T> GetRequestAsync<T>(string route, bool ignoreVersion = false)
        {
            try
            {
                if (Token == null)
                    return default;

                var request = new RestRequest(route)
                    .AddHeader("Authorization", $"Bearer {Token.AccessToken}")
                    .AddHeader("X-API-Version", GetCurrentVersion().ToString());

                if (!ignoreVersion)
                    request.Interceptors = new List<Interceptor>() { new VersionInterceptor() };

                var response = await ApiClient.GetAsync<T>(request);

                return response;
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex);
                
                return default;
            }
        }

        internal async Task<T> DeleteRequestAsync<T>(string route, bool ignoreVersion = false)
        {
            try
            {
                if (Token == null)
                    return default;

                var request = new RestRequest(route)
                    .AddHeader("Authorization", $"Bearer {Token.AccessToken}")
                    .AddHeader("X-API-Version", GetCurrentVersion().ToString());

                if (!ignoreVersion)
                    request.Interceptors = new List<Interceptor>() { new VersionInterceptor() };

                var response = await ApiClient.DeleteAsync<T>(request);

                return response;
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex);
                
                return default;
            }
        }

        internal async Task<string> DownloadRequestAsync(string route, Action<DownloadProgressChangedEventArgs> progressHandler, Action<AsyncCompletedEventArgs> completeHandler)
        {
            try
            {
                route = route.TrimStart('/');

                var client = new WebClient();
                var tempFile = Path.GetTempFileName();

                client.Headers.Add("Authorization", $"Bearer {Token.AccessToken}");
                client.Headers.Add("X-API-Version", GetCurrentVersion().ToString());
                client.DownloadProgressChanged += (s, e) => progressHandler(e);
                client.DownloadFileCompleted += (s, e) => completeHandler(e);

                try
                {
                    await client.DownloadFileTaskAsync(new Uri(BaseUrl, route), tempFile);
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "An unknown error occurred while downloading from the server at route {Route}", route);

                    if (File.Exists(tempFile))
                        File.Delete(tempFile);

                    tempFile = String.Empty;
                }

                return tempFile;
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex);
                
                return null;
            }
        }

        internal async Task<string> DownloadRequestAsync(string route, string destination)
        {
            try
            {
                route = route.TrimStart('/');

                var client = new WebClient();

                client.Headers.Add("Authorization", $"Bearer {Token.AccessToken}");
                client.Headers.Add("X-API-Version", GetCurrentVersion().ToString());

                try
                {
                    await client.DownloadFileTaskAsync(new Uri(BaseUrl, route), destination);
                }
                catch (Exception ex)
                {
                    Logger?.LogError(ex, "An unknown error occurred while downloading from the server at route {Route}",
                        route);

                    if (File.Exists(destination))
                        File.Delete(destination);

                    destination = String.Empty;
                }

                return destination;
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex);
                
                return null;
            }
        }

        internal TrackableStream StreamRequest(string route)
        {
            route = route.TrimStart('/');

            var client = new WebClient();

            client.Headers.Add("Authorization", $"Bearer {Token.AccessToken}");
            client.Headers.Add("X-API-Version", GetCurrentVersion().ToString());

            var ws = client.OpenRead(new Uri(BaseUrl, route));

            return new TrackableStream(ws, true, Convert.ToInt64(client.ResponseHeaders["Content-Length"]));
        }

        internal T UploadRequest<T>(string route, string fileName, byte[] data, bool ignoreVersion = false)
        {
            try
            {
                var request = new RestRequest(route, Method.Post)
                    .AddHeader("Authorization", $"Bearer {Token.AccessToken}")
                    .AddHeader("X-API-Version", GetCurrentVersion().ToString());

                if (!ignoreVersion)
                    request.Interceptors = new List<Interceptor>() { new VersionInterceptor() };

                request.AddFile(fileName, data, fileName);

                var response = ApiClient.Post<T>(request);

                return response;
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex);
                
                return default;
            }
        }

        internal async Task<T> UploadRequestAsync<T>(string route, string fileName, byte[] data, bool ignoreVersion = false)
        {
            try
            {
                var request = new RestRequest(route, Method.Post)
                    .AddHeader("Authorization", $"Bearer {Token.AccessToken}")
                    .AddHeader("X-API-Version", GetCurrentVersion().ToString());

                if (!ignoreVersion)
                    request.Interceptors = new List<Interceptor>() { new VersionInterceptor() };

                request.AddFile(fileName, data, fileName);

                var response = await ApiClient.PostAsync<T>(request);

                return response;
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex);
                
                return default;
            }
        }

        internal async Task<Guid> ChunkedUploadRequestAsync(string fileName, Stream stream, bool ignoreVersion = false)
        {
            try
            {
                var maxChunkSize = 1024 * 1024 * 50;
                var initResponse = await PostRequestAsync<UploadInitResponse>("/Upload/Init", ignoreVersion);

                var buffer = new byte[maxChunkSize];

                while (stream.Position < stream.Length)
                {
                    var chunkRequest = new UploadChunkRequest();

                    chunkRequest.Start = stream.Position;

                    if (stream.Position + maxChunkSize > stream.Length)
                    {
                        var bytes = stream.Length - stream.Position;

                        buffer = new byte[bytes];

                        await stream.ReadAsync(buffer, 0, (int)(stream.Length - stream.Position));
                    }
                    else
                        await stream.ReadAsync(buffer, 0, maxChunkSize);

                    chunkRequest.End = stream.Position;
                    chunkRequest.Total = stream.Length;
                    chunkRequest.File = buffer;
                    chunkRequest.Key = initResponse.Key;

                    await PostRequestAsync<object>("/Upload/Chunk", chunkRequest, ignoreVersion);
                }

                return initResponse.Key;
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex);
                
                return default;
            }
        }

        public async Task<AuthToken> AuthenticateAsync(string username, string password, bool ignoreVersion = false)
        {
            try
            {
                var request = new RestRequest("/api/Auth/Login", Method.Post);

                request.AddJsonBody(new AuthRequest()
                {
                    UserName = username,
                    Password = password
                });

                if (!ignoreVersion)
                    request.Interceptors = new List<Interceptor>() { new VersionInterceptor() };

                var response = await ApiClient.ExecuteAsync<AuthToken>(request);

                if (response.ErrorException != null)
                {
                    Logger?.LogError(response.ErrorException, "Authentication failed for user {UserName}", username);

                    throw new Exception(response.Content);
                }

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
                        Logger?.LogError("Authentication failed for user {UserName}: invalid username or password", username);
                        throw new WebException("Invalid username or password");

                    default:
                        Connected = false;
                        Logger?.LogError("Authentication failed for user {UserName}: could not communicate with the server", username);
                        throw new WebException("Could not communicate with the server");
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex);
                
                return default;
            }
        }

        public void Disconnect()
        {
            Connected = false;
        }

        public async Task LogoutAsync()
        {
            await ApiClient.ExecuteAsync(new RestRequest("/api/Auth/Logout", Method.Post));

            Connected = false;
            Token = null;
        }

        public async Task<AuthToken> RegisterAsync(string username, string password, string passwordConfirmation)
        {
            var response = await ApiClient.ExecuteAsync<AuthResponse>(new RestRequest("/api/Auth/Register", Method.Post).AddJsonBody(new AuthRequest()
            {
                UserName = username,
                Password = password
            }));

            if (response.ErrorException != null)
            {
                Logger?.LogError(response.ErrorException, "Registration failed for user {UserName}", username);

                if (!String.IsNullOrWhiteSpace(response?.Data?.Message))
                {
                    Logger?.LogError(response.Data.Message);
                    
                    OnError?.Invoke(this, response.ErrorException);

                    throw new Exception(response.Data.Message);
                }
                else
                    throw response.ErrorException;
            }

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

        public async Task<IEnumerable<AuthenticationProvider>> GetAuthenticationProvidersAsync()
        {
            var request = new RestRequest("/api/Auth/AuthenticationProviders")
                .AddHeader("X-API-Version", GetCurrentVersion().ToString());

            var response = await ApiClient.GetAsync<IEnumerable<AuthenticationProvider>>(request);

            return response;
        }

        public string GetAuthenticationProviderLoginUrl(string provider)
        {
            return $"{BaseUrl}api/Auth/Login?Provider={provider}";
        }

        public bool Ping()
        {
            try
            {
                var guid = Guid.NewGuid().ToString();
                var request = new RestRequest("/api/Ping", Method.Head);
                
                request.AddHeader("X-Ping", guid);

                var response = ApiClient.Execute(request);

                return response.StatusCode == HttpStatusCode.OK && response.GetHeaderValue("X-Pong") == guid.FastReverse();
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> PingAsync()
        {
            try
            {
                var guid = Guid.NewGuid().ToString();
                var request = new RestRequest("/api/Ping", Method.Head);
                
                request.AddHeader("X-Ping", guid);

                var response = await ApiClient.ExecuteAsync(request);

                return response.StatusCode == HttpStatusCode.OK && response.GetHeaderValue("X-Pong") == guid.FastReverse();
            }
            catch
            {
                return false;
            }
        }

        public bool ValidateToken()
        {
            return ValidateToken(Token);
        }

        public bool ValidateToken(AuthToken token, bool ignoreVersion = false)
        {
            Logger?.LogTrace("Validating token...");

            if (token == null)
            {
                Logger?.LogTrace("Token is null!");
                return false;
            }

            var request = new RestRequest("/api/Auth/Validate")
                .AddHeader("Authorization", $"Bearer {Token.AccessToken}")
                .AddHeader("X-API-Version", GetCurrentVersion().ToString());

            if (!ignoreVersion)
                request.Interceptors = new List<Interceptor>() { new VersionInterceptor() };

            if (String.IsNullOrEmpty(token.AccessToken) || String.IsNullOrEmpty(token.RefreshToken))
            {
                Logger?.LogTrace("Token is empty!");
                return false;
            }

            try
            {
                var response = ApiClient.Post(request);

                var valid = response.StatusCode == HttpStatusCode.OK;

                if (valid)
                    Logger?.LogTrace("Token is valid!");
                else
                    Logger?.LogTrace("Token is invalid!");

                Connected = valid;

                return response.StatusCode == HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                Logger?.LogTrace(ex, "Token could not be retrieved");

                return false;
            }
        }

        public async Task<bool> ValidateTokenAsync()
        {
            return await ValidateTokenAsync(Token);
        }

        public async Task<bool> ValidateTokenAsync(AuthToken token, bool ignoreVersion = false)
        {
            Logger?.LogTrace("Validating token...");

            if (token == null)
            {
                Logger?.LogTrace("Token is null!");
                return false;
            }

            var request = new RestRequest("/api/Auth/Validate")
                .AddHeader("Authorization", $"Bearer {token.AccessToken}")
                .AddHeader("X-API-Version", GetCurrentVersion().ToString());

            if (!ignoreVersion)
                request.Interceptors = new List<Interceptor>() { new VersionInterceptor() };

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
            catch (Exception ex)
            {
                Logger?.LogTrace(ex, "Could not validate token");

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

        internal async Task<string> GetIPXRelayHostAsync()
        {
            var host = Settings.IPXRelayHost;

            if (String.IsNullOrWhiteSpace(host))
            {
                var serverAddress = new Uri(GetServerAddress());

                host = serverAddress.DnsSafeHost;
            }

            var entry = await Dns.GetHostEntryAsync(host);

            if (entry.AddressList.Length > 0)
                host = entry.AddressList.First().ToString();

            return host;
        }
    }
}
