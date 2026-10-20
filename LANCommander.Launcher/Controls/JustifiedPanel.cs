using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;

namespace LANCommander.Launcher.Controls;

/// <summary>
/// Lays children out in rows that fill the available width exactly, like a photo gallery. Each child
/// keeps its aspect ratio (<see cref="AspectRatioProperty"/>); a row's height is whatever makes its
/// children span the width, and rows break where that height lands closest to
/// <see cref="TargetRowHeight"/>. The last row keeps the target height rather than stretching.
/// </summary>
public class JustifiedPanel : Panel
{
    /// <summary>Width divided by height for a child. Set it on the item container.</summary>
    public static readonly AttachedProperty<double> AspectRatioProperty =
        AvaloniaProperty.RegisterAttached<JustifiedPanel, Control, double>("AspectRatio", 16.0 / 9.0);

    public static double GetAspectRatio(Control control) => control.GetValue(AspectRatioProperty);
    public static void SetAspectRatio(Control control, double value) => control.SetValue(AspectRatioProperty, value);

    public static readonly StyledProperty<double> TargetRowHeightProperty =
        AvaloniaProperty.Register<JustifiedPanel, double>(nameof(TargetRowHeight), 220);

    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<JustifiedPanel, double>(nameof(Spacing), 12);

    public double TargetRowHeight
    {
        get => GetValue(TargetRowHeightProperty);
        set => SetValue(TargetRowHeightProperty, value);
    }

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    static JustifiedPanel()
    {
        AffectsMeasure<JustifiedPanel>(TargetRowHeightProperty, SpacingProperty);
        AffectsParentMeasure<JustifiedPanel>(AspectRatioProperty);
    }

    private readonly List<Rect> _slots = new();

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? TargetRowHeight * 4 : availableSize.Width;
        var height = Layout(width);

        for (var i = 0; i < Children.Count; i++)
            Children[i].Measure(_slots[i].Size);

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        Layout(finalSize.Width);

        for (var i = 0; i < Children.Count; i++)
            Children[i].Arrange(_slots[i]);

        return finalSize;
    }

    /// <summary>Fills <see cref="_slots"/> for the given width and returns the total height.</summary>
    private double Layout(double width)
    {
        _slots.Clear();

        var target = TargetRowHeight;
        var spacing = Spacing;
        var y = 0.0;
        var start = 0;

        while (start < Children.Count)
        {
            // Grow the row until it overflows at the target height, then keep whichever break
            // (with or without the overflowing child) gives a row height nearer the target.
            var aspectSum = 0.0;
            var end = start;

            while (end < Children.Count)
            {
                var next = aspectSum + Aspect(Children[end]);
                var count = end - start + 1;

                if (next * target + spacing * (count - 1) > width && count > 1)
                {
                    var withNext = (width - spacing * (count - 1)) / next;
                    var without = (width - spacing * (count - 2)) / aspectSum;

                    if (Math.Abs(withNext - target) < Math.Abs(without - target))
                    {
                        aspectSum = next;
                        end++;
                    }

                    break;
                }

                aspectSum = next;
                end++;
            }

            var items = end - start;
            var isLastRow = end >= Children.Count;
            var rowHeight = (width - spacing * (items - 1)) / aspectSum;

            // A short last row would blow up to fill the width; hold it at the target instead.
            if (isLastRow)
                rowHeight = Math.Min(rowHeight, target);

            var x = 0.0;

            for (var i = start; i < end; i++)
            {
                var itemWidth = rowHeight * Aspect(Children[i]);
                _slots.Add(new Rect(x, y, Math.Max(0, itemWidth), Math.Max(0, rowHeight)));
                x += itemWidth + spacing;
            }

            y += rowHeight + spacing;
            start = end;
        }

        return Math.Max(0, y - spacing);
    }

    private static double Aspect(Control child)
    {
        var aspect = GetAspectRatio(child);
        return double.IsFinite(aspect) && aspect > 0 ? aspect : 16.0 / 9.0;
    }
}
