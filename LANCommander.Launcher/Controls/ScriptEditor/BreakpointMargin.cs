using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using LANCommander.Launcher.ViewModels.ScriptDebugger;

namespace LANCommander.Launcher.Controls.ScriptEditor;

/// <summary>
/// The clickable gutter that sets and clears breakpoints.
/// </summary>
/// <remarks>
/// Ported from the WPF AvalonEdit idiom, where several things differ: the render override is
/// <c>Render</c> rather than <c>OnRender</c>, pointer input arrives through
/// <c>OnPointerPressed</c> with the button in <c>PointerPointProperties</c> rather than through a
/// button-specific event, and a control that draws without filling its bounds can fail to receive
/// pointer events at all.
/// </remarks>
public sealed class BreakpointMargin : AbstractMargin
{
    private const double MinimumWidth = 18.0;

    private static readonly IBrush Background = new ImmutableSolidColorBrush(Color.FromRgb(0x25, 0x25, 0x26));
    private static readonly IBrush EnabledFill = new ImmutableSolidColorBrush(Color.FromRgb(0xE5, 0x14, 0x00));
    private static readonly IBrush BoundFill = new ImmutableSolidColorBrush(Color.FromRgb(0xE5, 0x14, 0x00));
    private static readonly IPen OutlinePen =
        new ImmutablePen(new ImmutableSolidColorBrush(Color.FromRgb(0xE5, 0x14, 0x00)), 1.5);

    private readonly BreakpointStore _store;

    public BreakpointMargin(BreakpointStore store)
    {
        _store = store;
        _store.Changed += OnStoreChanged;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var lineHeight = TextView?.DefaultLineHeight ?? MinimumWidth;

        // Height 0 is correct: the margin stretches to the text view. Returning infinity here is
        // a layout error rather than "as much as you like".
        return new Size(Math.Max(MinimumWidth, lineHeight) + 4, 0);
    }

    protected override void OnTextViewChanged(TextView oldTextView, TextView newTextView)
    {
        if (oldTextView is not null)
        {
            oldTextView.VisualLinesChanged -= OnVisualLinesChanged;
            oldTextView.ScrollOffsetChanged -= OnScrollOffsetChanged;
        }

        if (newTextView is not null)
        {
            newTextView.VisualLinesChanged += OnVisualLinesChanged;
            newTextView.ScrollOffsetChanged += OnScrollOffsetChanged;
        }

        base.OnTextViewChanged(oldTextView, newTextView);
        InvalidateVisual();
    }

    private void OnStoreChanged(object? sender, EventArgs e) => InvalidateVisual();

    private void OnVisualLinesChanged(object? sender, EventArgs e) => InvalidateVisual();

    private void OnScrollOffsetChanged(object? sender, EventArgs e) => InvalidateVisual();

    public override void Render(DrawingContext context)
    {
        // Filling the whole margin is not only cosmetic: an Avalonia control with no background
        // can fall through hit-testing, and then the click handler below never runs.
        context.FillRectangle(Background, new Rect(Bounds.Size));

        var textView = TextView;
        if (textView is null || !textView.VisualLinesValid)
            return;

        foreach (var visualLine in textView.VisualLines)
        {
            var lineNumber = visualLine.FirstDocumentLine.LineNumber;
            var breakpoint = _store.Find(lineNumber);
            if (breakpoint is null)
                continue;

            var top = visualLine.VisualTop - textView.VerticalOffset;
            var centre = new Point(Bounds.Width / 2, top + visualLine.Height / 2);
            var radius = Math.Max(3.0, Math.Min(Bounds.Width, visualLine.Height) / 2 - 3);

            // Solid once the engine has confirmed it; hollow while disabled or still pending, the
            // same vocabulary VS and VS Code use.
            if (breakpoint.Enabled && breakpoint.IsBound)
                context.DrawEllipse(BoundFill, null, centre, radius, radius);
            else if (breakpoint.Enabled)
                context.DrawEllipse(EnabledFill, null, centre, radius, radius);
            else
                context.DrawEllipse(null, OutlinePen, centre, radius, radius);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var textView = TextView;
        if (textView is null || Document is null)
            return;

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        // VerticalOffset is essential. The margin's Y is in its own coordinates while the text
        // view works in document-visual coordinates, so without it breakpoints land on the wrong
        // line as soon as the document is scrolled -- and look perfectly correct at the top.
        var y = e.GetPosition(this).Y + textView.VerticalOffset;
        var visualLine = textView.GetVisualLineFromVisualTop(y);

        var lineNumber = visualLine?.FirstDocumentLine.LineNumber ?? Document.LineCount;

        _store.Toggle(lineNumber);
        InvalidateVisual();
        e.Handled = true;
    }

    /// <summary>Detach from the store. Leaked handlers keep replaced margins alive.</summary>
    public void Detach()
    {
        _store.Changed -= OnStoreChanged;

        if (TextView is not null)
        {
            TextView.VisualLinesChanged -= OnVisualLinesChanged;
            TextView.ScrollOffsetChanged -= OnScrollOffsetChanged;
        }
    }
}
