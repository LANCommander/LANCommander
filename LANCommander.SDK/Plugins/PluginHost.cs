using System;

namespace LANCommander.SDK.Plugins;

/// <summary>
/// Identifies which LANCommander host a plugin targets. Used both as a single value
/// (the host a plugin is being loaded into) and as a flags set (the hosts a plugin declares support for).
/// </summary>
[Flags]
public enum PluginHost
{
    None = 0,
    Server = 1 << 0,
    Launcher = 1 << 1,
}
