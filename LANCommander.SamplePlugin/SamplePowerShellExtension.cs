using LANCommander.SDK.Plugins;

namespace LANCommander.SamplePlugin;

/// <summary>
/// Registers the sample plugin's cmdlets (and optionally PowerShell modules) into the runspace used
/// by LANCommander scripts.
/// </summary>
public sealed class SamplePowerShellExtension : IPluginPowerShellExtension
{
    public IEnumerable<Type> GetCmdletTypes() => new[] { typeof(SampleGreetingCmdlet) };

    public IEnumerable<string> GetModulePaths() => Array.Empty<string>();
}
