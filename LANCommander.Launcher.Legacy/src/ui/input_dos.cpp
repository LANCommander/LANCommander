// input_dos.cpp — BIOS keyboard and INT 33h mouse, feeding the per-frame
// InputState.
//
// Keyboard comes from the BIOS enhanced-keystroke calls (INT 16h AH=11h/10h)
// rather than a hooked IRQ 1. The launcher's input model is "what happened
// this frame", which the BIOS type-ahead buffer already is, and the BIOS also
// gives typematic repeat and the keyboard's own layout translation for free.
// A hardware handler would only be needed for held-key state, which nothing
// in the UI asks for.
//
// The mouse is read as relative motion (INT 33h AX=0Bh) and integrated here
// rather than read as an absolute position (AX=03h). Drivers report absolute
// coordinates in a virtual screen whose size is theirs to choose — usually
// 640x200 — so in a VESA mode the absolute path lands the pointer in the
// wrong place on most drivers. Mickeys are the same everywhere.
//
// The pointer itself is drawn by gfx_dos.cpp; the driver's own cursor is
// switched off, because it cannot draw into a linear frame buffer the driver
// knows nothing about.

#include "ui/input.h"
#include "gfx/gfx.h"
#include "gfx/gfx_dos.h"

#include <dpmi.h>

#include <cstring>

namespace launcher
{
    namespace ui
    {

        namespace
        {
            bool s_mouse_present = false;
            bool s_mouse_wheel = false;
            bool s_initialised = false;

            int s_mouse_x = 0;
            int s_mouse_y = 0;
            int s_prev_buttons = 0;

            // --- BIOS / driver calls --------------------------------------

            inline int bios_int(int vec, __dpmi_regs *r)
            {
                return __dpmi_int(vec, r);
            }

            bool key_available()
            {
                __dpmi_regs r;
                std::memset(&r, 0, sizeof(r));
                r.h.ah = 0x11; // check for enhanced keystroke
                if (bios_int(0x16, &r) == -1)
                    return false;

                // ZF set means the buffer is empty.
                return (r.x.flags & 0x40) == 0;
            }

            // Returns AX from AH=10h: AL = character, AH = scan code.
            unsigned int read_key()
            {
                __dpmi_regs r;
                std::memset(&r, 0, sizeof(r));
                r.h.ah = 0x10; // read enhanced keystroke
                if (bios_int(0x16, &r) == -1)
                    return 0;
                return r.x.ax;
            }

            unsigned shift_state()
            {
                __dpmi_regs r;
                std::memset(&r, 0, sizeof(r));
                r.h.ah = 0x12; // get enhanced shift status
                if (bios_int(0x16, &r) == -1)
                    return ModNone;

                unsigned out = ModNone;
                if (r.h.al & 0x03) out |= ModShift; // either shift
                if (r.h.al & 0x04) out |= ModCtrl;
                if (r.h.al & 0x08) out |= ModAlt;
                return out;
            }

            void mouse_init()
            {
                __dpmi_regs r;
                std::memset(&r, 0, sizeof(r));
                r.x.ax = 0x0000; // reset driver and read status
                if (bios_int(0x33, &r) == -1 || r.x.ax != 0xFFFF)
                {
                    s_mouse_present = false;
                    return;
                }
                s_mouse_present = true;

                // The driver cannot draw a pointer into a frame buffer it was
                // never told about, so gfx_dos.cpp draws ours instead.
                std::memset(&r, 0, sizeof(r));
                r.x.ax = 0x0002; // hide cursor
                bios_int(0x33, &r);

                // CuteMouse's wheel API. Absent on the stock drivers, in
                // which case wheel_delta simply stays zero.
                std::memset(&r, 0, sizeof(r));
                r.x.ax = 0x0011;
                if (bios_int(0x33, &r) != -1 && r.x.ax == 0x574D && (r.x.cx & 1))
                    s_mouse_wheel = true;

                // Discard whatever motion accumulated before startup, so the
                // pointer does not jump on the first frame.
                std::memset(&r, 0, sizeof(r));
                r.x.ax = 0x000B;
                bios_int(0x33, &r);

                s_mouse_x = gfx::display_width() / 2;
                s_mouse_y = gfx::display_height() / 2;
            }

            // --- Scan code translation ------------------------------------

