using System.Text.RegularExpressions;
using Markdig;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace LANCommander.Server.Services.Pages;

/// <summary>
/// The Markdown dialect pages are written in. Raw HTML is disabled so visitors can't be served
/// stored script, and a line holding only <c>{{kind}}</c> or <c>{{kind:argument}}</c> becomes an
/// embed, such as <c>{{game:&lt;id&gt;}}</c> or <c>{{toc}}</c>, filled in by <see cref="PageRenderService"/>.
/// </summary>
public static class PageMarkdown
{
    public static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UseAdvancedExtensions()
        .UsePageEmbeds()
        .Build();

    public static MarkdownPipelineBuilder UsePageEmbeds(this MarkdownPipelineBuilder pipeline)
    {
        pipeline.Extensions.AddIfNotAlready<PageEmbedExtension>();

        return pipeline;
    }

    /// <summary>
    /// The text a reader sees, without Markdown syntax or embeds, for searching and snippets
    /// </summary>
    public static string ToPlainText(string? markdown)
    {
        if (String.IsNullOrWhiteSpace(markdown))
            return "";

        var text = Markdown.ToPlainText(markdown, Pipeline);

        return Whitespace.Replace(text, " ").Trim();
    }

    /// <summary>The embed syntax for a kind and optional argument, as the editor inserts it</summary>
    public static string Embed(string kind, object? argument = null) =>
        argument == null ? $"{{{{{kind}}}}}" : $"{{{{{kind}:{argument}}}}}";

    static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);
}

/// <summary>A line that embeds something, such as a game card, into a page</summary>
public class EmbedBlock(BlockParser parser) : LeafBlock(parser)
{
    /// <summary>What is embedded, lower case, such as "game" or "toc"</summary>
    public string Kind { get; set; } = "";

    /// <summary>What follows the colon, such as an id. Null when there is none.</summary>
    public string? Argument { get; set; }

    /// <summary>
    /// The HTML to render, set once the embed is resolved. Unresolved embeds render nothing,
    /// which is also what plain text extraction relies on.
    /// </summary>
    public string? Html { get; set; }
}

public class EmbedBlockParser : BlockParser
{
    static readonly Regex Syntax = new(@"^\{\{\s*([A-Za-z]+)\s*(?::\s*([^{}\s]+)\s*)?\}\}\s*$", RegexOptions.Compiled);

    public EmbedBlockParser()
    {
        OpeningCharacters = ['{'];
    }

    public override BlockState TryOpen(BlockProcessor processor)
    {
        if (processor.IsCodeIndent)
            return BlockState.None;

        var match = Syntax.Match(processor.Line.ToString());

        if (!match.Success)
            return BlockState.None;

        processor.NewBlocks.Push(new EmbedBlock(this)
        {
            Kind = match.Groups[1].Value.ToLowerInvariant(),
            Argument = match.Groups[2].Success ? match.Groups[2].Value : null,
            Column = processor.Column,
            Line = processor.LineIndex,
            Span = new SourceSpan(processor.Start, processor.Line.End),
        });

        return BlockState.BreakDiscard;
    }
}

public class EmbedBlockRenderer : HtmlObjectRenderer<EmbedBlock>
{
    protected override void Write(HtmlRenderer renderer, EmbedBlock block)
    {
        // Built by PageRenderService from encoded values, never from page text directly
        if (renderer.EnableHtmlForBlock && !String.IsNullOrEmpty(block.Html))
            renderer.WriteLine(block.Html);
    }
}

public class PageEmbedExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        // Ahead of the paragraph parser, which would otherwise claim the line as text
        if (!pipeline.BlockParsers.Contains<EmbedBlockParser>())
            pipeline.BlockParsers.Insert(0, new EmbedBlockParser());
    }

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        if (renderer is HtmlRenderer htmlRenderer)
            htmlRenderer.ObjectRenderers.AddIfNotAlready<EmbedBlockRenderer>();
    }
}
