using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace LANCommander.Launcher.Avalonia.Controls;

public class CarouselControl : TemplatedControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<CarouselControl, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<double> ItemWidthProperty =
        AvaloniaProperty.Register<CarouselControl, double>(nameof(ItemWidth), 200);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<CarouselControl, double>(nameof(Gap), 10);

    public static readonly StyledProperty<int> VisibleItemsProperty =
        AvaloniaProperty.Register<CarouselControl, int>(nameof(VisibleItems), 3);

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<CarouselControl, int>(nameof(SelectedIndex), 0);

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<CarouselControl, string?>(nameof(Title));

    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<CarouselControl, IDataTemplate?>(nameof(ItemTemplate));

    public static readonly StyledProperty<ICommand?> SeeAllCommandProperty =
        AvaloniaProperty.Register<CarouselControl, ICommand?>(nameof(SeeAllCommand));

    public static readonly StyledProperty<bool> IsInfiniteProperty =
        AvaloniaProperty.Register<CarouselControl, bool>(nameof(IsInfinite), false);

    public static readonly StyledProperty<bool> WrapItemsProperty =
        AvaloniaProperty.Register<CarouselControl, bool>(nameof(WrapItems), false);

    public static readonly StyledProperty<double> ItemOverflowProperty =
        AvaloniaProperty.Register<CarouselControl, double>(nameof(ItemOverflow), 0);

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public double ItemWidth
    {
        get => GetValue(ItemWidthProperty);
        set => SetValue(ItemWidthProperty, value);
    }

    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    public int VisibleItems
    {
        get => GetValue(VisibleItemsProperty);
        set => SetValue(VisibleItemsProperty, value);
    }

    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public IDataTemplate? ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public ICommand? SeeAllCommand
    {
        get => GetValue(SeeAllCommandProperty);
        set => SetValue(SeeAllCommandProperty, value);
    }

    public bool IsInfinite
    {
        get => GetValue(IsInfiniteProperty);
        set => SetValue(IsInfiniteProperty, value);
    }

    public bool WrapItems
    {
        get => GetValue(WrapItemsProperty);
        set => SetValue(WrapItemsProperty, value);
    }

    public double ItemOverflow
    {
        get => GetValue(ItemOverflowProperty);
        set => SetValue(ItemOverflowProperty, value);
    }

    private Control? _itemsContainer;
    private Control? _clipPanel;
    private ItemsControl? _itemsControl;
    private Button? _leftButton;
    private Button? _rightButton;
    private Transitions? _transitions;
    private RectangleGeometry? _clipGeometry;
    private int _virtualIndex = 0;

    static CarouselControl()
    {
        ItemsSourceProperty.Changed.AddClassHandler<CarouselControl>((c, _) => c.RebuildInternalSource());
        WrapItemsProperty.Changed.AddClassHandler<CarouselControl>((c, _) => { c.RebuildInternalSource(); c.UpdateClipGeometry(); });
        ItemOverflowProperty.Changed.AddClassHandler<CarouselControl>((c, _) => c.ApplyItemOverflow());
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _itemsContainer = e.NameScope.Find<Control>("PART_ItemsContainer");
        _clipPanel = e.NameScope.Find<Control>("PART_ClipPanel");
        _itemsControl = e.NameScope.Find<ItemsControl>("PART_ItemsControl");
        _leftButton = e.NameScope.Find<Button>("PART_LeftButton");
        _rightButton = e.NameScope.Find<Button>("PART_RightButton");

        if (_clipPanel != null)
        {
            _clipPanel.SizeChanged += (_, _) => { UpdateClipGeometry(); SnapToCurrentIndex(); };
            ApplyItemOverflow();
        }

        if (_itemsContainer != null)
        {
            _transitions = new Transitions
            {
                new ThicknessTransition
                {
                    Property = MarginProperty,
                    Duration = TimeSpan.FromMilliseconds(350),
                    Easing = new CubicEaseOut()
                }
            };
            _itemsContainer.Transitions = _transitions;
        }

        if (_leftButton != null)
            _leftButton.Click += (_, __) => Move(-1);

        if (_rightButton != null)
            _rightButton.Click += (_, __) => Move(1);

        RebuildInternalSource();
    }

    private void RebuildInternalSource()
    {
        if (_itemsControl == null) return;

        if (WrapItems && ItemsSource != null)
        {
            var items = ItemsSource.Cast<object>().ToList();
            var count = items.Count;

            if (count > 0)
            {
                _itemsControl.ItemsSource = items.Concat(items).Concat(items).ToList();
                _virtualIndex = count;
                ScrollInstant(Offset(-_virtualIndex * (ItemWidth + Gap)));
                return;
            }
        }

        _itemsControl.ItemsSource = ItemsSource;
        _virtualIndex = 0;
        ScrollInstant(Offset(-ClampedOffset(SelectedIndex, GetItemCount())));
    }

    private void Move(int delta)
    {
        var count = GetItemCount();
        if (count == 0) return;

        if (WrapItems)
        {
            // At right boundary: teleport to mirror in first copy, then animate forward into middle copy
            if (delta > 0 && _virtualIndex >= 2 * count - 1)
            {
                _virtualIndex -= count;
                ScrollInstant(Offset(-_virtualIndex * (ItemWidth + Gap)));
            }
            // At left boundary: teleport to mirror in third copy, then animate backward into middle copy
            else if (delta < 0 && _virtualIndex <= count)
            {
                _virtualIndex += count;
                ScrollInstant(Offset(-_virtualIndex * (ItemWidth + Gap)));
            }

            _virtualIndex = Math.Clamp(_virtualIndex + delta, 0, 3 * count - 1);
            SelectedIndex = _virtualIndex % count;
            ScrollAnimated(Offset(-_virtualIndex * (ItemWidth + Gap)));
        }
        else if (IsInfinite)
        {
            SelectedIndex = ((SelectedIndex + delta) % count + count) % count;
            ScrollAnimated(Offset(-SelectedIndex * (ItemWidth + Gap)));
        }
        else
        {
            var newIndex = Math.Clamp(SelectedIndex + delta, 0, count - 1);
            var newOffset = ClampedOffset(newIndex, count);
            // Don't advance past the point where the scroll position no longer changes
            if (newOffset != ClampedOffset(SelectedIndex, count) || newIndex < SelectedIndex)
                SelectedIndex = newIndex;
            ScrollAnimated(Offset(-ClampedOffset(SelectedIndex, count)));
        }
    }

    private void ScrollAnimated(double leftMargin)
    {
        if (_itemsContainer == null) return;
        _itemsContainer.Margin = new Thickness(leftMargin, 0, 0, 0);
    }

    private void ScrollInstant(double leftMargin)
    {
        if (_itemsContainer == null) return;
        _itemsContainer.Transitions = null;
        _itemsContainer.Margin = new Thickness(leftMargin, 0, 0, 0);
        _itemsContainer.Transitions = _transitions;
    }

    private void SnapToCurrentIndex()
    {
        if (WrapItems) return;
        var count = GetItemCount();
        if (count == 0) return;
        ScrollInstant(Offset(-ClampedOffset(SelectedIndex, count)));
    }

    private void ApplyItemOverflow()
    {
        if (_clipPanel == null) return;
        if (_itemsContainer is Border border)
            border.Padding = new Thickness(ItemOverflow);
        UpdateClipGeometry();
        SnapToCurrentIndex();
    }

    // Replaces ClipToBounds with a geometry clip that is tight on left/right
    // but extended by 2*ItemOverflow top and bottom, giving hovered items room
    // to scale without being clipped while scrolled-off items still are.
    private void UpdateClipGeometry()
    {
        if (_clipPanel == null) return;

        if (ItemOverflow > 0 || WrapItems)
        {
            _clipGeometry ??= new RectangleGeometry();
            var b = _clipPanel.Bounds;
            // Extend clip vertically by ItemOverflow to allow hover-scale effects, but
            // keep left/right tight so scrolled-off items don't bleed behind the nav buttons.
            var vPad = ItemOverflow * 2;
            _clipGeometry.Rect = new Rect(-ItemOverflow, -vPad, b.Width + 2 * ItemOverflow, b.Height + 2 * vPad);
            _clipPanel.Clip = _clipGeometry;
        }
        else
        {
            _clipPanel.Clip = null;
            _clipGeometry = null;
        }
    }

    // Compensates for the Border padding on PART_ItemsContainer: the padding
    // shifts items right by ItemOverflow, so the container must start that far
    // to the left so item[0]'s left edge aligns with the clip panel's left edge.
    private double Offset(double rawScrollOffset) => rawScrollOffset - ItemOverflow;

    private double ClampedOffset(int index, int count)
    {
        var rawOffset = index * (ItemWidth + Gap);
        var viewportWidth = _clipPanel?.Bounds.Width ?? (VisibleItems * ItemWidth + (VisibleItems - 1) * Gap);
        var totalContentWidth = count * ItemWidth + (count - 1) * Gap;
        var maxOffset = Math.Max(0, totalContentWidth - viewportWidth);
        return Math.Min(rawOffset, maxOffset);
    }

    private int GetItemCount()
    {
        if (ItemsSource is ICollection c) return c.Count;
        if (ItemsSource == null) return 0;
        var count = 0;
        foreach (var _ in ItemsSource) count++;
        return count;
    }
}
