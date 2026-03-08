using System.Net.Http.Json;
using System.Text.Json;
using LANCommander.SDK.Models;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Enums;
using LANCommander.Server.Services.Models;
using LANCommander.Server.Settings.Enums;
using LANCommander.Server.Settings.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services.ServerEngines;

public class RemoteServerEngine(
    ILogger<RemoteServerEngine> logger,
    SettingsProvider<Settings.Settings> settingsProvider,
    IServiceProvider serviceProvider) : IServerEngine
{
    private record RemoteServerInfo(Guid HostId, Guid RemoteServerId);

    private readonly Dictionary<Guid, RemoteServerInfo> _tracked = new();
    private readonly Dictionary<Guid, HttpClient> _clients = new();
    private readonly Dictionary<Guid, ServerProcessStatus> _status = new();
    private Timer _pollTimer;

    public event EventHandler<ServerStatusUpdateEventArgs>? OnServerStatusUpdate;
    public event EventHandler<ServerLogEventArgs>? OnServerLog;

    public async Task InitializeAsync()
    {
        foreach (var config in settingsProvider.CurrentValue.Server.GameServers.ServerEngines
            .Where(e => e.Type == ServerEngine.Remote))
        {
            if (!string.IsNullOrWhiteSpace(config.Address) &&
                Uri.TryCreate(config.Address, UriKind.Absolute, out _))
            {
                _clients[config.Id] = CreateHttpClient(config);
            }
        }

        using var scope = serviceProvider.CreateScope();
        var serverService = scope.ServiceProvider.GetRequiredService<ServerService>();

        var servers = await serverService.GetAsync(s => s.Engine == ServerEngine.Remote);

        foreach (var server in servers)
        {
            if (server.RemoteHostId.HasValue && server.RemoteServerId.HasValue &&
                _clients.ContainsKey(server.RemoteHostId.Value))
            {
                _tracked[server.Id] = new RemoteServerInfo(server.RemoteHostId.Value, server.RemoteServerId.Value);
            }
        }

        _pollTimer = new Timer(PollStatus, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
    }

    public bool IsManaging(Guid serverId) => _tracked.ContainsKey(serverId);

    public async Task StartAsync(Guid serverId)
    {
        if (!_tracked.ContainsKey(serverId))
            throw new Exception("Server is not being tracked by this engine.");

        var info = _tracked[serverId];
        await EnsureTokenValidAsync(info.HostId);

        var client = _clients[info.HostId];
        await client.PostAsync($"api/Server/{info.RemoteServerId}/Start", null);
    }

    public async Task StopAsync(Guid serverId)
    {
        if (!_tracked.ContainsKey(serverId))
            throw new Exception("Server is not being tracked by this engine.");

        var info = _tracked[serverId];
        await EnsureTokenValidAsync(info.HostId);

        var client = _clients[info.HostId];
        await client.PostAsync($"api/Server/{info.RemoteServerId}/Stop", null);
    }

    public async Task<ServerProcessStatus> GetStatusAsync(Guid serverId)
    {
        if (!_tracked.ContainsKey(serverId))
            return ServerProcessStatus.Stopped;

        var info = _tracked[serverId];

        try
        {
            await EnsureTokenValidAsync(info.HostId);

            var client = _clients[info.HostId];
            var response = await client.GetAsync($"api/Server/{info.RemoteServerId}/Status");

            if (!response.IsSuccessStatusCode)
                return ServerProcessStatus.Stopped;

            var status = await response.Content.ReadFromJsonAsync<ServerProcessStatus>();
            return status;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Could not retrieve status for remote server {ServerId}", serverId);
            return ServerProcessStatus.Stopped;
        }
    }

    public async Task<IEnumerable<SDK.Models.Server>> GetRemoteServersAsync(Guid hostId)
    {
        if (!_clients.TryGetValue(hostId, out var client))
            return [];

        try
        {
            await EnsureTokenValidAsync(hostId);

            var servers = await client.GetFromJsonAsync<IEnumerable<SDK.Models.Server>>("api/Server/");

            return servers ?? [];
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Could not retrieve servers from remote host {HostId}", hostId);
            return [];
        }
    }

    public async Task<string> AuthenticateAsync(Guid hostId, string username, string password)
    {
        var config = settingsProvider.CurrentValue.Server.GameServers.ServerEngines
            .FirstOrDefault(e => e.Id == hostId);

        if (config == null)
            return "Remote host configuration not found.";

        if (!Uri.TryCreate(config.Address, UriKind.Absolute, out _))
            return "Invalid address configured for remote host.";

        try
        {
            var tempClient = CreateHttpClient(config, includeAuth: false);
            var loginModel = new { Username = username, Password = password };

            var response = await tempClient.PostAsJsonAsync("api/Auth/Login", loginModel);

            if (!response.IsSuccessStatusCode)
                return $"Authentication failed: {response.StatusCode}";

            var token = await response.Content.ReadFromJsonAsync<AuthToken>();

            if (token == null)
                return "Authentication failed: empty response.";

            settingsProvider.Update(s =>
            {
                var c = s.Server.GameServers.ServerEngines.FirstOrDefault(e => e.Id == hostId);
                
                if (c == null)
                    return;
                
                c.AccessToken = token.AccessToken;
                c.RefreshToken = token.RefreshToken;
                c.TokenExpiration = token.Expiration;
            });

            _clients[hostId] = CreateHttpClient(settingsProvider.CurrentValue.Server.GameServers.ServerEngines
                .FirstOrDefault(e => e.Id == hostId) ?? config);

            return string.Empty;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error authenticating with remote host {HostId}", hostId);
            return ex.Message;
        }
    }

    private async Task EnsureTokenValidAsync(Guid hostId)
    {
        var config = settingsProvider.CurrentValue.Server.GameServers.ServerEngines
            .FirstOrDefault(e => e.Id == hostId);

        if (config == null)
            return;

        if (config.TokenExpiration > DateTime.UtcNow.AddMinutes(5))
            return;

        if (string.IsNullOrWhiteSpace(config.RefreshToken))
            return;

        try
        {
            var tempClient = CreateHttpClient(config, includeAuth: false);
            var refreshPayload = new AuthToken
            {
                AccessToken = config.AccessToken,
                RefreshToken = config.RefreshToken,
                Expiration = config.TokenExpiration
            };

            var response = await tempClient.PostAsJsonAsync("api/Auth/Refresh", refreshPayload);

            if (!response.IsSuccessStatusCode)
                return;

            var token = await response.Content.ReadFromJsonAsync<AuthToken>();

            if (token == null)
                return;

            settingsProvider.Update(s =>
            {
                var c = s.Server.GameServers.ServerEngines.FirstOrDefault(e => e.Id == hostId);
                if (c != null)
                {
                    c.AccessToken = token.AccessToken;
                    c.RefreshToken = token.RefreshToken;
                    c.TokenExpiration = token.Expiration;
                }
            });

            _clients[hostId] = CreateHttpClient(settingsProvider.CurrentValue.Server.GameServers.ServerEngines
                .FirstOrDefault(e => e.Id == hostId) ?? config);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Could not refresh token for remote host {HostId}", hostId);
        }
    }

    private HttpClient CreateHttpClient(ServerEngineConfiguration config, bool includeAuth = true)
    {
        var client = new HttpClient();
        client.BaseAddress = new Uri(config.Address.TrimEnd('/') + "/");

        if (includeAuth && !string.IsNullOrWhiteSpace(config.AccessToken))
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.AccessToken);

        return client;
    }

    private async void PollStatus(object? state)
    {
        foreach (var (serverId, info) in _tracked.ToList())
        {
            try
            {
                var newStatus = await GetStatusAsync(serverId);

                if (!_status.TryGetValue(serverId, out var oldStatus) || oldStatus != newStatus)
                {
                    _status[serverId] = newStatus;

                    using var scope = serviceProvider.CreateScope();
                    var serverService = scope.ServiceProvider.GetRequiredService<ServerService>();
                    var server = await serverService.GetAsync(serverId);

                    if (server != null)
                        OnServerStatusUpdate?.Invoke(this, new ServerStatusUpdateEventArgs(server, newStatus));
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Error polling status for remote server {ServerId}", serverId);
            }
        }
    }
}