            // Set 1 make codes, as the BIOS reports them in AH.
            Key translate_scancode(unsigned char sc)
            {
                // Contiguous runs first — the enum orders A..Z and F1..F12 to
                // make exactly this possible.
                static const char ROW_Q[] = "QWERTYUIOP";
                static const char ROW_A[] = "ASDFGHJKL";
                static const char ROW_Z[] = "ZXCVBNM";

                if (sc >= 0x10 && sc <= 0x19)
                    return (Key)((int)Key::A + (ROW_Q[sc - 0x10] - 'A'));
                if (sc >= 0x1E && sc <= 0x26)
                    return (Key)((int)Key::A + (ROW_A[sc - 0x1E] - 'A'));
                if (sc >= 0x2C && sc <= 0x32)
                    return (Key)((int)Key::A + (ROW_Z[sc - 0x2C] - 'A'));

                // Number row is 1..9 then 0, same order as the enum's Num1.
                if (sc >= 0x02 && sc <= 0x0A)
                    return (Key)((int)Key::Num1 + (sc - 0x02));

                if (sc >= 0x3B && sc <= 0x44)
                    return (Key)((int)Key::F1 + (sc - 0x3B));

                // Alt+F1..Alt+F10 arrive as their own codes rather than as
                // the function key plus a modifier.
                if (sc >= 0x68 && sc <= 0x71)
                    return (Key)((int)Key::F1 + (sc - 0x68));

                switch (sc)
                {
                case 0x0B: return Key::Num0;
                case 0x01: return Key::Escape;
                case 0x1C: return Key::Enter;
                case 0x0E: return Key::Backspace;
                case 0x0F: return Key::Tab;
                case 0x39: return Key::Space;
                case 0x47: return Key::Home;
                case 0x48: return Key::Up;
                case 0x49: return Key::PageUp;
                case 0x4B: return Key::Left;
                case 0x4D: return Key::Right;
                case 0x4F: return Key::End;
                case 0x50: return Key::Down;
                case 0x51: return Key::PageDown;
                case 0x52: return Key::Insert;
                case 0x53: return Key::Delete;
                case 0x57: return Key::F11;
                case 0x58: return Key::F12;
                case 0x8B: return Key::F11; // Alt+F11
                case 0x8C: return Key::F12; // Alt+F12
                default:   return Key::None;
                }
            }

            // --- Text ------------------------------------------------------

            // Code page 437, high half. The BIOS hands back whatever the
            // active code page produced, and the UI is UTF-8 throughout, so
            // something has to bridge the two. 437 is what a US DOS install
            // runs; a machine on another code page will mistranslate the high
            // half, which is a much smaller problem than dropping it.
            const unsigned short CP437_HIGH[128] = {
                0x00C7, 0x00FC, 0x00E9, 0x00E2, 0x00E4, 0x00E0, 0x00E5, 0x00E7,
                0x00EA, 0x00EB, 0x00E8, 0x00EF, 0x00EE, 0x00EC, 0x00C4, 0x00C5,
                0x00C9, 0x00E6, 0x00C6, 0x00F4, 0x00F6, 0x00F2, 0x00FB, 0x00F9,
                0x00FF, 0x00D6, 0x00DC, 0x00A2, 0x00A3, 0x00A5, 0x20A7, 0x0192,
                0x00E1, 0x00ED, 0x00F3, 0x00FA, 0x00F1, 0x00D1, 0x00AA, 0x00BA,
                0x00BF, 0x2310, 0x00AC, 0x00BD, 0x00BC, 0x00A1, 0x00AB, 0x00BB,
                0x2591, 0x2592, 0x2593, 0x2502, 0x2524, 0x2561, 0x2562, 0x2556,
                0x2555, 0x2563, 0x2551, 0x2557, 0x255D, 0x255C, 0x255B, 0x2510,
                0x2514, 0x2534, 0x252C, 0x251C, 0x2500, 0x253C, 0x255E, 0x255F,
                0x255A, 0x2554, 0x2569, 0x2566, 0x2560, 0x2550, 0x256C, 0x2567,
                0x2568, 0x2564, 0x2565, 0x2559, 0x2558, 0x2552, 0x2553, 0x256B,
                0x256A, 0x2518, 0x250C, 0x2588, 0x2584, 0x258C, 0x2590, 0x2580,
                0x03B1, 0x00DF, 0x0393, 0x03C0, 0x03A3, 0x03C3, 0x00B5, 0x03C4,
                0x03A6, 0x0398, 0x03A9, 0x03B4, 0x221E, 0x03C6, 0x03B5, 0x2229,
                0x2261, 0x00B1, 0x2265, 0x2264, 0x2320, 0x2321, 0x00F7, 0x2248,
                0x00B0, 0x2219, 0x00B7, 0x221A, 0x207F, 0x00B2, 0x25A0, 0x00A0
            };

