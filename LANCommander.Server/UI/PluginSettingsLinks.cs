using LANCommander.Server.Plugins;

namespace LANCommander.Server.UI;

/// <summary>The settings page each plugin links from its row on Settings → Plugins.</summary>
internal static class PluginSettingsLinks
{
    /// <summary>
    /// Each plugin's settings route, keyed by plugin id. Invalid links, links no loaded plugin owns, and
    /// every link of a plugin that registers more than one are dropped with a warning.
    /// </summary>
    /// <param name="ownerOf">The id of the plugin an extension belongs to, or null if none does.</param>
    internal static IReadOnlyDictionary<string, string> Resolve(
        IEnumerable<IPluginSettingsExtension> extensions,
        Func<IPluginSettingsExtension, string?> ownerOf,
        ILogger logger)
    {
        var owned = new List<(string PluginId, string Href)>();

        foreach (var extension in extensions)
        {
            if (string.IsNullOrWhiteSpace(extension.Href) || !extension.Href.StartsWith("/Plugins/", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Ignoring invalid plugin settings extension {ExtensionType}", extension.GetType().FullName);
                continue;
            }

            var pluginId = ownerOf(extension);

            if (pluginId == null)
            {
                logger.LogWarning("Ignoring plugin settings extension {ExtensionType}, which no loaded plugin owns", extension.GetType().FullName);
                continue;
            }

            owned.Add((pluginId, extension.Href));
        }

        var links = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var plugin in owned.GroupBy(link => link.PluginId, StringComparer.OrdinalIgnoreCase))
        {
            if (plugin.Count() > 1)
                logger.LogWarning("Ignoring the settings extensions of plugin {PluginId}, which registers more than one", plugin.Key);
            else
                links[plugin.Key] = plugin.Single().Href;
        }

        return links;
    }
}
