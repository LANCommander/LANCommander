using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LANCommander.Launcher.Avalonia.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    /// <summary>
    /// Saved vertical scroll offset, used to restore scroll position when a view
    /// is recreated (e.g. after navigating back). Views opt-in via the
    /// <c>ScrollPersistence.ViewModel</c> attached property on their ScrollViewer.
    /// </summary>
    public Vector SavedScrollOffset { get; set; }
}
