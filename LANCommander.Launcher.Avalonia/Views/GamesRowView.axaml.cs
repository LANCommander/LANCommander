using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using LANCommander.Launcher.Avalonia.ViewModels;

namespace LANCommander.Launcher.Avalonia.Views;

public partial class GamesRowView : UserControl
{
    public GamesRowView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is ViewModelBase vm
                && vm.SavedScrollOffset != default
                && GamesListBox.Scroll is ScrollViewer sv)
            {
                sv.Offset = vm.SavedScrollOffset;
            }
        }, DispatcherPriority.Loaded);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (DataContext is ViewModelBase vm && GamesListBox.Scroll is ScrollViewer sv)
            vm.SavedScrollOffset = sv.Offset;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnGameTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is GamesCollectionViewModel vm && vm.SelectedGame is not null)
            vm.ViewGameDetailsCommand.Execute(vm.SelectedGame);
    }
}
