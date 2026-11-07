using System;
using System.ComponentModel;
using System.Threading.Tasks;
using LANCommander.Launcher.ViewModels;

namespace LANCommander.Launcher.Services;

public interface INavigationService : INotifyPropertyChanged
{
    ViewModelBase? CurrentView { get; }
    bool CanGoBack { get; }
    bool IsNavigating { get; }

    /// <summary>
    /// Navigate to a view model. If <paramref name="clearHistory"/> is true the history
    /// stack is emptied first (use for root navigations like Library/Depot toggle).
    /// An optional <paramref name="initializeAsync"/> callback runs while
    /// <see cref="IsNavigating"/> is true, driving the loading indicator. With
    /// <paramref name="replaceCurrent"/> the view takes the current one's place instead of going on
    /// top of it, so Back skips the view being replaced.
    /// </summary>
    void NavigateTo(ViewModelBase viewModel, bool clearHistory = false, Func<Task>? initializeAsync = null, bool replaceCurrent = false);

    /// <summary>Pop the history stack and return to the previous view. No-op if the stack is empty.</summary>
    void GoBack();

    /// <summary>
    /// Replace the current view without pushing to history.
    /// Useful for in-place refreshes (e.g. re-initializing DepotBrowse with new filters).
    /// </summary>
    void ReplaceCurrent(ViewModelBase viewModel);
}
