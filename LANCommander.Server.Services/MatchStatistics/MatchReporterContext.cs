using LANCommander.SDK.Enums;

namespace LANCommander.Server.Services.MatchStatistics;

/// <summary>
/// Who is reporting to or reading match statistics, and how far they are trusted. Built by the
/// endpoints from the authenticated request, or by the plugin sink for a plugin.
/// </summary>
/// <param name="UserId">The authenticated user, or null for a plugin.</param>
/// <param name="PluginId">The reporting plugin, or null for a user.</param>
/// <param name="Trust">
/// <see cref="MatchReporterTrust.Authoritative"/> for administrators, tokens with the match reporting
/// scope and plugins; <see cref="MatchReporterTrust.Untrusted"/> for everyone else.
/// </param>
/// <param name="IsAdministrator">Administrators may act on any match, not only ones they report.</param>
public sealed record MatchReporterContext(Guid? UserId, string? PluginId, MatchReporterTrust Trust, bool IsAdministrator)
{
    public static MatchReporterContext ForPlugin(string pluginId) =>
        new(null, pluginId, MatchReporterTrust.Authoritative, true);

    /// <summary>
    /// The server itself, for plugin calls that name a match or reporter rather than a plugin. It may
    /// write as any reporter; the plugin was given the reporter id when it created the match.
    /// </summary>
    public static MatchReporterContext Host { get; } = new(null, null, MatchReporterTrust.Authoritative, true) { ActsForAnyReporter = true };

    /// <summary>Whether events may be sent as any reporter, not only the caller's own.</summary>
    public bool ActsForAnyReporter { get; init; }

    public bool IsTrusted => Trust == MatchReporterTrust.Authoritative;

    /// <summary>Whether the reporter was registered by this caller.</summary>
    public bool Owns(Guid? reporterUserId, string? reporterPluginId) =>
        PluginId != null
            ? string.Equals(reporterPluginId, PluginId, StringComparison.OrdinalIgnoreCase)
            : UserId != null && reporterUserId == UserId;
}

/// <summary>The caller can't act on the match or reporter.</summary>
public sealed class MatchAccessDeniedException(string message) : Exception(message);

/// <summary>Match statistics are turned off in the server's settings.</summary>
public sealed class MatchStatisticsDisabledException() : InvalidOperationException("Match statistics are turned off");

/// <summary>The event queue is full; the reporter should retry later.</summary>
public sealed class MatchQueueFullException() : Exception("The match event queue is full");
