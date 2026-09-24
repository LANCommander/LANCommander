using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace LANCommander.Launcher.Controls;

/// <summary>
/// Closes an overlay dialog on Escape (also gamepad B, which the gamepad service maps to Escape).
/// The key is caught on the way down from the window, before the shell's own Escape handler would
/// navigate back underneath the open dialog, and it doesn't depend on focus being inside the dialog.
/// Only the topmost overlay reacts, so a dialog opened from another closes first.
/// </summary>
public static class ModalEscape
{
    public static void Enable(Control overlay, Action close)
    {
        TopLevel? topLevel = null;

        void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape || e.Handled)
                return;

            if (overlay.Parent is Panel layer && layer.Children.LastOrDefault() != overlay)
                return;

            close();
            e.Handled = true;
        }

        overlay.AttachedToVisualTree += (_, _) =>
        {
            topLevel = TopLevel.GetTopLevel(overlay);
            topLevel?.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        };

        overlay.DetachedFromVisualTree += (_, _) =>
        {
            topLevel?.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
            topLevel = null;
        };
    }
}
