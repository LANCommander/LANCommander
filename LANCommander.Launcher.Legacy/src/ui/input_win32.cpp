// Allegro must come before Windows headers.
#include <allegro.h>
#ifdef ALLEGRO_WINDOWS
#include <winalleg.h>
#endif

#include "ui/input.h"
#include "gfx/gfx.h"

#include <windows.h>

namespace launcher
{
    namespace ui
    {

        // -----------------------------------------------------------------
        // Win32 keyboard polling — bypasses Allegro's DirectInput keyboard,
        // which breaks after the window style is changed to WS_POPUP.
        // Uses GetAsyncKeyState for real-time key reads (same approach as
        // the mouse button handling below).
        //
        // Transitional: SDL_PollEvent replaces this file wholesale, at which
        // point the two hand-written translation tables and the auto-repeat
        // timer below all disappear. The struct it fills is already the
        // target shape so the screens only get converted once.
        // -----------------------------------------------------------------

        static Key vk_to_key(int vk)
        {
            if (vk >= 'A' && vk <= 'Z')
                return (Key)((int)Key::A + (vk - 'A'));
            if (vk >= '0' && vk <= '9')
                return (Key)((int)Key::Num0 + (vk - '0'));
            if (vk >= VK_NUMPAD0 && vk <= VK_NUMPAD9)
                return (Key)((int)Key::Num0 + (vk - VK_NUMPAD0));
            if (vk >= VK_F1 && vk <= VK_F12)
                return (Key)((int)Key::F1 + (vk - VK_F1));

            switch (vk)
            {
            case VK_ESCAPE: return Key::Escape;
            case VK_BACK:   return Key::Backspace;
            case VK_TAB:    return Key::Tab;
            case VK_RETURN: return Key::Enter;
            case VK_SPACE:  return Key::Space;
            case VK_INSERT: return Key::Insert;
            case VK_DELETE: return Key::Delete;
            case VK_HOME:   return Key::Home;
            case VK_END:    return Key::End;
            case VK_PRIOR:  return Key::PageUp;
            case VK_NEXT:   return Key::PageDown;
            case VK_LEFT:   return Key::Left;
            case VK_RIGHT:  return Key::Right;
            case VK_UP:     return Key::Up;
            case VK_DOWN:   return Key::Down;
            default:        return Key::None;
            }
        }

        static bool is_modifier_vk(int vk)
        {
            return vk == VK_SHIFT   || vk == VK_CONTROL || vk == VK_MENU   ||
                   vk == VK_LSHIFT  || vk == VK_RSHIFT  ||
                   vk == VK_LCONTROL|| vk == VK_RCONTROL||
                   vk == VK_LMENU   || vk == VK_RMENU   ||
                   vk == VK_CAPITAL || vk == VK_NUMLOCK  || vk == VK_SCROLL;
        }

        // Direct VK → ASCII conversion (US layout).
        // ToAscii() fails when the keyboard state is built from
        // GetAsyncKeyState rather than the message queue, so we
        // map characters ourselves.
        static int vk_to_ascii_char(int vk, const BYTE *kb)
        {
            bool shift = (kb[VK_SHIFT] & 0x80) != 0;
            bool caps  = (kb[VK_CAPITAL] & 1) != 0;
            bool upper = shift ^ caps;

            if (vk >= 'A' && vk <= 'Z')
                return upper ? vk : (vk + 32);

            if (vk >= VK_NUMPAD0 && vk <= VK_NUMPAD9)
                return '0' + (vk - VK_NUMPAD0);

            if (!shift)
            {
                if (vk >= '0' && vk <= '9')  return vk;
                switch (vk)
                {
                case VK_SPACE:      return ' ';
                case VK_OEM_1:      return ';';
                case VK_OEM_PLUS:   return '=';
                case VK_OEM_COMMA:  return ',';
                case VK_OEM_MINUS:  return '-';
                case VK_OEM_PERIOD: return '.';
                case VK_OEM_2:      return '/';
                case VK_OEM_3:      return '`';
                case VK_OEM_4:      return '[';
                case VK_OEM_5:      return '\\';
                case VK_OEM_6:      return ']';
                case VK_OEM_7:      return '\'';
                }
            }
            else
            {
                switch (vk)
                {
                case '0': return ')';  case '1': return '!';
                case '2': return '@';  case '3': return '#';
                case '4': return '$';  case '5': return '%';
                case '6': return '^';  case '7': return '&';
                case '8': return '*';  case '9': return '(';
                case VK_SPACE:      return ' ';
                case VK_OEM_1:      return ':';
                case VK_OEM_PLUS:   return '+';
                case VK_OEM_COMMA:  return '<';
                case VK_OEM_MINUS:  return '_';
                case VK_OEM_PERIOD: return '>';
                case VK_OEM_2:      return '?';
                case VK_OEM_3:      return '~';
                case VK_OEM_4:      return '{';
                case VK_OEM_5:      return '|';
                case VK_OEM_6:      return '}';
                case VK_OEM_7:      return '"';
                }
            }

            return 0;
        }

