#nullable enable
using System;
using System.Text.Json.Serialization;

namespace LANCommander.SDK.PowerShell.Debugging;

/// <summary>How a run ended.</summary>
public sealed record RunCompletion(int? ExitCode, bool Faulted, string? ErrorMessage, TimeSpan Duration)
{
    /// <summary>
    /// The value produced by <see cref="DebugLaunchRequest.CaptureResult"/>. In-process only; it never
    /// crosses the debug pipe.
    /// </summary>
    [JsonIgnore]
    public object? Result { get; init; }
}
