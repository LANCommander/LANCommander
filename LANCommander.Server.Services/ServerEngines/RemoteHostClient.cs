using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LANCommander.SDK.Models;
using LANCommander.Server.Services.Enums;
using LANCommander.Server.Services.Models;
using LANCommander.Server.Settings.Enums;
using LANCommander.Server.Settings.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services.ServerEngines;

/// <summary>
/// Talks to the other LANCommander servers configured as Remote server engines, signed in with the
/// account saved for each. Shared by everything that reaches them, so each host's tokens are refreshed
/// by one caller at a time: a refresh token works once, and two refreshing it at once would sign the
/// host out.
/// </summary>
public sealed class RemoteHostClient : IDisposable
{
    private readonly ILogger<RemoteHostClient> logger;
    private readonly SettingsProvider<Settings.Settings> settingsProvider;
    private readonly HttpClient _http;

    public RemoteHostClient(ILogger<RemoteHostClient> logger, SettingsProvider<Settings.Settings> settingsProvider)
        : this(logger, settingsProvider, new SocketsHttpHandler())
    {
    }

    /// <param name="handler">Sends the requests; owned by the client from then on.</param>
    internal RemoteHostClient(ILogger<RemoteHostClient> logger, SettingsProvider<Settings.Settings> settingsProvider, HttpMessageHandler handler)
    {
        this.logger = logger;
        this.settingsProvider = settingsProvider;
        _http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }

    // Starting a server on an older host answers only once the server stops, so a start is given this
    // long to fail before it is taken to have begun
    private static readonly TimeSpan StartWait = TimeSpan.FromSeconds(5);

    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _refreshLocks = new();

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    /// <summary>The configured Remote hosts.</summary>
    public IReadOnlyList<ServerEngineConfiguration> Hosts => settingsProvider.CurrentValue.Server.GameServers.ServerEngines
        .Where(e => e.Type == ServerEngine.Remote)
        .ToList();

    /// <summary>The Remote host with the given id, or null.</summary>
    public ServerEngineConfiguration? GetHost(Guid hostId) => Hosts.FirstOrDefault(h => h.Id == hostId);

    /// <summary>Whether the host has an address that can be reached.</summary>
    public static bool HasAddress(ServerEngineConfiguration host) =>
        !string.IsNullOrWhiteSpace(host.Address) && Uri.TryCreate(host.Address, UriKind.Absolute, out _);

    /// <summary>Whether the host has been signed in to.</summary>
    public static bool IsSignedIn(ServerEngineConfiguration host) => !string.IsNullOrWhiteSpace(host.AccessToken);

    /// <summary>Where on the host an administrator manages one of its servers.</summary>
    public static string ManageUrl(ServerEngineConfiguration host, Guid remoteServerId) =>
        $"{host.Address.TrimEnd('/')}/Servers/{remoteServerId}";

    /// <summary>Signs in to the host, saving its tokens. Returns an empty string, or why it failed.</summary>
    public async Task<string> AuthenticateAsync(Guid hostId, string username, string password, CancellationToken cancellationToken = default)
    {
        var host = GetHost(hostId);

        if (host == null)
            return "Remote host configuration not found.";

        if (!HasAddress(host))
            return "Invalid address configured for remote host.";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Url(host, "api/Auth/Login"))
            {
                Content = JsonContent.Create(new { Username = username, Password = password }),
            };

            using var response = await SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return $"Authentication failed: {response.StatusCode}";

            var token = await response.Content.ReadFromJsonAsync<AuthToken>(cancellationToken);

            if (token == null)
                return "Authentication failed: empty response.";

            SaveToken(hostId, token);

            return string.Empty;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error authenticating with remote host {HostId}", hostId);

