using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using LANCommander.Launcher.Controls;
using LANCommander.Launcher.ViewModels.Components;
using ManifestAction = LANCommander.SDK.Models.Manifest.Action;

namespace LANCommander.Launcher.Views;

public partial class GameActionsOverlay : UserControl
{
    public event EventHandler<ManifestAction?>? ActionSelected;

    public GameActionsOverlay()
    {
        InitializeComponent();
        ModalEscape.Enable(this, () => Close(null));
    }

    private void Action_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: GameActionViewModel vm })
            Close(vm.Action);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);
    private void TitleBar_CloseRequested(object? sender, EventArgs e) => Close(null);

    private void Close(ManifestAction? action)
    {
        var layer = OverlayLayer.GetOverlayLayer(this);
        ActionSelected?.Invoke(this, action);
        layer?.Children.Remove(this);
    }
}
