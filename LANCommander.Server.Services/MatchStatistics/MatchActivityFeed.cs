using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Models;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services.MatchStatistics;

public enum MatchActivityKind
{
    Started,
    Event,
    Ended,
    Abandoned,
}

/// <summary>One line of a server's match activity: a match starting or ending, or an event written.</summary>
/// <param name="Key">Identifies the line, so one read back from the database and pushed live is shown once.</param>
public sealed record MatchActivity(
    string Key,
    Guid MatchId,
    DateTime Timestamp,
    MatchActivityKind Kind,
    string? Type = null,
    long GameTimeMilliseconds = 0,
    string? ActorKey = null,
    string? SubjectKey = null,
    string? Payload = null,
    string? Map = null,
    string? Mode = null,
    string? Reason = null)
{
    public static MatchActivity FromEvent(QueuedMatchEvent e) =>
        new(e.Id.ToString(), e.MatchId, e.ReceivedOn, MatchActivityKind.Event, e.Type, e.GameTimeMilliseconds, e.ActorKey, e.SubjectKey, e.Payload);

    public static MatchActivity FromEvent(MatchEvent e) =>
        new(e.Id.ToString(), e.MatchId, e.ReceivedOn, MatchActivityKind.Event, e.Type, e.GameTimeMilliseconds, e.ActorKey, e.SubjectKey, e.Payload);

    public static MatchActivity Started(Match match) =>
        new($"start:{match.Id}", match.Id, match.StartedOn ?? match.CreatedOn, MatchActivityKind.Started, Map: match.Map, Mode: match.Mode);

    public static MatchActivity Ended(Match match) =>
        new($"end:{match.Id}", match.Id, match.EndedOn ?? match.UpdatedOn,
            match.Status == MatchStatus.Abandoned ? MatchActivityKind.Abandoned : MatchActivityKind.Ended,
            Map: match.Map, Mode: match.Mode, Reason: match.EndReason);
}

/// <summary>
/// Pushes match activity to whoever is watching a server, such as its Statistics page. Nothing is kept:
/// what happened before someone started watching is read back from the database.
/// </summary>
/// <remarks>
/// Publishers check <see cref="IsWatched"/> first, so an unwatched server costs nothing. Handlers run on
/// the publisher's thread (often the event writer's), so they must only hand the lines off.
/// </remarks>
public sealed class MatchActivityFeed(ILogger<MatchActivityFeed> logger)
{
    private readonly object _lock = new();
    private readonly Dictionary<Guid, List<Action<IReadOnlyList<MatchActivity>>>> _subscribers = new();

    public bool IsWatched(Guid? serverId)
    {
        if (serverId is not { } id)
            return false;

        lock (_lock)
            return _subscribers.ContainsKey(id);
    }

    /// <summary>Calls <paramref name="handler"/> with each batch of the server's activity until disposed.</summary>
    public IDisposable Subscribe(Guid serverId, Action<IReadOnlyList<MatchActivity>> handler)
    {
        lock (_lock)
        {
            if (!_subscribers.TryGetValue(serverId, out var handlers))
                _subscribers[serverId] = handlers = [];

            handlers.Add(handler);
        }

        return new Subscription(this, serverId, handler);
    }

    public void Publish(Guid? serverId, IReadOnlyList<MatchActivity> activity)
    {
        if (serverId is not { } id || activity.Count == 0)
            return;

        Action<IReadOnlyList<MatchActivity>>[] handlers;

        lock (_lock)
        {
            if (!_subscribers.TryGetValue(id, out var list))
                return;

            handlers = list.ToArray();
        }

        foreach (var handler in handlers)
        {
            try
            {
                handler(activity);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "A match activity watcher of server {ServerId} failed", id);
            }
        }
    }

    public void Publish(Guid? serverId, MatchActivity activity) => Publish(serverId, [activity]);

    private void Unsubscribe(Guid serverId, Action<IReadOnlyList<MatchActivity>> handler)
    {
        lock (_lock)
        {
            if (!_subscribers.TryGetValue(serverId, out var handlers))
                return;

            handlers.Remove(handler);

            if (handlers.Count == 0)
                _subscribers.Remove(serverId);
        }
    }

    private sealed class Subscription(MatchActivityFeed feed, Guid serverId, Action<IReadOnlyList<MatchActivity>> handler) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                feed.Unsubscribe(serverId, handler);
        }
    }
}
