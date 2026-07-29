using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using LANCommander.Launcher.ViewModels;

namespace LANCommander.Launcher.Views;

public partial class ManageOverlay : UserControl
{
    public event EventHandler? Closed;

    private ManageOverlayViewModel? _wired;

    public ManageOverlay()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_wired is not null)
            _wired.RequestClose -= OnRequestClose;

        _wired = DataContext as ManageOverlayViewModel;

        if (_wired is not null)
            _wired.RequestClose += OnRequestClose;
    }

    private void OnRequestClose(object? sender, EventArgs e) => Close();

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();

    private void Close()
    {
        if (_wired is not null)
            _wired.RequestClose -= OnRequestClose;

        var layer = OverlayLayer.GetOverlayLayer(this);
        Closed?.Invoke(this, EventArgs.Empty);
        layer?.Children.Remove(this);
    }

    /// <summary>
    /// Shows the Manage dialog centered in the main window and waits for it to close.
    /// </summary>
    public static async Task ShowAsync(ManageOverlayViewModel viewModel)
    {
        var tcs = new TaskCompletionSource<bool>();

        await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            var overlay = new ManageOverlay
            {
                DataContext = viewModel,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch,
            };

            overlay.Closed += (_, _) => tcs.TrySetResult(true);

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

        await tcs.Task;
    }
}
