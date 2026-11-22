using LANCommander.SDK.Updating;

namespace LANCommander.Server.Services;

/// <summary>
/// Carries a staged server update from <see cref="UpdateService"/>, which stops the server, to the end
/// of the server's startup code, which applies it once the server has stopped.
/// </summary>
public static class ServerUpdateHandover
{
    private static string? _pendingJournalDirectory;

    /// <summary>The journal directory of an update to apply once the server stops, if one was requested.</summary>
    public static string? PendingJournalDirectory => _pendingJournalDirectory;

    public static void Request(string journalDirectory) => _pendingJournalDirectory = journalDirectory;

    /// <summary>Describes the running server's installation, or returns null when it can't update itself.</summary>
    public static UpdateInstallation? DetectInstallation()
    {
#if DEBUG
        return UpdateInstallation.Detect(isDebugBuild: true);
#else
        return UpdateInstallation.Detect();
#endif
    }
}
