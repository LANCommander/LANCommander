using CommunityToolkit.Mvvm.ComponentModel;

namespace LANCommander.Launcher.Plugins;

/// <summary>
/// Base type for view models supplied by plugins. Lives in this project (rather than the
/// launcher's ViewModels assembly) so plugins can derive from it without taking a dependency
/// on the launcher application itself, avoiding a circular reference.
/// </summary>
public abstract class PluginViewModelBase : ObservableObject
{
}
