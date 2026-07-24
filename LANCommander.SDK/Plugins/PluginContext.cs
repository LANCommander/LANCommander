using System;
using Microsoft.Extensions.Logging;

namespace LANCommander.SDK.Plugins;

/// <summary>
/// Runtime context handed to a plugin during <see cref="IPlugin.InitializeAsync"/>.
/// </summary>
public sealed class PluginContext
{
    /// <summary>The host the plugin is running inside (a single value, never a flags combination).</summary>
    public PluginHost Host { get; init; }

    /// <summary>The fully built host service provider (scoped per plugin during initialization).</summary>
    public IServiceProvider Services { get; init; } = default!;

    /// <summary>Absolute path to the folder the plugin was loaded from.</summary>
    public string PluginDirectory { get; init; } = string.Empty;

    /// <summary>Logger scoped to the plugin.</summary>
    public ILogger Logger { get; init; } = default!;
}
