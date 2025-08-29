using LANCommander.SDK.Models;
using LANCommander.SDK.PowerShell.Cmdlets;
using LANCommander.SDK.Services;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Exceptions;

using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RestSharp;
using RestSharp.Interceptors;
using Semver;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Reflection;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using LANCommander.SDK.Rpc;

namespace LANCommander.SDK
{
    public class Client
    {
        private readonly ILogger Logger;

        private RestClient ApiClient;
        private AuthToken Token;

        private bool Connected = false;
        private bool IgnoreVersion = false;

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
        public readonly TagService Tags;
        public readonly BeaconService Beacon;
        public readonly ChatService Chat;
        public readonly RpcClient RPC;

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
            Tags = new TagService(this);
            Beacon = new BeaconService(this);
            Chat = new ChatService(this);

            BaseCmdlet.Client = this;

            try
            {
                ConfigureServerAddress(baseUrl);
            }
            catch
            {
            }
        }

        public Client(string baseUrl, string defaultInstallDirectory, ILogger logger)
        {
            try
            {
                ConfigureServerAddress(baseUrl);
            }
            catch
            {
            }

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
            Tags = new TagService(this, logger);
            Beacon = new BeaconService(this, logger);
            Chat = new ChatService(this);
            RPC = new RpcClient(this);

            BaseCmdlet.Client = this;

            Logger = logger;
        }

        // Constructor for tests
        internal Client(HttpClient httpClient, string defaultInstallDirectory)
        {
            ApiClient = new RestClient(httpClient);
            
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
            Tags = new TagService(this);
            RPC = new RpcClient(this);

            IgnoreVersion = true;

            BaseCmdlet.Client = this;
        }

        public void ConfigureServerAddress(string baseUrl)
        {
            if (!String.IsNullOrWhiteSpace(baseUrl))
            {
                var urisToTry = baseUrl.SuggestValidUris();

                foreach (var uri in urisToTry)
                {
                    Logger?.LogInformation("Attempting to configure server at {ServerAddress}", uri.ToString());

                    try
                    {
                        ApiClient = new RestClient(uri);
                        BaseUrl = uri;

                        // Successful! Found our service
                        Logger?.LogInformation("Using server address {ServerAddress}", uri.ToString());

                        return;
                    }
                    catch
                    {
                        Logger?.LogError("Could not configure server at {ServerAddress}", uri.ToString());
                    }
                }

                throw new Exception("Could not configure a server at that address");
            }
        }

        public async Task ChangeServerAddressAsync(string baseUrl)
        {
            if (!String.IsNullOrWhiteSpace(baseUrl))
            {
                var urisToTry = baseUrl.SuggestValidUris();

                // if url is fully qualified, limit specific urls
                if (Uri.TryCreate(baseUrl, UriKind.RelativeOrAbsolute, out var baseUri))
                {
                    var hasPort = baseUrl.Replace(Uri.SchemeDelimiter, "").Contains(':');
                    if (hasPort)
                    {
                        urisToTry = urisToTry.Take(baseUri.IsAbsoluteUri ? 1 : 2);
                    }
                }

                foreach (var uri in urisToTry)
                {
                    Logger?.LogInformation("Attempting to find server at {ServerAddress}", uri.ToString());
                    
                    try
                    {
                        ApiClient = new RestClient(uri);

                        if (await PingAsync())
                        {
                            BaseUrl = uri;

                            // Successful! Found our service
                            Logger?.LogInformation("Using server address {ServerAddress}", uri.ToString());

                            await RPC.ConnectAsync();

                            return;
                        }
                    }
                    catch
                    {
                        Logger?.LogError("Did not find server at {ServerAddress}", uri.ToString());
                    }
                }

                throw new Exception("Could not find a server at that address");
            }
        }

