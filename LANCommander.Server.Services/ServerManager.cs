using System.Collections.Concurrent;
using System.Linq.Expressions;
using LANCommander.SDK.Enums;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Enums;
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
    SettingsProvider<Settings.Settings> settingsProvider)
{
    // Pending debounced stops keyed by gameId. Held here (singleton) because the request-scoped
    // ServerService can't keep pending-stop state across requests.
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _pendingStops = new();

    #region Initialization & tracking

    /// <summary>
    /// Initializes every engine and primes its tracking. Called once at application start.
    /// </summary>
    public async Task InitializeAsync()
    {
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
