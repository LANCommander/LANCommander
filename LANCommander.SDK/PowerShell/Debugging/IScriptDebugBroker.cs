#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.PowerShell.Debugging.Hosting;

namespace LANCommander.SDK.PowerShell.Debugging;

/// <summary>
/// Decides whether a script that is about to run should run under the debugger. Registered in DI by hosts
/// that have a debugger UI (the launcher) or proxy to one (an elevated child process). When nothing is
/// registered, or nothing matches, scripts run exactly as they always have.
/// </summary>
public interface IScriptDebugBroker
{
    /// <summary>True when a debugger wants this script. Cheap; safe to call from any thread.</summary>
    bool IsAttached(ScriptIdentity identity);

    /// <summary>
    /// Called just before the script file is read, so a debugger can flush unsaved edits or promote a
    /// draft to <paramref name="path"/>. What runs is then exactly what the editor shows.
    /// </summary>
    void PrepareScriptFile(ScriptIdentity identity, string path);

    /// <summary>Attach a debugger to the run, or return null to run the script normally.</summary>
    ValueTask<ScriptDebugAttachment?> TryAttachAsync(ScriptIdentity identity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Where an elevated child process should connect to debug this script, or null when cross-process
    /// debugging is unavailable.
    /// </summary>
    ScriptDebugRemoteEndpoint? GetRemoteEndpoint(ScriptIdentity identity);
}

/// <summary>
/// One debugger UI (in the launcher, one window per game) registered with a <see cref="ScriptDebugBroker"/>.
/// Every member may be called on a background thread and must not wait on the UI thread.
/// </summary>
public interface IScriptDebugTarget
{
    bool Matches(ScriptIdentity identity);

    /// <summary>See <see cref="IScriptDebugBroker.PrepareScriptFile"/>.</summary>
    void PrepareScriptFile(ScriptIdentity identity, string path);

    /// <summary>
    /// Begin debugging a run, or return null if the target is busy with another one (the script then
    /// runs without the debugger).
    /// </summary>
    ScriptDebugAttachment? BeginAttach(ScriptIdentity identity);

    /// <summary>The pipe an elevated child should connect to, or null if the target has none.</summary>
    ScriptDebugRemoteEndpoint? RemoteEndpoint { get; }
}

/// <summary>What a debugger hands the engine when it attaches to a run.</summary>
public sealed class ScriptDebugAttachment
{
    /// <summary>Receives the script's output and answers its Read-Host calls.</summary>
    public required IConsoleSink Sink { get; init; }

    /// <summary>Breakpoints for this script, already mapped to the path it runs from.</summary>
    public IReadOnlyList<BreakpointRequest> Breakpoints { get; init; } = Array.Empty<BreakpointRequest>();

    public bool StepIntoOnStart { get; init; }

    /// <summary>
    /// Called once the session exists and before it starts, so the UI can subscribe to its events
    /// without missing the first stop.
    /// </summary>
    public required Action<IDebugSessionHandle> SessionStarted { get; init; }
}

/// <summary>A named pipe an elevated process connects to, and the one-time token it must present.</summary>
public sealed record ScriptDebugRemoteEndpoint(string PipeName, string Token);
