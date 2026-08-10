// input_sdl.cpp — SDL3 event pump feeding the per-frame InputState.
//
// This replaces input_win32.cpp's 280 lines of GetAsyncKeyState polling, two
// hand-written translation tables and a hand-rolled auto-repeat timer. SDL
// provides key repeat, and SDL_EVENT_TEXT_INPUT provides committed characters
// — which also means non-US layouts, dead keys and IMEs work, none of which
// the hardcoded US-layout table could handle.

#include "ui/input.h"
#include "gfx/gfx.h"

#include <SDL3/SDL.h>

namespace launcher
{
    namespace gfx
    {
        // Defined in gfx_sdl.cpp. Not in gfx.h — backend-internal plumbing.
        void display_set_close_requested();
        SDL_Window *display_window();
    }

    namespace ui
    {

        namespace
        {
            int s_prev_buttons = 0;

            unsigned translate_mods(SDL_Keymod m)
            {
                unsigned out = ModNone;
                if (m & SDL_KMOD_SHIFT) out |= ModShift;
                if (m & SDL_KMOD_CTRL)  out |= ModCtrl;
                if (m & SDL_KMOD_ALT)   out |= ModAlt;
                return out;
            }

            Key translate_scancode(SDL_Scancode sc)
            {
                if (sc >= SDL_SCANCODE_A && sc <= SDL_SCANCODE_Z)
                    return (Key)((int)Key::A + (sc - SDL_SCANCODE_A));

                // SDL orders the number row 1..9 then 0, so 0 is separate.
                if (sc >= SDL_SCANCODE_1 && sc <= SDL_SCANCODE_9)
                    return (Key)((int)Key::Num1 + (sc - SDL_SCANCODE_1));

                if (sc >= SDL_SCANCODE_F1 && sc <= SDL_SCANCODE_F12)
                    return (Key)((int)Key::F1 + (sc - SDL_SCANCODE_F1));

                switch (sc)
                {
                case SDL_SCANCODE_0:          return Key::Num0;
                case SDL_SCANCODE_KP_0:       return Key::Num0;
                case SDL_SCANCODE_ESCAPE:     return Key::Escape;
                case SDL_SCANCODE_RETURN:     return Key::Enter;
                case SDL_SCANCODE_KP_ENTER:   return Key::KeypadEnter;
                case SDL_SCANCODE_BACKSPACE:  return Key::Backspace;
                case SDL_SCANCODE_TAB:        return Key::Tab;
                case SDL_SCANCODE_SPACE:      return Key::Space;
                case SDL_SCANCODE_LEFT:       return Key::Left;
                case SDL_SCANCODE_RIGHT:      return Key::Right;
                case SDL_SCANCODE_UP:         return Key::Up;
                case SDL_SCANCODE_DOWN:       return Key::Down;
                case SDL_SCANCODE_HOME:       return Key::Home;
                case SDL_SCANCODE_END:        return Key::End;
                case SDL_SCANCODE_PAGEUP:     return Key::PageUp;
                case SDL_SCANCODE_PAGEDOWN:   return Key::PageDown;
                case SDL_SCANCODE_INSERT:     return Key::Insert;
                case SDL_SCANCODE_DELETE:     return Key::Delete;
                default:                      return Key::None;
                }
            }
        } // namespace

        void InputState::poll()
        {
            keys.clear();
            text.clear();
            mouse.wheel_delta = 0;
            resized = false;

            SDL_Event ev;
            while (SDL_PollEvent(&ev))
            {
                switch (ev.type)
                {
                case SDL_EVENT_QUIT:
                    // Covers the taskbar Close and Alt+F4, so neither needs
                    // the special-casing the Win32 path had.
                    quit_requested = true;
                    gfx::display_set_close_requested();
                    break;

                case SDL_EVENT_KEY_DOWN:
                {
                    Key k = translate_scancode(ev.key.scancode);
                    if (k != Key::None)
                    {
                        KeyEvent e;
                        e.key = k;
                        e.repeat = ev.key.repeat != 0;
                        e.mods = translate_mods(ev.key.mod);
                        keys.push_back(e);
                    }
                    break;
                }

                case SDL_EVENT_TEXT_INPUT:
                    text += ev.text.text;
                    break;

                case SDL_EVENT_MOUSE_WHEEL:
                    // SDL reports fractional/high-resolution wheel deltas;
                    // the UI only ever uses the sign and magnitude in notches.
                    mouse.wheel_delta += (int)ev.wheel.y;
                    break;

                case SDL_EVENT_WINDOW_RESIZED:
                    resized = true;
                    resize_w = ev.window.data1;
                    resize_h = ev.window.data2;
                    break;

                case SDL_EVENT_WINDOW_EXPOSED:
                    // Repaint immediately so dragging a resize edge does not
                    // flash: the main loop would not otherwise present until
                    // its next frame.
                    gfx::present();
                    break;

                default:
                    break;
                }
            }

            // Mouse position and buttons as an end-of-pump snapshot, matching
            // the immediate-mode widgets' expectations.
            float mx = 0.0f, my = 0.0f;
            SDL_MouseButtonFlags b = SDL_GetMouseState(&mx, &my);

            mouse.x = (int)mx;
            mouse.y = (int)my;

            int buttons = 0;
            if (b & SDL_BUTTON_LMASK) buttons |= 1;
            if (b & SDL_BUTTON_RMASK) buttons |= 2;
            if (b & SDL_BUTTON_MMASK) buttons |= 4;
            mouse.buttons = buttons;

            // Click = left released this frame; pressed = left went down.
            mouse.clicked = (s_prev_buttons & 1) && !(buttons & 1);
            mouse.pressed = !(s_prev_buttons & 1) && (buttons & 1);

            s_prev_buttons = buttons;
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
