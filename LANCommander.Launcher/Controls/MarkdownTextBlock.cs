using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using AvaloniaInline = Avalonia.Controls.Documents.Inline;
using MarkdigInline = Markdig.Syntax.Inlines.Inline;

namespace LANCommander.Launcher.Controls;

/// <summary>
/// A panel that renders a markdown string as native Avalonia controls.
/// Handles paragraphs, headings, code blocks, bold, italic, inline code, and line breaks.
/// </summary>
public class MarkdownTextBlock : StackPanel
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MarkdownTextBlock, string?>(nameof(Text));

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<MarkdownTextBlock, double>(nameof(FontSize), 14.0);

    private static readonly MarkdownPipeline Pipeline =
        new MarkdownPipelineBuilder()
            .UseEmphasisExtras()
            .UseAutoLinks()
            .Build();

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        
        if (change.Property == TextProperty || change.Property == FontSizeProperty)
            Rebuild();
    }

    private void Rebuild()
    {
        Children.Clear();

        var markdown = Text;
        
        if (string.IsNullOrEmpty(markdown))
            return;

        var doc = Markdown.Parse(markdown, Pipeline);

        foreach (var block in doc)
        {
            var element = RenderBlock(block);
            if (element != null)
                Children.Add(element);
        }
    }

    private Control? RenderBlock(Block block)
    {
        return block switch
        {
            HeadingBlock heading     => RenderHeading(heading),
            ParagraphBlock para      => RenderParagraph(para),
            FencedCodeBlock fenced   => RenderCodeBlock(fenced.Lines.ToString()),
            CodeBlock code           => RenderCodeBlock(code.Lines.ToString()),
            ListBlock list           => RenderList(list),
            ThematicBreakBlock       => RenderThematicBreak(),
            QuoteBlock quote         => RenderQuote(quote),
            _                        => null,
        };
    }

    private Control RenderHeading(HeadingBlock heading)
    {
        var tb = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = heading.Level switch
            {
                1 => FontSize * Scale("MarkdownHeading1Scale", 1.6),
                2 => FontSize * Scale("MarkdownHeading2Scale", 1.4),
                3 => FontSize * Scale("MarkdownHeading3Scale", 1.2),
                _ => FontSize * Scale("MarkdownHeadingScale", 1.1),
            },
            FontWeight = FontWeight.Bold,
            Margin = Space("MarkdownHeadingMargin", new Thickness(0, 4, 0, 2)),
        };

        if (heading.Inline != null)
            foreach (var inline in RenderInlines(heading.Inline))
                tb.Inlines!.Add(inline);

        return tb;
    }

    private Control RenderParagraph(ParagraphBlock para)
    {
        var tb = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = FontSize,
            LineHeight = FontSize * Scale("MarkdownLineHeightScale", 1.4),
            Margin = Space("MarkdownParagraphMargin", new Thickness(0, 1, 0, 1)),
        };

        if (para.Inline != null)
            foreach (var inline in RenderInlines(para.Inline))
                tb.Inlines!.Add(inline);

        return tb;
    }

    private Control RenderCodeBlock(string code)
    {
        var text = new TextBlock
        {
            Text = code.TrimEnd('\n', '\r'),
            FontSize = FontSize * Scale("MarkdownCodeFontScale", 0.9),
            TextWrapping = TextWrapping.Wrap,
        };
        
        text.Bind(TextBlock.FontFamilyProperty, this.GetResourceObservable("CodeFontFamily"));

        var border = new Border
        {
            CornerRadius = Corner("MarkdownCodeCornerRadius", new CornerRadius(4)),
            Padding = Space("MarkdownCodeBlockPadding", new Thickness(10, 6)),
            Margin = Space("MarkdownCodeBlockMargin", new Thickness(0, 3, 0, 3)),
            Child = text,
        };
        
        border.Bind(Border.BackgroundProperty, this.GetResourceObservable("CodeSurfaceBrush"));

        return border;
    }

    private Control RenderList(ListBlock list)
    {
        var panel = new StackPanel { Spacing = 2, Margin = new Thickness(0, 2, 0, 2) };
        var index = 1;

        foreach (var item in list)
        {
            if (item is not ListItemBlock listItem)
                continue;

            var bullet = list.IsOrdered ? $"{index++}." : "•";

            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            };

            row.Children.Add(new TextBlock
            {
                Text = bullet,
                FontSize = FontSize,
                Margin = new Thickness(8, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Top,
            });

            var content = new StackPanel { Spacing = 2 };
            Grid.SetColumn(content, 1);

            foreach (var block in listItem)
            {
                var element = RenderBlock(block);
                
                if (element != null)
                    content.Children.Add(element);
            }

            row.Children.Add(content);
            panel.Children.Add(row);
        }

        return panel;
    }

    private Control RenderThematicBreak()
    {
        var border = new Border
        {
            Height = 1,
            Margin = Space("MarkdownRuleMargin", new Thickness(0, 6, 0, 6)),
        };
        
        border.Bind(Border.BackgroundProperty, this.GetResourceObservable("InputBorderBrush"));
        
        return border;
    }

    private Control RenderQuote(QuoteBlock quote)
    {
        var inner = new StackPanel { Spacing = 2 };
        foreach (var block in quote)
        {
            var element = RenderBlock(block);
            if (element != null)
                inner.Children.Add(element);
        }

        var border = new Border
        {
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = Space("MarkdownQuotePadding", new Thickness(8, 4)),
            Margin = Space("MarkdownQuoteMargin", new Thickness(0, 3, 0, 3)),
            Child = inner,
        };
        
        border.Bind(Border.BorderBrushProperty, this.GetResourceObservable("PrimaryBrush"));
        
        return border;
    }

    private IEnumerable<AvaloniaInline> RenderInlines(ContainerInline container)
    {
        foreach (var inline in container)
        {
            foreach (var result in RenderInline(inline))
                yield return result;
        }
    }

    private IEnumerable<AvaloniaInline> RenderInline(MarkdigInline inline)
    {
        switch (inline)
        {
            case LiteralInline literal:
                yield return new Run(literal.Content.ToString());
                break;

            case CodeInline code:
            {
                var run = new Run(code.Content)
                {
                    FontSize = FontSize * Scale("MarkdownCodeFontScale", 0.9),
                };
                
                run.Bind(Run.FontFamilyProperty, this.GetResourceObservable("CodeFontFamily"));
                run.Bind(Run.BackgroundProperty, this.GetResourceObservable("CodeSurfaceBrush"));
                
                yield return run;
                break;
            }

            case EmphasisInline emphasis:
            {
                var span = new Span();
                var isBold   = emphasis.DelimiterCount >= 2;
                var isItalic = emphasis.DelimiterCount == 1 || emphasis.DelimiterCount == 3;

                if (isBold)
                    span.FontWeight = FontWeight.Bold;
                
                if (isItalic)
                    span.FontStyle  = FontStyle.Italic;

                foreach (var child in RenderInlines(emphasis))
                    span.Inlines.Add(child);
                
                yield return span;
                break;
            }

            case LinkInline link:
            {
                var linkSpan = new Span
                {
                    TextDecorations = TextDecorations.Underline,
                };
                
                linkSpan.Bind(Span.ForegroundProperty, this.GetResourceObservable("PrimaryBrush"));
                
                foreach (var child in RenderInlines(link))
                    linkSpan.Inlines.Add(child);
                
                yield return linkSpan;
                break;
            }

            case LineBreakInline:
                yield return new LineBreak();
                break;

            case ContainerInline container:
                foreach (var child in RenderInlines(container))
                    yield return child;
                break;
        }
    }

    private double Scale(string key, double fallback)
        => this.TryFindResource(key, out var v) && v is double d ? d : fallback;

    private Thickness Space(string key, Thickness fallback)
        => this.TryFindResource(key, out var v) && v is Thickness t ? t : fallback;

    private CornerRadius Corner(string key, CornerRadius fallback)
        => this.TryFindResource(key, out var v) && v is CornerRadius c ? c : fallback;
}
