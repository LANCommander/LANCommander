using System;
using System.Reflection;

namespace LANCommander.SDK.Plugins;

/// <summary>
/// Parsed metadata describing a discovered plugin, derived from its <see cref="LANCommanderPluginAttribute"/>.
/// </summary>
public sealed class PluginManifest
{
    public string Id { get; init; } = string.Empty;
    public Type EntryPoint { get; init; } = default!;
    public string? MinHostVersion { get; init; }
    public string? MaxHostVersion { get; init; }
    public PluginHost Hosts { get; init; } = PluginHost.Server | PluginHost.Launcher;

    /// <summary>The assembly the plugin was loaded from.</summary>
    public Assembly Assembly { get; init; } = default!;

    /// <summary>Absolute path to the folder the plugin was loaded from.</summary>
    public string Directory { get; init; } = string.Empty;

    /// <summary>Builds a manifest from an assembly-level plugin attribute.</summary>
    public static PluginManifest FromAttribute(LANCommanderPluginAttribute attribute, Assembly assembly, string directory)
        => new()
        {
            Id = attribute.Id ?? attribute.EntryPoint.FullName ?? attribute.EntryPoint.Name,
            EntryPoint = attribute.EntryPoint,
            MinHostVersion = attribute.MinHostVersion,
            MaxHostVersion = attribute.MaxHostVersion,
            Hosts = attribute.Hosts,
            Assembly = assembly,
            Directory = directory,
        };
}
