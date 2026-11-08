using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services.MatchStatistics;

/// <summary>
/// Runs follow-up work for written events, one item at a time and in order: raising
/// <c>MatchEventsRecordedEvent</c> to plugins and linking new participants to users.
/// </summary>
/// <remarks>
/// The plugin event bus awaits its handlers, so publishing from the writer would let a slow plugin
/// hold up every write. Here it only delays later notifications. When the backlog is full, work is
/// dropped with a warning; unlinked participants are picked up again by the maintenance job.
/// </remarks>
public sealed class MatchNotificationDispatcher(
    IServiceScopeFactory scopeFactory,
    ILogger<MatchNotificationDispatcher> logger) : BackgroundService
{
    private const int Capacity = 10_000;

    private readonly Channel<(string Description, Func<IServiceProvider, CancellationToken, Task> Work)> _channel =
        Channel.CreateBounded<(string, Func<IServiceProvider, CancellationToken, Task>)>(
            new BoundedChannelOptions(Capacity) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    /// <summary>Queues work to run in its own service scope. Returns false when the backlog is full.</summary>
    public bool Post(string description, Func<IServiceProvider, CancellationToken, Task> work)
    {
        if (_channel.Writer.TryWrite((description, work)))
            return true;

        logger.LogWarning("Match notification backlog is full; skipped {Description}", description);

        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var (description, work) in _channel.Reader.ReadAllAsync(stoppingToken))
                await RunAsync(description, work, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task RunAsync(string description, Func<IServiceProvider, CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();

            await work(scope.ServiceProvider, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Match notification failed: {Description}", description);
        }
    }
}
