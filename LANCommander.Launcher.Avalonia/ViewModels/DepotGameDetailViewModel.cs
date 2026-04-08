using System;

namespace LANCommander.Launcher.Avalonia.ViewModels;

/// <summary>
/// Depot-specific game detail view model. Extends <see cref="GameDetailViewModel"/>
/// so the ShellView DataTemplate can route to a distinct depot detail view.
/// FromLibrary is always false since this is only used in the depot context.
/// </summary>
public partial class DepotGameDetailViewModel : GameDetailViewModel
{
    public DepotGameDetailViewModel(IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
        FromLibrary = false;
    }
}
