#nullable enable

namespace LANCommander.SDK.PowerShell.Debugging;

/// <summary>
/// A breakpoint as the engine sees it: a plain value, snapshotted from the editor's anchors on the UI
/// thread and safe to hand to the pipeline thread (or across the debug pipe).
/// </summary>
public readonly record struct BreakpointRequest(string ScriptPath, int Line, bool Enabled);
