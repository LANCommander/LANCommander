using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace LANCommander.Launcher.Avalonia.Views.Components;

public partial class DownloadQueueItemView : UserControl
{
    public static readonly StyledProperty<ICommand?> ToggleExpandedCommandProperty =
        AvaloniaProperty.Register<DownloadQueueItemView, ICommand?>(nameof(ToggleExpandedCommand));

    public static readonly StyledProperty<ICommand?> CancelCommandProperty =
        AvaloniaProperty.Register<DownloadQueueItemView, ICommand?>(nameof(CancelCommand));

    public static readonly StyledProperty<ICommand?> RemoveCommandProperty =
        AvaloniaProperty.Register<DownloadQueueItemView, ICommand?>(nameof(RemoveCommand));

    public static readonly StyledProperty<ICommand?> ViewInLibraryCommandProperty =
        AvaloniaProperty.Register<DownloadQueueItemView, ICommand?>(nameof(ViewInLibraryCommand));

    public static readonly StyledProperty<ICommand?> PlayCommandProperty =
        AvaloniaProperty.Register<DownloadQueueItemView, ICommand?>(nameof(PlayCommand));

    public ICommand? ToggleExpandedCommand
    {
        get => GetValue(ToggleExpandedCommandProperty);
        set => SetValue(ToggleExpandedCommandProperty, value);
    }

    public ICommand? CancelCommand
    {
        get => GetValue(CancelCommandProperty);
        set => SetValue(CancelCommandProperty, value);
    }

    public ICommand? RemoveCommand
    {
        get => GetValue(RemoveCommandProperty);
        set => SetValue(RemoveCommandProperty, value);
    }

    public ICommand? ViewInLibraryCommand
    {
        get => GetValue(ViewInLibraryCommandProperty);
        set => SetValue(ViewInLibraryCommandProperty, value);
    }

    public ICommand? PlayCommand
    {
        get => GetValue(PlayCommandProperty);
        set => SetValue(PlayCommandProperty, value);
    }

    public DownloadQueueItemView()
    {
        InitializeComponent();
    }
}
