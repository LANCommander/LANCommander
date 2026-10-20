#nullable enable
using System;
using System.Collections.Generic;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;
using PowerShellInstance = System.Management.Automation.PowerShell;

namespace LANCommander.SDK.PowerShell.Debugging;

/// <summary>
/// Everything <see cref="DebugSession.Start"/> needs for one run.
/// </summary>
public sealed class DebugLaunchRequest
{
    /// <summary>
    /// The path line breakpoints bind to and the stop location is reported against. The file does not
    /// have to contain <see cref="ScriptContents"/>; the contents are compiled with this path as their
    /// source, which is what makes breakpoints bind.
    /// </summary>
    public required string ScriptPath { get; init; }

    /// <summary>The script text that actually runs.</summary>
    public required string ScriptContents { get; init; }

    /// <summary>
    /// Creates and opens the runspace. Invoked on the pipeline thread with the session's host; the
    /// runspace must use <c>PSThreadOptions.UseCurrentThread</c> so every engine call stays on that thread.
    /// </summary>
    public required Func<PSHost, Runspace> OpenRunspace { get; init; }

    public IReadOnlyList<BreakpointRequest> Breakpoints { get; init; } = Array.Empty<BreakpointRequest>();

    /// <summary>Break on the first statement rather than running to a breakpoint.</summary>
    public bool StepIntoOnStart { get; init; }

    /// <summary>Script text run in the same pipeline ahead of the script itself, e.g. the banner.</summary>
    public string? Preamble { get; init; }

    /// <summary>
    /// Called on the pipeline thread after the script finishes but before the runspace is torn down,
    /// so reading <c>$Return</c> (and any ScriptProperty getters on it) still works. The value lands in
    /// <see cref="RunCompletion.Result"/>. The second argument is the script's raw pipeline output.
    /// </summary>
    public Func<PowerShellInstance, IReadOnlyList<object?>, object?>? CaptureResult { get; init; }

    /// <summary>First thing run on the pipeline thread. Per-thread setup such as Wow64 redirection goes here.</summary>
    public Action? OnPipelineThreadStarted { get; init; }

    /// <summary>Last thing run on the pipeline thread.</summary>
    public Action? OnPipelineThreadCompleted { get; init; }
}
