using Avalonia.Controls;
using Avalonia.Interactivity;
using LANCommander.Launcher.Avalonia.ViewModels;
using LANCommander.Launcher.Settings.Enums;

namespace LANCommander.Launcher.Avalonia.Views;

public partial class GamesListView : UserControl
{
    public GamesListView()
    {
        InitializeComponent();
    }

    private void GridViewButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GamesCollectionViewModel vm)
            vm.SelectedViewType = GameViewType.Grid;
    }

    private void ListViewButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GamesCollectionViewModel vm)
            vm.SelectedViewType = GameViewType.List;
    }

    private void HorizontalViewButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is GamesCollectionViewModel vm)
            vm.SelectedViewType = GameViewType.Horizontal;
    }
}