        public bool IsConfigured()
        {
            return ApiClient != null && !string.IsNullOrWhiteSpace(BaseUrl?.ToString());
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

                if (!ignoreVersion && !IgnoreVersion)
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

                if (!ignoreVersion && !IgnoreVersion)
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

                if (!ignoreVersion && !IgnoreVersion)
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

                if (!ignoreVersion && !IgnoreVersion)
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

                if (!ignoreVersion && !IgnoreVersion)
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

                if (!ignoreVersion && !IgnoreVersion)
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

                if (!ignoreVersion && !IgnoreVersion)
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

                if (!ignoreVersion && !IgnoreVersion)
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

                if (!ignoreVersion && !IgnoreVersion)
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

        internal async Task<T> UploadRequestAsync<T>(string route, Stream stream, bool ignoreVersion = false)
        {
            try
            {
                var request = new RestRequest(route, Method.Post)
                    .AddHeader("Authorization", $"Bearer {Token.AccessToken}")
                    .AddHeader("X-API-Version", GetCurrentVersion().ToString());

                if (!ignoreVersion && !IgnoreVersion)
                    request.Interceptors = new List<Interceptor>() { new VersionInterceptor() };

                request.AddFile("File", () => stream, "File");

                var response = await ApiClient.PostAsync<T>(request);

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

                if (!ignoreVersion && !IgnoreVersion)
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

                if (!ignoreVersion && !IgnoreVersion)
                    request.Interceptors = new List<Interceptor>() { new VersionInterceptor() };

                var response = await ApiClient.ExecuteAsync<AuthToken>(request);

                ErrorResponse errorResponse = null;
                if (response.ResponseStatus == ResponseStatus.Error || response.ErrorException != null)
                {
                    string message = response.ErrorMessage ?? response?.ErrorException.Message;
                    Logger?.LogError(response.ErrorException, "Authentication failed for user {UserName}: {Message}", username, message);
                    errorResponse = ParseErrorResponse(response);
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
                        throw new AuthFailedException(AuthFailedException.AuthenticationErrorCode.InvalidCredentials, "Invalid username or password", errorData: errorResponse, innerException: response.ErrorException);

                    default:
                        Connected = false;
                        Logger?.LogError("Authentication failed for user {UserName}: could not communicate with the server", username);
                        throw new WebException("Could not communicate with the server", innerException: response.ErrorException);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex);
                
                throw;
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
            try
            {
                var request = new RestRequest("/api/Auth/Register", Method.Post);
                request.AddJsonBody(new AuthRequest()
                {
                    UserName = username,
                    Password = password
                });

                var response = await ApiClient.ExecuteAsync<AuthResponse>(request);

                ErrorResponse errorResponse = null;
                if (response.ResponseStatus == ResponseStatus.Error || response.ErrorException != null)
                {
                    string message = response.ErrorMessage ?? response?.ErrorException.Message;
                    Logger?.LogError(response.ErrorException, "Registration failed for user {UserName}: {Message}", username, message);
                    errorResponse = ParseErrorResponse(response);
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
                        throw new RegisterFailedException(response.Data.Message, errorData: errorResponse, innerException: response.ErrorException);

                    default:
                        Connected = false;
                        Logger?.LogError("Registering failed for user {UserName}: could not communicate with the server", username);
                        throw new WebException("Could not communicate with the server", innerException: response.ErrorException);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex);

                throw;
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

                // specify timeout for ping response
                request.Timeout = TimeSpan.FromSeconds(4);

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

            // specify timeout for auth response
            request.Timeout = TimeSpan.FromSeconds(8);

            if (!ignoreVersion && !IgnoreVersion)
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

            // specify timeout for auth response
            request.Timeout = TimeSpan.FromSeconds(8);

            if (!ignoreVersion && !IgnoreVersion)
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

        internal ErrorResponse ParseErrorResponse(RestResponse response, bool defaultToGenericResponse = false)
        {
            ErrorResponse errorResponse = null;

            // Try to deserialize the error response.
            try
            {
                errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(response.Content);
                return errorResponse;
            }
            catch (Exception deserializationEx)
            {
                // Log error and create a fallback message if deserialization fails.
                if (defaultToGenericResponse)
                {
                    Logger?.LogError(deserializationEx, "Error deserializing error response for route {Route}", response.Request);
                    errorResponse = new ErrorResponse
                    {
                        Message = "Could not process the server response."
                    };
                }
            }

            return errorResponse;
        }
    }
}
