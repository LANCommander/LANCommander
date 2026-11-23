using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace LANCommander.SDK.Models;

/// <summary>
/// Who is typing in one thread, as heard from the other participants. Each start holds for
/// <see cref="Lifetime"/> and lapses unless renewed, so someone whose stop never arrives (they
/// closed the launcher mid-sentence, say) doesn't stay typing forever.
/// </summary>
public sealed class ChatTypingState : IDisposable
{
    /// <summary>
    /// How long a start holds without being renewed. Comfortably longer than
    /// <see cref="Services.ChatTypingNotifier.RenewInterval"/>, so a steady typist never flickers out
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(10);

    readonly TimeProvider _timeProvider;
    readonly ITimer _expiryTimer;
    readonly object _lock = new();

    // In the order they started typing, so the description doesn't reshuffle as starts are renewed
    readonly List<(Guid UserId, DateTimeOffset ExpiresOn)> _typists = new();

    /// <summary>Someone started or stopped typing. Raised on whichever thread noticed</summary>
    public event System.Action? Changed;

    public ChatTypingState(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _expiryTimer = _timeProvider.CreateTimer(_ => Expire(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public IReadOnlyList<Guid> UserIds
    {
        get
        {
            lock (_lock)
                return _typists.Select(t => t.UserId).ToList();
        }
    }

    public bool IsAnyoneTyping
    {
        get
        {
            lock (_lock)
                return _typists.Count > 0;
        }
    }

    public void Start(Guid userId)
    {
        bool added;

        lock (_lock)
        {
            var expiresOn = _timeProvider.GetUtcNow() + Lifetime;
            var index = _typists.FindIndex(t => t.UserId == userId);

            added = index < 0;

            if (added)
                _typists.Add((userId, expiresOn));
            else
                _typists[index] = (userId, expiresOn);

            ScheduleExpiry();
        }

        if (added)
            Changed?.Invoke();
    }

    public void Stop(Guid userId)
    {
        bool removed;

        lock (_lock)
        {
            removed = _typists.RemoveAll(t => t.UserId == userId) > 0;

            ScheduleExpiry();
        }

        if (removed)
            Changed?.Invoke();
    }

    /// <summary>
    /// A line such as "Alice is typing…" naming the typists among the given participants, or
    /// null while nobody is. Anyone typing who isn't among them (the viewer, say) is left out
    /// </summary>
    public string? Describe(IEnumerable<User> participants)
    {
        var names = participants.ToDictionary(p => p.Id, p => p.Name);

        return Describe(UserIds.Where(names.ContainsKey).Select(id => names[id]).ToList());
    }

    public static string? Describe(IReadOnlyList<string> names) => names.Count switch
    {
        0 => null,
        1 => $"{names[0]} is typing…",
        2 => $"{names[0]} and {names[1]} are typing…",
        3 => $"{names[0]}, {names[1]} and {names[2]} are typing…",
        _ => "Several people are typing…",
    };

    void Expire()
    {
        bool removed;

        lock (_lock)
        {
            var now = _timeProvider.GetUtcNow();

            removed = _typists.RemoveAll(t => t.ExpiresOn <= now) > 0;

            ScheduleExpiry();
        }

        if (removed)
            Changed?.Invoke();
    }

    // Called under the lock: wakes when the soonest start lapses, or not at all if nobody's typing
    void ScheduleExpiry()
    {
        if (_typists.Count == 0)
        {
            _expiryTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

            return;
        }

        var due = _typists.Min(t => t.ExpiresOn) - _timeProvider.GetUtcNow();

        _expiryTimer.Change(due < TimeSpan.Zero ? TimeSpan.Zero : due, Timeout.InfiniteTimeSpan);
    }

    public void Dispose() => _expiryTimer.Dispose();
}
