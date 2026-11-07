using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using LANCommander.Launcher.ViewModels;

namespace LANCommander.Launcher.Views.Components;

/// <summary>
/// The library rail's title list. Hosted by the shell beside the library and the library's game
/// pages, so choosing a title swaps only the content next to it.
/// </summary>
public partial class LibraryRail : UserControl
{
    public LibraryRail()
    {
        InitializeComponent();
    }

    /// <summary>Whether <paramref name="element"/> is the title list or inside it.</summary>
    public bool ContainsInList(Visual element) =>
        GameListBox == element || GameListBox.IsVisualAncestorOf(element);

    /// <summary>Moves focus to the selected title, selecting the first one when none is.</summary>
    public void FocusSelectedTitle()
    {
        if (GameListBox.SelectedIndex < 0 && GameListBox.ItemCount > 0)
            GameListBox.SelectedIndex = 0;

        if (GameListBox.SelectedIndex >= 0)
        {
            // Scroll the selected item into view so its container is realized
            GameListBox.ScrollIntoView(GameListBox.SelectedIndex);

            if (GameListBox.ContainerFromIndex(GameListBox.SelectedIndex) is InputElement container)
            {
                container.Focus(NavigationMethod.Directional);
                return;
            }
        }

        GameListBox.Focus(NavigationMethod.Directional);
    }

    private void GameList_Tapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is LibraryViewModel vm && vm.SelectedGame != null)
            vm.ViewGameDetailsCommand.Execute(vm.SelectedGame);
    }

    private void GameList_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.Key != Key.Return)
            return;

        if (DataContext is LibraryViewModel vm && vm.SelectedGame != null)
        {
            vm.ViewGameDetailsCommand.Execute(vm.SelectedGame);
            e.Handled = true;
        }
    }
}
