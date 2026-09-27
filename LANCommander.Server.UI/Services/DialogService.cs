using LANCommander.Server.UI.Controls;
using LANCommander.Server.UI.Controls.Internal;
using Radzen;

namespace LANCommander.Server.UI.Services;

/// <summary>
/// Opens <see cref="Dialog{TOptions,TResult}"/> components as modal windows or side drawers and
/// returns what they produce.
/// </summary>
/// <example>
/// <code>
/// var collections = await Dialogs.OpenAsync&lt;AddToCollectionDialog, AddToCollectionOptions, IEnumerable&lt;Collection&gt;&gt;(
///     "Add to Collection", new AddToCollectionOptions { GameIds = selected }, new DialogSettings { OkText = "Add" });
///
/// if (collections != null)
///     await ReloadAsync();
/// </code>
/// </example>
public sealed class DialogService(Radzen.DialogService dialogs)
{
    /// <summary>
    /// Opens <typeparamref name="TDialog"/> in a modal window. Completes when it closes, with the
    /// dialog's result, or the default value if it was cancelled or dismissed.
    /// </summary>
    public async Task<TResult?> OpenAsync<TDialog, TOptions, TResult>(string title, TOptions options, DialogSettings? settings = null)
        where TDialog : Dialog<TOptions, TResult>
    {
        settings ??= new DialogSettings();

        var result = await dialogs.OpenAsync<DialogFrame<TDialog, TOptions, TResult>>(title, Parameters(options, settings, drawer: false), new DialogOptions
        {
            Width = settings.Width,
            ShowClose = !settings.NotClosable,
            CloseDialogOnEsc = !settings.NotClosable,
            CloseDialogOnOverlayClick = false,
            Draggable = false,
            Resizable = false,
            CssClass = "lc-dialog-window",
        });

        return result is TResult typed ? typed : default;
    }

    /// <inheritdoc cref="OpenAsync{TDialog,TOptions,TResult}"/>
    public Task<TResult?> OpenAsync<TDialog, TResult>(string title, DialogSettings? settings = null)
        where TDialog : Dialog<NoOptions, TResult> =>
        OpenAsync<TDialog, NoOptions, TResult>(title, NoOptions.Instance, settings);

    /// <summary>
    /// Opens <typeparamref name="TDialog"/> in a drawer sliding in from the right, for pickers and
    /// settings that relate to the page behind them.
    /// </summary>
    public async Task<TResult?> OpenDrawerAsync<TDialog, TOptions, TResult>(string title, TOptions options, DialogSettings? settings = null)
        where TDialog : Dialog<TOptions, TResult>
    {
        settings ??= new DialogSettings();

        var result = await dialogs.OpenSideAsync<DialogFrame<TDialog, TOptions, TResult>>(title, Parameters(options, settings, drawer: true), new SideDialogOptions
        {
            Position = DialogPosition.Right,
            Width = settings.Width,
            ShowClose = !settings.NotClosable,
            ShowMask = true,
            CloseDialogOnOverlayClick = !settings.NotClosable,
            CssClass = "lc-dialog-window lc-drawer-window",
        });

        return result is TResult typed ? typed : default;
    }

    private static Dictionary<string, object> Parameters<TOptions>(TOptions options, DialogSettings settings, bool drawer) => new()
    {
        ["Options"] = options!,
        ["Settings"] = settings,
        ["Drawer"] = drawer,
    };
}
