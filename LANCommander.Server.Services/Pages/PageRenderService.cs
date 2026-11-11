using System.Text;
using LANCommander.SDK.Enums;
using LANCommander.Server.Services.Enums;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.Extensions.Logging;
using static System.Net.WebUtility;

namespace LANCommander.Server.Services.Pages;

public record PageRenderOptions
{
    /// <summary>The page being rendered, which {{children}} lists beneath. Null outside a page, such as in the footer.</summary>
    public Guid? PageId { get; init; }

    /// <summary>Administrators get links to edit embedded items and see embeds that can't be resolved.</summary>
    public bool IsAdministrator { get; init; }
}

/// <summary>
/// Renders page Markdown to HTML, filling in embeds such as <c>{{game:&lt;id&gt;}}</c> with the
/// item's current details. Everything taken from the database is HTML-encoded.
/// </summary>
public sealed class PageRenderService(
    ILogger<PageRenderService> logger,
    PageService pageService,
    GameService gameService,
    ServerService serverService,
    RedistributableService redistributableService,
    ServerManager serverManager)
{
    /// <summary>The embeds the editor offers, by kind, with whether they take an id</summary>
    public static readonly IReadOnlyDictionary<string, bool> Kinds = new Dictionary<string, bool>
    {
        ["game"] = true,
        ["server"] = true,
        ["redistributable"] = true,
        ["page"] = true,
        ["children"] = false,
        ["toc"] = false,
    };

    const int DescriptionLength = 240;

    public async Task<string> RenderAsync(string? markdown, PageRenderOptions? options = null)
    {
        options ??= new PageRenderOptions();

        var document = Markdown.Parse(markdown ?? "", PageMarkdown.Pipeline);
        var embeds = document.Descendants<EmbedBlock>().ToList();

        if (embeds.Count > 0)
        {
            try
            {
                await ResolveAsync(document, embeds, options);
            }
            catch (Exception ex)
            {
                // Show the rest of the page rather than nothing
                logger.LogError(ex, "Could not resolve the embeds in a page");
            }
        }

        using var writer = new StringWriter();

        var renderer = new HtmlRenderer(writer);

        PageMarkdown.Pipeline.Setup(renderer);

        renderer.Render(document);
        writer.Flush();

        return writer.ToString();
    }

    async Task ResolveAsync(MarkdownDocument document, List<EmbedBlock> embeds, PageRenderOptions options)
    {
        Guid[] IdsOf(string kind) => embeds
            .Where(e => e.Kind == kind && Guid.TryParse(e.Argument, out _))
            .Select(e => Guid.Parse(e.Argument!))
            .Distinct()
            .ToArray();

        var gameIds = IdsOf("game");
        var serverIds = IdsOf("server");
        var redistributableIds = IdsOf("redistributable");
        var pageIds = IdsOf("page");

        var games = gameIds.Length == 0 ? [] : (await gameService
            .Include(g => g.Media, g => g.Genres)
            .GetAsync(g => gameIds.Contains(g.Id))).ToDictionary(g => g.Id);

        var servers = serverIds.Length == 0 ? [] : (await serverService
            .Include(s => s.Game)
            .GetAsync(s => serverIds.Contains(s.Id))).ToDictionary(s => s.Id);

        var redistributables = redistributableIds.Length == 0 ? [] : (await redistributableService
            .GetAsync(r => redistributableIds.Contains(r.Id))).ToDictionary(r => r.Id);

        var pages = pageIds.Length == 0 ? [] : (await pageService
            .GetAsync(p => pageIds.Contains(p.Id))).ToDictionary(p => p.Id);

        var children = new List<Data.Models.Page>();

        if (options.PageId is Guid pageId && embeds.Any(e => e.Kind == "children"))
            children = (await pageService.GetAsync(p => p.ParentId == pageId)).OrderBy(p => p.SortOrder).ToList();

        var statuses = new Dictionary<Guid, ServerProcessStatus>();

        foreach (var server in servers.Values)
            statuses[server.Id] = await GetStatusAsync(server.Id);

        foreach (var embed in embeds)
        {
            Guid.TryParse(embed.Argument, out var id);

            embed.Html = embed.Kind switch
            {
                "game" => games.TryGetValue(id, out var game) ? RenderGame(game, options) : null,
                "server" => servers.TryGetValue(id, out var server) ? RenderServer(server, statuses[server.Id], options) : null,
                "redistributable" => redistributables.TryGetValue(id, out var redistributable) ? RenderRedistributable(redistributable, options) : null,
                "page" => pages.TryGetValue(id, out var page) ? RenderPageLink(page) : null,
                "children" => RenderChildren(children),
                "toc" => RenderTableOfContents(document),
                _ => null,
            };

            // Visitors don't need to know something was meant to be here, but whoever wrote it does
            if (embed.Html == null && options.IsAdministrator && (embed.Kind != "children" || options.PageId != null))
                embed.Html = $"<div class=\"page-embed page-embed-missing\">Nothing to show for {HtmlEncode(PageMarkdown.Embed(embed.Kind, embed.Argument))}</div>";
        }
    }

    async Task<ServerProcessStatus> GetStatusAsync(Guid serverId)
    {
        try
        {
            // A remote engine that doesn't answer shouldn't hold up the page
            var status = serverManager.GetStatusAsync(serverId);

            if (await Task.WhenAny(status, Task.Delay(TimeSpan.FromSeconds(2))) == status)
                return await status;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not get the status of server {ServerId} for a page", serverId);
        }

        return ServerProcessStatus.Retrieving;
    }

    static string RenderGame(Data.Models.Game game, PageRenderOptions options)
    {
        var html = new StringBuilder("<div class=\"page-embed page-embed-game\">");

        var cover = game.Media?.FirstOrDefault(m => m.Type == MediaType.Cover);

        if (cover != null)
            html.Append($"<img class=\"page-embed-cover\" src=\"/api/Media/{cover.Id}/Thumbnail\" alt=\"\" loading=\"lazy\" />");

        html.Append("<div class=\"page-embed-body\">");
        html.Append($"<div class=\"page-embed-title\">{Title(game.Title, options.IsAdministrator ? $"/Games/{game.Id}" : null)}</div>");

        var details = new List<string>();

        if (game.ReleasedOn is DateTime releasedOn)
            details.Add(releasedOn.Year.ToString());

        if (game.Genres?.Count > 0)
            details.Add(String.Join(", ", game.Genres.Select(g => g.Name).Order()));

        if (details.Count > 0)
            html.Append($"<div class=\"page-embed-meta\">{HtmlEncode(String.Join(" · ", details))}</div>");

        AppendDescription(html, game.Description);

        html.Append("</div></div>");

        return html.ToString();
    }

    static string RenderServer(Data.Models.Server server, ServerProcessStatus status, PageRenderOptions options)
    {
        var html = new StringBuilder("<div class=\"page-embed page-embed-server\">");

        html.Append("<div class=\"page-embed-body\">");
        html.Append($"<div class=\"page-embed-title\">{Title(server.Name, options.IsAdministrator ? $"/Servers/{server.Id}" : null)}");
        html.Append($" <span class=\"page-embed-status page-embed-status-{status.ToString().ToLowerInvariant()}\">{HtmlEncode(StatusText(status))}</span></div>");

        var details = new List<string>();

        if (server.Game != null)
            details.Add(server.Game.Title);

        if (!String.IsNullOrWhiteSpace(server.Host))
            details.Add(server.Port > 0 ? $"{server.Host}:{server.Port}" : server.Host);

        if (details.Count > 0)
            html.Append($"<div class=\"page-embed-meta\">{HtmlEncode(String.Join(" · ", details))}</div>");

        html.Append("</div></div>");

        return html.ToString();
    }

    static string StatusText(ServerProcessStatus status) => status switch
    {
        ServerProcessStatus.Running => "Online",
        ServerProcessStatus.Starting => "Starting",
        ServerProcessStatus.Stopping => "Stopping",
        ServerProcessStatus.Stopped => "Offline",
        ServerProcessStatus.Error => "Offline",
        _ => "Unknown",
    };

    static string RenderRedistributable(Data.Models.Redistributable redistributable, PageRenderOptions options)
    {
        var html = new StringBuilder("<div class=\"page-embed page-embed-redistributable\"><div class=\"page-embed-body\">");

        html.Append($"<div class=\"page-embed-title\">{Title(redistributable.Name, options.IsAdministrator ? $"/Redistributables/{redistributable.Id}" : null)}</div>");

        AppendDescription(html, redistributable.Description);

        html.Append("</div></div>");

        return html.ToString();
    }

    static string RenderPageLink(Data.Models.Page page) =>
        $"<div class=\"page-embed page-embed-page\"><a href=\"/{HtmlEncode(page.Route)}\">{HtmlEncode(page.Title)}</a></div>";

    static string? RenderChildren(List<Data.Models.Page> children)
    {
        if (children.Count == 0)
            return null;

        var html = new StringBuilder("<ul class=\"page-embed-children\">");

        foreach (var child in children)
            html.Append($"<li><a href=\"/{HtmlEncode(child.Route)}\">{HtmlEncode(child.Title)}</a></li>");

        html.Append("</ul>");

        return html.ToString();
    }

    static string? RenderTableOfContents(MarkdownDocument document)
    {
        var headings = document.Descendants<HeadingBlock>()
            .Select(h => (h.Level, Id: h.GetAttributes().Id, Text: HeadingText(h)))
            .Where(h => !String.IsNullOrEmpty(h.Id) && !String.IsNullOrWhiteSpace(h.Text))
            .ToList();

        if (headings.Count == 0)
            return null;

        // Indent relative to the page's top heading level rather than assuming it starts at h1
        var topLevel = headings.Min(h => h.Level);

        var html = new StringBuilder("<nav class=\"page-embed-toc\"><ul>");

        foreach (var heading in headings)
            html.Append($"<li class=\"page-embed-toc-level-{heading.Level - topLevel + 1}\"><a href=\"#{HtmlEncode(heading.Id)}\">{HtmlEncode(heading.Text)}</a></li>");

        html.Append("</ul></nav>");

        return html.ToString();
    }

    static string HeadingText(HeadingBlock heading)
    {
        if (heading.Inline == null)
            return "";

        var text = new StringBuilder();

        foreach (var inline in heading.Inline.Descendants<Inline>())
        {
            if (inline is LiteralInline literal)
                text.Append(literal.Content.ToString());
            else if (inline is CodeInline code)
                text.Append(code.Content);
        }

        return text.ToString().Trim();
    }

    static string Title(string text, string? href) => href == null
        ? HtmlEncode(text)
        : $"<a href=\"{HtmlEncode(href)}\">{HtmlEncode(text)}</a>";

    static void AppendDescription(StringBuilder html, string? description)
    {
        if (String.IsNullOrWhiteSpace(description))
            return;

        description = description.Trim();

        if (description.Length > DescriptionLength)
            description = description[..DescriptionLength].TrimEnd() + "…";

        html.Append($"<p class=\"page-embed-description\">{HtmlEncode(description)}</p>");
    }
}
