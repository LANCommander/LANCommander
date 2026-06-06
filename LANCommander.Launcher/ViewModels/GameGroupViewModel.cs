using System.Collections.ObjectModel;
using LANCommander.Launcher.ViewModels.Components;

namespace LANCommander.Launcher.ViewModels;

/// <summary>
/// A named group of games used when GroupBy is active.
/// </summary>
public class GameGroupViewModel
{
    public string Name { get; set; } = string.Empty;
    public ObservableCollection<GameItemViewModel> Items { get; set; } = new();
}
