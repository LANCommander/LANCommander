using LANCommander.Server.Services;
using LANCommander.Server.UI.Services;
using Semver;

namespace LANCommander.Server.UI.Controls;

/// <param name="Notice">The update the notes follow, or null when just viewing the running version's notes.</param>
/// <param name="ReleaseNotes">Markdown for the versions covered; empty when there are none.</param>
public sealed record ServerReleaseNotesOptions(ServerUpdateNotice? Notice, string Version, string ReleaseNotes)
{
    /// <summary>After an update: its release notes, or why it was undone.</summary>
    public static Task OpenAsync(DialogService dialogs, ServerUpdateNotice notice)
    {
        var title = notice.Succeeded
            ? $"What's New in LANCommander Server {notice.Version}"
            : "Server Update Failed";

        var notes = notice.Succeeded ? NotesBetween(notice.PreviousVersion, notice.Version) : String.Empty;

        return OpenAsync(dialogs, title, new ServerReleaseNotesOptions(notice, notice.Version, notes));
    }

    /// <summary>
    /// The running version's release notes. When the server got here by updating itself, the notes for
    /// every version it moved past, as shown after the update.
    /// </summary>
    public static Task OpenForCurrentAsync(DialogService dialogs, SemVersion currentVersion, ServerUpdateNotice? lastUpdate)
    {
        var current = currentVersion.WithoutMetadata().ToString();

        if (lastUpdate is { Succeeded: true }
            && SemVersion.TryParse(lastUpdate.Version, SemVersionStyles.Any, out var updatedTo)
            && updatedTo.ComparePrecedenceTo(currentVersion) == 0)
            return OpenAsync(dialogs, $"Release Notes for LANCommander Server {current}",
                new ServerReleaseNotesOptions(lastUpdate, current, NotesBetween(lastUpdate.PreviousVersion, current)));

        return OpenAsync(dialogs, $"Release Notes for LANCommander Server {current}",
            new ServerReleaseNotesOptions(null, current, NotesBetween(null, current)));
    }

    private static Task OpenAsync(DialogService dialogs, string title, ServerReleaseNotesOptions options) =>
        dialogs.OpenAsync<ServerReleaseNotesDialog, ServerReleaseNotesOptions, bool>(title, options,
            new DialogSettings { NoCancel = true, Size = DialogSize.Large });

    private static string NotesBetween(string? previousVersion, string version)
    {
        if (!SemVersion.TryParse(version, SemVersionStyles.Any, out var current))
            return String.Empty;

        SemVersion.TryParse(previousVersion, SemVersionStyles.Any, out var previous);

        var notes = SDK.Updating.ReleaseNotes.Load(typeof(Program).Assembly);

        return SDK.Updating.ReleaseNotes.ToMarkdown(SDK.Updating.ReleaseNotes.Between(notes, previous, current));
    }
}
