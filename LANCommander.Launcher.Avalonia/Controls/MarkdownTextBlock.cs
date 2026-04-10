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

namespace LANCommander.Launcher.Avalonia.Controls;

/// <summary>
/// A panel that renders a markdown string as native Avalonia controls.
/// Handles paragraphs, headings, code blocks, bold, italic, inline code, and line breaks.
/// </summary>
public class MarkdownTextBlock : StackPanel
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<MarkdownTextBlock, string?>(nameof(Text));

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<MarkdownTextBlock, double>(nameof(FontSize), 13.0);

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
        var tb = new SelectableTextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = heading.Level switch
            {
                1 => FontSize * 1.6,
                2 => FontSize * 1.4,
                3 => FontSize * 1.2,
                _ => FontSize * 1.1,
            },
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 4, 0, 2),
        };

        if (heading.Inline != null)
            foreach (var inline in RenderInlines(heading.Inline))
                tb.Inlines!.Add(inline);

        return tb;
    }

    private Control RenderParagraph(ParagraphBlock para)
    {
        var tb = new SelectableTextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = FontSize,
            Margin = new Thickness(0, 1, 0, 1),
        };

        if (para.Inline != null)
            foreach (var inline in RenderInlines(para.Inline))
                tb.Inlines!.Add(inline);

        return tb;
    }

    private Control RenderCodeBlock(string code)
    {
        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1AFFFFFF")),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 6),
            Margin = new Thickness(0, 3, 0, 3),
            Child = new SelectableTextBlock
            {
                Text = code.TrimEnd('\n', '\r'),
                FontFamily = new FontFamily("Cascadia Code,Consolas,Menlo,Courier New,monospace"),
                FontSize = FontSize * 0.9,
                TextWrapping = TextWrapping.Wrap,
            },
        };
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
        return new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.Parse("#33FFFFFF")),
            Margin = new Thickness(0, 6, 0, 6),
        };
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

        return new Border
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#4488CC")),
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(8, 4),
            Margin = new Thickness(0, 3, 0, 3),
            Child = inner,
        };
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
                yield return new Run(code.Content)
                {
                    FontFamily = new FontFamily("Cascadia Code,Consolas,Menlo,Courier New,monospace"),
                    Background = new SolidColorBrush(Color.Parse("#22FFFFFF")),
                    FontSize = FontSize * 0.9,
                };
                break;

            case EmphasisInline emphasis:
            {
                var span = new Span();
                var isBold   = emphasis.DelimiterCount >= 2;
                var isItalic = emphasis.DelimiterCount == 1 || emphasis.DelimiterCount == 3;

                if (isBold)   span.FontWeight = FontWeight.Bold;
                if (isItalic) span.FontStyle  = FontStyle.Italic;

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
                    Foreground = new SolidColorBrush(Color.Parse("#4488CC")),
                };
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
}
