namespace LANCommander.Server.Plugins;

/// <summary>
/// Adds a kind of embed (shortcode) to the server's pages, alongside the built-in ones such as
/// <c>{{game:&lt;id&gt;}}</c> and <c>{{toc}}</c>, shown as a Razor component of the plugin's. Register
/// an implementation in <c>IPlugin.ConfigureServices</c>; any number of plugins may, one kind per
/// implementation.
/// </summary>
/// <remarks>
/// <para>
/// A page author writes the embed on a line of its own, as <c>{{kind}}</c> or
/// <c>{{kind:argument}}</c>; it is not recognised inside a paragraph or a code block. The argument
/// can't contain whitespace or braces. The editor's Insert menu lists the extension by its
/// <see cref="Name"/>.
/// </para>
/// <para>
/// The <see cref="Component"/> is rendered in place of the embed, interactively, wherever the page is
/// shown: on the page itself, in the site's header and footer, and in the editor's preview. It is
/// given the embed as its <c>Embed</c> parameter, a <see cref="PageEmbed"/>, so declare
/// <c>[Parameter] public PageEmbed Embed { get; set; }</c>. It loads what it shows itself, e.g. in
/// <c>OnParametersSetAsync</c>, and decides what to show when the argument names nothing, e.g. nothing
/// for visitors and a note for <see cref="PageEmbed.IsAdministrator"/>.
/// </para>
/// <para>
/// Embeds of a plugin's kind are only shown at the top level of a page, not inside a list or a quote,
/// where administrators see a note instead. A component that throws shows nothing to visitors and
/// the error to administrators, without affecting the rest of the page.
/// </para>
/// <para>
/// Pages are public, so show only what any visitor may see unless
/// <see cref="PageEmbed.IsAdministrator"/> is set. The page's built-in embed styles can be reused
/// through the classes <c>page-embed</c>, <c>page-embed-body</c>, <c>page-embed-title</c>,
/// <c>page-embed-meta</c> and <c>page-embed-description</c>.
/// </para>
/// </remarks>
public interface IPageEmbedExtension
{
    /// <summary>
    /// What authors write between the braces, letters only and matched without regard to case, e.g.
    /// <c>leaderboard</c> for <c>{{leaderboard:&lt;id&gt;}}</c>. Must not be one of the built-in kinds
    /// (game, server, redistributable, page, children, toc) or another extension's.
    /// </summary>
    string Kind { get; }

    /// <summary>The name the editor's Insert menu lists the embed by, e.g. "Leaderboard".</summary>
    string Name { get; }

    /// <summary>
    /// The Razor component shown in place of each embed, taking the <see cref="PageEmbed"/> as its
    /// <c>Embed</c> parameter. An extension whose component doesn't is left out.
    /// </summary>
    Type Component { get; }

    /// <summary>
    /// Whether the embed takes an argument. When true, the editor asks the author to pick one of
    /// <see cref="GetChoicesAsync"/> before inserting it; when false, it inserts <c>{{kind}}</c>.
    /// </summary>
    bool TakesArgument => false;

    /// <summary>
    /// What the editor offers as the argument when <see cref="TakesArgument"/> is set, e.g. the
    /// plugin's leaderboards. Only asked by administrators editing a page. Choices whose value has
    /// whitespace or braces aren't offered.
    /// </summary>
    Task<IReadOnlyList<PageEmbedChoice>> GetChoicesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PageEmbedChoice>>([]);
}

/// <summary>An argument the editor offers for an <see cref="IPageEmbedExtension"/>.</summary>
/// <param name="Value">What is written after the colon: no whitespace or braces.</param>
/// <param name="Label">What the author picks it by.</param>
public sealed record PageEmbedChoice(string Value, string Label);

/// <summary>
/// One embed of an <see cref="IPageEmbedExtension"/>'s kind, given to its component as the
/// <c>Embed</c> parameter.
/// </summary>
public sealed record PageEmbed
{
    /// <summary>The embed's kind, lower case.</summary>
    public required string Kind { get; init; }

    /// <summary>What follows the colon, as written. Null when there is none.</summary>
    public string? Argument { get; init; }

    /// <summary>The page being shown. Null outside a page, such as in the site's header or footer.</summary>
    public Guid? PageId { get; init; }

    /// <summary>Whether the visitor is an administrator, e.g. to add a link to edit what is shown.</summary>
    public bool IsAdministrator { get; init; }
}
