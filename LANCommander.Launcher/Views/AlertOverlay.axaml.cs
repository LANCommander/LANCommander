using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using LANCommander.Launcher.Controls;

namespace LANCommander.Launcher.Views;

public partial class AlertOverlay : UserControl
{
    public event EventHandler? Closed;

    /// <summary>Whether the user chose OK rather than Cancel, Escape or the close button.</summary>
    public bool Confirmed { get; private set; }

    public AlertOverlay()
    {
        InitializeComponent();
        ModalEscape.Enable(this, () => Close());
    }

    public AlertOverlay(string title, string message) : this()
    {
        TitleBar.Title = title;
        MessageText.Text = message;
    }

    /// <summary>An alert that asks the user to confirm, with <paramref name="confirmText"/> on its OK button.</summary>
    public AlertOverlay(string title, string message, string confirmText) : this(title, message)
    {
        OkButton.Content = confirmText;
        CancelButton.IsVisible = true;
    }

    private void OK_Click(object? sender, RoutedEventArgs e)
    {
        Confirmed = true;
        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close();
    private void TitleBar_CloseRequested(object? sender, EventArgs e) => Close();

    private void Close()
    {
        var layer = OverlayLayer.GetOverlayLayer(this);
        Closed?.Invoke(this, EventArgs.Empty);
        layer?.Children.Remove(this);
    }

    /// <summary>
    /// Shows an alert overlay centered in the main window and waits for the user to dismiss it.
    /// </summary>
    public static Task ShowAsync(string title, string message) =>
        ShowAsync(new AlertOverlay(title, message));

    /// <summary>
    /// Asks the user to confirm, in an overlay centered in the main window. Returns true when they
    /// choose <paramref name="confirmText"/>, false when they cancel or dismiss it.
    /// </summary>
    public static Task<bool> ConfirmAsync(string title, string message, string confirmText) =>
        ShowAsync(new AlertOverlay(title, message, confirmText));

    private static async Task<bool> ShowAsync(AlertOverlay overlay)
    {
        var tcs = new TaskCompletionSource<bool>();

        await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            overlay.HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch;
            overlay.VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch;

            overlay.Closed += (_, _) => tcs.TrySetResult(overlay.Confirmed);

            var mainWindow = (Application.Current?.ApplicationLifetime
                as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

            var layer = OverlayLayer.GetOverlayLayer(mainWindow);

            if (layer is not null)
            {
                overlay.Bind(global::Avalonia.Layout.Layoutable.WidthProperty,
                    new Binding("Bounds.Width") { Source = layer });
                overlay.Bind(global::Avalonia.Layout.Layoutable.HeightProperty,
                    new Binding("Bounds.Height") { Source = layer });
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
