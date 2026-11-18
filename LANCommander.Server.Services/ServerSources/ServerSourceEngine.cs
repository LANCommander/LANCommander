using System.Collections.Concurrent;
using LANCommander.Server.Plugins;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Enums;
using LANCommander.Server.Services.Models;
using LANCommander.Server.Services.Utilities;
using Microsoft.Extensions.Logging;
using ServerEntity = LANCommander.Server.Data.Models.Server;

namespace LANCommander.Server.Services.ServerSources;

/// <summary>A server an <see cref="IServerSourceExtension"/> listed, as the Servers page shows it.</summary>
/// <param name="Id">The id the server knows it by; see <see cref="ExternalServerIds"/>.</param>
/// <param name="SourceId">The source's id.</param>
/// <param name="SourceName">The source's name.</param>
/// <param name="Server">What the source last said about it.</param>
/// <param name="Status">Its status: the source's, or what it reported since, or Error when it couldn't be listed.</param>
/// <param name="StatusMessage">Why it is in Error, where known.</param>
public sealed record ExternalServerListing(
    Guid Id,
    string SourceId,
    string SourceName,
    ExternalServer Server,
    ServerProcessStatus Status,
    string? StatusMessage);

/// <summary>
/// Runs the servers an <see cref="IServerSourceExtension"/> lists as though they were the server's own:
/// lists them every <see cref="IServerSourceExtension.RefreshInterval"/>, answers for their status and
/// address from the latest list, and passes starts and stops on to the source.
/// </summary>
public sealed class ServerSourceEngine : IServerEngine, IServerSourceHost, IAsyncDisposable
{
    private static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ListTimeout = TimeSpan.FromSeconds(30);

    private readonly IServerSourceExtension _source;
    private readonly ILogger _logger;

    private readonly ConcurrentDictionary<Guid, ExternalServerListing> _servers = new();
    private readonly ConcurrentDictionary<string, Guid> _keys = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _refreshing = new(1, 1);
    private readonly CancellationTokenSource _stopping = new();

    // Released to list again before the interval is up
    private readonly SemaphoreSlim _refreshRequested = new(0, 1);

    private Task? _loop;
    private bool _listed;

    public ServerSourceEngine(IServerSourceExtension source, ILogger logger)
    {
        _source = source;
        _logger = logger;
    }

    public IServerSourceExtension Source => _source;

    public event EventHandler<ServerStatusUpdateEventArgs>? OnServerStatusUpdate;
    public event EventHandler<ServerLogEventArgs>? OnServerLog { add { } remove { } }

    /// <summary>The servers last listed, by name.</summary>
    public IReadOnlyList<ExternalServerListing> Servers => _servers.Values
        .OrderBy(s => s.Server.Name, StringComparer.CurrentCultureIgnoreCase)
        .ToList();

    /// <summary>The server with the given id, or null.</summary>
    public ExternalServerListing? Find(Guid serverId) => _servers.TryGetValue(serverId, out var server) ? server : null;

    public async Task InitializeAsync()
    {
        await _source.InitializeAsync(this, _stopping.Token);

        // Listed in the background: a source that is slow to answer, e.g. a host that is down, mustn't
        // hold up the server starting
        _loop ??= Task.Run(() => RunAsync(_stopping.Token));
    }

