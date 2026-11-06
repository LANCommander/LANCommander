using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using LANCommander.Launcher.ViewModels.Components;
using LANCommander.Launcher.Views.Components;

namespace LANCommander.Launcher.Controls;

/// <summary>
/// Marks a control as showing a player, so it tells when they are playing: it takes the
/// <c>Playing</c> style class (names turn the success colour, see LANCommander.axaml), and a card
/// with what they are playing opens over it on hover, or on focus for a gamepad.
/// Usage: <c>&lt;TextBlock Text="{Binding Name}" controls:Presence.UserId="{Binding Id}" /&gt;</c>
/// </summary>
public class Presence : AvaloniaObject
{
    public static readonly AttachedProperty<Guid> UserIdProperty =
        AvaloniaProperty.RegisterAttached<Presence, Control, Guid>("UserId");

    // The player the control follows, while it is in the visual tree
    private static readonly AttachedProperty<Tracking?> TrackingProperty =
        AvaloniaProperty.RegisterAttached<Presence, Control, Tracking?>("Tracking");

    static Presence()
    {
        UserIdProperty.Changed.AddClassHandler<Control>((control, _) => Refresh(control));
    }

    public static Guid GetUserId(Control control) => control.GetValue(UserIdProperty);
    public static void SetUserId(Control control, Guid value) => control.SetValue(UserIdProperty, value);

    private static void Refresh(Control control)
    {
        control.AttachedToVisualTree -= OnAttached;
        control.DetachedFromVisualTree -= OnDetached;
        control.GotFocus -= OnGotFocus;
        control.LostFocus -= OnLostFocus;

        Stop(control);

        if (GetUserId(control) == Guid.Empty)
            return;

        control.AttachedToVisualTree += OnAttached;
        control.DetachedFromVisualTree += OnDetached;
        control.GotFocus += OnGotFocus;
        control.LostFocus += OnLostFocus;

        if (TopLevel.GetTopLevel(control) != null)
            Start(control);
    }

    private static void OnAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Control control)
            Start(control);
    }

    private static void OnDetached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is Control control)
            Stop(control);
    }

    // A gamepad has no hover; focus stands in for it
    private static void OnGotFocus(object? sender, GotFocusEventArgs e)
    {
        if (sender is Control control && e.NavigationMethod is NavigationMethod.Directional or NavigationMethod.Tab
            && control.GetValue(TrackingProperty)?.Player.IsPlaying == true)
            ToolTip.SetIsOpen(control, true);
    }

    private static void OnLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Control control)
            ToolTip.SetIsOpen(control, false);
    }

    private static void Start(Control control)
    {
        if (control.GetValue(TrackingProperty) != null || PresenceDirectory.Current is not { } directory)
            return;

        var tracking = new Tracking(control, directory.For(GetUserId(control)));

        control.SetValue(TrackingProperty, tracking);
        tracking.Apply();
    }

    private static void Stop(Control control)
    {
        if (control.GetValue(TrackingProperty) is not { } tracking)
            return;

        tracking.Dispose();
        control.ClearValue(TrackingProperty);
        control.Classes.Remove("Playing");
        ToolTip.SetTip(control, null);
    }

    private sealed class Tracking : IDisposable
    {
        private readonly Control _control;
        private PresenceCard? _card;

        public Tracking(Control control, PlayerPresenceViewModel player)
        {
            _control = control;
            Player = player;
            Player.PropertyChanged += OnPlayerChanged;
        }

        public PlayerPresenceViewModel Player { get; }

        private void OnPlayerChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PlayerPresenceViewModel.IsPlaying))
                Apply();
        }

        public void Apply()
        {
            _control.Classes.Set("Playing", Player.IsPlaying);

            if (Player.IsPlaying)
            {
                _card ??= new PresenceCard { DataContext = Player };
                ToolTip.SetTip(_control, _card);
            }
            else
            {
                ToolTip.SetIsOpen(_control, false);
                ToolTip.SetTip(_control, null);
            }
        }

        public void Dispose() => Player.PropertyChanged -= OnPlayerChanged;
    }
}
