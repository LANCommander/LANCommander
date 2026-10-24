using System.Collections.Generic;

namespace LANCommander.SDK.Plugins;

/// <summary>
/// The NuGet package types a plugin package declares. Every plugin package declares <see cref="Plugin"/>,
/// plus the type of each host it runs on; hosts search their package sources for their own type.
/// </summary>
public static class PluginPackageTypes
{
    /// <summary>Declared by every LANCommander plugin package.</summary>
    public const string Plugin = "LANCommander.Plugin";

    /// <summary>Declared by plugin packages that run on the server.</summary>
    public const string Server = "LANCommander.Plugin.Server";

    /// <summary>Declared by plugin packages that run in the launcher.</summary>
    public const string Launcher = "LANCommander.Plugin.Launcher";

    /// <summary>The package type a host searches for.</summary>
    public static string For(PluginHost host) => host switch
    {
        PluginHost.Server => Server,
        PluginHost.Launcher => Launcher,
        _ => throw new System.ArgumentOutOfRangeException(nameof(host), host, "Expected a single host."),
    };

    /// <summary>Every package type a plugin targeting <paramref name="hosts"/> declares.</summary>
    public static IReadOnlyList<string> All(PluginHost hosts)
    {
        var types = new List<string> { Plugin };

        if ((hosts & PluginHost.Server) != 0)
            types.Add(Server);

        if ((hosts & PluginHost.Launcher) != 0)
            types.Add(Launcher);

        return types;
    }
}
