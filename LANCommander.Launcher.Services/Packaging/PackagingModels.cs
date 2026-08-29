using LANCommander.Packaging;
using LANCommander.Packaging.Changes;

namespace LANCommander.Launcher.Services.Packaging;

/// <summary>
/// What the user asked the session to monitor.
/// </summary>
public class PackagingSessionOptions
{
    public string InstallerPath { get; set; } = string.Empty;

    public string? Arguments { get; set; }

    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Forward read events as well as writes. Diagnostics only — this is a firehose and will
    /// swamp the change store.
    /// </summary>
    public bool IncludeReads { get; set; }
}

/// <summary>
/// Lightweight counters raised to the UI on a timer. Deliberately carries no items: the file
/// and registry trees are built once when monitoring stops, never incrementally, because a
/// large install produces far more nodes than a TreeView can hold.
/// </summary>
public class PackagingCounters
{
    public int FileCount { get; init; }

    public int RegistryCount { get; init; }

    public int ProcessCount { get; init; }

    /// <summary>
    /// Processes seen but never instrumented — no worker of their architecture, or they exited
    /// before injection landed. Surfaced so an incomplete capture is visible rather than silent.
    /// </summary>
    public int UninstrumentedProcessCount { get; init; }

    /// <summary>Events dropped because a worker's queue overflowed.</summary>
    public int DroppedEventCount { get; init; }

    public PackagingSessionState State { get; init; }

    public bool AnyWorkerElevated { get; init; }
}

public enum PackagingSessionState
{
    Idle = 0,
    Starting = 1,
    Monitoring = 2,
    Stopping = 3,
    Stopped = 4,
    Failed = 5,
}

/// <summary>
/// A process the session has heard about, from any worker.
/// </summary>
public class ProcessLedgerEntry
{
    public int ProcessId { get; init; }

    public int ParentProcessId { get; init; }

    public string? ImagePath { get; init; }

    public ProcessArchitecture Architecture { get; init; }

    /// <summary>True once some worker reported a successful injection.</summary>
    public bool Instrumented { get; set; }

    /// <summary>Why instrumentation did not happen, when it did not.</summary>
    public string? InstrumentationError { get; set; }

    public bool HasExited { get; set; }

    private int _injectionRequested;

    /// <summary>
    /// Claims the right to route this process for injection, exactly once.
    /// </summary>
    /// <remarks>
    /// Every worker polls the same subtrees, so the same child is routinely discovered by more
    /// than one of them at almost the same moment. Without this an x86 child found by both
    /// workers would be sent two InjectCommands.
    /// </remarks>
    internal bool TryBeginInjection() => Interlocked.Exchange(ref _injectionRequested, 1) == 0;

    /// <summary>
    /// Releases the claim so a later discovery can try again, used when the routing attempt
    /// itself failed to reach a worker.
    /// </summary>
    internal void ReleaseInjectionClaim() => Interlocked.Exchange(ref _injectionRequested, 0);
}

/// <summary>
/// A point-in-time copy of everything the session has captured.
/// </summary>
public class PackagingSessionSnapshot
{
    public IReadOnlyList<FileChange> Files { get; init; } = [];

    public IReadOnlyList<RegistryChange> Registry { get; init; } = [];

    public IReadOnlyList<ProcessLedgerEntry> Processes { get; init; } = [];

    public int DroppedEventCount { get; init; }

    public PackagingSessionState State { get; init; }
}
