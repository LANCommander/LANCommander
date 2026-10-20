using LANCommander.Packaging;
using LANCommander.Packaging.IPC;

namespace LANCommander.Launcher.Services.Packaging;

/// <summary>
/// One connected worker, from the session's point of view.
/// </summary>
/// <remarks>
/// The session depends on this rather than on <c>Process</c> and <c>NamedPipeServerStream</c>
/// directly, so the routing and ledger logic — the part most likely to be wrong — can be tested
/// against a fake without spawning anything or injecting into anything.
/// </remarks>
public interface IPackagingWorkerChannel : IAsyncDisposable
{
    /// <summary>Architecture this worker can inject into.</summary>
    ProcessArchitecture Architecture { get; }

    bool IsElevated { get; }

    bool IsConnected { get; }

    /// <summary>Raised for every message the worker sends.</summary>
    event EventHandler<PackagingMessage>? MessageReceived;

    /// <summary>Raised when the worker dies, hangs, or the channel breaks.</summary>
    event EventHandler<string>? Faulted;

    Task SendAsync(PackagingMessage message, CancellationToken cancellationToken = default);
}

/// <summary>
/// Starts the worker set for a session. Separated so the session can be driven by fakes.
/// </summary>
public interface IPackagingWorkerFactory
{
    /// <summary>
    /// True when workers can run at all — Windows, with worker binaries deployed alongside the
    /// launcher.
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// Architectures a worker exists for. Anything outside this set cannot be instrumented.
    /// </summary>
    IReadOnlyList<ProcessArchitecture> SupportedArchitectures { get; }

    /// <summary>
    /// Starts every worker for a session and completes their handshakes.
    /// </summary>
    /// <param name="elevated">
    /// Start the workers through UAC. Used only after a non-elevated attempt reported that it
    /// needed elevation, so the user sees at most one consent prompt per session — the
    /// implementation escalates one worker and has it start the others as its own children,
    /// which inherit elevation without a second prompt.
    /// </param>
    /// <returns>
    /// The workers that connected. May be a subset of <see cref="SupportedArchitectures"/> if
    /// one failed to start; a partial capture is better than none.
    /// </returns>
    Task<IReadOnlyList<IPackagingWorkerChannel>> StartWorkersAsync(
        Guid sessionId, bool elevated, CancellationToken cancellationToken);
}
