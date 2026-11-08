using System.Threading.Channels;

namespace LANCommander.Server.Services.MatchStatistics;

/// <summary>An accepted event waiting to be written, with its payload already reduced to JSON text.</summary>
public sealed record QueuedMatchEvent(
    Guid Id,
    Guid MatchId,
    Guid GameId,
    Guid ReporterId,
    long Sequence,
    string Type,
    long GameTimeMilliseconds,
    long? Tick,
    string? ActorKey,
    string? SubjectKey,
    string? Payload,
    DateTime? ClientTime,
    DateTime ReceivedOn);

/// <summary>
/// Where reported events wait to be written. Requests only validate and enqueue, so reporting never
/// waits on the database; <see cref="MatchEventWriter"/> drains the queue in batches.
/// </summary>
/// <remarks>
/// The in-memory implementation loses what is queued if the process dies. Reporters cover that by
/// resending everything after their reporter's last written sequence number. An external broker can
/// replace it by implementing this and <see cref="IMatchEventSource"/>.
/// </remarks>
public interface IMatchEventQueue
{
    /// <summary>Queues every event, or none of them when there isn't room for all.</summary>
    bool TryEnqueue(IReadOnlyList<QueuedMatchEvent> events);

    /// <summary>Queues every event, waiting for room.</summary>
    ValueTask EnqueueAsync(IReadOnlyList<QueuedMatchEvent> events, CancellationToken cancellationToken = default);

    /// <summary>Events queued and not yet taken by the writer.</summary>
    int Count { get; }
}

/// <summary>The writer's side of <see cref="IMatchEventQueue"/>.</summary>
public interface IMatchEventSource
{
    /// <summary>
    /// Waits for at least one event, then gathers more until <paramref name="maximum"/> are taken or
    /// <paramref name="linger"/> has passed. Returns an empty list only once the queue is completed.
    /// </summary>
    ValueTask<IReadOnlyList<QueuedMatchEvent>> ReadBatchAsync(int maximum, TimeSpan linger, CancellationToken cancellationToken);

    /// <summary>Takes whatever is queued without waiting, for draining on shutdown.</summary>
    IReadOnlyList<QueuedMatchEvent> TakeAll();
}

/// <summary>
/// A <see cref="Channel{T}"/> with its own count, so a batch is accepted or refused whole: a channel's
/// own bound applies per item, which would let half a batch in.
/// </summary>
public sealed class ChannelMatchEventQueue : IMatchEventQueue, IMatchEventSource
{
    private readonly Channel<QueuedMatchEvent> _channel = Channel.CreateUnbounded<QueuedMatchEvent>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    private readonly Func<int> _capacity;
    private int _count;

    public ChannelMatchEventQueue(Func<int> capacity)
    {
        _capacity = capacity;
    }

    public int Count => Volatile.Read(ref _count);

    public bool TryEnqueue(IReadOnlyList<QueuedMatchEvent> events)
    {
        if (events.Count == 0)
            return true;

        var capacity = Math.Max(1, _capacity());

        while (true)
        {
            var current = Volatile.Read(ref _count);

            if (current + events.Count > capacity)
                return false;

            if (Interlocked.CompareExchange(ref _count, current + events.Count, current) == current)
                break;
        }

        foreach (var queued in events)
        {
            // Unbounded, so this only fails once completed, which happens after the writer stops
            if (!_channel.Writer.TryWrite(queued))
                Interlocked.Decrement(ref _count);
        }

        return true;
    }

    public async ValueTask EnqueueAsync(IReadOnlyList<QueuedMatchEvent> events, CancellationToken cancellationToken = default)
    {
        if (events.Count > Math.Max(1, _capacity()))
            throw new ArgumentException("The batch is larger than the queue", nameof(events));

        while (!TryEnqueue(events))
            await Task.Delay(50, cancellationToken);
    }

    public async ValueTask<IReadOnlyList<QueuedMatchEvent>> ReadBatchAsync(int maximum, TimeSpan linger, CancellationToken cancellationToken)
    {
        var batch = new List<QueuedMatchEvent>(Math.Min(maximum, 1024));
        var reader = _channel.Reader;

        if (!await reader.WaitToReadAsync(cancellationToken))
            return batch;

        Drain(batch, maximum);

        if (batch.Count < maximum && linger > TimeSpan.Zero)
        {
            using var lingerSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            lingerSource.CancelAfter(linger);

            try
            {
                while (batch.Count < maximum && await reader.WaitToReadAsync(lingerSource.Token))
                    Drain(batch, maximum);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // The linger ran out: write what there is
            }
        }

        return batch;
    }

    public IReadOnlyList<QueuedMatchEvent> TakeAll()
    {
        var batch = new List<QueuedMatchEvent>();

        Drain(batch, int.MaxValue);

        return batch;
    }

    private void Drain(List<QueuedMatchEvent> batch, int maximum)
    {
        while (batch.Count < maximum && _channel.Reader.TryRead(out var queued))
        {
            batch.Add(queued);
            Interlocked.Decrement(ref _count);
        }
    }
}
