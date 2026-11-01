using System;
using System.Threading;
using System.Threading.Tasks;

namespace LANCommander.SDK.Plugins;

/// <summary>
/// A plugin's own settings, kept by the host in a YAML file of their own:
/// <c>Data/Settings.yaml</c> in the plugin's own folder (see <see cref="PluginManifest.DataDirectory"/>). Inject it with the
/// plugin's settings type; the host knows which plugin is asking from the assembly that type is
/// declared in, so a plugin can only reach its own file.
/// </summary>
/// <remarks>
/// The file is written the way the host's own <c>Settings.yaml</c> is: property names in PascalCase,
/// and saving debounced, so a burst of changes is written once. Properties the plugin no longer has
/// are dropped from the file on the next save; ones it doesn't have yet take their defaults.
/// </remarks>
/// <typeparam name="TSettings">A plain class with a parameterless constructor, declared in the plugin.</typeparam>
/// <example>
/// <code>
/// public sealed class MySettings
/// {
///     public bool Enabled { get; set; }
///     public int Port { get; set; } = 1234;
/// }
///
/// public sealed class MyService(IPluginSettings&lt;MySettings&gt; settings)
/// {
///     public void TurnOn() => settings.Update(s => s.Enabled = true);
/// }
/// </code>
/// </example>
public interface IPluginSettings<TSettings> where TSettings : class, new()
{
    /// <summary>The settings, read from the file on first use, or the type's defaults when there is none.</summary>
    TSettings Current { get; }

    /// <summary>Changes the settings and schedules them to be written.</summary>
    void Update(Action<TSettings> mutator);

    /// <summary>
    /// Writes a pending change now instead of after the debounce, for a value that must not be lost
    /// to a crash, such as a rotated credential.
    /// </summary>
    Task FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>Raised after <see cref="Update"/> changes the settings.</summary>
    event Action? Changed;
}
