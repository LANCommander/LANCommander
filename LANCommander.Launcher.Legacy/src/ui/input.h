#ifndef LAUNCHER_UI_INPUT_H
#define LAUNCHER_UI_INPUT_H

#include <string>
#include <vector>

namespace launcher {
    namespace ui {

        // Backend-independent key identity.
        //
        // This used to be a raw Allegro KEY_* scancode, which leaked the
        // graphics library's vocabulary into every screen. Scoped so it can
        // never collide with Allegro's KEY_* macros while both backends
        // coexist, and so the compiler flags every use site if it changes.
        enum class Key {
            None = 0,

            Escape, Enter, KeypadEnter, Backspace, Tab, Space,

            Left, Right, Up, Down,
            Home, End, PageUp, PageDown, Insert, Delete,

            // Contiguous — F1 + n and A + n are relied on by the mapping table.
            F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,

            A, B, C, D, E, F, G, H, I, J, K, L, M,
            N, O, P, Q, R, S, T, U, V, W, X, Y, Z,

            Num0, Num1, Num2, Num3, Num4,
            Num5, Num6, Num7, Num8, Num9
        };

        // Mixed case deliberately: winuser.h already defines MOD_SHIFT /
        // MOD_CONTROL / MOD_ALT as macros for RegisterHotKey.
        enum Mod {
            ModNone  = 0,
            ModShift = 1,
            ModCtrl  = 2,
            ModAlt   = 4
        };

        // A single key event captured during a frame.
        struct KeyEvent {
            Key key;
            bool repeat;    // synthesized by auto-repeat rather than a fresh press
            unsigned mods;  // bitmask of Mod
        };

        // Mouse state snapshot for the current frame.
        struct MouseState {
            int x, y;
            int buttons;        // bitmask — bit 0 = left
            int wheel_delta;    // scroll wheel change this frame
            bool clicked;       // left button released this frame (click)
            bool pressed;       // left button just went down this frame
        };

        // Per-frame input state. Call poll() once at the start of each frame,
        // then pass this to all widgets and screens.
        struct InputState {
            std::vector<KeyEvent> keys;

            // Text committed this frame, UTF-8. Separate from `keys` because
            // producing characters is a different concern from detecting key
            // presses — this is what SDL_EVENT_TEXT_INPUT will feed, and it
            // is what makes non-US layouts and IMEs work.
            std::string text;

            MouseState mouse;

            // Set when the OS asks the window to close.
            bool quit_requested;

            // Set when the window changed size this frame. The Win32 backend
            // leaves this false — its WndProc notifies App directly — while
            // the SDL backend reports SDL_EVENT_WINDOW_RESIZED here, since it
            // has no WndProc of its own.
            bool resized;
            int resize_w, resize_h;

            InputState() : quit_requested(false), resized(false),
                           resize_w(0), resize_h(0)
            {
                mouse.x = mouse.y = 0;
                mouse.buttons = 0;
                mouse.wheel_delta = 0;
                mouse.clicked = false;
                mouse.pressed = false;
            }

            // Drain all pending input into this struct.
            void poll();

            // Convenience: was a specific key pressed this frame?
            bool key_pressed(Key k) const;

            // Was a modifier held during any key event this frame?
            bool mod_down(Mod m) const;
        };


    } // namespace ui
} // namespace launcher

#endif // LAUNCHER_UI_INPUT_H
