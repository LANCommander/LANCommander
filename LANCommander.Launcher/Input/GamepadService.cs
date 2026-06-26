using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.Logging;
using SDL;
using static SDL.SDL3;

namespace LANCommander.Launcher.Input;

/// <summary>
/// Polls SDL3 for gamepad input on a background thread and injects
/// the equivalent keyboard events into Avalonia's input pipeline.
/// Only affects the app when it has focus — no OS-level input injection.
/// </summary>
public sealed class GamepadService : IDisposable
{
    private static readonly TimeSpan RepeatDelay    = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan RepeatInterval = TimeSpan.FromMilliseconds(120);
    private const short DeadZone = 8_000;

    /// <summary>
    /// True after any gamepad input has been received this session.
    /// Used by AutoFocus to decide whether to auto-focus on view load.
    /// </summary>
    public static bool HasReceivedInput { get; private set; }

    private readonly ILogger<GamepadService> _logger;
    private Thread? _thread;
    private CancellationTokenSource? _cts;
    private bool _sdlInitialized;

    private readonly Dictionary<SDL_JoystickID, nint> _controllers = new();

    // Per-axis repeat state (Key.None = centered)
    private Key _axisXKey = Key.None;
    private Key _axisYKey = Key.None;
    private DateTime _axisXStart,  _axisYStart;
    private DateTime _axisXRepeat, _axisYRepeat;

    public GamepadService(ILogger<GamepadService> logger)
    {
        _logger = logger;
    }

    public void Start()
    {
        _cts    = new CancellationTokenSource();
        _thread = new Thread(PollLoop) { IsBackground = true, Name = "GamepadPollThread" };
        _thread.Start();
    }

    public void Stop() => _cts?.Cancel();

    // -------------------------------------------------------------------------
    // Background poll loop
    // -------------------------------------------------------------------------

    private unsafe void PollLoop()
    {
        try
        {
            if (!SDL_Init(SDL_InitFlags.SDL_INIT_GAMEPAD))
            {
                _logger.LogWarning("SDL_Init failed — gamepad navigation disabled");
                return;
            }

            _sdlInitialized = true;
            _logger.LogInformation("SDL3 gamepad subsystem initialized");

            int count;
            var gamepads = SDL_GetGamepads(&count);
            if (gamepads != null)
            {
                for (var i = 0; i < count; i++)
                    TryOpenController(gamepads[i]);

                SDL_free(gamepads);
            }

            while (!_cts!.IsCancellationRequested)
            {
                SDL_Event ev;
                while (SDL_PollEvent(&ev))
                    HandleEvent(ref ev);

                TickAxisRepeat();
                Thread.Sleep(8); // ~120 Hz
            }
        }
        catch (DllNotFoundException)
        {
            _logger.LogWarning("SDL3 native library not found — gamepad navigation disabled");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in gamepad poll loop");
        }
        finally
        {
            foreach (var handle in _controllers.Values)
                unsafe { SDL_CloseGamepad((SDL_Gamepad*)handle); }

            _controllers.Clear();

            if (_sdlInitialized)
                SDL_Quit();
        }
    }

    // -------------------------------------------------------------------------
    // SDL event dispatch
    // -------------------------------------------------------------------------

    private void HandleEvent(ref SDL_Event ev)
    {
        switch (ev.Type)
        {
            case SDL_EventType.SDL_EVENT_GAMEPAD_ADDED:
                TryOpenController(ev.gdevice.which);
                break;

            case SDL_EventType.SDL_EVENT_GAMEPAD_REMOVED:
                CloseController(ev.gdevice.which);
                break;

            case SDL_EventType.SDL_EVENT_GAMEPAD_BUTTON_DOWN:
                OnButton(ev.gbutton.Button);
                break;

            case SDL_EventType.SDL_EVENT_GAMEPAD_AXIS_MOTION:
                OnAxis(ev.gaxis.Axis, ev.gaxis.value);
                break;
        }
    }

    private unsafe void TryOpenController(SDL_JoystickID instanceId)
    {
        if (!SDL_IsGamepad(instanceId)) return;

        var handle = SDL_OpenGamepad(instanceId);
        if (handle == null) return;

        _controllers[instanceId] = (nint)handle;
        _logger.LogInformation("Gamepad {InstanceId} connected", instanceId);
    }

    private unsafe void CloseController(SDL_JoystickID instanceId)
    {
        if (_controllers.TryGetValue(instanceId, out var handle))
        {
            SDL_CloseGamepad((SDL_Gamepad*)handle);
            _controllers.Remove(instanceId);
        }

        _logger.LogInformation("Gamepad {InstanceId} disconnected", instanceId);
    }

    // -------------------------------------------------------------------------
    // Button → key mapping
    //   South (A) / Start  → Enter   (confirm / activate)
    //   East  (B)          → Escape  (back)
    //   North (Y)          → Apps    (open game context menu)
    //   D-pad               → arrow keys
    //   LB / RB             → Page Up / Page Down
    // -------------------------------------------------------------------------

