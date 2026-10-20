#nullable enable
using System.Management.Automation;

namespace LANCommander.SDK.PowerShell.Debugging.Hosting;

/// <summary>
/// A serializable snapshot of a <see cref="ProgressRecord"/>. The engine's record type cannot cross the
/// debug pipe, so the sink receives this instead.
/// </summary>
public sealed record ScriptProgress(
    int ActivityId,
    string Activity,
    string StatusDescription,
    string? CurrentOperation,
    int PercentComplete,
    int SecondsRemaining,
    bool IsCompleted)
{
    public static ScriptProgress From(ProgressRecord record) => new(
        record.ActivityId,
        record.Activity ?? string.Empty,
        record.StatusDescription ?? string.Empty,
        record.CurrentOperation,
        record.PercentComplete,
        record.SecondsRemaining,
        record.RecordType == ProgressRecordType.Completed);
}
