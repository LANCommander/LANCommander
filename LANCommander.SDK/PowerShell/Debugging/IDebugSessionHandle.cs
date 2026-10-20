#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LANCommander.SDK.PowerShell.Debugging.Commands;

namespace LANCommander.SDK.PowerShell.Debugging;

/// <summary>
/// What a debugger UI talks to. Implemented by <see cref="DebugSession"/> for scripts running in this
/// process and by <c>RemoteDebugSession</c> for scripts running in an elevated child process, so the UI
/// cannot tell the two apart.
/// </summary>
/// <remarks>
/// Events are raised on a background thread (the pipeline thread, or the pipe reader); the UI marshals.
/// None of the members block.
/// </remarks>
public interface IDebugSessionHandle
{
    /// <summary>The normalised path of the script this run is executing.</summary>
    string ScriptPath { get; }

    DebugSessionState State { get; }

    /// <summary>True when the script runs in another process.</summary>
    bool IsRemote { get; }

    /// <summary>The process running the script, for remote sessions.</summary>
    int? ProcessId { get; }

    event Action<DebuggerStopInfo>? Stopped;

    event Action? Resumed;

    event Action<DebugSessionState>? StateChanged;

    event Action<BreakpointUpdate>? BreakpointChanged;

    event Action<RunCompletion>? RunCompleted;

    void Resume(DebugResumeKind kind);

    void RequestStop();

    /// <summary>
    /// Stop observing the run without ending it: breakpoints are dropped, output is discarded, and a
    /// pending Read-Host is answered with an empty line. Used when the debugger UI goes away.
    /// </summary>
    void Detach();

    void ChangeBreakpoint(BreakpointRequest request, bool add);

    Task<EvaluationResult> EvaluateAsync(string expression, TimeSpan timeout);

    Task<EvaluationResult> ExecuteConsoleCommandAsync(string command, TimeSpan timeout);

    Task<IReadOnlyList<VariableInfo>> GetFrameVariablesAsync(int scope, TimeSpan timeout);

    Task<IReadOnlyList<VariableInfo>> ExpandVariableAsync(int handle, TimeSpan timeout);

    Task<IReadOnlyList<CommandInfoSnapshot>> GetCommandsAsync(TimeSpan timeout);

    Task<CommandDetail?> GetCommandDetailAsync(string name, TimeSpan timeout);
}
