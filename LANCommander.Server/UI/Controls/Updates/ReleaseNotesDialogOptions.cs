using LANCommander.PluginManagement;
using LANCommander.Server.UI.Services;

namespace LANCommander.Server.UI.Controls;

/// <param name="Summary">A line above the notes, or null for none.</param>
/// <param name="ReleaseNotes">The notes, in markdown.</param>
public sealed record ReleaseNotesDialogOptions(string? Summary, string ReleaseNotes)
{
    /// <summary>The release notes of the plugins updated since the server last started.</summary>
    public static Task OpenForPluginsAsync(DialogService dialogs, IReadOnlyList<PluginUpdateNote> plugins)
    {
        var title = plugins.Count == 1 ? $"{plugins[0].Name} Was Updated" : "Plugins Were Updated";

        var summary = plugins.Count == 1
            ? $"{plugins[0].Name} was updated to version {plugins[0].Version}."
            : $"{plugins.Count} plugins were updated.";

        return OpenAsync(dialogs, title, new ReleaseNotesDialogOptions(summary, PluginUpdateNote.ToMarkdown(plugins)));
    }

    /// <summary>One installed plugin's release notes.</summary>
    public static Task OpenForPluginAsync(DialogService dialogs, string name, string? version, string releaseNotes) =>
        OpenAsync(dialogs, version == null ? $"{name} Release Notes" : $"{name} {version} Release Notes",
            new ReleaseNotesDialogOptions(null, releaseNotes));

    private static Task OpenAsync(DialogService dialogs, string title, ReleaseNotesDialogOptions options) =>
        dialogs.OpenAsync<ReleaseNotesDialog, ReleaseNotesDialogOptions, bool>(title, options,
            new DialogSettings { NoCancel = true, Size = DialogSize.Large });
}
