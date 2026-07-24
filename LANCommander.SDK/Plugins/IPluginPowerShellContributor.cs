using System;
using System.Collections.Generic;

namespace LANCommander.SDK.Plugins;

/// <summary>
/// Implemented by plugins that want to contribute PowerShell cmdlets or script modules into the
/// LANCommander runspace. Register the implementation in <see cref="IPlugin.ConfigureServices"/>;
/// the SDK's PowerShell runspace picks up all registered contributors when a script is executed.
/// </summary>
public interface IPluginPowerShellContributor
{
    /// <summary>
    /// Returns cmdlet types (classes decorated with <c>[Cmdlet]</c>) to register into each runspace.
    /// </summary>
    IEnumerable<Type> GetCmdletTypes();

    /// <summary>
    /// Returns absolute paths to PowerShell script modules (.psm1/.psd1) shipped with the plugin that
    /// should be imported into each runspace.
    /// </summary>
    IEnumerable<string> GetModulePaths();
}
