using Avalonia.Controls;

namespace LANCommander.Launcher.Plugins.Extensions;

/// <summary>
/// Adds a top-level navigable destination reachable from the launcher shell. The view model
/// is registered with the <see cref="IViewRegistry"/> so the shell's content control can render the
/// associated view when navigated to.
/// </summary>
public interface INavigationPageExtension
{
    /// <summary>Label shown for the navigation entry.</summary>
    string Label { get; }

    /// <summary>Relative position among extension destinations; lower values appear first.</summary>
    int Order { get; }

    /// <summary>The view model type used both as the navigation target and the registry key.</summary>
    Type ViewModelType { get; }

    /// <summary>Creates the view model instance shown when the destination is activated.</summary>
    PluginViewModelBase CreateViewModel();

    /// <summary>Builds the control that renders <see cref="ViewModelType"/>.</summary>
    Control BuildView();
}
