using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services;

/// <summary>
/// Periodically ends play sessions that have stopped sending keepalives (e.g. the launcher crashed
/// or lost its connection), so the active-session list stays accurate for autostop and player
/// counts. Stale sessions are ended at their last known-alive time. Each affected game is handed to
/// <see cref="ServerManager.ScheduleStop"/> so on-player-activity servers still autostop.
/// </summary>
public sealed class PlaySessionSweepService(
    IServiceScopeFactory scopeFactory,
    ServerManager serverManager,
    SettingsProvider<Settings.Settings> settingsProvider,
    ILogger<PlaySessionSweepService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var gameServers = settingsProvider.CurrentValue.Server.GameServers;
                var interval = TimeSpan.FromSeconds(Math.Max(5, gameServers.KeepAliveSweepInterval));
                var timeout = TimeSpan.FromSeconds(Math.Max(1, gameServers.KeepAliveTimeout));

                await Task.Delay(interval, stoppingToken);

                using var scope = scopeFactory.CreateScope();
                var playSessionService = scope.ServiceProvider.GetRequiredService<PlaySessionService>();

                var affectedGameIds = await playSessionService.EndStaleSessionsAsync(timeout);

                foreach (var gameId in affectedGameIds)
                    serverManager.ScheduleStop(gameId);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Play session sweep failed");
            }
        }
    }
}