            void append_utf8(std::string *out, unsigned int cp)
            {
                if (cp < 0x80)
                {
                    *out += (char)cp;
                }
                else if (cp < 0x800)
                {
                    *out += (char)(0xC0 | (cp >> 6));
                    *out += (char)(0x80 | (cp & 0x3F));
                }
                else
                {
                    *out += (char)(0xE0 | (cp >> 12));
                    *out += (char)(0x80 | ((cp >> 6) & 0x3F));
                    *out += (char)(0x80 | (cp & 0x3F));
                }
            }
        } // namespace

        void InputState::poll()
        {
            keys.clear();
            text.clear();
            mouse.wheel_delta = 0;
            resized = false; // the screen mode never changes after startup

            if (!s_initialised)
            {
                mouse_init();
                s_initialised = true;
            }

            const unsigned mods = shift_state();

            // Drain the type-ahead buffer, but not without bound: holding a
            // key while the frame is slow would otherwise let the buffer
            // refill as fast as it is read.
            for (int guard = 0; guard < 32 && key_available(); ++guard)
            {
                const unsigned int ax = read_key();
                const unsigned char ascii = (unsigned char)(ax & 0xFF);
                const unsigned char scan = (unsigned char)(ax >> 8);

                const Key k = translate_scancode(scan);
                if (k != Key::None)
                {
                    KeyEvent e;
                    e.key = k;
                    // The BIOS does not distinguish a typematic repeat from a
                    // fresh press, and nothing in the UI reads this.
                    e.repeat = false;
                    e.mods = mods;
                    keys.push_back(e);
                }

                // AL = 0 or E0h marks a key that produced no character (the
                // gray navigation block, the function keys, Alt combinations).
                // Control characters are the key events above, not text.
                if (ascii >= 0x20 && ascii != 0x7F && ascii != 0xE0)
                {
                    if (ascii < 0x80)
                        append_utf8(&text, ascii);
                    else
                        append_utf8(&text, CP437_HIGH[ascii - 0x80]);
                }
            }

            if (s_mouse_present)
            {
                __dpmi_regs r;

                // Relative motion since the last read, in mickeys. Signed.
                std::memset(&r, 0, sizeof(r));
                r.x.ax = 0x000B;
                if (bios_int(0x33, &r) != -1)
                {
                    s_mouse_x += (short)r.x.cx;
                    s_mouse_y += (short)r.x.dx;
                }

                const int w = gfx::display_width();
                const int h = gfx::display_height();

                if (s_mouse_x < 0) s_mouse_x = 0;
                if (s_mouse_y < 0) s_mouse_y = 0;
                if (w > 0 && s_mouse_x > w - 1) s_mouse_x = w - 1;
                if (h > 0 && s_mouse_y > h - 1) s_mouse_y = h - 1;

                std::memset(&r, 0, sizeof(r));
                r.x.ax = 0x0003; // button status (position ignored, see above)
                if (bios_int(0x33, &r) != -1)
                {
                    mouse.buttons = r.x.bx & 0x07;

                    // CuteMouse returns the wheel counter in BH, which the
                    // stock drivers leave as part of the button mask — hence
                    // reading it only after AX=11h said there is a wheel.
                    if (s_mouse_wheel)
                        mouse.wheel_delta = -(int)(signed char)r.h.bh;
                }
            }

            mouse.x = s_mouse_x;
            mouse.y = s_mouse_y;

            gfx::dos_set_cursor(s_mouse_x, s_mouse_y, s_mouse_present);

            // Click = left released this frame; pressed = left went down.
            mouse.clicked = (s_prev_buttons & 1) && !(mouse.buttons & 1);
            mouse.pressed = !(s_prev_buttons & 1) && (mouse.buttons & 1);

            s_prev_buttons = mouse.buttons;
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
