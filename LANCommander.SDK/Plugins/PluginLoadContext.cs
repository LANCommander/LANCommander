using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Loader;

namespace LANCommander.SDK.Plugins;

/// <summary>
/// An isolated <see cref="AssemblyLoadContext"/> for a single plugin. Uses an
/// <see cref="AssemblyDependencyResolver"/> to resolve the plugin's private dependencies while
/// deferring host-provided assemblies (the SDK, DI abstractions, Avalonia, etc.) to the default
/// context so that shared types keep a single identity across the ALC boundary.
/// </summary>
public sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    /// <summary>
    /// Simple assembly names that must always resolve to the host's already-loaded copy so that
    /// contract types (<see cref="IPlugin"/>, DI, event bus, UI contracts) share identity. Matched
    /// as a prefix so, e.g., all Avalonia.* assemblies are covered.
    /// </summary>
    private static readonly string[] SharedAssemblyPrefixes =
    {
        "LANCommander.SDK",
        "LANCommander.Launcher.Plugins",
        "LANCommander.Server.Services",
        "Microsoft.Extensions.DependencyInjection",
        "Microsoft.Extensions.Logging",
        "Microsoft.Extensions.Hosting",
        "Microsoft.Extensions.Options",
        "Microsoft.Extensions.Configuration",
        "Avalonia",
        "CommunityToolkit.Mvvm",
        "System.Management.Automation",
    };

    public PluginLoadContext(string mainAssemblyPath)
        : base(name: $"Plugin:{System.IO.Path.GetFileNameWithoutExtension(mainAssemblyPath)}", isCollectible: false)
    {
        _resolver = new AssemblyDependencyResolver(mainAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (IsShared(assemblyName.Name))
            return null; // defer to the default context for shared/host-provided assemblies

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }

    private static bool IsShared(string? simpleName)
    {
        if (string.IsNullOrEmpty(simpleName))
            return false;

        foreach (var prefix in SharedAssemblyPrefixes)
        {
            if (simpleName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
