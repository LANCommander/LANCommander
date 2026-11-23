using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using LANCommander.Server.Plugins;
using LANCommander.Server.Services.Utilities;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services.Pages;

/// <summary>
/// The embed kinds plugins add to pages through <see cref="IPageEmbedExtension"/>s. An extension
/// whose kind can't be written, is built in, or is taken by another extension, or whose component
/// can't be shown, is left out.
/// </summary>
/// <remarks>
/// Scoped, so extensions may be registered with any lifetime and use scoped services such as a
/// plugin's database context.
/// </remarks>
public sealed class PageEmbedExtensions
{
    static readonly Regex KindSyntax = new("^[A-Za-z]+$", RegexOptions.Compiled);

    // Each scope finds the same problems, so they are logged once
    static readonly ConcurrentDictionary<string, byte> Reported = new();

    readonly Dictionary<string, IPageEmbedExtension> _byKind = new(StringComparer.OrdinalIgnoreCase);

    public PageEmbedExtensions(IEnumerable<IPageEmbedExtension> extensions, ILogger<PageEmbedExtensions> logger)
    {
        foreach (var extension in extensions)
        {
            var kind = extension.Kind;

            if (String.IsNullOrEmpty(kind) || !KindSyntax.IsMatch(kind))
            {
                if (Reported.TryAdd($"{extension.GetType().FullName}:{kind}", 0))
                    logger.LogWarning("The page embed extension {Extension} has a kind that isn't only letters, so it isn't offered", extension.GetType().FullName);
                continue;
            }

            if (PageRenderService.Kinds.ContainsKey(kind.ToLowerInvariant()))
            {
                if (Reported.TryAdd($"{extension.GetType().FullName}:{kind}", 0))
                    logger.LogWarning("The page embed extension {Extension} has the built-in kind {Kind}, so it isn't offered", extension.GetType().FullName, kind);
                continue;
            }

            if (!PluginComponents.IsComponent(extension.Component) || !PluginComponents.HasParameter(extension.Component, "Embed", typeof(PageEmbed)))
            {
                if (Reported.TryAdd($"{extension.GetType().FullName}:{kind}", 0))
                    logger.LogWarning("The page embed extension {Extension} has no component, or one without a PageEmbed Embed parameter, so it isn't offered", extension.GetType().FullName);
                continue;
            }

            if (!_byKind.TryAdd(kind, extension) && Reported.TryAdd($"{extension.GetType().FullName}:{kind}", 0))
                logger.LogWarning("Another page embed extension already has the kind {Kind}, so {Extension} isn't offered", kind, extension.GetType().FullName);
        }

        All = _byKind.Values.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    /// <summary>The usable extensions, by name, as the editor lists them</summary>
    public IReadOnlyList<IPageEmbedExtension> All { get; }

    public IPageEmbedExtension? Find(string kind) => _byKind.GetValueOrDefault(kind);
}
