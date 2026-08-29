using System.Collections.Concurrent;
using LANCommander.Launcher.Services.Packaging;
using LANCommander.Packaging;
using LANCommander.Packaging.Ipc;

namespace LANCommander.Launcher.Services.Tests.Packaging;

/// <summary>
/// A worker that records what it was told to do and can be made to say anything back.
/// </summary>
/// <remarks>
/// This is the seam that makes the routing and ledger logic testable: no processes are
/// spawned, no pipes are opened, and nothing is injected into anything.
/// </remarks>
public sealed class FakePackagingWorker : IPackagingWorkerChannel
{
    public FakePackagingWorker(ProcessArchitecture architecture, bool isElevated = false)
    {
        Architecture = architecture;
        IsElevated = isElevated;
    }

    public ProcessArchitecture Architecture { get; }

    public bool IsElevated { get; }

    public bool IsConnected { get; set; } = true;

    public ConcurrentQueue<PackagingMessage> Sent { get; } = new();

    public event EventHandler<PackagingMessage>? MessageReceived;

    public event EventHandler<string>? Faulted;

    public Task SendAsync(PackagingMessage message, CancellationToken cancellationToken = default)
    {
        Sent.Enqueue(message);

        return Task.CompletedTask;
    }

    /// <summary>Simulates the worker reporting something to the launcher.</summary>
    public void Report(PackagingMessage message) => MessageReceived?.Invoke(this, message);

    public void Fault(string reason)
    {
        IsConnected = false;

        Faulted?.Invoke(this, reason);
    }

    public IReadOnlyList<T> SentOfType<T>() where T : PackagingMessage =>
        [.. Sent.OfType<T>()];

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// Hands out <see cref="FakePackagingWorker"/> instances and records how it was asked to.
/// </summary>
public sealed class FakePackagingWorkerFactory : IPackagingWorkerFactory
{
    private readonly ProcessArchitecture[] _architectures;

    public FakePackagingWorkerFactory(params ProcessArchitecture[] architectures)
    {
        _architectures = architectures.Length > 0
            ? architectures
            : [ProcessArchitecture.X64, ProcessArchitecture.X86];
    }

    public bool IsSupported { get; set; } = true;

    public IReadOnlyList<ProcessArchitecture> SupportedArchitectures => _architectures;

    /// <summary>Workers handed out, in creation order.</summary>
    public List<FakePackagingWorker> Created { get; } = [];

    /// <summary>Whether each call asked for elevation.</summary>
    public List<bool> ElevationRequests { get; } = [];

    /// <summary>When set, the next call returns no workers at all.</summary>
    public bool FailToStart { get; set; }

    public Task<IReadOnlyList<IPackagingWorkerChannel>> StartWorkersAsync(
        Guid sessionId, bool elevated, CancellationToken cancellationToken)
    {
        ElevationRequests.Add(elevated);

        if (FailToStart)
            return Task.FromResult<IReadOnlyList<IPackagingWorkerChannel>>([]);

        var workers = _architectures
            .Select(a => new FakePackagingWorker(a, elevated))
            .ToList();

        Created.AddRange(workers);

        return Task.FromResult<IReadOnlyList<IPackagingWorkerChannel>>([.. workers]);
    }

    public FakePackagingWorker Worker(ProcessArchitecture architecture) =>
        Created.Last(w => w.Architecture == architecture);
}