    /// <summary>
    /// Called when the server's own servers change; a source's aren't among them, so this only asks for
    /// them to be listed again.
    /// </summary>
    public Task RefreshTrackingAsync()
    {
        ((IServerSourceHost)this).Refresh();

        return Task.CompletedTask;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RefreshAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "The servers {Source} listed could not be applied", _source.Name);
            }

            var interval = _source.RefreshInterval;

            if (interval < MinimumInterval)
                interval = MinimumInterval;

            try
            {
                await _refreshRequested.WaitAsync(interval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>Lists the source's servers now, keeping those last listed if it can't.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _refreshing.WaitAsync(cancellationToken);

        try
        {
            IReadOnlyList<ExternalServer> listed;

            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                timeout.CancelAfter(ListTimeout);

                listed = await _source.GetServersAsync(timeout.Token) ?? [];
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "{Source} could not list its servers", _source.Name);

                // Shown as errored rather than dropped, so a host going away for a moment doesn't make
                // its servers vanish and reappear
                foreach (var server in _servers.Values)
                    Update(server with { Status = ServerProcessStatus.Error, StatusMessage = $"{_source.Name} could not be reached: {ex.Message}" });

                return;
            }

            Apply(listed);
        }
        finally
        {
            _refreshing.Release();
        }
    }

    private void Apply(IReadOnlyList<ExternalServer> listed)
    {
        var seen = new HashSet<Guid>();

        foreach (var server in listed)
        {
            if (server == null || string.IsNullOrEmpty(server.Key))
                continue;

            var id = ExternalServerIds.Create(_source.Id, server.Key);

            if (!seen.Add(id))
            {
                _logger.LogWarning("{Source} listed more than one server with the key {Key}; only the first is shown", _source.Name, server.Key);
                continue;
            }

            var normalized = server with
            {
                Name = string.IsNullOrWhiteSpace(server.Name) ? server.Key : server.Name,
                Host = server.Host?.Trim() ?? "",
                Port = server.Port is >= 1 and <= 65535 ? server.Port : 0,
                Location = server.Location ?? "",
            };

            _keys[server.Key] = id;

            Update(new ExternalServerListing(id, _source.Id, _source.Name, normalized, ToProcessStatus(normalized.Status), normalized.StatusMessage));
        }

        foreach (var (id, gone) in _servers.Where(s => !seen.Contains(s.Key)).ToList())
        {
            _servers.TryRemove(id, out _);
            _keys.TryRemove(gone.Server.Key, out _);
        }

        _listed = true;
    }

    // Keeps the server's latest, raising a status change where it changed. Servers seen for the first
    // time at start-up aren't announced: they didn't start then, they were found running
    private void Update(ExternalServerListing server)
    {
        ExternalServerListing? previous = null;

        _servers.AddOrUpdate(server.Id, server, (_, last) =>
        {
            previous = last;

            return server;
        });

        if (previous == null ? _listed : previous.Status != server.Status)
            Emit(server, server.Status, server.StatusMessage);
    }

    public bool IsManaging(Guid serverId) => _servers.ContainsKey(serverId);

    public Task<ServerProcessStatus> GetStatusAsync(Guid serverId) =>
        Task.FromResult(_servers.TryGetValue(serverId, out var server) ? server.Status : ServerProcessStatus.Stopped);

    public ReportedServerAddress? GetReportedAddress(Guid serverId)
    {
        if (!_servers.TryGetValue(serverId, out var server))
            return null;

        return new ReportedServerAddress(server.Server.Host, server.Server.Port > 0 ? server.Server.Port : null);
    }

    public async Task StartAsync(Guid serverId)
    {
        var server = Find(serverId) ?? throw new InvalidOperationException($"{_source.Name} no longer lists the server.");

        if (!server.Server.CanStart)
            throw new NotSupportedException($"{_source.Name} doesn't allow {server.Server.Name} to be started from here.");

        Update(server with { Status = ServerProcessStatus.Starting, StatusMessage = null });

        try
        {
            await _source.StartAsync(server.Server, _stopping.Token);
        }
        catch (Exception ex)
        {
            Update(server with { Status = ServerProcessStatus.Error, StatusMessage = ex.Message });

            throw;
        }
        finally
        {
            ((IServerSourceHost)this).Refresh();
        }
    }

    public async Task StopAsync(Guid serverId)
    {
        var server = Find(serverId) ?? throw new InvalidOperationException($"{_source.Name} no longer lists the server.");

        if (!server.Server.CanStop)
            throw new NotSupportedException($"{_source.Name} doesn't allow {server.Server.Name} to be stopped from here.");

        Update(server with { Status = ServerProcessStatus.Stopping, StatusMessage = null });

        try
        {
            await _source.StopAsync(server.Server, _stopping.Token);
        }
        catch (Exception ex)
        {
            Update(server with { Status = ServerProcessStatus.Error, StatusMessage = ex.Message });

            throw;
        }
        finally
        {
            ((IServerSourceHost)this).Refresh();
        }
    }

    // Samples are taken every couple of seconds while a Monitor page is open; one that hangs, e.g. on
    // a host that has gone away, mustn't hold up the page
    private static readonly TimeSpan SampleTimeout = TimeSpan.FromSeconds(10);

    public async Task<ServerProcessInfo?> GetProcessInfoAsync(Guid serverId)
    {
        if (Find(serverId) is not { } server
            || !server.Server.Features.HasFlag(ExternalServerFeatures.ProcessInfo)
            || server.Status is not (ServerProcessStatus.Running or ServerProcessStatus.Starting))
            return null;

        using var timeout = new CancellationTokenSource(SampleTimeout);

        var info = await _source.GetProcessInfoAsync(server.Server, timeout.Token);

        return info == null ? null : new ServerProcessInfo(info.Pid, info.StartTime, info.CpuPercent, info.MemoryBytes);
    }

    /// <summary>
    /// The source's own pages for the server, without any that couldn't be shown: a slug that isn't
    /// letters, digits and <c>-</c>, or that another page has, or a component that isn't one.
    /// </summary>
    public IReadOnlyList<ExternalServerSection> GetSections(Guid serverId)
    {
        if (Find(serverId) is not { } server)
            return [];

        try
        {
            var sections = new List<ExternalServerSection>();

            foreach (var section in _source.GetSections(server.Server) ?? [])
            {
                if (section == null || !PluginComponents.IsSlug(section.Slug) || !PluginComponents.IsComponent(section.Component))
                {
                    _logger.LogWarning("{Source} offered a page for {ServerName} that can't be shown: {Slug}", _source.Name, server.Server.Name, section?.Slug);
                    continue;
                }

                if (sections.Any(s => string.Equals(s.Slug, section.Slug, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning("{Source} offered more than one page {Slug} for {ServerName}; only the first is shown", _source.Name, section.Slug, server.Server.Name);
                    continue;
                }

                sections.Add(section with { Title = string.IsNullOrWhiteSpace(section.Title) ? section.Slug : section.Title });
            }

            return sections;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Source} could not list its pages for {ServerName}", _source.Name, server.Server.Name);

            return [];
        }
    }

    /// <summary>
    /// The source's own component for the top of the server's General page, or null when it has none,
    /// or offers something that isn't a component.
    /// </summary>
    public Type? GetOverview(Guid serverId)
    {
        if (Find(serverId) is not { } server)
            return null;

        try
        {
            var overview = _source.GetOverview(server.Server);

            if (overview == null || PluginComponents.IsComponent(overview))
                return overview;

            _logger.LogWarning("{Source} offered {Type} as {ServerName}'s overview, which isn't a component", _source.Name, overview.FullName, server.Server.Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Source} could not say what to show on {ServerName}'s General page", _source.Name, server.Server.Name);
        }

        return null;
    }

    /// <summary>Whether the server's Monitor page can read its output.</summary>
    public bool CanReadLog(Guid serverId) => Find(serverId)?.Server.Features.HasFlag(ExternalServerFeatures.Log) ?? false;

    /// <summary>Reads the server's output through the source; see <see cref="IServerSourceExtension.ReadLogAsync"/>.</summary>
    public IAsyncEnumerable<ServerEngineLogLine> ReadLogAsync(Guid serverId, ServerEngineLogRequest request, CancellationToken cancellationToken)
    {
        var server = Find(serverId) ?? throw new InvalidOperationException($"{_source.Name} no longer lists the server.");

        if (!server.Server.Features.HasFlag(ExternalServerFeatures.Log))
            throw new NotSupportedException($"{_source.Name} can't read {server.Server.Name}'s output.");

        return _source.ReadLogAsync(server.Server, request, cancellationToken);
    }

    public bool CanSendInput(Guid serverId) =>
        Find(serverId) is { } server
        && server.Server.Features.HasFlag(ExternalServerFeatures.Commands)
        && server.Status == ServerProcessStatus.Running;

    public async Task<bool> SendInputAsync(Guid serverId, string line) =>
        (await SendCommandAsync(serverId, line)).Sent;

    /// <summary>Sends a command through the source, with what the server answered where it answers directly.</summary>
    public async Task<ServerCommandResult> SendCommandAsync(Guid serverId, string command, CancellationToken cancellationToken = default)
    {
        if (Find(serverId) is not { } server)
            return ServerCommandResult.Failed($"{_source.Name} no longer lists the server");

        if (!server.Server.Features.HasFlag(ExternalServerFeatures.Commands))
            return ServerCommandResult.Failed($"{_source.Name} can't send commands to {server.Server.Name}");

        try
        {
            return new ServerCommandResult(true, await _source.SendCommandAsync(server.Server, command, cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "{Source} could not send a command to {ServerName}", _source.Name, server.Server.Name);

            return ServerCommandResult.Failed(ex.Message);
        }
    }

    void IServerSourceHost.Refresh()
    {
        try
        {
            _refreshRequested.Release();
        }
        catch (SemaphoreFullException)
        {
            // Already asked for
        }
    }

    void IServerSourceHost.ReportStatus(string key, ServerEngineStatus status, string? error)
    {
        if (key == null || !_keys.TryGetValue(key, out var id) || !_servers.TryGetValue(id, out var server))
            return;

        Update(server with
        {
            Server = server.Server with { Status = status, StatusMessage = error },
            Status = ToProcessStatus(status),
            StatusMessage = error,
        });
    }

    private void Emit(ExternalServerListing server, ServerProcessStatus status, string? error)
    {
        // Those listening only want the id, the name for messages, and the game
        var entity = new ServerEntity { Id = server.Id, Name = server.Server.Name, GameId = server.Server.GameId };

        try
        {
            OnServerStatusUpdate?.Invoke(this, error == null || status != ServerProcessStatus.Error
                ? new ServerStatusUpdateEventArgs(entity, status)
                : new ServerStatusUpdateEventArgs(entity, status, new Exception(error)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not publish the status of server {ServerId}", server.Id);
        }
    }

    private static ServerProcessStatus ToProcessStatus(ServerEngineStatus status) => status switch
    {
        ServerEngineStatus.Starting => ServerProcessStatus.Starting,
        ServerEngineStatus.Running => ServerProcessStatus.Running,
        ServerEngineStatus.Stopping => ServerProcessStatus.Stopping,
        ServerEngineStatus.Error => ServerProcessStatus.Error,
        _ => ServerProcessStatus.Stopped,
    };

    private int _disposed;

    public async ValueTask DisposeAsync()
    {
        // The registry is disposed by the container once under each name it is registered by
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
