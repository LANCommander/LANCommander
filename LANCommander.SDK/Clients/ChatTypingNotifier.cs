using System;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.Services;

/// <summary>
/// Tells a thread's other participants while this user is typing in it, without a message per
/// keystroke. A start goes out on the first key and again every <see cref="RenewInterval"/> while
/// keys keep coming, renewing it before <see cref="ChatTypingState.Lifetime"/> lapses on the far
/// end; a stop goes out once they pause for <see cref="IdleTimeout"/>, or call <see cref="Stop"/>
/// (sent, cleared the box, moved to another thread).
/// </summary>
public sealed class ChatTypingNotifier : IDisposable
{
    public static readonly TimeSpan RenewInterval = TimeSpan.FromSeconds(3);
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(5);

    readonly Func<Guid, bool, Task> _send;
    readonly TimeProvider _timeProvider;
    readonly ITimer _idleTimer;
    readonly object _lock = new();

    Guid? _threadId;
    DateTimeOffset _startSentOn;

    // Sends go one after another, so a stop can't overtake the start it follows
    Task _sending = Task.CompletedTask;

    /// <param name="send">Sends a start (true) or stop (false) for a thread. Failures are ignored: typing is only a hint</param>
    public ChatTypingNotifier(Func<Guid, bool, Task> send, TimeProvider? timeProvider = null)
    {
        _send = send;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _idleTimer = _timeProvider.CreateTimer(_ => Stop(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>The user changed what they're writing in the given thread</summary>
    public void Typed(Guid threadId)
    {
        lock (_lock)
        {
            var now = _timeProvider.GetUtcNow();

            // Typing somewhere else means they've stopped here
            if (_threadId is { } previous && previous != threadId)
            {
                Enqueue(previous, false);

                _threadId = null;
            }

            if (_threadId == null || now - _startSentOn >= RenewInterval)
            {
                Enqueue(threadId, true);

                _startSentOn = now;
            }

            _threadId = threadId;

            _idleTimer.Change(IdleTimeout, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>The user stopped typing, if they were</summary>
    public void Stop()
    {
        lock (_lock)
        {
            _idleTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

            if (_threadId is not { } threadId)
                return;

            _threadId = null;

            Enqueue(threadId, false);
        }
    }

    // Called under the lock, so sends are chained in the order they were decided on
    void Enqueue(Guid threadId, bool typing)
    {
        _sending = _sending.ContinueWith(async _ =>
        {
            try
            {
                await _send(threadId, typing);
            }
            catch
            {
                // Typing is only a hint; the far end lets a missed stop lapse on its own
            }
        }, TaskScheduler.Default).Unwrap();
    }

    public void Dispose()
    {
        Stop();

        _idleTimer.Dispose();
    }
}
