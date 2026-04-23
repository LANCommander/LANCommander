using global::Avalonia.Controls;
using global::Avalonia.Controls.ApplicationLifetimes;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Data;
using global::Avalonia.Input;
using LANCommander.Launcher.Avalonia.Controls;
using LANCommander.Launcher.Avalonia.ViewModels.Components;

namespace LANCommander.Launcher.Avalonia.Views;

public partial class GameDetailView : UserControl
{
    public GameDetailView()
    {
        InitializeComponent();
    }

    private void VideoItem_Tapped(object? sender, TappedEventArgs e)
    {
        // The tapped element is the Border whose child is the InlineVideoPlayer.
        if (sender is not Decorator { Child: InlineVideoPlayer inlinePlayer })
            return;

        if (inlinePlayer.DataContext is not GameMediaItemViewModel vm || !vm.IsVideo)
            return;

        // Capture timestamp and pause carousel playback.
        var timeMs = inlinePlayer.CurrentTimeMs;
        inlinePlayer.Pause();

        // Build overlay.
        var overlay = new VideoPlayerOverlay
        {
            HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch,
        };

        // When the overlay closes, sync the timestamp back and resume.
        overlay.Closed += (_, closedTimeMs) =>
        {
            inlinePlayer.ResumeAt(closedTimeMs);
        };

        var mainWindow = (global::Avalonia.Application.Current?.ApplicationLifetime
            as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

        var layer = OverlayLayer.GetOverlayLayer(mainWindow);

        if (layer is not null)
        {
            overlay.Bind(global::Avalonia.Layout.Layoutable.WidthProperty,
                new Binding("Bounds.Width") { Source = layer });
            overlay.Bind(global::Avalonia.Layout.Layoutable.HeightProperty,
                new Binding("Bounds.Height") { Source = layer });

            layer.Children.Add(overlay);
            overlay.PlayVideo(vm.Path, timeMs);
        }
        else
        {
            // No overlay layer — just resume the carousel player.
            inlinePlayer.ResumeAt(timeMs);
        }
    }
}
