using LANCommander.Server.Plugins;

namespace LANCommander.Server.Services.Pages;

/// <summary>
/// A page rendered by <see cref="PageRenderService"/>: its HTML, broken where a plugin's embed is
/// shown as a component. Each piece of HTML is complete, since embeds of a plugin's kind are only
/// shown at the top level of a page.
/// </summary>
public sealed record RenderedPage(IReadOnlyList<RenderedPageSegment> Segments)
{
    public static readonly RenderedPage Empty = new([]);

    /// <summary>The page's HTML without the plugins' components</summary>
    public string Html => String.Concat(Segments.OfType<RenderedPageHtml>().Select(s => s.Html));

    public bool IsEmpty => Segments.All(s => s is RenderedPageHtml html && String.IsNullOrWhiteSpace(html.Html));
}

public abstract record RenderedPageSegment;

/// <summary>Rendered Markdown and built-in embeds, safe to show as it is</summary>
public sealed record RenderedPageHtml(string Html) : RenderedPageSegment;

/// <summary>An embed shown as its <see cref="IPageEmbedExtension"/>'s component</summary>
public sealed record RenderedPageComponent(IPageEmbedExtension Extension, PageEmbed Embed) : RenderedPageSegment;
