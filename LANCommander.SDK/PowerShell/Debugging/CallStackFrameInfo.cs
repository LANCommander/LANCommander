#nullable enable
using System.IO;

namespace LANCommander.SDK.PowerShell.Debugging;

/// <summary>
/// A flattened <c>CallStackFrame</c>. Captured on the pipeline thread while stopped; the live frame
/// object must never outlive the stop.
/// </summary>
public sealed class CallStackFrameInfo
{
    /// <summary>
    /// Depth from the top of the stack. Doubles as the PowerShell scope number used to fetch this
    /// frame's variables, which are loaded on demand rather than captured at the stop.
    /// </summary>
    public required int Index { get; init; }

    public required string FunctionName { get; init; }

    /// <summary>Null when the frame has no source file: a dynamic ScriptBlock, Invoke-Expression, or a compiled module function.</summary>
    public required string? ScriptName { get; init; }

    public required int LineNumber { get; init; }

    public string Display =>
        ScriptName is null
            ? FunctionName
            : $"{FunctionName}  —  {Path.GetFileName(ScriptName)}:{LineNumber}";
}
