using Avalonia.Controls;
using Avalonia.Input;
using LANCommander.Launcher.Avalonia.ViewModels;

namespace LANCommander.Launcher.Avalonia.Views;

public partial class GamesRowView : UserControl
{
    public GamesRowView()
    {
        InitializeComponent();
    }

    private void OnGameTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is GamesCollectionViewModel vm && vm.SelectedGame is not null)
            vm.ViewGameDetailsCommand.Execute(vm.SelectedGame);
    }
}