            return ex.Message;
        }
    }

    /// <summary>The host's servers, at the addresses its players use. Throws when they can't be read.</summary>
    public async Task<IReadOnlyList<SDK.Models.Server>> GetServersAsync(Guid hostId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(hostId, HttpMethod.Get, "api/Server/", cancellationToken);

        await EnsureOkAsync(hostId, response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<List<SDK.Models.Server>>(cancellationToken) ?? [];
    }

    /// <summary>
    /// The status of each of the host's servers given. Asks for all at once, or one at a time of a host
    /// too old to answer that.
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, ServerProcessStatus>> GetStatusesAsync(Guid hostId, IEnumerable<Guid> remoteServerIds, CancellationToken cancellationToken = default)
    {
        using (var response = await SendAuthorizedAsync(hostId, HttpMethod.Get, "api/Server/Status", cancellationToken))
        {
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<Dictionary<Guid, ServerProcessStatus>>(cancellationToken) ?? [];

            // Older hosts take "Status" for a server id that doesn't parse, and so don't find the route
            if (response.StatusCode is not (HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed or HttpStatusCode.BadRequest))
                await EnsureOkAsync(hostId, response, cancellationToken);
        }

        var statuses = new Dictionary<Guid, ServerProcessStatus>();

        foreach (var id in remoteServerIds)
            statuses[id] = await GetStatusAsync(hostId, id, cancellationToken);

        return statuses;
    }

    /// <summary>The status of one of the host's servers. Throws when it can't be read.</summary>
    public async Task<ServerProcessStatus> GetStatusAsync(Guid hostId, Guid remoteServerId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(hostId, HttpMethod.Get, $"api/Server/{remoteServerId}/Status", cancellationToken);

        await EnsureOkAsync(hostId, response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<ServerProcessStatus>(cancellationToken);
    }

    /// <summary>
    /// Asks the host to start one of its servers, returning once it has begun to rather than when it
    /// stops. Throws when the host refuses.
    /// </summary>
    public async Task StartAsync(Guid hostId, Guid remoteServerId, CancellationToken cancellationToken = default)
    {
        var send = SendAuthorizedAsync(hostId, HttpMethod.Post, $"api/Server/{remoteServerId}/Start", CancellationToken.None, timeout: Timeout.InfiniteTimeSpan);

        if (await Task.WhenAny(send, Task.Delay(StartWait, cancellationToken)) != send)
        {
            // Still waiting on an older host that answers when the server stops: what it says then is
            // only worth a log entry
            _ = send.ContinueWith(task =>
            {
                if (task.IsFaulted)
                    logger.LogDebug(task.Exception, "Starting server {RemoteServerId} on remote host {HostId} ended in an error", remoteServerId, hostId);
                else
                    task.Result.Dispose();
            }, TaskScheduler.Default);

            return;
        }

        using var response = await send;

        await EnsureSuccessAsync(response, "start", cancellationToken);
    }

    /// <summary>Asks the host to stop one of its servers. Throws when the host refuses.</summary>
    public async Task StopAsync(Guid hostId, Guid remoteServerId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(hostId, HttpMethod.Post, $"api/Server/{remoteServerId}/Stop", cancellationToken, timeout: TimeSpan.FromMinutes(1));

        await EnsureSuccessAsync(response, "stop", cancellationToken);
    }

    /// <summary>
    /// The server's output on the host numbered after <paramref name="after"/>, at most
    /// <paramref name="tail"/> of the latest.
    /// </summary>
    public async Task<ServerLogPage> GetLogAsync(Guid hostId, Guid remoteServerId, long after, int tail, CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(hostId, HttpMethod.Get, $"api/Server/{remoteServerId}/Log?after={after}&tail={tail}", cancellationToken);

        await EnsureOkAsync(hostId, response, cancellationToken, "read server output");

        return await response.Content.ReadFromJsonAsync<ServerLogPage>(cancellationToken) ?? new ServerLogPage([], 0);
    }

    /// <summary>A sample of the server's process on the host, or null when it isn't running.</summary>
    public async Task<ServerProcessInfo?> GetProcessInfoAsync(Guid hostId, Guid remoteServerId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(hostId, HttpMethod.Get, $"api/Server/{remoteServerId}/ProcessInfo", cancellationToken);

        await EnsureOkAsync(hostId, response, cancellationToken, "sample servers");

        return await response.Content.ReadFromJsonAsync<ServerProcessInfo?>(cancellationToken);
    }

    /// <summary>Sends a command to one of the host's servers, as its Monitor page would.</summary>
    public async Task<ServerCommandResult> SendCommandAsync(Guid hostId, Guid remoteServerId, string command, CancellationToken cancellationToken = default)
    {
        using var response = await SendAuthorizedAsync(hostId, HttpMethod.Post, $"api/Server/{remoteServerId}/Command", cancellationToken,
            content: JsonContent.Create(new { Text = command }));

        await EnsureOkAsync(hostId, response, cancellationToken, "send commands");

        return await response.Content.ReadFromJsonAsync<ServerCommandResult>(cancellationToken)
            ?? ServerCommandResult.Failed("The remote host didn't answer");
    }

    // What the host refused, in terms an administrator can act on
    private async Task EnsureOkAsync(Guid hostId, HttpResponseMessage response, CancellationToken cancellationToken, string? newerFeature = null)
    {
        if (response.IsSuccessStatusCode)
            return;

        var name = GetHost(hostId)?.Name ?? "The remote host";

        throw response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => new HttpRequestException($"{name} didn't accept the saved sign-in; connect to it again on Settings › Servers.", null, response.StatusCode),
            HttpStatusCode.Forbidden => new HttpRequestException($"The account {name} is signed in with isn't an administrator there.", null, response.StatusCode),
            HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed when newerFeature != null =>
                new NotSupportedException($"{name} can't {newerFeature}; update it to this version of LANCommander."),
            _ => new HttpRequestException($"{name} answered {(int)response.StatusCode} {response.ReasonPhrase}: {await response.Content.ReadAsStringAsync(cancellationToken)}", null, response.StatusCode),
        };
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string verb, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new InvalidOperationException($"The remote host could not {verb} the server: the account signed in to it isn't an administrator there.");

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        throw new InvalidOperationException(string.IsNullOrWhiteSpace(body)
            ? $"The remote host could not {verb} the server: {(int)response.StatusCode} {response.ReasonPhrase}"
            : $"The remote host could not {verb} the server: {body}");
    }

    private async Task<HttpResponseMessage> SendAuthorizedAsync(Guid hostId, HttpMethod method, string path, CancellationToken cancellationToken, TimeSpan? timeout = null, HttpContent? content = null)
    {
        var host = await EnsureTokenValidAsync(hostId, cancellationToken);

        using var request = new HttpRequestMessage(method, Url(host, path)) { Content = content };

        if (IsSignedIn(host))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", host.AccessToken);

        return await SendAsync(request, cancellationToken, timeout);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken, TimeSpan? timeout = null)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timeoutSource.CancelAfter(timeout ?? RequestTimeout);

        return await _http.SendAsync(request, timeoutSource.Token);
    }

    /// <summary>The host, its access token refreshed first if it is about to expire.</summary>
    private async Task<ServerEngineConfiguration> EnsureTokenValidAsync(Guid hostId, CancellationToken cancellationToken)
    {
        var host = GetHost(hostId) ?? throw new InvalidOperationException("The remote host is no longer configured.");

        if (!HasAddress(host))
            throw new InvalidOperationException($"The remote host {host.Name} has no valid address.");

        if (!NeedsRefresh(host))
            return host;

        var refreshLock = _refreshLocks.GetOrAdd(hostId, _ => new SemaphoreSlim(1, 1));

        await refreshLock.WaitAsync(cancellationToken);

        try
        {
            // Another caller may have refreshed it while this one waited
            host = GetHost(hostId) ?? throw new InvalidOperationException("The remote host is no longer configured.");

            if (!NeedsRefresh(host))
                return host;

            using var request = new HttpRequestMessage(HttpMethod.Post, Url(host, "api/Auth/Refresh"))
            {
                Content = JsonContent.Create(new AuthToken
                {
                    AccessToken = host.AccessToken,
                    RefreshToken = host.RefreshToken,
                    Expiration = host.TokenExpiration,
                }),
            };

            using var response = await SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Could not refresh the token for remote host {HostName}: {StatusCode}", host.Name, response.StatusCode);

                return host;
            }

            var token = await response.Content.ReadFromJsonAsync<AuthToken>(cancellationToken);

            if (token != null)
                SaveToken(hostId, token);

            return GetHost(hostId) ?? host;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not refresh the token for remote host {HostId}", hostId);

            return host;
        }
        finally
        {
            refreshLock.Release();
        }
    }

    /// <summary>Records that the host's servers have been added here, so none is added again.</summary>
    public void MarkAdded(Guid hostId, IEnumerable<Guid> remoteServerIds)
    {
        var ids = remoteServerIds.ToList();

        if (ids.Count == 0)
            return;

        settingsProvider.Update(s =>
        {
            var host = s.Server.GameServers.ServerEngines.FirstOrDefault(e => e.Id == hostId);

            if (host == null)
                return;

            host.AddedServerIds = [.. (host.AddedServerIds ?? []).Union(ids)];
        });
    }

    private static bool NeedsRefresh(ServerEngineConfiguration host) =>
        !string.IsNullOrWhiteSpace(host.RefreshToken) && host.TokenExpiration <= DateTime.UtcNow.AddMinutes(5);

    private void SaveToken(Guid hostId, AuthToken token)
    {
        settingsProvider.Update(s =>
        {
            var host = s.Server.GameServers.ServerEngines.FirstOrDefault(e => e.Id == hostId);

            if (host == null)
                return;

            host.AccessToken = token.AccessToken;
            host.RefreshToken = token.RefreshToken;
            host.TokenExpiration = token.Expiration;
        });
    }

    private static Uri Url(ServerEngineConfiguration host, string path) => new(new Uri(host.Address.TrimEnd('/') + "/"), path);

    public void Dispose() => _http.Dispose();
}
