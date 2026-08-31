namespace LANCommander.Packaging.IPC;

/// <summary>
/// Constants shared by both ends of the launcher &lt;-&gt; worker channel.
/// </summary>
public static class PackagingProtocol
{
    /// <summary>
    /// Wire protocol version. Bump on any breaking change to the message set.
    /// <para>
    /// Negotiated during the handshake. This matters in practice because an in-place launcher
    /// update can leave a stale worker executable on disk; rejecting the handshake turns silent
    /// protocol corruption into a clear error.
    /// </para>
    /// </summary>
    public const int Version = 1;

    /// <summary>Frames larger than this are rejected rather than allocated.</summary>
    public const int MaxFrameLength = 1024 * 1024;

    /// <summary>How often the launcher pings an idle worker.</summary>
    public static readonly TimeSpan PingInterval = TimeSpan.FromSeconds(5);

    /// <summary>Consecutive missed pings before a live worker is considered unresponsive.</summary>
    public const int MissedPingsBeforeUnresponsive = 3;

    /// <summary>Longest a worker batches changes before flushing.</summary>
    public static readonly TimeSpan ChangeBatchInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>Number of pending changes that forces an early flush.</summary>
    public const int ChangeBatchSize = 500;

    /// <summary>How often a worker rescans for new child processes.</summary>
    public static readonly TimeSpan ChildScanInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>Bound on the worker's pending-change queue before it starts dropping.</summary>
    public const int ChangeQueueCapacity = 100_000;

    /// <summary>
    /// Builds a pipe name unique to one worker in one session.
    /// </summary>
    public static string BuildPipeName(int hostProcessId, Guid sessionId, Guid workerId) =>
        $"lancommander-packaging-{hostProcessId}-{sessionId:N}-{workerId:N}";
}
