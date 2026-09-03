using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services.HQ;

/// <summary>
/// Verifies the stored LANCommander HQ credential shortly after boot, then keeps it honest on an
/// interval.
///
/// This is what makes connection state real at startup. Before it existed, nothing checked HQ until
/// an admin happened to open the HQ settings page, so a credential could rot unnoticed.
///
/// The interval also keeps the refresh token alive: it expires on inactivity, and the clock resets
/// on every use, so a server that never calls HQ would eventually be signed out for idling.
///
/// Modelled on <see cref="PlaySessionSweepService"/>, including re-reading the interval from
/// settings each pass so configuration changes apply without a restart.
/// </summary>
public sealed class HqConnectionMonitor(
    HqConnectionService connection,
    SettingsProvider<Settings.Settings> settingsProvider,
    ILogger<HqConnectionMonitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Guarantees the host's startup is never blocked behind a network call.
        await Task.Yield();

        // Let migrations and game server autostart settle before adding outbound traffic.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await connection.VerifyAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // BackgroundServiceExceptionBehavior defaults to StopHost, so nothing may escape
                // here — a failed HQ check must never take the server down.
                logger.LogError(ex, "LANCommander HQ connection verification failed.");
            }

            try
            {
                await Task.Delay(GetInterval(), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Backs off when there is nothing to gain from checking, and retries sooner when the problem
    /// looks transient.
    /// </summary>
    private TimeSpan GetInterval()
    {
        var configured = TimeSpan.FromSeconds(
            Math.Clamp(settingsProvider.CurrentValue.Server.HQ.VerifyIntervalSeconds, 60, 86400));

        return connection.Current.Status switch
        {
            // Probably a blip; look again sooner.
            HqConnectionStatus.Unreachable => TimeSpan.FromTicks(Math.Min(
                configured.Ticks, TimeSpan.FromMinutes(5).Ticks)),

            // Neither self-heals without an admin reconnecting, so stop hammering HQ.
            HqConnectionStatus.Unauthorized or HqConnectionStatus.Disconnected => TimeSpan.FromTicks(
                Math.Max(configured.Ticks, TimeSpan.FromMinutes(30).Ticks)),

            _ => configured,
        };
    }
}