    private void OnButton(SDL_GamepadButton button)
    {
        var key = button switch
        {
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH            => Key.Return,
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_START            => Key.Return,
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_EAST             => Key.Escape,
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_NORTH            => Key.Apps,
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_UP          => Key.Up,
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_DOWN        => Key.Down,
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_LEFT        => Key.Left,
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_RIGHT       => Key.Right,
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_SHOULDER    => Key.PageUp,
            SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER   => Key.PageDown,
            _                                                     => Key.None
        };

        if (key != Key.None)
            SimulateKey(key);
    }

    // -------------------------------------------------------------------------
    // Left-stick axes → arrow keys with initial delay + auto-repeat
    // -------------------------------------------------------------------------

    private void OnAxis(SDL_GamepadAxis axis, short value)
    {
        if (axis == SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTX)
        {
            var key = value < -DeadZone ? Key.Left
                    : value >  DeadZone ? Key.Right
                    : Key.None;

            if (key != _axisXKey)
            {
                _axisXKey = key;
                if (key != Key.None)
                {
                    _axisXStart = _axisXRepeat = DateTime.UtcNow;
                    SimulateKey(key);
                }
            }
        }
        else if (axis == SDL_GamepadAxis.SDL_GAMEPAD_AXIS_LEFTY)
        {
            var key = value < -DeadZone ? Key.Up
                    : value >  DeadZone ? Key.Down
                    : Key.None;

            if (key != _axisYKey)
            {
                _axisYKey = key;
                if (key != Key.None)
                {
                    _axisYStart = _axisYRepeat = DateTime.UtcNow;
                    SimulateKey(key);
                }
            }
        }
    }

    private void TickAxisRepeat()
    {
        var now = DateTime.UtcNow;

        if (_axisXKey != Key.None
            && now - _axisXStart  > RepeatDelay
            && now - _axisXRepeat > RepeatInterval)
        {
            _axisXRepeat = now;
            SimulateKey(_axisXKey);
        }

        if (_axisYKey != Key.None
            && now - _axisYStart  > RepeatDelay
            && now - _axisYRepeat > RepeatInterval)
        {
            _axisYRepeat = now;
            SimulateKey(_axisYKey);
        }
    }

    // -------------------------------------------------------------------------
    // Inject a key press into Avalonia's input pipeline via public interfaces.
    // -------------------------------------------------------------------------

    private static void SimulateKey(Key key)
    {
        HasReceivedInput = true;

        Dispatcher.UIThread.Post(() =>
        {
            var window = (Application.Current?.ApplicationLifetime
                as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

            if (window is null || !window.IsActive) return;

            // If an overlay is open, constrain all interaction to it
            var overlay = GetTopmostOverlay(window);

            var focused = window.FocusManager?.GetFocusedElement() as InputElement;

            // If focus is outside the active overlay, pull it back in
            if (overlay != null && focused is Visual focusedVisual
                && !overlay.IsVisualAncestorOf(focusedVisual))
            {
                focused = null;
            }

            // If nothing is (validly) focused, find and focus the first content item
            if (focused is null)
            {
                var searchRoot = overlay ?? (Control)window;

                focused = searchRoot.GetVisualDescendants()
                    .OfType<InputElement>()
                    .FirstOrDefault(IsGamepadFocusCandidate);

                if (focused is null) return;
                focused.Focus(NavigationMethod.Directional);
                return; // First press just establishes focus
            }

            focused.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyDeviceType = KeyDeviceType.Gamepad });
            focused.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent,   Key = key, KeyDeviceType = KeyDeviceType.Gamepad });

        }, DispatcherPriority.Normal);
    }

    /// <summary>
    /// Determines whether an element is a suitable target for initial gamepad focus.
    /// Skips container controls and text inputs — we want interactive "content" items.
    /// </summary>
    private static bool IsGamepadFocusCandidate(InputElement e)
    {
        return e.Focusable && e.IsEffectivelyVisible
            && e is not Window
            && e is not ItemsControl  // ListBox, ComboBox, ItemsRepeater, etc.
            && e is not ScrollViewer
            && e is not TextBox;
    }

    /// <summary>
    /// Returns the topmost overlay if one is open, otherwise null.
    /// </summary>
    private static Control? GetTopmostOverlay(Window window)
    {
        var layer = OverlayLayer.GetOverlayLayer(window);
        if (layer is null) return null;

        // Walk children in reverse to find the topmost actual overlay control
        for (var i = layer.Children.Count - 1; i >= 0; i--)
        {
            if (layer.Children[i] is Control { IsVisible: true } child)
                return child;
        }

        return null;
    }

    // -------------------------------------------------------------------------

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}
