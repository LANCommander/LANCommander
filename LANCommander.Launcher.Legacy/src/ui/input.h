#ifndef LAUNCHER_UI_INPUT_H
#define LAUNCHER_UI_INPUT_H

#include <vector>

namespace launcher {
    namespace ui {

        // A single key event captured during a frame.
        struct KeyEvent {
            int scancode;   // Allegro scancode (KEY_*)
            int ascii;      // ASCII character (0 if non-printable)
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
            MouseState mouse;

            // Drain all pending Allegro input into this struct.
            void poll();

            // Convenience: was a specific scancode pressed this frame?
            bool key_pressed(int scancode) const;
        };

    } // namespace ui
} // namespace launcher

#endif // LAUNCHER_UI_INPUT_H
