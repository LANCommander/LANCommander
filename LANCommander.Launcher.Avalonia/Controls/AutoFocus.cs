using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace LANCommander.Launcher.Avalonia.Controls;

/// <summary>
/// Attached behavior that automatically focuses the first focusable descendant
/// when a control is attached to the visual tree.
/// Usage: <c>&lt;UserControl controls:AutoFocus.IsEnabled="True" /&gt;</c>
/// </summary>
public class AutoFocus : AvaloniaObject
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<AutoFocus, Control, bool>("IsEnabled");

    static AutoFocus()
    {
        IsEnabledProperty.Changed.AddClassHandler<Control>(OnIsEnabledChanged);
    }

    public static bool GetIsEnabled(Control control) => control.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(Control control, bool value) => control.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        control.AttachedToVisualTree -= OnAttached;

        if (e.NewValue is true)
            control.AttachedToVisualTree += OnAttached;
    }

    private static void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not Control control) return;

        Dispatcher.UIThread.Post(() =>
        {
            // Don't steal focus if something is already focused within this control
            var topLevel = TopLevel.GetTopLevel(control);
            var currentFocus = topLevel?.FocusManager?.GetFocusedElement() as Visual;
            if (currentFocus != null && IsDescendantOf(currentFocus, control))
                return;

            var focusable = control.GetVisualDescendants()
                .OfType<InputElement>()
                .FirstOrDefault(el => el.Focusable && el.IsEffectivelyVisible);

            focusable?.Focus(NavigationMethod.Directional);
        }, DispatcherPriority.Loaded);
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
}
