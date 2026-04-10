using System;
using System.Collections.Generic;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Avalonia.Input;

/// <summary>
/// Polls SDL2 for gamepad input on a background thread and injects
/// the equivalent keyboard events into Avalonia's input pipeline.
/// Only affects the app when it has focus — no OS-level input injection.
/// </summary>
public sealed class GamepadService : IDisposable
{
    private static readonly TimeSpan RepeatDelay    = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan RepeatInterval = TimeSpan.FromMilliseconds(120);
    private const short DeadZone = 8_000;

    private readonly ILogger<GamepadService> _logger;
    private Thread? _thread;
    private CancellationTokenSource? _cts;
    private bool _sdlInitialized;

    private readonly Dictionary<int, IntPtr> _controllers = new();

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

    private void PollLoop()
    {
        try
        {
            if (Sdl2.SDL_Init(Sdl2.SDL_INIT_GAMECONTROLLER) < 0)
            {
                _logger.LogWarning("SDL_Init failed — gamepad navigation disabled");
                return;
            }

            _sdlInitialized = true;
            _logger.LogInformation("SDL2 gamepad subsystem initialized");

            for (var i = 0; i < Sdl2.SDL_NumJoysticks(); i++)
                TryOpenController(i);

            while (!_cts!.IsCancellationRequested)
            {
                while (Sdl2.SDL_PollEvent(out var ev) != 0)
                    HandleEvent(ref ev);

                TickAxisRepeat();
                Thread.Sleep(8); // ~120 Hz
            }
        }
        catch (DllNotFoundException)
        {
            _logger.LogWarning("SDL2 native library not found — gamepad navigation disabled");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in gamepad poll loop");
        }
        finally
        {
            foreach (var handle in _controllers.Values)
                Sdl2.SDL_GameControllerClose(handle);

            _controllers.Clear();

            if (_sdlInitialized)
                Sdl2.SDL_Quit();
        }
    }

    // -------------------------------------------------------------------------
    // SDL event dispatch
    // -------------------------------------------------------------------------

    private void HandleEvent(ref Sdl2.SDL_Event ev)
    {
        switch (ev.type)
        {
            case Sdl2.SDL_CONTROLLERDEVICEADDED:
                TryOpenController(ev.cdevice.which);
                break;

            case Sdl2.SDL_CONTROLLERDEVICEREMOVED:
                CloseController(ev.cdevice.which);
                break;

            case Sdl2.SDL_CONTROLLERBUTTONDOWN:
                OnButton(ev.cbutton.button);
                break;

            case Sdl2.SDL_CONTROLLERAXISMOTION:
                OnAxis(ev.caxis.axis, ev.caxis.value);
                break;
        }
    }

    private void TryOpenController(int joystickIndex)
    {
        if (!Sdl2.SDL_IsGameController(joystickIndex)) return;

        var handle = Sdl2.SDL_GameControllerOpen(joystickIndex);
        if (handle == IntPtr.Zero) return;

        _controllers[joystickIndex] = handle;
        _logger.LogInformation("Gamepad {Index} connected", joystickIndex);
    }

    private void CloseController(int instanceId)
    {
        var handle = Sdl2.SDL_GameControllerFromInstanceID(instanceId);
        if (handle != IntPtr.Zero)
            Sdl2.SDL_GameControllerClose(handle);

        _controllers.Remove(instanceId);
        _logger.LogInformation("Gamepad {InstanceId} disconnected", instanceId);
    }

    // -------------------------------------------------------------------------
    // Button → key mapping
    //   A / Start  → Enter   (confirm / activate)
    //   B          → Escape  (back)
    //   D-pad      → arrow keys
    //   LB / RB    → Page Up / Page Down
    // -------------------------------------------------------------------------

    private void OnButton(byte button)
    {
        var key = button switch
        {
            Sdl2.SDL_CONTROLLER_BUTTON_A             => Key.Return,
            Sdl2.SDL_CONTROLLER_BUTTON_START         => Key.Return,
            Sdl2.SDL_CONTROLLER_BUTTON_B             => Key.Escape,
            Sdl2.SDL_CONTROLLER_BUTTON_DPAD_UP       => Key.Up,
            Sdl2.SDL_CONTROLLER_BUTTON_DPAD_DOWN     => Key.Down,
            Sdl2.SDL_CONTROLLER_BUTTON_DPAD_LEFT     => Key.Left,
            Sdl2.SDL_CONTROLLER_BUTTON_DPAD_RIGHT    => Key.Right,
            Sdl2.SDL_CONTROLLER_BUTTON_LEFTSHOULDER  => Key.PageUp,
            Sdl2.SDL_CONTROLLER_BUTTON_RIGHTSHOULDER => Key.PageDown,
            _                                        => Key.None
        };

        if (key != Key.None)
            SimulateKey(key);
    }

    // -------------------------------------------------------------------------
    // Left-stick axes → arrow keys with initial delay + auto-repeat
    // -------------------------------------------------------------------------

    private void OnAxis(byte axis, short value)
    {
        if (axis == Sdl2.SDL_CONTROLLER_AXIS_LEFTX)
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
        else if (axis == Sdl2.SDL_CONTROLLER_AXIS_LEFTY)
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
    // IInputManager and IKeyboardDevice are both public; only the concrete
    // InputManager class and KeyboardDevice.Instance were internalized.
    // -------------------------------------------------------------------------

    private static void SimulateKey(Key key)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var window = (Application.Current?.ApplicationLifetime
                as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

            if (window is null) return;

            var focused = window.FocusManager?.GetFocusedElement() as InputElement;
            if (focused is null) return;

            focused.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });
            focused.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent,   Key = key });

        }, DispatcherPriority.Input);
    }

    // -------------------------------------------------------------------------

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}
