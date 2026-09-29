using System.Text.RegularExpressions;
using LANCommander.Server.Services.Enums;

namespace LANCommander.Server.Services.Utilities;

/// <summary>
/// Infers a <see cref="ServerLogLevel"/> from a line of plain-text server output. Checked in order
/// of importance: errors, then warnings, then debug/trace chatter, then readiness milestones.
/// </summary>
public static partial class ServerLogClassifier
{
    [GeneratedRegex(@"\b(error|fatal|exception|failed|crash(ed)?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ErrorLine();

    [GeneratedRegex(@"\bwarn(ing)?\b", RegexOptions.IgnoreCase)]
    private static partial Regex WarningLine();

    [GeneratedRegex(@"\b(debug|trace)\b", RegexOptions.IgnoreCase)]
    private static partial Regex TraceLine();

    [GeneratedRegex(@"\b(ready|started|listening)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ReadyLine();

    public static ServerLogLevel Classify(string? line)
    {
        if (string.IsNullOrEmpty(line))
            return ServerLogLevel.Info;

        if (ErrorLine().IsMatch(line))
            return ServerLogLevel.Error;

        if (WarningLine().IsMatch(line))
            return ServerLogLevel.Warn;

        if (TraceLine().IsMatch(line))
            return ServerLogLevel.Trace;

        if (ReadyLine().IsMatch(line))
            return ServerLogLevel.Ready;

        return ServerLogLevel.Info;
    }
}
