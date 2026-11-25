using LANCommander.SDK.Enums;
using LANCommander.Server.Services.Utilities;

namespace LANCommander.Server.Plugins;

/// <summary>
/// The places plugins add to the import dialog through <see cref="IImportSourceExtension"/>s. An
/// extension whose id is invalid or taken, or whose component can't be given an
/// <see cref="ImportSourceContext"/>, is left out with a warning.
/// </summary>
public sealed class PluginImportSources
{
    private readonly IReadOnlyList<IImportSourceExtension> _extensions;

    public PluginImportSources(IEnumerable<IImportSourceExtension> extensions, ILogger<PluginImportSources> logger)
    {
        var valid = new List<IImportSourceExtension>();

        foreach (var extension in extensions)
        {
            try
            {
                if (!PluginComponents.IsExtensionId(extension.Id))
                {
                    logger.LogWarning("The import source {Extension} has no id, or an invalid one, so it isn't offered", extension.GetType().FullName);
                    continue;
                }

                if (valid.Any(e => string.Equals(e.Id, extension.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    logger.LogWarning("Another import source already has the id {ExtensionId}, so {Extension} isn't offered", extension.Id, extension.GetType().FullName);
                    continue;
                }

                if (!PluginComponents.IsComponent(extension.Component)
                    || !PluginComponents.HasParameter(extension.Component, "Context", typeof(ImportSourceContext)))
                {
                    logger.LogWarning("The import source {ExtensionId}'s component isn't a component taking an ImportSourceContext as its Context parameter, so it isn't offered", extension.Id);
                    continue;
                }

                if (extension.SupportedTypes is not { Count: > 0 })
                {
                    logger.LogWarning("The import source {ExtensionId} supports no kind of record, so it isn't offered", extension.Id);
                    continue;
                }

                valid.Add(extension);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "The import source {Extension} could not be read, so it isn't offered", extension.GetType().FullName);
            }
        }

        _extensions = valid.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>
    /// The sources that can import the kind of record, by name; every source when
    /// <paramref name="manifestType"/> is null.
    /// </summary>
    public IReadOnlyList<IImportSourceExtension> For(ManifestType? manifestType) =>
        manifestType == null
            ? _extensions
            : _extensions.Where(e => e.SupportedTypes.Contains(manifestType.Value)).ToList();
}
