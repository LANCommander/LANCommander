namespace LANCommander.Server.Plugins;

/// <summary>
/// Links a plugin's settings page from its row on <b>Settings → Plugins</b>, under the row's menu.
/// Use it for settings too small to deserve an entry in the server's navigation; a plugin with pages
/// of its own registers an <see cref="IServerNavigationExtension"/> instead.
/// Register an implementation in <c>IPlugin.ConfigureServices</c>.
/// </summary>
/// <remarks>
/// The plugin the link belongs to is the one whose assembly holds the implementation, so a plugin
/// registers at most one. The link is offered only while the plugin is running.
/// </remarks>
public interface IPluginSettingsExtension
{
    /// <summary>Plugin application route of the settings page, beginning with <c>/Plugins/</c>.</summary>
    string Href { get; }
}
