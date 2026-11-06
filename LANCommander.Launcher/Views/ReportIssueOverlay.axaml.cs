using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LANCommander.Launcher.Controls;

namespace LANCommander.Launcher.Views;

/// <summary>
/// Lets the user describe a problem with a game and send it to the server. The dialog stays open
/// while the report is sent and if it fails, so the user's text isn't lost.
/// </summary>
public partial class ReportIssueOverlay : UserControl
{
    /// <summary>Raised when the dialog closes; the argument is true if a report was sent.</summary>
    public event EventHandler<bool>? DialogClosed;

    // Sends the description; returns null on success, otherwise a message to show the user.
    private readonly Func<string, Task<string?>>? _submit;
    private bool _isSubmitting;

    public ReportIssueOverlay()
    {
        InitializeComponent();
        ModalEscape.Enable(this, () => Close(false));

        DescriptionBox.TextChanged += (_, _) => UpdateSubmitEnabled();
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => DescriptionBox.Focus());
    }

    public ReportIssueOverlay(string gameTitle, Func<string, Task<string?>> submit) : this()
    {
        TitleBar.Title = string.IsNullOrWhiteSpace(gameTitle) ? "Report Issue" : $"Report Issue: {gameTitle}";
        _submit = submit;
    }

    private void UpdateSubmitEnabled()
        => SubmitButton.IsEnabled = !_isSubmitting && !string.IsNullOrWhiteSpace(DescriptionBox.Text);

    private async void Submit_Click(object? sender, RoutedEventArgs e)
    {
        var description = DescriptionBox.Text?.Trim();

        if (_submit == null || _isSubmitting || string.IsNullOrEmpty(description))
            return;

        _isSubmitting = true;
        ErrorText.IsVisible = false;
        DescriptionBox.IsEnabled = false;
        SubmitButton.Content = "Submitting...";
        UpdateSubmitEnabled();

        string? error;

        try
        {
            error = await _submit(description);
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }

        _isSubmitting = false;
        DescriptionBox.IsEnabled = true;
        SubmitButton.Content = "Submit";
        UpdateSubmitEnabled();

        if (error == null)
        {
            Close(true);
            return;
        }

        ErrorText.Text = error;
        ErrorText.IsVisible = true;
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);
    private void TitleBar_CloseRequested(object? sender, EventArgs e) => Close(false);

    private void Close(bool submitted)
    {
        // Closing mid-request would hide the outcome; the request is short, so just wait for it.
        if (_isSubmitting)
            return;

        var layer = OverlayLayer.GetOverlayLayer(this);
        DialogClosed?.Invoke(this, submitted);
        layer?.Children.Remove(this);
    }

    /// <summary>
    /// Shows the dialog over the main window. Returns true once a report has been sent, false if
    /// the user cancelled.
    /// </summary>
    public static async Task<bool> ShowAsync(string gameTitle, Func<string, Task<string?>> submit)
    {
        var tcs = new TaskCompletionSource<bool>();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var overlay = new ReportIssueOverlay(gameTitle, submit)
            {
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch,
            };

            overlay.DialogClosed += (_, submitted) => tcs.TrySetResult(submitted);

            var mainWindow = (Application.Current?.ApplicationLifetime
                as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

            var layer = OverlayLayer.GetOverlayLayer(mainWindow);

            if (layer is not null)
            {
                overlay.Bind(WidthProperty, new Binding("Bounds.Width") { Source = layer });
                overlay.Bind(HeightProperty, new Binding("Bounds.Height") { Source = layer });
                layer.Children.Add(overlay);
            }
            else
            {
                tcs.TrySetResult(false);
            }
        });

        return await tcs.Task;
    }
}
