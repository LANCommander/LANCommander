using System.Text.Json.Serialization;
using LANCommander.Packaging.Changes;

namespace LANCommander.Packaging.Ipc;

/// <summary>
/// Base for every message exchanged between the launcher and a packaging worker.
/// </summary>
/// <remarks>
/// Serialized as JSON with a "$kind" discriminator. JSON rather than a packed binary format
/// because the message set will keep changing, source-generated serialization costs no
/// reflection and survives trimming, and a readable wire makes worker problems diagnosable
/// from a log. Volume is handled by batching and source-side filtering, not by byte shaving.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$kind")]
[JsonDerivedType(typeof(WorkerHelloMessage), "workerHello")]
[JsonDerivedType(typeof(HostHelloMessage), "hostHello")]
[JsonDerivedType(typeof(SetFilterCommand), "setFilter")]
[JsonDerivedType(typeof(LaunchInstallerCommand), "launchInstaller")]
[JsonDerivedType(typeof(InjectCommand), "inject")]
[JsonDerivedType(typeof(AdoptSubtreeCommand), "adoptSubtree")]
[JsonDerivedType(typeof(StopCommand), "stop")]
[JsonDerivedType(typeof(PingCommand), "ping")]
[JsonDerivedType(typeof(PongMessage), "pong")]
[JsonDerivedType(typeof(CommandResultMessage), "commandResult")]
[JsonDerivedType(typeof(ChangeBatchMessage), "changeBatch")]
[JsonDerivedType(typeof(ProcessDiscoveredMessage), "processDiscovered")]
[JsonDerivedType(typeof(ProcessExitedMessage), "processExited")]
[JsonDerivedType(typeof(WorkerLogMessage), "workerLog")]
[JsonDerivedType(typeof(WorkerStoppedMessage), "workerStopped")]
public abstract class PackagingMessage
{
}

// ─────────────────────────────────────────────────────────────────────────────
// Handshake
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>First message on the wire. Sent by the worker once connected.</summary>
public class WorkerHelloMessage : PackagingMessage
{
    public int ProtocolVersion { get; set; }

    /// <summary>Echo of the one-time token the launcher passed on the command line.</summary>
    public string Token { get; set; } = string.Empty;

    public int WorkerProcessId { get; set; }

    public ProcessArchitecture Architecture { get; set; }

    /// <summary>Whether the worker is running elevated. Drives the UAC fallback warning.</summary>
    public bool IsElevated { get; set; }

    /// <summary>Resolved path of the native Interposer DLL the worker will inject.</summary>
    public string? InterposerDllPath { get; set; }
}

/// <summary>Launcher's reply to <see cref="WorkerHelloMessage"/>.</summary>
public class HostHelloMessage : PackagingMessage
{
    public bool Accepted { get; set; }

    public int NegotiatedVersion { get; set; }

    /// <summary>Populated when <see cref="Accepted"/> is false.</summary>
    public string? RejectionReason { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Launcher -> Worker
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Pushes capture policy to the worker. Policy lives in the launcher.</summary>
public class SetFilterCommand : PackagingMessage
{
    public string[] WriteVerbs { get; set; } = [];
    public string[] RegistryWriteVerbs { get; set; } = [];
    public string[] IgnoredPathPrefixes { get; set; } = [];
    public bool IncludeReads { get; set; }
}

/// <summary>Launches the installer suspended, injects, and resumes it.</summary>
public class LaunchInstallerCommand : PackagingMessage
{
    public Guid CorrelationId { get; set; }
    public string ExecutablePath { get; set; } = string.Empty;
    public string? Arguments { get; set; }
    public string? WorkingDirectory { get; set; }
}

/// <summary>
/// Injects into an already-running process. This is the cross-architecture handoff: a worker
/// that cannot inject into a discovered process reports it, and the launcher routes this
/// command to the worker whose bitness matches.
/// </summary>
public class InjectCommand : PackagingMessage
{
    public Guid CorrelationId { get; set; }
    public int ProcessId { get; set; }
}

/// <summary>Takes over child-process polling for a subtree.</summary>
public class AdoptSubtreeCommand : PackagingMessage
{
    public Guid CorrelationId { get; set; }
    public int RootProcessId { get; set; }
}

/// <summary>Stops monitoring and shuts the worker down cleanly.</summary>
public class StopCommand : PackagingMessage
{
    /// <summary>
    /// When false (the default) monitored processes are left running — the user may be
    /// midway through an install.
    /// </summary>
    public bool TerminateTargets { get; set; }
}

public class PingCommand : PackagingMessage
{
    public long Sequence { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Worker -> Launcher
// ─────────────────────────────────────────────────────────────────────────────

public class PongMessage : PackagingMessage
{
    public long Sequence { get; set; }
}

/// <summary>Outcome of a correlated command.</summary>
public class CommandResultMessage : PackagingMessage
{
    public Guid CorrelationId { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }

    /// <summary>Win32 error code when the failure came from the OS, otherwise 0.</summary>
    public int Win32Error { get; set; }

    /// <summary>Set for commands that produce a process, e.g. LaunchInstaller.</summary>
    public int? ProcessId { get; set; }

    /// <summary>
    /// True when the operation failed because the worker is not elevated. The launcher uses
    /// this to offer respawning the worker through UAC.
    /// </summary>
    public bool RequiresElevation { get; set; }
}

/// <summary>A batch of captured changes. The bulk of the traffic.</summary>
public class ChangeBatchMessage : PackagingMessage
{
    public FileChange[] Files { get; set; } = [];
    public RegistryChange[] Registry { get; set; } = [];

    /// <summary>
    /// Number of events dropped since the last batch because the worker's queue was full.
    /// Surfaced in the UI so an incomplete capture is visible rather than silent.
    /// </summary>
    public int DroppedCount { get; set; }
}

/// <summary>
/// A process appeared in a monitored subtree. Reported for every process, whether or not this
/// worker was able to inject into it.
/// </summary>
public class ProcessDiscoveredMessage : PackagingMessage
{
    public int ProcessId { get; set; }
    public int ParentProcessId { get; set; }
    public string? ImagePath { get; set; }
    public ProcessArchitecture Architecture { get; set; }

    /// <summary>True when this worker already injected into it.</summary>
    public bool InjectedLocally { get; set; }

    /// <summary>Populated when this worker tried to inject and failed.</summary>
    public string? InjectionError { get; set; }
}

public class ProcessExitedMessage : PackagingMessage
{
    public int ProcessId { get; set; }
    public int ExitCode { get; set; }

    /// <summary>True when this was the installer the session started.</summary>
    public bool IsRoot { get; set; }
}

/// <summary>Worker diagnostics, forwarded into the launcher's logger.</summary>
public class WorkerLogMessage : PackagingMessage
{
    public PackagingLogLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class WorkerStoppedMessage : PackagingMessage
{
    public string? Reason { get; set; }
}

public enum PackagingLogLevel
{
    Debug = 0,
    Information = 1,
    Warning = 2,
    Error = 3,
}
