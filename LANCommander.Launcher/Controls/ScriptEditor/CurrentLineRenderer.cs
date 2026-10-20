using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace LANCommander.Launcher.Controls.ScriptEditor;

/// <summary>Highlights the line the debugger is currently stopped on.</summary>
public sealed class CurrentLineRenderer : IBackgroundRenderer
{
    private static readonly IBrush Fill =
        new ImmutableSolidColorBrush(Color.FromArgb(0x50, 0xFF, 0xD7, 0x00));

    private static readonly IPen Border =
        new ImmutablePen(new ImmutableSolidColorBrush(Color.FromArgb(0xC0, 0xFF, 0xD7, 0x00)), 1.0);

    private int _line;

    /// <summary>The stopped line, or 0 when the debugger is not stopped in this document.</summary>
    public int Line
    {
        get => _line;
        set => _line = value;
    }

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        var line = _line;
        if (line < 1 || textView.Document is null || line > textView.Document.LineCount)
            return;

        textView.EnsureVisualLines();
        if (!textView.VisualLinesValid)
            return;

        var documentLine = textView.Document.GetLineByNumber(line);
        var segment = new SimpleSegment(documentLine.Offset, Math.Max(documentLine.Length, 1));

        foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
        {
            // Widen to the full viewport so the highlight reads as a row, not as a text selection.
            var full = new Rect(0, rect.Y, Math.Max(textView.Bounds.Width, rect.Right), rect.Height);
            drawingContext.FillRectangle(Fill, full);
            drawingContext.DrawRectangle(null, Border, full);
        }
    }
}
