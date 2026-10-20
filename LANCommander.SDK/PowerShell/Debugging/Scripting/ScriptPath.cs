#nullable enable
using System;
using System.IO;

namespace LANCommander.SDK.PowerShell.Debugging.Scripting;

/// <summary>
/// Every comparison between a path the engine reports (<c>InvocationInfo.ScriptName</c>) and a path we
/// hold goes through here. Nowhere else should compare paths with <c>==</c>.
/// </summary>
/// <remarks>
/// Covered: case drift (<c>d:\a.ps1</c> vs <c>D:\a.ps1</c>, the classic "my breakpoints don't bind"),
/// <c>.</c>/<c>..</c> segments, and doubled separators. Not covered: 8.3 short names, <c>subst</c> drives
/// and junctions.
/// </remarks>
public static class ScriptPath
{
    public static StringComparer Comparer { get; } =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public static StringComparison Comparison { get; } =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static string Normalize(string path) =>
        string.IsNullOrWhiteSpace(path) ? string.Empty : Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    public static bool AreSame(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return false;

        try
        {
            return string.Equals(Normalize(a), Normalize(b), Comparison);
        }
        catch (Exception)
        {
            // GetFullPath throws on malformed input; a path we cannot normalise is not a match.
            return false;
        }
    }

    /// <summary>Quote a path for interpolation into a PowerShell command string.</summary>
    public static string ToSingleQuoted(string path) => "'" + path.Replace("'", "''") + "'";
}
