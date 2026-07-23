namespace LANCommander.Server.Services.Abstractions;

/// <summary>
/// Elects a single "coordinator" instance among horizontally-scaled servers. Coordinator-only work
/// (boot autostart, autostop scheduling, the play-session sweep, the IPX relay, and the discovery
/// beacon) is gated behind <see cref="IsLeader"/> so it runs on exactly one node.
///
/// In single-instance mode the default implementation always reports leadership, so gated code runs
/// unchanged.
/// </summary>
public interface ICoordinatorElection
{
    /// <summary>
    /// True when this instance currently holds the coordinator role.
    /// </summary>
    bool IsLeader { get; }

    /// <summary>
    /// Attempts to acquire (or confirm) the coordinator role immediately and returns the result.
    /// Used at boot to gate one-time startup work before the background renewal loop is running.
    /// </summary>
    Task<bool> TryAcquireAsync(CancellationToken cancellationToken = default);
}
