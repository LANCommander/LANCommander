using LANCommander.SDK.Plugins;
using LANCommander.Server.Plugins;
using LANCommander.SDK.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LANCommander.Server.Services.Events;

public sealed class ServerLifecycleEvents(
    IPluginEventBus pluginEvents,
    IServiceScopeFactory scopeFactory) : IHostedService, IDisposable
{
    private readonly List<IDisposable> _subscriptions = [];
    private readonly HashSet<Guid> _runningServers = [];
    private readonly object _serverLock = new();

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_subscriptions.Count > 0)
            return Task.CompletedTask;
        _subscriptions.Add(pluginEvents.Subscribe<GameCreatedEvent>((e, _) =>
            PublishAsync(ServerEventTypes.GameCreated, "game", e.GameId, new { e.Title })));
        _subscriptions.Add(pluginEvents.Subscribe<ArchiveUploadedEvent>((e, _) => e.GameId.HasValue
            ? PublishAsync(ServerEventTypes.ArchiveUploaded, "game", e.GameId.Value,
                new { e.ArchiveId, e.GameVersionId, e.Version })
            : Task.CompletedTask));
        _subscriptions.Add(pluginEvents.Subscribe<PlaySessionStartedEvent>((e, _) =>
            PublishAsync(ServerEventTypes.PlayStarted, "game", e.GameId,
                new { e.SessionId, e.Start }, e.UserId)));
        _subscriptions.Add(pluginEvents.Subscribe<PlaySessionEndedEvent>((e, _) =>
            PublishAsync(ServerEventTypes.PlayEnded, "game", e.GameId,
                new { e.SessionId, e.Start, e.End, e.TimedOut }, e.UserId)));
        _subscriptions.Add(pluginEvents.Subscribe<ServerStatusChangedEvent>((e, _) =>
        {
            string? type;
            lock (_serverLock)
            {
                type = e.Status switch
                {
                    ServerEngineStatus.Running when _runningServers.Add(e.ServerId) => ServerEventTypes.ServerStarted,
                    ServerEngineStatus.Stopped when _runningServers.Remove(e.ServerId) => ServerEventTypes.ServerStopped,
                    ServerEngineStatus.Error when _runningServers.Remove(e.ServerId) => ServerEventTypes.ServerStopped,
                    _ => null,
                };
            }
            return type == null ? Task.CompletedTask
                : PublishAsync(type, "server", e.ServerId, new { e.ServerName });
        }));
        return Task.CompletedTask;
    }

    private async Task PublishAsync(string type, string target, Guid id, object payload, Guid? actor = null)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IServerEventPublisher>()
            .PublishAsync(type, target, id, payload, actor);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        foreach (var subscription in _subscriptions)
            subscription.Dispose();
        _subscriptions.Clear();
    }
}