        // -----------------------------------------------------------------

        static int  s_prev_mouse_b = 0;
        static int  s_prev_mouse_z = 0;
        static BYTE s_prev_kb[256] = {};
        static int  s_repeat_vk    = 0;
        static DWORD s_repeat_start = 0;
        static DWORD s_repeat_last  = 0;

        static const DWORD REPEAT_DELAY_MS = 400;
        static const DWORD REPEAT_RATE_MS  = 45;

        void InputState::poll()
        {
            // --- Keyboard: poll directly from Windows ---
            keys.clear();
            text.clear();

            // Build real-time key state from GetAsyncKeyState.
            BYTE kb[256];
            for (int vk = 0; vk < 256; ++vk)
            {
                SHORT st = GetAsyncKeyState(vk);
                kb[vk] = (st & 0x8000) ? 0x80 : 0;
            }
            // Toggle state for Caps Lock (needed by vk_to_ascii_char).
            kb[VK_CAPITAL] |= (BYTE)(GetKeyState(VK_CAPITAL) & 1);

            unsigned mods = ModNone;
            if (kb[VK_SHIFT]   & 0x80) mods |= ModShift;
            if (kb[VK_CONTROL] & 0x80) mods |= ModCtrl;
            if (kb[VK_MENU]    & 0x80) mods |= ModAlt;

            DWORD now = GetTickCount();

            for (int vk = 1; vk < 256; ++vk)
            {
                if (is_modifier_vk(vk))
                    continue;

                bool down     = (kb[vk]          & 0x80) != 0;
                bool was_down = (s_prev_kb[vk]   & 0x80) != 0;

                bool emit = false;
                bool repeat = false;

                if (down && !was_down)
                {
                    // New press
                    emit = true;
                    s_repeat_vk    = vk;
                    s_repeat_start = now;
                    s_repeat_last  = now;
                }
                else if (down && was_down && vk == s_repeat_vk)
                {
                    // Key held — auto-repeat
                    if (now - s_repeat_start >= REPEAT_DELAY_MS &&
                        now - s_repeat_last  >= REPEAT_RATE_MS)
                    {
                        emit = true;
                        repeat = true;
                        s_repeat_last = now;
                    }
                }

                if (emit)
                {
                    Key k = vk_to_key(vk);
                    int ascii = vk_to_ascii_char(vk, kb);

                    // Control chords produce no text.
                    if (ascii >= 32 && ascii < 127 && !(mods & ModCtrl) && !(mods & ModAlt))
                        text += (char)ascii;

                    if (k != Key::None)
                    {
                        KeyEvent ev;
                        ev.key = k;
                        ev.repeat = repeat;
                        ev.mods = mods;
                        keys.push_back(ev);
                    }
                }
            }

            // Clear repeat tracking when the key is released.
            if (s_repeat_vk && !(kb[s_repeat_vk] & 0x80))
                s_repeat_vk = 0;

            memcpy(s_prev_kb, kb, 256);

            // --- Quit ---
            if (gfx::display_close_requested())
                quit_requested = true;

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

        bool InputState::key_pressed(Key k) const
        {
            for (size_t i = 0; i < keys.size(); ++i)
            {
                if (keys[i].key == k)
                    return true;
            }

            return false;
        }

        bool InputState::mod_down(Mod m) const
        {
            for (size_t i = 0; i < keys.size(); ++i)
            {
                if (keys[i].mods & m)
                    return true;
            }

            return false;
        }

    } // namespace ui
} // namespace launcher
