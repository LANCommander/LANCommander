using System;
using System.Runtime.InteropServices;

namespace LANCommander.Launcher.Avalonia.Input;

/// <summary>
/// Minimal SDL2 P/Invoke bindings for gamepad input.
/// Requires SDL2 to be present at runtime (SDL2.dll on Windows,
/// libSDL2-2.0.so.0 on Linux, libSDL2.dylib on macOS).
/// </summary>
internal static partial class Sdl2
{
    private const string LibName = "SDL2";

    static Sdl2()
    {
        NativeLibrary.SetDllImportResolver(typeof(Sdl2).Assembly, (name, assembly, path) =>
        {
            if (name != LibName)
                return IntPtr.Zero;

            var nativeName = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? "libSDL2-2.0.so.0"
                : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? "libSDL2.dylib"
                    : "SDL2.dll";

            return NativeLibrary.TryLoad(nativeName, assembly, path, out var handle)
                ? handle
                : IntPtr.Zero;
        });
    }

    // Init flags
    public const uint SDL_INIT_GAMECONTROLLER = 0x00002000;

    // Event types
    public const uint SDL_CONTROLLERAXISMOTION   = 0x650;
    public const uint SDL_CONTROLLERBUTTONDOWN   = 0x651;
    public const uint SDL_CONTROLLERBUTTONUP     = 0x652;
    public const uint SDL_CONTROLLERDEVICEADDED  = 0x653;
    public const uint SDL_CONTROLLERDEVICEREMOVED = 0x654;

    // Axes
    public const byte SDL_CONTROLLER_AXIS_LEFTX       = 0;
    public const byte SDL_CONTROLLER_AXIS_LEFTY       = 1;
    public const byte SDL_CONTROLLER_AXIS_TRIGGERLEFT  = 4;
    public const byte SDL_CONTROLLER_AXIS_TRIGGERRIGHT = 5;

    // Buttons
    public const byte SDL_CONTROLLER_BUTTON_A             = 0;
    public const byte SDL_CONTROLLER_BUTTON_B             = 1;
    public const byte SDL_CONTROLLER_BUTTON_X             = 2;
    public const byte SDL_CONTROLLER_BUTTON_Y             = 3;
    public const byte SDL_CONTROLLER_BUTTON_BACK          = 4;
    public const byte SDL_CONTROLLER_BUTTON_START         = 6;
    public const byte SDL_CONTROLLER_BUTTON_LEFTSHOULDER  = 9;
    public const byte SDL_CONTROLLER_BUTTON_RIGHTSHOULDER = 10;
    public const byte SDL_CONTROLLER_BUTTON_DPAD_UP       = 11;
    public const byte SDL_CONTROLLER_BUTTON_DPAD_DOWN     = 12;
    public const byte SDL_CONTROLLER_BUTTON_DPAD_LEFT     = 13;
    public const byte SDL_CONTROLLER_BUTTON_DPAD_RIGHT    = 14;

    // SDL_Event is a 56-byte union; we overlay only the sub-structs we care about.
    [StructLayout(LayoutKind.Explicit, Size = 56)]
    public struct SDL_Event
    {
        [FieldOffset(0)] public uint type;
        [FieldOffset(0)] public SDL_ControllerAxisEvent   caxis;
        [FieldOffset(0)] public SDL_ControllerButtonEvent cbutton;
        [FieldOffset(0)] public SDL_ControllerDeviceEvent cdevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SDL_ControllerAxisEvent
    {
        public uint  type;
        public uint  timestamp;
        public int   which;     // joystick instance id
        public byte  axis;
        byte _pad1, _pad2, _pad3;
        public short value;
        ushort _pad4;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SDL_ControllerButtonEvent
    {
        public uint type;
        public uint timestamp;
        public int  which;
        public byte button;
        public byte state;
        byte _pad1, _pad2;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SDL_ControllerDeviceEvent
    {
        public uint type;
        public uint timestamp;
        public int  which;   // joystick index on ADDED; instance id on REMOVED
    }

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int SDL_Init(uint flags);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_Quit();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int SDL_PollEvent(out SDL_Event sdlEvent);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int SDL_NumJoysticks();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool SDL_IsGameController(int joystick_index);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr SDL_GameControllerOpen(int joystick_index);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SDL_GameControllerClose(IntPtr gamecontroller);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr SDL_GameControllerFromInstanceID(int joyid);
}
