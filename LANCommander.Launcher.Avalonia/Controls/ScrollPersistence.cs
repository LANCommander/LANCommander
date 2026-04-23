using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LANCommander.Launcher.Avalonia.ViewModels;

namespace LANCommander.Launcher.Avalonia.Controls;

/// <summary>
/// Attached property that saves/restores scroll position to a <see cref="ViewModelBase"/>.
/// Usage: <c>&lt;ScrollViewer controls:ScrollPersistence.ViewModel="{Binding}" /&gt;</c>
/// On detach the current offset is written to <see cref="ViewModelBase.SavedScrollOffset"/>;
/// on attach the offset is restored.
/// </summary>
public class ScrollPersistence : AvaloniaObject
{
    public static readonly AttachedProperty<ViewModelBase?> ViewModelProperty =
        AvaloniaProperty.RegisterAttached<ScrollPersistence, ScrollViewer, ViewModelBase?>("ViewModel");

    static ScrollPersistence()
    {
        ViewModelProperty.Changed.AddClassHandler<ScrollViewer>(OnViewModelChanged);
    }

    public static ViewModelBase? GetViewModel(ScrollViewer sv) => sv.GetValue(ViewModelProperty);
    public static void SetViewModel(ScrollViewer sv, ViewModelBase? value) => sv.SetValue(ViewModelProperty, value);

    private static void OnViewModelChanged(ScrollViewer sv, AvaloniaPropertyChangedEventArgs e)
    {
        sv.DetachedFromVisualTree -= OnDetached;
        sv.AttachedToVisualTree -= OnAttached;

        if (e.NewValue is ViewModelBase)
        {
            sv.AttachedToVisualTree += OnAttached;
            sv.DetachedFromVisualTree += OnDetached;

            // If already in the visual tree (property set after load), restore now
            if (sv.GetVisualRoot() != null)
                RestoreOffset(sv);
        }
    }

    private static void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is ScrollViewer sv)
            Dispatcher.UIThread.Post(() => RestoreOffset(sv), DispatcherPriority.Loaded);
    }

    private static void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is ScrollViewer sv && GetViewModel(sv) is { } vm)
            vm.SavedScrollOffset = sv.Offset;
    }

    private static void RestoreOffset(ScrollViewer sv)
    {
        if (GetViewModel(sv) is { } vm && vm.SavedScrollOffset != default)
            sv.Offset = vm.SavedScrollOffset;
    }
}
