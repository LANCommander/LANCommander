using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;

namespace LANCommander.Launcher.Controls;

/// <summary>
/// Implemented by carousel item content (e.g. inline video players) that should
/// only run while on-screen. The carousel toggles activity as items scroll in
/// and out of view to avoid wasting CPU on off-screen playback.
/// </summary>
public interface ICarouselPlaybackItem
{
    void SetCarouselActive(bool active);
}

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
    private double _currentOffsetX = 0;
    private WindowBase? _hostWindow;
    private bool _windowActive = true;

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

        if (_itemsControl != null)
        {
            // Items load progressively (e.g. media popping in), so recompute which
            // items are on-screen whenever a container is realized.
            _itemsControl.ContainerPrepared += (_, _) => UpdateItemVisibility();
        }

        if (_clipPanel != null)
        {
            _clipPanel.SizeChanged += (_, _) => { UpdateClipGeometry(); SnapToCurrentIndex(); };
            ApplyItemOverflow();
        }

        if (_itemsContainer != null)
        {
            // Scroll via a composited RenderTransform rather than Margin so the
            // animation doesn't trigger a layout pass over every item each frame.
            _transitions = new Transitions
            {
                new TransformOperationsTransition
                {
                    Property = RenderTransformProperty,
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

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Pause playback items while the window is in the background — there's no
        // point decoding video the user can't see.
        _hostWindow = TopLevel.GetTopLevel(this) as WindowBase;
        if (_hostWindow != null)
        {
            _windowActive = _hostWindow.IsActive;
            _hostWindow.Activated += OnHostWindowActivated;
            _hostWindow.Deactivated += OnHostWindowDeactivated;
        }

        UpdateItemVisibility();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_hostWindow != null)
        {
            _hostWindow.Activated -= OnHostWindowActivated;
            _hostWindow.Deactivated -= OnHostWindowDeactivated;
            _hostWindow = null;
        }

        base.OnDetachedFromVisualTree(e);
    }

    private void OnHostWindowActivated(object? sender, EventArgs e)
    {
        _windowActive = true;
        UpdateItemVisibility();
    }

    private void OnHostWindowDeactivated(object? sender, EventArgs e)
    {
        _windowActive = false;
        UpdateItemVisibility();
    }

    /// <summary>
    /// When a child item gains focus (e.g. via gamepad), scroll to keep it visible.
    /// </summary>
    protected override void OnGotFocus(GotFocusEventArgs e)
    {
        base.OnGotFocus(e);

        // Only scroll when focus was gained via keyboard/gamepad navigation.
        // Pointer-initiated focus should not scroll, as it moves the item out
        // from under the cursor and prevents the click from registering.
        if (e.NavigationMethod == NavigationMethod.Pointer)
            return;

        if (_itemsControl == null || e.Source is not Visual focusedVisual)
            return;

        var index = GetItemIndexForVisual(focusedVisual);
        if (index >= 0)
            ScrollToItemIndex(index);
    }

    /// <summary>
    /// Handle Left/Right arrow keys to navigate between carousel items.
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled) return;

        if (e.Key is not (Key.Left or Key.Right))
            return;

        if (_itemsControl == null)
            return;

        var focusedElement = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        if (focusedElement == null)
            return;

        var currentIndex = GetItemIndexForVisual(focusedElement);
        if (currentIndex < 0)
            return;

        var itemCount = _itemsControl.ItemCount;
        if (itemCount == 0)
            return;

        var newIndex = e.Key == Key.Right ? currentIndex + 1 : currentIndex - 1;

        if (WrapItems)
        {
            // At logical index 0 going left, let the event bubble
            // so cross-pane navigation (e.g. to sidebar) can handle it
            var sourceCount = GetItemCount();
            if (e.Key == Key.Left && sourceCount > 0 && currentIndex % sourceCount == 0)
                return;

            newIndex = ((newIndex % itemCount) + itemCount) % itemCount;
        }
        else
        {
            if (newIndex < 0 || newIndex >= itemCount)
                return; // Let the event bubble to navigate to adjacent controls
        }

        var container = _itemsControl.ContainerFromIndex(newIndex);
        if (container == null)
            return;

        var focusTarget = container.GetVisualDescendants()
            .OfType<InputElement>()
            .FirstOrDefault(el => el.Focusable) ?? container as InputElement;

        if (focusTarget != null)
        {
            focusTarget.Focus(NavigationMethod.Directional);
            ScrollToItemIndex(newIndex);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Determines the item index for a visual that is (or is within) an item container.
    /// </summary>
    private int GetItemIndexForVisual(Visual visual)
    {
        if (_itemsControl == null) return -1;

        var panel = _itemsControl.ItemsPanelRoot;
        if (panel == null) return -1;

        // Walk up the visual tree until we find a direct child of the items panel
        var current = visual;
        while (current != null)
        {
            if (current is Control control && current.GetVisualParent() == panel)
            {
                return _itemsControl.IndexFromContainer(control);
            }
            current = current.GetVisualParent() as Visual;
        }

        return -1;
    }

    /// <summary>
    /// Scrolls the carousel so the item at the given index is visible.
    /// </summary>
    private void ScrollToItemIndex(int displayIndex)
    {
        var sourceCount = GetItemCount();
        if (sourceCount == 0) return;

        if (WrapItems)
        {
            // In wrap mode, items are tripled. Map display index to virtual index.
            _virtualIndex = displayIndex;
            SelectedIndex = displayIndex % sourceCount;
            ScrollAnimated(Offset(-displayIndex * (ItemWidth + Gap)));
        }
        else
        {
            SelectedIndex = Math.Clamp(displayIndex, 0, sourceCount - 1);
            ScrollAnimated(Offset(-ClampedOffset(SelectedIndex, sourceCount)));
        }
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

    private void ScrollAnimated(double offsetX)
    {
        if (_itemsContainer == null) return;
        _currentOffsetX = offsetX;
        _itemsContainer.RenderTransform = TranslateX(offsetX);
        UpdateItemVisibility();
    }

    private void ScrollInstant(double offsetX)
    {
        if (_itemsContainer == null) return;
        _currentOffsetX = offsetX;
        _itemsContainer.Transitions = null;
        _itemsContainer.RenderTransform = TranslateX(offsetX);
        _itemsContainer.Transitions = _transitions;
        UpdateItemVisibility();
    }

    private static ITransform TranslateX(double x) =>
        TransformOperations.Parse(
            $"translateX({x.ToString("F3", CultureInfo.InvariantCulture)}px)");

    /// <summary>
    /// Activates the carousel items whose horizontal span currently intersects the
    /// viewport (and only while the host window is active) and deactivates the rest,
    /// so off-screen or background playback items (e.g. inline videos) stop consuming
    /// CPU. Deferred when containers aren't realized yet.
    /// </summary>
    private void UpdateItemVisibility()
    {
        if (_itemsControl == null || _clipPanel == null) return;

        var count = _itemsControl.ItemCount;
        if (count == 0) return;

        var viewportWidth = _clipPanel.Bounds.Width;
        if (viewportWidth <= 0)
        {
            // Layout hasn't run yet; retry once it has.
            Dispatcher.UIThread.Post(UpdateItemVisibility, DispatcherPriority.Loaded);
            return;
        }

        // _currentOffsetX is the (negative) translate applied to the container,
        // which itself starts at -ItemOverflow (see Offset). The first visible
        // pixel in item-space is therefore the inverse of that, minus the padding.
        var scroll = -_currentOffsetX - ItemOverflow;
        var step = ItemWidth + Gap;

        for (var i = 0; i < count; i++)
        {
            var itemLeft = i * step;
            var itemRight = itemLeft + ItemWidth;
            var visible = _windowActive && itemRight > scroll && itemLeft < scroll + viewportWidth;

            var container = _itemsControl.ContainerFromIndex(i);
            if (container == null) continue;

            foreach (var item in container.GetVisualDescendants().OfType<ICarouselPlaybackItem>())
                item.SetCarouselActive(visible);
        }
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
