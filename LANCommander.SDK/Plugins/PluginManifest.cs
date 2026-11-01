using System;
using System.Collections.Generic;
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

    /// <summary>The folder, inside a plugin's folder, that holds what the plugin writes.</summary>
    public const string DataFolderName = "Data";

    /// <summary>Absolute path to the folder the plugin was loaded from.</summary>
    public string Directory { get; init; } = string.Empty;

    /// <summary>
    /// Absolute path to the folder for the plugin's own files: its settings and anything else it keeps.
    /// It lives inside <see cref="Directory"/>, survives the plugin being upgraded, and goes when the
    /// plugin is removed. It may not exist yet.
    /// </summary>
    public string DataDirectory => System.IO.Path.Combine(Directory, DataFolderName);

    /// <summary>See <see cref="LANCommanderPluginAttribute.DataId"/>.</summary>
    public string? DataId { get; init; }

    /// <summary>See <see cref="LANCommanderPluginAttribute.MigrationAssemblies"/>.</summary>
    public IReadOnlyList<string> MigrationAssemblies { get; init; } = Array.Empty<string>();

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
            DataId = attribute.DataId,
            MigrationAssemblies = attribute.MigrationAssemblies ?? Array.Empty<string>(),
        };
}
