// Allegro must come before Windows headers.
#include <allegro.h>
#ifdef ALLEGRO_WINDOWS
#include <winalleg.h>
#endif

#include "ui/input.h"

#include <windows.h>

namespace launcher
{
    namespace ui
    {

        static int s_prev_mouse_b = 0;
        static int s_prev_mouse_z = 0;

        void InputState::poll()
        {
            // --- Keyboard: drain all pending keys ---
            keys.clear();

            if (keyboard_needs_poll())
                poll_keyboard();

            while (keypressed())
            {
                int k = readkey();

                KeyEvent ev;

                ev.scancode = k >> 8;
                ev.ascii = k & 0xFF;

                keys.push_back(ev);
            }

            // --- Mouse ---
            poll_mouse();

            // Read cursor position directly from Windows so it stays
            // correct after we strip the window frame (Allegro caches the
            // old non-client metrics and mouse_x/mouse_y drift).
#ifdef ALLEGRO_WINDOWS
            {
                POINT pt;
                GetCursorPos(&pt);
                HWND hwnd = win_get_window();
                if (hwnd)
                    ScreenToClient(hwnd, &pt);
                mouse.x = pt.x;
                mouse.y = pt.y;
            }
#else
            mouse.x = mouse_x;
            mouse.y = mouse_y;
#endif

            // Read button state directly from Windows too — Allegro's
            // mouse_b can get stale after window style changes / drag loops.
#ifdef ALLEGRO_WINDOWS
            {
                int btns = 0;
                if (GetAsyncKeyState(VK_LBUTTON) & 0x8000) btns |= 1;
                if (GetAsyncKeyState(VK_RBUTTON) & 0x8000) btns |= 2;
                if (GetAsyncKeyState(VK_MBUTTON) & 0x8000) btns |= 4;
                mouse.buttons = btns;
            }
#else
            mouse.buttons = mouse_b;
#endif
            mouse.wheel_delta = mouse_z - s_prev_mouse_z;
            s_prev_mouse_z = mouse_z;

            // Click = left button was down last frame, now released
            mouse.clicked = (s_prev_mouse_b & 1) && !(mouse.buttons & 1);
            // Pressed = left button just went down this frame
            mouse.pressed = !(s_prev_mouse_b & 1) && (mouse.buttons & 1);

            s_prev_mouse_b = mouse.buttons;
        }

        bool InputState::key_pressed(int scancode) const
        {
            for (size_t i = 0; i < keys.size(); ++i)
            {
                if (keys[i].scancode == scancode)
                    return true;
            }

            return false;
        }

    } // namespace ui
} // namespace launcher
