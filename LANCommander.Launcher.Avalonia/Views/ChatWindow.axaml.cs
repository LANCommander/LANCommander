using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LANCommander.Launcher.Avalonia.ViewModels;

namespace LANCommander.Launcher.Avalonia.Views;

public partial class ChatWindow : Window
{
    private ChatWindowViewModel? ViewModel => DataContext as ChatWindowViewModel;

    public ChatWindow()
    {
        InitializeComponent();

        Activated   += (_, _) => { if (ViewModel != null) ViewModel.IsWindowActive = true; };
        Deactivated += (_, _) => { if (ViewModel != null) ViewModel.IsWindowActive = false; };

        Closing += (_, e) =>
        {
            // Hide instead of closing so the window (and its unread counts) survive
            e.Cancel = true;
            Hide();
        };
    }

    // ── Resize grips ─────────────────────────────────────────────────────────

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

    // ── Titlebar interactions ─────────────────────────────────────────────────

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

    private void MinimizeButton_Click(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void CloseButton_Click(object? sender, RoutedEventArgs e) => Close();

    // ── Message input ─────────────────────────────────────────────────────────

    private void MessageInputBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ViewModel?.SendMessageCommand.CanExecute(null) == true)
        {
            ViewModel.SendMessageCommand.Execute(null);
            e.Handled = true;
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Show the window and scroll to the bottom of the active thread.</summary>
    public new void Show()
    {
        base.Show();
        Activate();
        ScrollToBottom();
    }

    internal void ScrollToBottom()
    {
        if (MessageScrollViewer is { } sv)
            sv.ScrollToEnd();
    }
}
