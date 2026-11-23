using LANCommander.SDK.Models;
using LANCommander.SDK.Services;

namespace LANCommander.SDK.Tests.Chat;

public class ChatTypingStateTests
{
    static readonly Guid Alice = Guid.NewGuid();
    static readonly Guid Bob = Guid.NewGuid();

    [Fact]
    public void StartAndStopRaiseChangedOnlyWhenTheTypistsChange()
    {
        var clock = new ManualTimeProvider();
        using var state = new ChatTypingState(clock);
        var changes = 0;
        state.Changed += () => changes++;

        state.Start(Alice);
        state.Start(Alice);
        state.Stop(Bob);

        Assert.Equal(1, changes);
        Assert.Equal([Alice], state.UserIds);

        state.Stop(Alice);

        Assert.Equal(2, changes);
        Assert.False(state.IsAnyoneTyping);
    }

    [Fact]
    public void AStartLapsesUnlessRenewed()
    {
        var clock = new ManualTimeProvider();
        using var state = new ChatTypingState(clock);

        state.Start(Alice);
        state.Start(Bob);

        clock.Advance(ChatTypingState.Lifetime - TimeSpan.FromSeconds(1));
        state.Start(Bob);

        clock.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal([Bob], state.UserIds);

        clock.Advance(ChatTypingState.Lifetime);

        Assert.Empty(state.UserIds);
    }

    [Fact]
    public void DescribeNamesTheParticipantsTyping()
    {
        using var state = new ChatTypingState(new ManualTimeProvider());
        var participants = new[]
        {
            new User { Id = Alice, UserName = "alice", Alias = "Alice" },
            new User { Id = Bob, UserName = "bob", Alias = "Bob" },
        };

        Assert.Null(state.Describe(participants));

        state.Start(Alice);
        Assert.Equal("Alice is typing…", state.Describe(participants));

        state.Start(Bob);
        Assert.Equal("Alice and Bob are typing…", state.Describe(participants));

        // Someone outside the list given (the viewer, say) isn't named
        state.Start(Guid.NewGuid());
        Assert.Equal("Alice and Bob are typing…", state.Describe(participants));
    }

    [Fact]
    public void DescribeSummarisesACrowd()
    {
        Assert.Equal("A, B and C are typing…", ChatTypingState.Describe(["A", "B", "C"]));
        Assert.Equal("Several people are typing…", ChatTypingState.Describe(["A", "B", "C", "D"]));
    }
}

public class ChatTypingNotifierTests
{
    static readonly Guid ThreadA = Guid.NewGuid();
    static readonly Guid ThreadB = Guid.NewGuid();

    readonly ManualTimeProvider _clock = new();
    readonly List<(Guid ThreadId, bool Typing)> _sent = new();

    ChatTypingNotifier CreateNotifier() => new((threadId, typing) =>
    {
        lock (_sent)
            _sent.Add((threadId, typing));

        return Task.CompletedTask;
    }, _clock);

    async Task<List<(Guid ThreadId, bool Typing)>> SentAsync()
    {
        // Sends are queued off the caller's thread
        for (var i = 0; i < 20; i++)
        {
            await Task.Delay(10);
        }

        lock (_sent)
            return _sent.ToList();
    }

    [Fact]
    public async Task KeystrokesSendOneStartUntilItNeedsRenewing()
    {
        using var notifier = CreateNotifier();

        notifier.Typed(ThreadA);
        _clock.Advance(TimeSpan.FromSeconds(1));
        notifier.Typed(ThreadA);
        _clock.Advance(TimeSpan.FromSeconds(1));
        notifier.Typed(ThreadA);

        Assert.Equal([(ThreadA, true)], await SentAsync());

        _clock.Advance(ChatTypingNotifier.RenewInterval);
        notifier.Typed(ThreadA);

        Assert.Equal([(ThreadA, true), (ThreadA, true)], await SentAsync());
    }

    [Fact]
    public async Task PausingSendsAStop()
    {
        using var notifier = CreateNotifier();

        notifier.Typed(ThreadA);
        _clock.Advance(ChatTypingNotifier.IdleTimeout);

        Assert.Equal([(ThreadA, true), (ThreadA, false)], await SentAsync());
    }

    [Fact]
    public async Task StopIsOnlySentWhileTyping()
    {
        using var notifier = CreateNotifier();

        notifier.Stop();
        notifier.Typed(ThreadA);
        notifier.Stop();
        notifier.Stop();

        // Nor does the idle timeout send a second one
        _clock.Advance(ChatTypingNotifier.IdleTimeout);

        Assert.Equal([(ThreadA, true), (ThreadA, false)], await SentAsync());
    }

    [Fact]
    public async Task TypingInAnotherThreadStopsTheFirst()
    {
        using var notifier = CreateNotifier();

        notifier.Typed(ThreadA);
        notifier.Typed(ThreadB);

        Assert.Equal([(ThreadA, true), (ThreadA, false), (ThreadB, true)], await SentAsync());
    }

    [Fact]
    public async Task AFailedSendDoesNotStopLaterOnes()
    {
        var sent = new List<bool>();

        using var notifier = new ChatTypingNotifier((_, typing) =>
        {
            lock (sent)
                sent.Add(typing);

            return typing ? Task.FromException(new InvalidOperationException()) : Task.CompletedTask;
        }, _clock);

        notifier.Typed(ThreadA);
        notifier.Stop();

        await SentAsync();

        lock (sent)
            Assert.Equal([true, false], sent);
    }
}

/// <summary>A clock that only moves when told to, firing whatever timers fall due along the way</summary>
sealed class ManualTimeProvider : TimeProvider
{
    readonly object _lock = new();
    readonly List<ManualTimer> _timers = new();
    DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow()
    {
        lock (_lock)
            return _now;
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);

        lock (_lock)
            _timers.Add(timer);

        timer.Change(dueTime, period);

        return timer;
    }

    public void Advance(TimeSpan by)
    {
        DateTimeOffset target;

        lock (_lock)
            target = _now + by;

        while (true)
        {
            ManualTimer? next;

            lock (_lock)
            {
                next = _timers
                    .Where(t => t.DueOn is { } due && due <= target)
                    .MinBy(t => t.DueOn);

                if (next == null)
                {
                    _now = target;

                    return;
                }

                _now = next.DueOn!.Value;
                next.DueOn = null;
            }

            next.Fire();
        }
    }

    void Remove(ManualTimer timer)
    {
        lock (_lock)
            _timers.Remove(timer);
    }

    sealed class ManualTimer(ManualTimeProvider clock, TimerCallback callback, object? state) : ITimer
    {
        public DateTimeOffset? DueOn { get; set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (clock._lock)
                DueOn = dueTime == Timeout.InfiniteTimeSpan ? null : clock._now + dueTime;

            return true;
        }

        public void Fire() => callback(state);

        public void Dispose() => clock.Remove(this);

        public ValueTask DisposeAsync()
        {
            Dispose();

            return ValueTask.CompletedTask;
        }
    }
}
