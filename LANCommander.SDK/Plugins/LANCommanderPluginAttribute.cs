using System;

namespace LANCommander.SDK.Plugins;

/// <summary>
/// Assembly-level attribute that marks an assembly as a LANCommander plugin and declares its
/// entry point and compatibility metadata. This is the primary discovery mechanism used by the loader.
/// </summary>
/// <example>
/// [assembly: LANCommanderPlugin(typeof(MyPlugin), Id = "com.acme.myplugin",
///     MinHostVersion = "1.1.0", Hosts = PluginHost.Server | PluginHost.Launcher)]
/// </example>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class LANCommanderPluginAttribute : Attribute
{
    /// <summary>The concrete type implementing <see cref="IPlugin"/> that serves as the entry point.</summary>
    public Type EntryPoint { get; }

    /// <summary>Optional override for the plugin id; when null the loader falls back to the instance's <see cref="IPlugin.Id"/>.</summary>
    public string? Id { get; set; }

    /// <summary>Minimum compatible host (SDK) version, inclusive. Null means no lower bound.</summary>
    public string? MinHostVersion { get; set; }

    /// <summary>Maximum compatible host (SDK) version, inclusive. Null means no upper bound.</summary>
    public string? MaxHostVersion { get; set; }

    /// <summary>The hosts this plugin supports. Defaults to both server and launcher.</summary>
    public PluginHost Hosts { get; set; } = PluginHost.Server | PluginHost.Launcher;

    public LANCommanderPluginAttribute(Type entryPoint)
    {
        EntryPoint = entryPoint;
    }
}
