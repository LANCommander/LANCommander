using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using LANCommander.Launcher.ViewModels;

namespace LANCommander.Launcher.Views;

public partial class GameVersionsOverlay : UserControl
{
    /// <summary>Raised when the overlay closes. Carries the chosen version, or null when dismissed.</summary>
    public event EventHandler<SDK.Models.GameVersion?>? VersionSelected;

    public GameVersionsOverlay()
    {
        InitializeComponent();
    }

    private void Install_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: GameVersionItemViewModel item })
            Close(item.Version);
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close(null);

    private void Close(SDK.Models.GameVersion? version)
    {
        var layer = OverlayLayer.GetOverlayLayer(this);
        VersionSelected?.Invoke(this, version);
        layer?.Children.Remove(this);
    }
}
