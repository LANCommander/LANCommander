using System;
using System.IO;
using System.Reflection;

namespace LANCommander.SDK.Plugins;

/// <summary>
/// Where a plugin keeps its files, for code that runs before <see cref="PluginContext"/> is available,
/// such as service registrations in <see cref="IPlugin.ConfigureServices"/>.
/// </summary>
public static class PluginPaths
{
    /// <summary>
    /// The plugin's data folder (see <see cref="PluginManifest.DataDirectory"/>), found from any type
    /// in the plugin's main assembly: <c>PluginPaths.GetDataDirectory&lt;MyPlugin&gt;()</c>. It may not
    /// exist yet.
    /// </summary>
    public static string GetDataDirectory<T>() => GetDataDirectory(typeof(T).Assembly);

    /// <inheritdoc cref="GetDataDirectory{T}"/>
    /// <param name="assembly">The plugin's main assembly, which sits in the plugin's folder.</param>
    public static string GetDataDirectory(Assembly assembly) =>
        Path.Combine(GetPluginDirectory(assembly), PluginManifest.DataFolderName);

    /// <summary>The folder the plugin was loaded from (see <see cref="PluginManifest.Directory"/>).</summary>
    /// <param name="assembly">The plugin's main assembly.</param>
    public static string GetPluginDirectory(Assembly assembly)
    {
        if (string.IsNullOrEmpty(assembly.Location))
            throw new InvalidOperationException($"{assembly.GetName().Name} was not loaded from a file, so it has no plugin folder.");

        return Path.GetDirectoryName(assembly.Location)!;
    }
}
