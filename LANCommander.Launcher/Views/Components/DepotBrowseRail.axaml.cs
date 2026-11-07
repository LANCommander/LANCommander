using Avalonia.Controls;
using Avalonia.Interactivity;
using LANCommander.Launcher.Settings.Enums;
using LANCommander.Launcher.ViewModels;

namespace LANCommander.Launcher.Views.Components;

/// <summary>
/// The depot rail's section on the browse page: the BROWSE rows and the page's filters. Hosted by the
/// shell beside the browse results and the depot game pages opened from them.
/// </summary>
public partial class DepotBrowseRail : UserControl
{
    public DepotBrowseRail()
    {
        InitializeComponent();
    }

    private void GridViewButton_Click(object? sender, RoutedEventArgs e) => SetViewType(GameViewType.Grid);

    private void ListViewButton_Click(object? sender, RoutedEventArgs e) => SetViewType(GameViewType.List);

    private void HorizontalViewButton_Click(object? sender, RoutedEventArgs e) => SetViewType(GameViewType.Horizontal);

    private void SetViewType(GameViewType viewType)
    {
        if (DataContext is GamesCollectionViewModel vm)
            vm.SelectedViewType = viewType;
    }
}
