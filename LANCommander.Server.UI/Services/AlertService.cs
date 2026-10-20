using LANCommander.Server.UI.Controls;
using LANCommander.Server.UI.Controls.Internal;

namespace LANCommander.Server.UI.Services;

public enum UnsavedChangesChoice
{
    Cancel,
    Save,
    Discard,
}

/// <summary>
/// Modal message boxes: asking the user to confirm before something irreversible, or telling them
/// something they must acknowledge.
/// </summary>
public sealed class AlertService(DialogService dialogs)
{
    /// <summary>Asks a yes/no question. Returns <c>true</c> only if the user pressed OK.</summary>
    /// <param name="danger">The action destroys something; the OK button is drawn as dangerous.</param>
    public async Task<bool> ConfirmAsync(string message, string title = "Are you sure?", string okText = "OK", bool danger = false) =>
        await dialogs.OpenAsync<ConfirmDialog, ConfirmDialogOptions, bool>(title, new ConfirmDialogOptions(message, danger), new DialogSettings
        {
            Size = DialogSize.Small,
            OkText = okText,
            Danger = danger,
        });

    /// <summary>Shows a message with a single OK button and completes when it is dismissed.</summary>
    public Task AlertAsync(string message, string title = "") =>
        dialogs.OpenAsync<ConfirmDialog, ConfirmDialogOptions, bool>(title, new ConfirmDialogOptions(message, Danger: false), new DialogSettings
        {
            Size = DialogSize.Small,
            NoCancel = true,
        });

    /// <summary>
    /// Asks for a single line of text, e.g. a name. Returns the trimmed text, or null if cancelled.
    /// </summary>
    /// <param name="required">OK stays disabled until something is entered.</param>
    public Task<string?> PromptAsync(string title, string label, string? initialValue = null, string okText = "OK", string? placeholder = null, bool required = true) =>
        dialogs.OpenAsync<PromptDialog, PromptDialogOptions, string>(title, new PromptDialogOptions(label, initialValue, placeholder, required), new DialogSettings
        {
            Size = DialogSize.Small,
            OkText = okText,
        });

    /// <summary>
    /// Asks what to do with unsaved changes before leaving them: save, discard, or stay (cancel).
    /// </summary>
    public async Task<UnsavedChangesChoice> ConfirmUnsavedChangesAsync(string message = "You have unsaved changes.") =>
        await dialogs.OpenAsync<UnsavedChangesDialog, string, UnsavedChangesChoice>("Unsaved Changes", message, new DialogSettings
        {
            Size = DialogSize.Small,
            NoFooter = true,
        });
}
