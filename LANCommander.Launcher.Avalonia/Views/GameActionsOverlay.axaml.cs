using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using LANCommander.Launcher.Avalonia.ViewModels.Components;
using ManifestAction = LANCommander.SDK.Models.Manifest.Action;

namespace LANCommander.Launcher.Avalonia.Views;

public partial class GameActionsOverlay : UserControl
{
    public event EventHandler<ManifestAction?>? ActionSelected;

    public GameActionsOverlay()
    {
        InitializeComponent();
    }

    private void Action_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: GameActionViewModel vm })
            Close(vm.Action);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);

    private void Close(ManifestAction? action)
    {
        var layer = OverlayLayer.GetOverlayLayer(this);
        ActionSelected?.Invoke(this, action);
        layer?.Children.Remove(this);
    }
}
