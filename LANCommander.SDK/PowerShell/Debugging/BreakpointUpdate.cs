#nullable enable
using System.Management.Automation;

namespace LANCommander.SDK.PowerShell.Debugging;

/// <summary>
/// A <c>BreakpointUpdated</c> notification flattened for the UI. Raised on the pipeline thread; the UI
/// marshals it.
/// </summary>
public readonly record struct BreakpointUpdate(
    int BreakpointId,
    int Line,
    BreakpointUpdateType UpdateType,
    int HitCount);
