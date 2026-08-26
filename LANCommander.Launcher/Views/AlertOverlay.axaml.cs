using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace LANCommander.Launcher.Views;

public partial class AlertOverlay : UserControl
{
    public event EventHandler? Closed;

    public AlertOverlay()
    {
        InitializeComponent();
    }

    public AlertOverlay(string title, string message) : this()
    {
        TitleText.Text = title;
        MessageText.Text = message;
    }

    private void OK_Click(object? sender, RoutedEventArgs e) => Close();

    private void Close()
    {
        var layer = OverlayLayer.GetOverlayLayer(this);
        Closed?.Invoke(this, EventArgs.Empty);
        layer?.Children.Remove(this);
    }

    /// <summary>
    /// Shows an alert overlay centered in the main window and waits for the user to dismiss it.
    /// </summary>
    public static async Task ShowAsync(string title, string message)
    {
        var tcs = new TaskCompletionSource<bool>();

        await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            var overlay = new AlertOverlay(title, message)
            {
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch,
            };

            overlay.Closed += (_, _) => tcs.TrySetResult(true);

            var mainWindow = (Application.Current?.ApplicationLifetime
                as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

            // GetOverlayLayer throws on a null visual, and there genuinely may be no main window
            // yet (or any more) when an alert is raised — during startup, shutdown, or headless
            // runs. Throwing here would replace the error being reported with an unrelated one,
            // right inside the catch block that was trying to report it. Fall through to the
            // no-layer branch instead, which already handles "nowhere to show this".
            var layer = mainWindow is null ? null : OverlayLayer.GetOverlayLayer(mainWindow);

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

        await tcs.Task;
    }
}
