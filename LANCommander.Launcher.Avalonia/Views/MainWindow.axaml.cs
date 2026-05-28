using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using LANCommander.Launcher.Avalonia.ViewModels;

namespace LANCommander.Launcher.Avalonia.Views;

public partial class MainWindow : Window
{
    private WindowState _stateBeforeBigScreen = WindowState.Normal;

    public MainWindow()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.BigScreenModeChanged += OnBigScreenModeChanged;
                vm.ExitLauncherRequested += (_, _) => Close();

                // Apply big screen mode if it was persisted or set via command line
                if (vm.IsBigScreenMode)
                    WindowState = WindowState.FullScreen;
            }
        };
    }

    private void OnBigScreenModeChanged(object? sender, System.EventArgs e)
    {
        if (sender is not MainWindowViewModel vm) return;

        if (vm.IsBigScreenMode)
        {
            _stateBeforeBigScreen = WindowState;
            WindowState = WindowState.FullScreen;
        }
        else
        {
            WindowState = _stateBeforeBigScreen;
        }
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
