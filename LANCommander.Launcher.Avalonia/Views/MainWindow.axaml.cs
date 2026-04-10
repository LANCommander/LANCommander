using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace LANCommander.Launcher.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void ResizeGrip_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (WindowState != WindowState.Normal) return;
        if (sender is not Border { Name: var name }) return;
        var edge = name switch
        {
            "ResizeNW" => WindowEdge.NorthWest,
            "ResizeN"  => WindowEdge.North,
            "ResizeNE" => WindowEdge.NorthEast,
            "ResizeW"  => WindowEdge.West,
            "ResizeE"  => WindowEdge.East,
            "ResizeSW" => WindowEdge.SouthWest,
            "ResizeS"  => WindowEdge.South,
            "ResizeSE" => WindowEdge.SouthEast,
            _          => (WindowEdge?)null
        };
        if (edge.HasValue) BeginResizeDrag(edge.Value, e);
    }

    private void TitleBarDragRegion_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void TitleBarDragRegion_DoubleTapped(object? sender, TappedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void MinimizeButton_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
