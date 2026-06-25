using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace LANCommander.Launcher.Views;

public partial class GameOptionsOverlay : UserControl
{
    public event EventHandler<bool?>? DialogClosed;

    public GameOptionsOverlay()
    {
        InitializeComponent();
    }

    private void Confirm_Click(object? sender, RoutedEventArgs e) => Close(true);
    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);

    private void Close(bool? result)
    {
        var layer = OverlayLayer.GetOverlayLayer(this);
        DialogClosed?.Invoke(this, result);
        layer?.Children.Remove(this);
    }
}
