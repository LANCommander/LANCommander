#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;

namespace LANCommander.SDK.PowerShell.Debugging;

/// <summary>
/// Everything the UI needs to render a stop, captured on the pipeline thread into immutable data before
/// the UI is notified. Nothing here holds a live engine object.
/// </summary>
public sealed class DebuggerStopInfo
{
    /// <summary>Frames deeper than this are not captured; a runaway recursion should not stall the stop.</summary>
    private const int MaxFrames = 50;

    /// <summary>
    /// The file the debugger stopped in, or null when there is no source: a dynamically created
    /// ScriptBlock, Invoke-Expression, or a function from a compiled module.
    /// </summary>
    public required string? ScriptName { get; init; }

    public required int LineNumber { get; init; }

    public required int ColumnNumber { get; init; }

    /// <summary>Ids of the breakpoints that caused this stop. Empty for a step.</summary>
    public required IReadOnlyList<int> HitBreakpointIds { get; init; }

    public required IReadOnlyList<CallStackFrameInfo> Frames { get; init; }

    /// <summary>
    /// Snapshot the stop. Frames only: variables are fetched per frame on demand, because
    /// <c>CallStackFrame.GetFrameVariables()</c> yields only the frame's automatic variables and never
    /// the user's locals. Those have to come from Get-Variable run in the frame's scope.
    /// </summary>
    public static DebuggerStopInfo Capture(DebuggerStopEventArgs args, IEnumerable<CallStackFrame> callStack)
    {
        var frames = new List<CallStackFrameInfo>();
        var index = 0;

        foreach (var frame in callStack)
        {
            if (index >= MaxFrames)
                break;

            frames.Add(new CallStackFrameInfo
            {
                Index = index++,
                FunctionName = frame.FunctionName ?? "<ScriptBlock>",
                ScriptName = frame.ScriptName,
                LineNumber = frame.ScriptLineNumber,
            });
        }

        var invocation = args.InvocationInfo;

        return new DebuggerStopInfo
        {
            ScriptName = invocation?.ScriptName,
            LineNumber = invocation?.ScriptLineNumber ?? 0,
            ColumnNumber = invocation?.OffsetInLine ?? 0,
            HitBreakpointIds = args.Breakpoints?.Select(b => b.Id).ToArray() ?? Array.Empty<int>(),
            Frames = frames,
        };
    }
}
