using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace LANCommander.Launcher.Avalonia.Controls;

/// <summary>
/// Attached behavior that adds arrow-key (and gamepad D-pad) navigation
/// to an <see cref="ItemsRepeater"/>. Moves focus between realized children
/// based on their on-screen positions.
/// Usage: <c>&lt;ItemsRepeater controls:DirectionalNavigation.IsEnabled="True" /&gt;</c>
/// </summary>
public class DirectionalNavigation : AvaloniaObject
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<DirectionalNavigation, ItemsRepeater, bool>("IsEnabled");

    static DirectionalNavigation()
    {
        IsEnabledProperty.Changed.AddClassHandler<ItemsRepeater>(OnIsEnabledChanged);
    }

    public static bool GetIsEnabled(ItemsRepeater repeater) => repeater.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(ItemsRepeater repeater, bool value) => repeater.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(ItemsRepeater repeater, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            repeater.KeyDown += OnKeyDown;
        }
        else
        {
            repeater.KeyDown -= OnKeyDown;
        }
    }

    private static void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not ItemsRepeater repeater)
            return;

        if (e.Key is not (Key.Up or Key.Down or Key.Left or Key.Right))
            return;

        var focused = FindFocusedChild(repeater);
        if (focused == null)
        {
            // Nothing focused in this repeater — focus the first child
            FocusFirstChild(repeater);
            e.Handled = true;
            return;
        }

        var target = FindNeighbor(repeater, focused, e.Key);
        if (target != null && target != focused)
        {
            FocusElement(target);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Finds the realized child element in the repeater that contains the
    /// currently focused element.
    /// </summary>
    private static Control? FindFocusedChild(ItemsRepeater repeater)
    {
        var focusManager = TopLevel.GetTopLevel(repeater)?.FocusManager;
        var focused = focusManager?.GetFocusedElement() as Visual;
        if (focused == null) return null;

        foreach (var child in repeater.Children)
        {
            if (child == focused || focused.FindAncestorOfType<Control>() != null && IsDescendantOf(focused, child))
                return child;
        }

        return null;
    }

    private static bool IsDescendantOf(Visual element, Visual ancestor)
    {
        var current = element;
        while (current != null)
        {
            if (current == ancestor) return true;
            current = current.GetVisualParent() as Visual;
        }
        return false;
    }

    /// <summary>
    /// Finds the nearest neighbor in the given direction based on the
    /// on-screen bounds of realized children.
    /// </summary>
    private static Control? FindNeighbor(ItemsRepeater repeater, Control current, Key direction)
    {
        var currentBounds = GetBoundsRelativeTo(current, repeater);
        if (currentBounds == null) return null;

        var currentCenter = currentBounds.Value.Center;

        Control? best = null;
        double bestDistance = double.MaxValue;

        foreach (var child in repeater.Children)
        {
            if (child == current || !child.IsVisible) continue;

            var childBounds = GetBoundsRelativeTo(child, repeater);
            if (childBounds == null) continue;

            var childCenter = childBounds.Value.Center;

            bool isValid = direction switch
            {
                Key.Left  => childCenter.X < currentCenter.X - 1,
                Key.Right => childCenter.X > currentCenter.X + 1,
                Key.Up    => childCenter.Y < currentCenter.Y - 1,
                Key.Down  => childCenter.Y > currentCenter.Y + 1,
                _ => false
            };

            if (!isValid) continue;

            // For horizontal movement, prefer same row (similar Y).
            // For vertical movement, prefer same column (similar X).
            double primaryDist;
            double secondaryDist;

            if (direction is Key.Left or Key.Right)
            {
                primaryDist = Math.Abs(childCenter.X - currentCenter.X);
                secondaryDist = Math.Abs(childCenter.Y - currentCenter.Y);
            }
            else
            {
                primaryDist = Math.Abs(childCenter.Y - currentCenter.Y);
                secondaryDist = Math.Abs(childCenter.X - currentCenter.X);
            }

            // Weight: strongly prefer items on the same row/column
            double distance = primaryDist + secondaryDist * 10;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = child;
            }
        }

        return best;
    }

    private static Rect? GetBoundsRelativeTo(Visual element, Visual relativeTo)
    {
        try
        {
            var transform = element.TransformToVisual(relativeTo);
            if (transform == null) return null;
            return new Rect(element.Bounds.Size).TransformToAABB(transform.Value);
        }
        catch
        {
            return null;
        }
    }

    public static void FocusFirstChild(ItemsRepeater repeater)
    {
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var child in repeater.Children)
            {
                var focusable = FindFocusableDescendant(child);
                if (focusable != null)
                {
                    FocusElement(child);
                    return;
                }
            }
        }, DispatcherPriority.Loaded);
    }

    private static void FocusElement(Control element)
    {
        var focusable = FindFocusableDescendant(element) ?? element;

        if (focusable is InputElement input)
        {
            input.Focus(NavigationMethod.Directional);

            if (input is Control control)
                control.BringIntoView();
        }
    }

    private static InputElement? FindFocusableDescendant(Control element)
    {
        if (element is InputElement { Focusable: true } input)
            return input;

        return element.GetVisualDescendants()
            .OfType<InputElement>()
            .FirstOrDefault(e => e.Focusable);
    }
}
