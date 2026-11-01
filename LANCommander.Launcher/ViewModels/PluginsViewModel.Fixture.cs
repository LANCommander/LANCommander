#if DEBUG
using System.Collections.Generic;
using System.Linq;
using LANCommander.Launcher.Settings;

namespace LANCommander.Launcher.ViewModels;

public partial class PluginsViewModel
{
    /// <summary>Fills the page in directly, in place of <see cref="LoadAsync"/>, which reads the plugins folder and the server.</summary>
    internal void SeedFixture(
        IEnumerable<InstalledPluginItem> installed,
        IEnumerable<PluginListingItem> results,
        IEnumerable<PluginPackageSource> sources,
        bool restartRequired = false)
    {
        LoadRegistries();

        foreach (var source in sources)
        {
            Registries.Add(new PluginRegistryItem(PluginRegistryKind.Custom)
            {
                Name = source.Name,
                Url = source.Url,
                Enabled = source.Enabled,
                Username = source.Username,
            });
        }

        Installed.Clear();

        foreach (var item in installed)
            Installed.Add(item);

        Results.Clear();

        foreach (var item in results)
            Results.Add(item);

        HasNoInstalled = Installed.Count == 0;
        HasNoResults = Results.Count == 0;
        RestartRequired = restartRequired;
    }
}
#endif
