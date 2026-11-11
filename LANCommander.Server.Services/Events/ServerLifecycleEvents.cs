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
        _subscriptions.Add(pluginEvents.Subscribe<GameUpdatedEvent>((e, _) =>
            PublishAsync(ServerEventTypes.GameUpdated, "game", e.GameId, new { e.Title })));
        _subscriptions.Add(pluginEvents.Subscribe<GameDeletedEvent>((e, _) =>
            PublishAsync(ServerEventTypes.GameDeleted, "game", e.GameId, new { e.Title })));
        _subscriptions.Add(pluginEvents.Subscribe<UserCreatedEvent>((e, _) =>
            PublishAsync(ServerEventTypes.UserCreated, "user", e.UserId, new { e.UserName, e.Approved })));
        _subscriptions.Add(pluginEvents.Subscribe<UserApprovedEvent>((e, _) =>
            PublishAsync(ServerEventTypes.UserApproved, "user", e.UserId, new { e.UserName })));
        _subscriptions.Add(pluginEvents.Subscribe<UserDeletedEvent>((e, _) =>
            PublishAsync(ServerEventTypes.UserDeleted, "user", e.UserId, new { e.UserName })));
        _subscriptions.Add(pluginEvents.Subscribe<IssueCreatedEvent>((e, _) =>
            PublishAsync(ServerEventTypes.IssueCreated, "game", e.GameId,
                new { e.IssueId, e.Description }, e.ReportedByUserId)));
        _subscriptions.Add(pluginEvents.Subscribe<IssueResolvedEvent>((e, _) =>
            PublishAsync(ServerEventTypes.IssueResolved, "game", e.GameId,
                new { e.IssueId }, e.ResolvedByUserId)));
        _subscriptions.Add(pluginEvents.Subscribe<MatchCreatedEvent>((e, _) =>
            PublishAsync(ServerEventTypes.MatchCreated, "game", e.GameId,
                new { e.MatchId, e.ServerId, e.Map, e.Mode })));
        // Results go out without each reporter's free-form payload, which has no size limit or schema.
        _subscriptions.Add(pluginEvents.Subscribe<MatchEndedEvent>((e, _) =>
            PublishAsync(ServerEventTypes.MatchEnded, "game", e.GameId, new
            {
                e.MatchId,
                e.ServerId,
                e.Status,
                e.Reason,
                Results = e.Results.Select(r => new
                {
                    r.ParticipantId, r.PlayerKey, r.UserId, r.Team, r.Rank, r.Score, r.Status, r.TimeMilliseconds,
                }).ToArray(),
            })));
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
