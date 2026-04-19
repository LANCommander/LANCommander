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

    public DownloadQueueItemView()
    {
        InitializeComponent();
    }
}
