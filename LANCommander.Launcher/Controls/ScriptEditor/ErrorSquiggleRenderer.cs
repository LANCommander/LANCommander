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

/// <summary>Draws a red zig-zag under every parse error.</summary>
public sealed class ErrorSquiggleRenderer : IBackgroundRenderer
{
    private const double Step = 3.0;
    private const double Amplitude = 2.5;

    private static readonly IPen Pen =
        new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0xF4, 0x47, 0x47)), 1.0);

    private SyntaxSnapshot _snapshot = SyntaxSnapshot.Empty;

    public void Update(SyntaxSnapshot snapshot) => Volatile.Write(ref _snapshot, snapshot);

    /// <summary>
    /// Above the selection highlight but under the glyphs, which is how Visual Studio renders it.
    /// </summary>
    public KnownLayer Layer => KnownLayer.Selection;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        var snapshot = Volatile.Read(ref _snapshot);
        if (snapshot.Errors.Length == 0 || textView.Document is null)
            return;

        textView.EnsureVisualLines();
        if (!textView.VisualLinesValid)
            return;

        var documentLength = textView.Document.TextLength;

        foreach (var error in snapshot.Errors)
        {
            var start = Math.Clamp(error.Extent.StartOffset, 0, documentLength);
            var end = Math.Clamp(error.Extent.EndOffset, 0, documentLength);

            // Zero-width errors are common ("unexpected end of input"); widen so there is
            // something to draw under.
            if (end <= start)
                end = Math.Min(start + 1, documentLength);

            if (end <= start)
                continue;

            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(
                         textView, new SimpleSegment(start, end - start)))
            {
                DrawSquiggle(drawingContext, rect);
            }
        }
    }

    private static void DrawSquiggle(DrawingContext context, Rect rect)
    {
        if (rect.Width < Step)
            return;

        var geometry = new StreamGeometry();
        using (var stream = geometry.Open())
        {
            var baseline = rect.Bottom - 1;
            stream.BeginFigure(new Point(rect.Left, baseline), isFilled: false);

            var up = true;
            for (var x = rect.Left + Step; x < rect.Right; x += Step, up = !up)
                stream.LineTo(new Point(x, up ? baseline - Amplitude : baseline));

            stream.EndFigure(false);
        }

        context.DrawGeometry(null, Pen, geometry);
    }
}
