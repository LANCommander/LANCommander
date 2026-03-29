using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using LANCommander.Launcher.Avalonia.ViewModels;

namespace LANCommander.Launcher.Avalonia.Views;

public partial class GamesGroupedView : UserControl
{
    public GamesGroupedView()
    {
        InitializeComponent();
    }

    private void IndexLetter_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: GameGroupViewModel group })
            ScrollToGroup(group.Name);
    }

    private void ScrollToGroup(string name)
    {
        if (DataContext is not GamesCollectionViewModel vm || GroupedItemsControl is null) return;

        var group = vm.GroupedGames.FirstOrDefault(g => g.Name == name);
        if (group != null)
            GroupedItemsControl.ScrollIntoView(group);
    }
}
