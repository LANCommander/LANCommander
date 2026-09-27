using Microsoft.AspNetCore.Components;

namespace LANCommander.Server.UI.Controls;

/// <summary>
/// Base class for dialog content opened with <see cref="Services.DialogService"/>. The dialog
/// receives <see cref="Options"/>, renders its body, and produces a <typeparamref name="TResult"/>
/// when the user confirms. The OK/Cancel footer, busy state and error display come from the frame
/// the service wraps it in.
/// </summary>
/// <example>
/// <code>
/// @inherits Dialog&lt;AddToCollectionOptions, IEnumerable&lt;Collection&gt;&gt;
///
/// protected override async Task&lt;bool&gt; OnOkAsync()
/// {
///     Result = await SaveAsync();
///     return true;
/// }
/// </code>
/// </example>
public abstract class Dialog<TOptions, TResult> : ComponentBase
{
    /// <summary>What the caller passed to <c>DialogService.OpenAsync</c>.</summary>
    [Parameter] public TOptions Options { get; set; } = default!;

    [CascadingParameter] internal IDialogFrame? Frame { get; set; }

    /// <summary>The value handed back to the caller when the dialog closes through OK.</summary>
    protected TResult? Result { get; set; }

    /// <summary>
    /// Whether the OK button is enabled, e.g. once required input is filled in. Call
    /// <see cref="ComponentBase.StateHasChanged"/> after the value changes so the footer updates.
    /// </summary>
    protected virtual bool CanConfirm => true;

    /// <summary>
    /// Runs when OK is pressed, while the OK button shows a spinner. Set <see cref="Result"/> and
    /// return <c>true</c> to close, or return <c>false</c> to stay open (for example when validation
    /// fails). An exception keeps the dialog open and shows its message above the content.
    /// </summary>
    protected virtual Task<bool> OnOkAsync() => Task.FromResult(true);

    /// <summary>Closes the dialog with <paramref name="result"/>, without going through OK.</summary>
    protected Task CloseAsync(TResult? result) => Frame?.CloseAsync(result) ?? Task.CompletedTask;

    /// <summary>
    /// Runs OK as if its button were pressed, with the same busy state and error display; e.g. when
    /// Enter is pressed in the dialog's form.
    /// </summary>
    protected Task ConfirmAsync() => Frame?.ConfirmAsync() ?? Task.CompletedTask;

    /// <summary>Closes the dialog as if Cancel were pressed; the caller receives the default result.</summary>
    protected Task CancelAsync() => Frame?.CloseAsync(default(TResult)) ?? Task.CompletedTask;

    internal Task<bool> InvokeOkAsync() => OnOkAsync();

    internal bool CanConfirmInternal => CanConfirm;

    internal TResult? ResultInternal => Result;

    protected override void OnAfterRender(bool firstRender) => Frame?.ContentRendered();
}

/// <summary>A dialog that needs no options from its caller.</summary>
public abstract class Dialog<TResult> : Dialog<NoOptions, TResult>
{
}

/// <summary>Placeholder options for dialogs that take none.</summary>
public sealed class NoOptions
{
    public static readonly NoOptions Instance = new();

    private NoOptions()
    {
    }
}

internal interface IDialogFrame
{
    Task CloseAsync(object? result);

    /// <summary>Runs OK as if its button were pressed.</summary>
    Task ConfirmAsync();

    /// <summary>The content re-rendered; the frame refreshes the footer if the OK state changed.</summary>
    void ContentRendered();
}
