using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using LANCommander.Server.Plugins;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Enums;
using LANCommander.Server.Services.Models;
using LANCommander.Server.Settings.Enums;
using LANCommander.Server.Settings.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServerEntity = LANCommander.Server.Data.Models.Server;

namespace LANCommander.Server.Services.ServerEngines;

/// <summary>
/// Runs servers whose engine is Remote: each stands for a server on another LANCommander server,
/// started, stopped, watched and sent commands through that server's API.
/// </summary>
/// <remarks>
/// Each host is asked for its servers and their statuses every <see cref="PollInterval"/>, and what it
/// said answers for their status and address until the next time, so a page watching many of them
/// doesn't ask the host for each. Hosts set to <see cref="ServerEngineConfiguration.AddServers"/> have
/// their servers added here as they appear, filled in with what the host says about them.
/// </remarks>
public class RemoteServerEngine(
    ILogger<RemoteServerEngine> logger,
    RemoteHostClient remoteHosts,
    IServiceProvider serviceProvider) : IServerEngine, IAsyncDisposable
{
    internal static TimeSpan PollInterval = TimeSpan.FromSeconds(10);
    internal static TimeSpan LogPollInterval = TimeSpan.FromSeconds(1);

    // Failures in a row that end following a server's output
    private const int LogPollFailures = 10;

    private static readonly TimeSpan SampleTimeout = TimeSpan.FromSeconds(10);

    private sealed record RemoteServerInfo(Guid HostId, Guid RemoteServerId);

    /// <summary>What a host last said: its servers and their statuses, or why it couldn't be asked.</summary>
    private sealed record HostSnapshot(
        IReadOnlyDictionary<Guid, SDK.Models.Server> Servers,
        IReadOnlyDictionary<Guid, ServerProcessStatus> Statuses,
        string? Error);

    private readonly ConcurrentDictionary<Guid, RemoteServerInfo> _tracked = new();
    private readonly ConcurrentDictionary<Guid, HostSnapshot> _hosts = new();

    // Starting or Stopping, from a start or stop asked for here, until the host next says
    private readonly ConcurrentDictionary<Guid, ServerProcessStatus> _pending = new();

    // The status last announced for each server
    private readonly ConcurrentDictionary<Guid, ServerProcessStatus> _announced = new();

    private readonly SemaphoreSlim _polling = new(1, 1);
    private readonly SemaphoreSlim _pollRequested = new(0, 1);
    private readonly CancellationTokenSource _stopping = new();
    private Task? _loop;
    private int _disposed;

    public event EventHandler<ServerStatusUpdateEventArgs>? OnServerStatusUpdate;
    public event EventHandler<ServerLogEventArgs>? OnServerLog { add { } remove { } }

    public Task InitializeAsync()
    {
        // In the background: a host that is slow to answer mustn't hold up the server starting
        _loop ??= Task.Run(() => RunAsync(_stopping.Token));

        return Task.CompletedTask;
    }

    public async Task RefreshTrackingAsync()
    {
        using var scope = serviceProvider.CreateScope();
        var serverService = scope.ServiceProvider.GetRequiredService<ServerService>();

        var servers = await serverService.AsNoTracking().GetAsync(s => s.Engine == ServerEngine.Remote);

        foreach (var server in servers)
        {
            if (server.RemoteHostId.HasValue && server.RemoteServerId.HasValue && remoteHosts.GetHost(server.RemoteHostId.Value) != null)
                _tracked[server.Id] = new RemoteServerInfo(server.RemoteHostId.Value, server.RemoteServerId.Value);
            else
                _tracked.TryRemove(server.Id, out _);
        }

        foreach (var id in _tracked.Keys.Except(servers.Select(s => s.Id)).ToList())
            _tracked.TryRemove(id, out _);
    }

    public bool IsManaging(Guid serverId) => _tracked.ContainsKey(serverId);

    #region Polling

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await PollAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not poll the remote hosts");
            }

            try
            {
                await _pollRequested.WaitAsync(PollInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>Asks every signed-in host for its servers now, adding new ones where it is set to.</summary>
    public async Task PollAsync(CancellationToken cancellationToken = default)
    {
        await _polling.WaitAsync(cancellationToken);

        try
        {
            var hosts = remoteHosts.Hosts;

            foreach (var hostId in _hosts.Keys.Except(hosts.Select(h => h.Id)).ToList())
                _hosts.TryRemove(hostId, out _);

            var added = false;

            foreach (var host in hosts)
            {
                // Its servers can't be reached until it can be; say why rather than leave them waiting
                if (!RemoteHostClient.HasAddress(host) || !RemoteHostClient.IsSignedIn(host))
                {
                    var last = _hosts.GetValueOrDefault(host.Id);

                    _hosts[host.Id] = new HostSnapshot(
                        last?.Servers ?? new Dictionary<Guid, SDK.Models.Server>(),
                        last?.Statuses ?? new Dictionary<Guid, ServerProcessStatus>(),
                        RemoteHostClient.HasAddress(host)
                            ? $"{host.Name} hasn't been connected to; connect to it on Settings › Servers."
                            : $"{host.Name} has no valid address; set one on Settings › Servers.");

                    continue;
                }

                if (await PollHostAsync(host, cancellationToken) is { Error: null } snapshot && host.AddServers)
                    added |= await AddNewServersAsync(host, snapshot, cancellationToken);
            }

            if (added)
                await RefreshTrackingAsync();

            await AnnounceChangesAsync();
        }
        finally
        {
            _polling.Release();
        }
    }

    private async Task<HostSnapshot> PollHostAsync(ServerEngineConfiguration host, CancellationToken cancellationToken)
    {
        HostSnapshot snapshot;

        try
        {
            var servers = await remoteHosts.GetServersAsync(host.Id, cancellationToken);

            var statuses = servers.Count == 0
                ? new Dictionary<Guid, ServerProcessStatus>()
                : await remoteHosts.GetStatusesAsync(host.Id, servers.Select(s => s.Id), cancellationToken);

            snapshot = new HostSnapshot(servers.ToDictionary(s => s.Id), statuses, null);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not ask remote host {HostName} for its servers", host.Name);

            // Keep what it last said, so a server's details don't vanish while its host is away
            var last = _hosts.GetValueOrDefault(host.Id);

            snapshot = new HostSnapshot(
                last?.Servers ?? new Dictionary<Guid, SDK.Models.Server>(),
                last?.Statuses ?? new Dictionary<Guid, ServerProcessStatus>(),
                $"{host.Name} could not be reached: {ex.Message}");
        }

        _hosts[host.Id] = snapshot;

        // The host has had its say about anything started or stopped here
        foreach (var (serverId, info) in _tracked)
        {
            if (info.HostId == host.Id)
                _pending.TryRemove(serverId, out _);
        }

        return snapshot;
    }

    /// <summary>
    /// Adds the host's servers that haven't been yet, named and linked to their game (where it is one
    /// of this server's) as the host has them. Those already added here by hand aren't added twice, and
    /// none is added again once an administrator deletes it.
    /// </summary>
    private async Task<bool> AddNewServersAsync(ServerEngineConfiguration host, HostSnapshot snapshot, CancellationToken cancellationToken)
    {
        var known = host.AddedServerIds ?? [];
        var appeared = snapshot.Servers.Values.Where(s => !known.Contains(s.Id)).ToList();

        if (appeared.Count == 0)
            return false;

        using var scope = serviceProvider.CreateScope();
        var serverService = scope.ServiceProvider.GetRequiredService<ServerService>();
        var gameService = scope.ServiceProvider.GetRequiredService<GameService>();

        var existing = (await serverService.AsNoTracking().GetAsync(s => s.Engine == ServerEngine.Remote && s.RemoteHostId == host.Id))
            .Where(s => s.RemoteServerId.HasValue)
            .Select(s => s.RemoteServerId!.Value)
            .ToHashSet();

        var gameIds = appeared.Where(s => s.GameId.HasValue).Select(s => s.GameId!.Value).Distinct().ToList();
        var games = gameIds.Count == 0
            ? new HashSet<Guid>()
            : (await gameService.AsNoTracking().GetAsync(g => gameIds.Contains(g.Id))).Select(g => g.Id).ToHashSet();

        var addedAny = false;
        var recorded = new List<Guid>();

        foreach (var remote in appeared)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (!existing.Contains(remote.Id))
                {
                    await serverService.AddAsync(new ServerEntity
                    {
                        Name = string.IsNullOrWhiteSpace(remote.Name) ? $"{host.Name} server" : remote.Name,
                        Engine = ServerEngine.Remote,
                        RemoteHostId = host.Id,
                        RemoteServerId = remote.Id,
                        GameId = remote.GameId is { } gameId && games.Contains(gameId) ? gameId : null,
                    });

                    addedAny = true;

                    logger.LogInformation("Added server \"{ServerName}\" from remote host {HostName}", remote.Name, host.Name);
                }

                recorded.Add(remote.Id);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not add server \"{ServerName}\" from remote host {HostName}", remote.Name, host.Name);
            }
        }

        remoteHosts.MarkAdded(host.Id, recorded);

        return addedAny;
    }

    private async Task AnnounceChangesAsync()
    {
        foreach (var serverId in _tracked.Keys)
        {
            var status = Status(serverId);

            if (status == ServerProcessStatus.Retrieving)
                continue;

            var previous = _announced.GetValueOrDefault(serverId, ServerProcessStatus.Retrieving);

            _announced[serverId] = status;

            // Servers found running when the server starts didn't start then
            if (previous == status || previous == ServerProcessStatus.Retrieving)
                continue;

            await AnnounceAsync(serverId, status, status == ServerProcessStatus.Error ? GetProblem(serverId) : null);
        }
    }

    private async Task AnnounceAsync(Guid serverId, ServerProcessStatus status, string? error = null)
    {
        _announced[serverId] = status;

        try
        {
            using var scope = serviceProvider.CreateScope();
            var server = await scope.ServiceProvider.GetRequiredService<ServerService>().AsNoTracking().GetAsync(serverId);

            if (server != null)
                OnServerStatusUpdate?.Invoke(this, error == null
                    ? new ServerStatusUpdateEventArgs(server, status)
                    : new ServerStatusUpdateEventArgs(server, status, new Exception(error)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not announce the status of remote server {ServerId}", serverId);
        }
    }

    private void RequestPoll()
    {
        try
        {
            _pollRequested.Release();
        }
        catch (SemaphoreFullException)
        {
            // Already asked for
        }
    }

    #endregion

    #region Status and address

    private ServerProcessStatus Status(Guid serverId)
    {
        if (!_tracked.TryGetValue(serverId, out var info))
            return ServerProcessStatus.Stopped;

        if (_pending.TryGetValue(serverId, out var pending))
            return pending;

        if (!_hosts.TryGetValue(info.HostId, out var snapshot))
            return ServerProcessStatus.Retrieving;

        if (snapshot.Error != null || !snapshot.Servers.ContainsKey(info.RemoteServerId))
            return ServerProcessStatus.Error;

        return snapshot.Statuses.GetValueOrDefault(info.RemoteServerId, ServerProcessStatus.Stopped);
    }

    public Task<ServerProcessStatus> GetStatusAsync(Guid serverId) => Task.FromResult(Status(serverId));

    /// <summary>
    /// Why the server can't be reached right now: its host can't be, or no longer has it. Null when
    /// nothing is wrong, or the host hasn't been asked yet.
    /// </summary>
    public string? GetProblem(Guid serverId)
    {
        if (!_tracked.TryGetValue(serverId, out var info) || !_hosts.TryGetValue(info.HostId, out var snapshot))
            return null;

        if (snapshot.Error != null)
            return snapshot.Error;

        if (!snapshot.Servers.ContainsKey(info.RemoteServerId))
            return $"{remoteHosts.GetHost(info.HostId)?.Name ?? "The remote host"} no longer has this server. Delete it here, or point it at another of the host's servers.";

        return null;
    }

    /// <summary>The server as its host last described it, or null.</summary>
    public SDK.Models.Server? GetRemoteServer(Guid serverId) =>
        _tracked.TryGetValue(serverId, out var info) && _hosts.TryGetValue(info.HostId, out var snapshot)
            ? snapshot.Servers.GetValueOrDefault(info.RemoteServerId)
            : null;

    /// <summary>Where an administrator manages the server on its host, or null.</summary>
    public string? GetManageUrl(Guid serverId) =>
        _tracked.TryGetValue(serverId, out var info) && remoteHosts.GetHost(info.HostId) is { } host
            ? RemoteHostClient.ManageUrl(host, info.RemoteServerId)
            : null;

    /// <summary>The name of the host the server is on, or null.</summary>
    public string? GetHostName(Guid serverId) =>
        _tracked.TryGetValue(serverId, out var info) ? remoteHosts.GetHost(info.HostId)?.Name : null;

    /// <summary>Where the host says players connect; its own address where it leaves the host blank.</summary>
    public ReportedServerAddress? GetReportedAddress(Guid serverId)
    {
        if (!_tracked.TryGetValue(serverId, out var info) || GetRemoteServer(serverId) is not { } remote)
            return null;

        var host = string.IsNullOrWhiteSpace(remote.Host)
            ? remoteHosts.GetHost(info.HostId) is { } config && Uri.TryCreate(config.Address, UriKind.Absolute, out var address) ? address.Host : null
            : remote.Host;

        return new ReportedServerAddress(host, remote.Port > 0 ? remote.Port : null);
    }

    #endregion

    #region Start and stop

    public async Task StartAsync(Guid serverId)
    {
        if (!_tracked.TryGetValue(serverId, out var info))
            throw new Exception("Server is not being tracked by this engine.");

        _pending[serverId] = ServerProcessStatus.Starting;
        await AnnounceAsync(serverId, ServerProcessStatus.Starting);

        try
        {
            await remoteHosts.StartAsync(info.HostId, info.RemoteServerId);
        }
        catch (Exception ex)
        {
            _pending.TryRemove(serverId, out _);
            await AnnounceAsync(serverId, ServerProcessStatus.Error, ex.Message);

            throw;
        }
        finally
        {
            RequestPoll();
        }
    }

    public async Task StopAsync(Guid serverId)
    {
        if (!_tracked.TryGetValue(serverId, out var info))
            throw new Exception("Server is not being tracked by this engine.");

        _pending[serverId] = ServerProcessStatus.Stopping;
        await AnnounceAsync(serverId, ServerProcessStatus.Stopping);

        try
        {
            await remoteHosts.StopAsync(info.HostId, info.RemoteServerId);
        }
        catch (Exception ex)
        {
            _pending.TryRemove(serverId, out _);
            await AnnounceAsync(serverId, ServerProcessStatus.Error, ex.Message);

            throw;
        }
        finally
        {
            RequestPoll();
        }
    }

    #endregion

    #region Monitor

    public async Task<ServerProcessInfo?> GetProcessInfoAsync(Guid serverId)
    {
        if (!_tracked.TryGetValue(serverId, out var info) || Status(serverId) is not (ServerProcessStatus.Running or ServerProcessStatus.Starting))
            return null;

        using var timeout = new CancellationTokenSource(SampleTimeout);

        return await remoteHosts.GetProcessInfoAsync(info.HostId, info.RemoteServerId, timeout.Token);
    }

    public bool CanSendInput(Guid serverId) => _tracked.ContainsKey(serverId) && Status(serverId) == ServerProcessStatus.Running;

    public async Task<bool> SendInputAsync(Guid serverId, string line) => (await SendCommandAsync(serverId, line)).Sent;

    /// <summary>Sends a command through the host, as its own Monitor page would, with what the server answered.</summary>
    public async Task<ServerCommandResult> SendCommandAsync(Guid serverId, string command, CancellationToken cancellationToken = default)
    {
        if (!_tracked.TryGetValue(serverId, out var info))
            return ServerCommandResult.Failed("The server isn't run by a remote host");

        try
        {
            return await remoteHosts.SendCommandAsync(info.HostId, info.RemoteServerId, command, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not send a command to remote server {ServerId}", serverId);

            return ServerCommandResult.Failed(ex.Message);
        }
    }

    /// <summary>
    /// Reads the server's output on its host: the latest <see cref="ServerEngineLogRequest.Tail"/> lines,
    /// then, when following, new lines as the host has them.
    /// </summary>
    public async IAsyncEnumerable<ServerEngineLogLine> ReadLogAsync(Guid serverId, ServerEngineLogRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!_tracked.TryGetValue(serverId, out var info))
            throw new InvalidOperationException("The server isn't run by a remote host.");

        var page = await remoteHosts.GetLogAsync(info.HostId, info.RemoteServerId, 0, request.Tail ?? ServerManager.RecentLogCapacity, cancellationToken);

        foreach (var line in page.Lines)
            yield return ToLogLine(line);

        if (!request.Follow)
            yield break;

        var cursor = page.Last;
        var failures = 0;

        while (true)
        {
            await Task.Delay(LogPollInterval, cancellationToken);

            ServerLogPage next;

            try
            {
                next = await remoteHosts.GetLogAsync(info.HostId, info.RemoteServerId, cursor, ServerManager.RecentLogCapacity, cancellationToken);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested && ++failures < LogPollFailures)
            {
                logger.LogDebug(ex, "Could not read new output of remote server {ServerId}; trying again", serverId);
                continue;
            }

            failures = 0;

            // The host restarted and numbers its lines from the start again
            if (next.Last < cursor)
                next = await remoteHosts.GetLogAsync(info.HostId, info.RemoteServerId, 0, ServerManager.RecentLogCapacity, cancellationToken);

            foreach (var line in next.Lines)
                yield return ToLogLine(line);

            cursor = next.Last;
        }
    }

    private static ServerEngineLogLine ToLogLine(SequencedServerLogLine line) =>
        new(line.Line.Message, line.Line.Timestamp, line.Line.Level == ServerLogLevel.Error);

    #endregion

    #region Hosts

    /// <summary>The servers on a Remote host, or none when they can't be read.</summary>
    public async Task<IEnumerable<SDK.Models.Server>> GetRemoteServersAsync(Guid hostId)
    {
        try
        {
            return await remoteHosts.GetServersAsync(hostId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not retrieve servers from remote host {HostId}", hostId);

            return [];
        }
    }

    /// <summary>Signs in to a Remote host, then asks it for its servers. Returns an empty string, or why it failed.</summary>
    public async Task<string> AuthenticateAsync(Guid hostId, string username, string password)
    {
        var error = await remoteHosts.AuthenticateAsync(hostId, username, password);

        if (string.IsNullOrEmpty(error))
            RequestPoll();

        return error;
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        await _stopping.CancelAsync();

        if (_loop != null)
        {
            try
            {
                await _loop;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _stopping.Dispose();
    }
}
