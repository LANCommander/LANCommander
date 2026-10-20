using System.Collections.Concurrent;
using System.Linq.Expressions;
using LANCommander.SDK.Enums;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Enums;
using LANCommander.Server.Services.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services;

/// <summary>
/// Singleton coordinator for the full lifetime of game servers. Owns engine routing (which
/// <see cref="IServerEngine"/> manages a given server), tracking refresh, autostart (including
/// the boot-time pass), and debounced autostop. Server lifecycle is process-wide state, so it
/// can't live in the request-scoped <see cref="ServerService"/>.
/// </summary>
public sealed class ServerManager(
    ILogger<ServerManager> logger,
    IServiceScopeFactory scopeFactory,
    IEnumerable<IServerEngine> serverEngines,
    SettingsProvider<Settings.Settings> settingsProvider,
    IRconCommandSender rconCommandSender)
{
    /// <summary>How many recent output lines are kept per server for <see cref="GetRecentLog"/>.</summary>
    public const int RecentLogCapacity = 1000;

    // Pending debounced stops keyed by gameId. Held here (singleton) because the request-scoped
    // ServerService can't keep pending-stop state across requests.
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _pendingStops = new();

    #region Initialization & tracking

    /// <summary>
    /// Initializes every engine and primes its tracking. Called once at application start.
    /// </summary>
    public async Task InitializeAsync()
    {
        SubscribeToEngineLogs();

        foreach (var engine in serverEngines)
        {
            await engine.InitializeAsync();
            await engine.RefreshTrackingAsync();
        }
    }

    /// <summary>
    /// Re-syncs each engine's tracked-server set with the database. Called after a server is
    /// added or edited so freshly added/edited servers become immediately manageable.
    /// </summary>
    public async Task RefreshTrackingAsync()
    {
        foreach (var engine in serverEngines)
            await engine.RefreshTrackingAsync();
    }

    public bool IsManaging(Guid serverId) => serverEngines.Any(engine => engine.IsManaging(serverId));

    #endregion

    #region Routing

    public async Task StartAsync(Guid serverId)
    {
        foreach (var engine in serverEngines)
        {
            if (engine.IsManaging(serverId))
            {
                await engine.StartAsync(serverId);
                return;
            }
        }
    }

    public async Task StopAsync(Guid serverId)
    {
        foreach (var engine in serverEngines)
        {
            if (engine.IsManaging(serverId))
            {
                await engine.StopAsync(serverId);
                return;
            }
        }
    }

    public async Task<ServerProcessStatus> GetStatusAsync(Guid serverId)
    {
        foreach (var engine in serverEngines)
        {
            if (engine.IsManaging(serverId))
                return await engine.GetStatusAsync(serverId);
        }

        return ServerProcessStatus.Stopped;
    }

    /// <summary>
    /// Stops the server, waits for it to report <see cref="ServerProcessStatus.Stopped"/>, then
    /// starts it again. Returns once the new start has been kicked off, not for the server's
    /// lifetime (unlike <see cref="StartAsync"/> on the local engine).
    /// </summary>
    public async Task RestartAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var engine = GetEngine(serverId);

        if (engine == null)
            return;

        if (await engine.GetStatusAsync(serverId) != ServerProcessStatus.Stopped)
        {
            await engine.StopAsync(serverId);

            await WaitForStatusAsync(engine, serverId, s => s is ServerProcessStatus.Stopped or ServerProcessStatus.Error, TimeSpan.FromSeconds(30), cancellationToken);
        }

        // An engine may still be unwinding the previous run for a moment after it reports Stopped
        // and ignore a start in that window, so retry until the server leaves the Stopped state.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await engine.StartAsync(serverId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Could not restart server {ServerId}", serverId);
                }
            }, CancellationToken.None);

            if (await WaitForStatusAsync(engine, serverId, s => s != ServerProcessStatus.Stopped, TimeSpan.FromSeconds(2), cancellationToken))
                return;
        }

        logger.LogWarning("Server {ServerId} did not start after a restart", serverId);
    }

    private static async Task<bool> WaitForStatusAsync(IServerEngine engine, Guid serverId, Func<ServerProcessStatus, bool> predicate, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (true)
        {
            if (predicate(await engine.GetStatusAsync(serverId)))
                return true;

            if (DateTime.UtcNow >= deadline)
                return false;

            await Task.Delay(100, cancellationToken);
        }
    }

    /// <summary>
    /// Samples the server's process: pid, start time, CPU% and memory. Null when the server
    /// isn't running or its engine can't observe the process (e.g. remote servers).
    /// </summary>
    public async Task<ServerProcessInfo?> GetProcessInfoAsync(Guid serverId)
    {
        var engine = GetEngine(serverId);

        if (engine == null)
            return null;

        try
        {
            return await engine.GetProcessInfoAsync(serverId);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not sample the process for server {ServerId}", serverId);

            return null;
        }
    }

    private IServerEngine? GetEngine(Guid serverId) => serverEngines.FirstOrDefault(engine => engine.IsManaging(serverId));

    #endregion

    #region Commands

    /// <summary>
    /// Whether <see cref="SendCommandAsync"/> can currently deliver a command. With a console id
    /// for an RCON console this means the console is configured; otherwise (no console, or a log
    /// file console) it means the server process is running with redirected standard input,
    /// which isn't the case for servers started with shell execute.
    /// </summary>
    public async Task<bool> CanSendCommandAsync(Guid serverId, Guid? consoleId = null)
    {
        if (consoleId.HasValue && consoleId.Value != Guid.Empty)
        {
            var console = await GetConsoleAsync(consoleId.Value);

            if (console == null || console.ServerId != serverId)
                return false;

            if (console.Type == ServerConsoleType.RCON)
                return rconCommandSender.CanSend(console);
        }

        return GetEngine(serverId)?.CanSendInput(serverId) ?? false;
    }

    /// <summary>
    /// Sends a command to a server. RCON consoles receive it over RCON (and the response is
    /// returned); anything else is written as a line to the server process's standard input,
    /// whose effects arrive as ordinary output lines.
    /// </summary>
    public async Task<ServerCommandResult> SendCommandAsync(Guid serverId, Guid? consoleId, string text, CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrWhiteSpace(text))
            return ServerCommandResult.Failed("The command is empty");

        if (consoleId.HasValue && consoleId.Value != Guid.Empty)
        {
            var console = await GetConsoleAsync(consoleId.Value);

            if (console == null || console.ServerId != serverId)
                return ServerCommandResult.Failed("The console could not be found");

            if (console.Type == ServerConsoleType.RCON)
            {
                if (!rconCommandSender.CanSend(console))
                    return ServerCommandResult.Failed("The RCON console has no host or port configured");

                try
                {
                    var response = await rconCommandSender.SendCommandAsync(console, text, cancellationToken);

                    return new ServerCommandResult(true, response);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Could not send an RCON command to server {ServerId}", serverId);

                    return ServerCommandResult.Failed($"RCON command failed: {ex.Message}");
                }
            }
        }

        var engine = GetEngine(serverId);

        if (engine == null)
            return ServerCommandResult.Failed("The server is not managed by any engine");

        if (!engine.CanSendInput(serverId))
            return ServerCommandResult.Failed("The server isn't running, or wasn't started with redirected input (shell execute)");

        return await engine.SendInputAsync(serverId, text)
            ? new ServerCommandResult(true)
            : ServerCommandResult.Failed("The command could not be written to the server's input");
    }

    private async Task<Data.Models.ServerConsole?> GetConsoleAsync(Guid consoleId)
    {
        using var scope = scopeFactory.CreateScope();
        var serverConsoleService = scope.ServiceProvider.GetRequiredService<ServerConsoleService>();

        return await serverConsoleService.AsNoTracking().GetAsync(consoleId);
    }

    #endregion

    #region Output

    private readonly ConcurrentDictionary<Guid, Queue<ServerLogLine>> _recentLogs = new();
    private int _logsSubscribed;

    /// <summary>
    /// Raised for every line of server output from any engine, already timestamped and
    /// classified. Handlers run on the engine's output thread; keep them short.
    /// </summary>
    public event EventHandler<ServerLogEventArgs>? OnServerLog;

    /// <summary>
    /// The most recent output lines for a server (up to <see cref="RecentLogCapacity"/>), oldest
    /// first. Pass a console id to get only that log file console's lines, or null for the
    /// process's own output.
    /// </summary>
    public IReadOnlyList<ServerLogLine> GetRecentLog(Guid serverId, Guid? consoleId = null)
    {
        if (!_recentLogs.TryGetValue(serverId, out var buffer))
            return [];

        lock (buffer)
            return buffer.Where(l => l.ConsoleId == consoleId).ToList();
    }

    /// <summary>Forgets the buffered output for a server (the console's Clear button).</summary>
    public void ClearRecentLog(Guid serverId)
    {
        if (_recentLogs.TryGetValue(serverId, out var buffer))
            lock (buffer)
                buffer.Clear();
    }

    private void SubscribeToEngineLogs()
    {
        if (Interlocked.Exchange(ref _logsSubscribed, 1) == 1)
            return;

        foreach (var engine in serverEngines)
            engine.OnServerLog += HandleServerLog;
    }

    private void HandleServerLog(object? sender, ServerLogEventArgs args)
    {
        var buffer = _recentLogs.GetOrAdd(args.ServerId, _ => new Queue<ServerLogLine>());

        lock (buffer)
        {
            buffer.Enqueue(args.ToLogLine());

            while (buffer.Count > RecentLogCapacity)
                buffer.Dequeue();
        }

        try
        {
            OnServerLog?.Invoke(this, args);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "A server output handler failed");
        }
    }

    #endregion

    #region Autostart

    /// <summary>
    /// Starts a game's autostart servers matching the given method. Each start is fire-and-forget
    /// because <see cref="IServerEngine.StartAsync"/> blocks for the server process's lifetime.
    /// Cancels any pending debounced stop for the game first, so a returning player keeps the
    /// servers running.
    /// </summary>
    public async Task AutostartAsync(Guid gameId, ServerAutostartMethod method)
    {
        CancelPendingStop(gameId);

        try
        {
            var servers = await GetServersAsync(s =>
                s.GameId == gameId && s.Autostart && s.AutostartMethod == method);

            foreach (var engine in serverEngines)
            {
                foreach (var server in servers)
                {
                    try
                    {
                        var status = await engine.GetStatusAsync(server.Id);

                        if (engine.IsManaging(server.Id) && status == ServerProcessStatus.Stopped)
                            _ = engine.StartAsync(server.Id);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to start server {ServerName} ({ServerId})", server.Name, server.Id);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Servers could not be autostarted");
        }
    }

    /// <summary>
    /// Autostarts every server configured for <see cref="ServerAutostartMethod.OnApplicationStart"/>,
    /// honoring each server's configured startup delay. Called once at boot.
    /// </summary>
    public async Task AutostartApplicationServersAsync()
    {
        var servers = await GetServersAsync(s =>
            s.Autostart && s.AutostartMethod == ServerAutostartMethod.OnApplicationStart);

        foreach (var server in servers)
        {
            var serverId = server.Id;
            var serverName = server.Name;
            var delaySeconds = server.AutostartDelay;

            logger.LogDebug("Autostarting server {ServerName} with a delay of {AutostartDelay} seconds", serverName, delaySeconds);

            _ = Task.Run(async () =>
            {
                try
                {
                    if (delaySeconds > 0)
                        await Task.Delay(TimeSpan.FromSeconds(delaySeconds));

                    await StartAsync(serverId);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An unexpected error occurred while trying to autostart the server {ServerName}", serverName);
                }
            });
        }
    }

    #endregion

    #region Autostop

    /// <summary>
    /// Immediately stops a game's autostart servers matching the given method.
    /// </summary>
    public async Task AutostopAsync(Guid gameId, ServerAutostartMethod method)
    {
        try
        {
            var servers = await GetServersAsync(s =>
                s.GameId == gameId && s.Autostart && s.AutostartMethod == method);

            foreach (var engine in serverEngines)
            {
                foreach (var server in servers)
                {
                    try
                    {
                        var status = await engine.GetStatusAsync(server.Id);

                        if (engine.IsManaging(server.Id) && status != ServerProcessStatus.Stopped)
                            await engine.StopAsync(server.Id);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to stop server {ServerName} ({ServerId})", server.Name, server.Id);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Servers could not be autostopped");
        }
    }

    /// <summary>
    /// Stops every tracked server across all engines. Used when the application needs to bring
    /// everything down (e.g. before applying a server update).
    /// </summary>
    public async Task StopAllAsync()
    {
        var servers = await GetServersAsync(_ => true);

        foreach (var engine in serverEngines)
        {
            foreach (var server in servers)
            {
                if (engine.IsManaging(server.Id))
                    await engine.StopAsync(server.Id);
            }
        }
    }

    /// <summary>
    /// Cancels any pending autostop for the game (e.g. a player relaunched within the delay).
    /// </summary>
    public void CancelPendingStop(Guid gameId)
    {
        if (_pendingStops.TryRemove(gameId, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    /// <summary>
    /// Schedules the game's on-player-activity servers to stop after the configured delay,
    /// resetting any already-pending stop. Debounces stop/start thrash when players relaunch.
    /// </summary>
    public void ScheduleStop(Guid gameId)
    {
        var delay = TimeSpan.FromSeconds(Math.Max(0, settingsProvider.CurrentValue.Server.GameServers.AutostopDelay));

        CancelPendingStop(gameId);

        var cts = new CancellationTokenSource();
        _pendingStops[gameId] = cts;

        _ = RunDelayedStopAsync(gameId, delay, cts.Token);
    }

    private async Task RunDelayedStopAsync(Guid gameId, TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);

            using var scope = scopeFactory.CreateScope();
            var playSessionService = scope.ServiceProvider.GetRequiredService<PlaySessionService>();

            // Re-check after the delay: a player may have started again without us having observed
            // a cancellation, so the authoritative source is the session table.
            var activeSessions = await playSessionService.GetAsync(ps => ps.GameId == gameId && ps.End == null);

            if (!activeSessions.Any())
                await AutostopAsync(gameId, ServerAutostartMethod.OnPlayerActivity);
        }
        catch (OperationCanceledException)
        {
            // A player relaunched within the debounce window; leave the servers running.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to autostop servers for game {GameId}", gameId);
        }
        finally
        {
            _pendingStops.TryRemove(gameId, out _);
        }
    }

    #endregion

    private async Task<ICollection<Data.Models.Server>> GetServersAsync(Expression<Func<Data.Models.Server, bool>> predicate)
    {
        using var scope = scopeFactory.CreateScope();
        var serverService = scope.ServiceProvider.GetRequiredService<ServerService>();

        return await serverService.GetAsync(predicate);
    }
}
