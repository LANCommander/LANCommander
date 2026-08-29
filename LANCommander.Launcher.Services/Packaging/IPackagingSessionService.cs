namespace LANCommander.Launcher.Services.Packaging;

/// <summary>
/// Owns a packaging capture: worker lifetimes, the merged change store, and the process ledger.
/// </summary>
/// <remarks>
/// Registered as a singleton. When several workers run at once — an x64 installer that spawns
/// an x86 child, say — this is the single place their reports merge, so the user sees one
/// deduplicated change set rather than one per architecture.
/// </remarks>
public interface IPackagingSessionService : IAsyncDisposable
{
    /// <summary>False on non-Windows, or when no worker binaries are deployed.</summary>
    bool IsSupported { get; }

    PackagingSessionState State { get; }

    /// <summary>
    /// Raised at most a few times a second with counters only. Subscribers must marshal to the
    /// UI thread themselves; this service has no UI dependency.
    /// </summary>
    event EventHandler<PackagingCounters>? CountersChanged;

    /// <summary>Diagnostics from the workers, coalesced.</summary>
    event EventHandler<string>? Logged;

    /// <summary>Raised when the installer that was launched exits.</summary>
    event EventHandler? InstallerExited;

    /// <summary>
    /// Raised when a worker reports that it needs elevation to continue. The UI should offer to
    /// restart the session elevated.
    /// </summary>
    event EventHandler<string>? ElevationRequired;

    /// <summary>
    /// Starts workers and launches the installer under instrumentation.
    /// </summary>
    Task StartAsync(PackagingSessionOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restarts the current session with elevated workers, after the user consents to UAC.
    /// </summary>
    Task RestartElevatedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops monitoring. Safe to call before the installer exits — an installer that leaves an
    /// updater running would otherwise strand the session forever.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// A copy of everything captured so far.
    /// </summary>
    PackagingSessionSnapshot Snapshot();

    /// <summary>
    /// Clears all captured state so a new package can be started.
    /// </summary>
    void Reset();
}
