using LANCommander.Server.Plugins.Records;
using LANCommander.Server.Services.Utilities;
using ServerEntity = LANCommander.Server.Data.Models.Server;

namespace LANCommander.Server.Plugins;

/// <summary>A plugin's pages for one server, under its extension's name.</summary>
public sealed record PluginServerSectionGroup(IServerSectionExtension Extension, IReadOnlyList<ServerSection> Sections);

/// <summary>A plugin's component for the top of one server's General page.</summary>
public sealed record PluginServerOverview(IServerSectionExtension Extension, Type Component);

/// <summary>
/// The pages plugins add to game servers through <see cref="IServerSectionExtension"/>s. An extension
/// whose id can't be part of an address, or that another already has, is left out with a warning; what
/// an extension offers that can't be shown, or fails to offer, is left out of that server's pages.
/// </summary>
public sealed class PluginServerSections
{
    private readonly IReadOnlyList<IServerSectionExtension> _extensions;
    private readonly ILogger<PluginServerSections> _logger;

    public PluginServerSections(IEnumerable<IServerSectionExtension> extensions, ILogger<PluginServerSections> logger)
    {
        _logger = logger;

        var valid = new List<IServerSectionExtension>();

        foreach (var extension in extensions)
        {
            if (!PluginComponents.IsExtensionId(extension.Id))
            {
                logger.LogWarning("The server page extension {Extension} has no id, or one that can't be part of an address, so its pages aren't offered", extension.GetType().FullName);
                continue;
            }

            if (valid.Any(e => string.Equals(e.Id, extension.Id, StringComparison.OrdinalIgnoreCase)))
            {
                logger.LogWarning("Another server page extension already has the id {ExtensionId}, so {Extension}'s pages aren't offered", extension.Id, extension.GetType().FullName);
                continue;
            }

            valid.Add(extension);
        }

        _extensions = valid.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>The server as the extensions see it.</summary>
    public static ServerInfo ToInfo(ServerEntity server) => ServerProvider.Info(server);

    /// <summary>Every extension's pages for the server, by extension name; extensions with none are left out.</summary>
    public IReadOnlyList<PluginServerSectionGroup> GetSections(ServerEntity server)
    {
        if (server.Id == Guid.Empty || _extensions.Count == 0)
            return [];

        var info = ToInfo(server);
        var groups = new List<PluginServerSectionGroup>();

        foreach (var extension in _extensions)
        {
            var sections = SectionsOf(extension, info);

            if (sections.Count > 0)
                groups.Add(new PluginServerSectionGroup(extension, sections));
        }

        return groups;
    }

    /// <summary>The page an extension adds to the server at the given slug, or null.</summary>
    public (IServerSectionExtension Extension, ServerSection Section)? Find(ServerEntity server, string? extensionId, string? slug)
    {
        var extension = _extensions.FirstOrDefault(e => string.Equals(e.Id, extensionId, StringComparison.OrdinalIgnoreCase));

        if (extension == null || server.Id == Guid.Empty)
            return null;

        var section = SectionsOf(extension, ToInfo(server))
            .FirstOrDefault(s => string.Equals(s.Slug, slug, StringComparison.OrdinalIgnoreCase));

        return section == null ? null : (extension, section);
    }

    /// <summary>Every extension's component for the top of the server's General page, by extension name.</summary>
    public IReadOnlyList<PluginServerOverview> GetOverviews(ServerEntity server)
    {
        if (server.Id == Guid.Empty || _extensions.Count == 0)
            return [];

        var info = ToInfo(server);
        var overviews = new List<PluginServerOverview>();

        foreach (var extension in _extensions)
        {
            try
            {
                var overview = extension.GetOverview(info);

                if (overview == null)
                    continue;

                if (PluginComponents.IsComponent(overview))
                    overviews.Add(new PluginServerOverview(extension, overview));
                else
                    _logger.LogWarning("{Extension} offered {Type} as {ServerName}'s overview, which isn't a component", extension.Name, overview.FullName, server.Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "{Extension} could not say what to show on {ServerName}'s General page", extension.Name, server.Name);
            }
        }

        return overviews;
    }

    private IReadOnlyList<ServerSection> SectionsOf(IServerSectionExtension extension, ServerInfo server)
    {
        try
        {
            var sections = new List<ServerSection>();

            foreach (var section in extension.GetSections(server) ?? [])
            {
                if (section == null || !PluginComponents.IsSlug(section.Slug) || !PluginComponents.IsComponent(section.Component))
                {
                    _logger.LogWarning("{Extension} offered a page for {ServerName} that can't be shown: {Slug}", extension.Name, server.Name, section?.Slug);
                    continue;
                }

                if (sections.Any(s => string.Equals(s.Slug, section.Slug, StringComparison.OrdinalIgnoreCase)))
                {
                    _logger.LogWarning("{Extension} offered more than one page {Slug} for {ServerName}; only the first is shown", extension.Name, section.Slug, server.Name);
                    continue;
                }

                sections.Add(section with { Title = string.IsNullOrWhiteSpace(section.Title) ? section.Slug : section.Title });
            }

            return sections;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Extension} could not list its pages for {ServerName}", extension.Name, server.Name);

            return [];
        }
    }
}
