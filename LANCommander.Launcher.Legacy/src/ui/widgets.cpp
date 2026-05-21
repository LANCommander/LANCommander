#include "ui/widgets.h"
#include "ui/theme.h"

#include <allegro.h>

namespace launcher
{
    namespace ui
    {

        // ---------------------------------------------------------------------------
        // Button
        // ---------------------------------------------------------------------------

        ButtonState button(BITMAP *bmp, int x, int y, int w, int h, const char *label,
                           const InputState &input)
        {
            ButtonState state;
            state.hovered = (input.mouse.x >= x && input.mouse.x < x + w &&
                             input.mouse.y >= y && input.mouse.y < y + h);
            state.clicked = state.hovered && input.mouse.clicked;

            int bg = state.hovered ? theme().primary_hover : theme().primary;
            rectfill(bmp, x, y, x + w - 1, y + h - 1, bg);

            int tx = x + (w - text_width(label)) / 2;
            int ty = y + (h - text_height()) / 2;
            draw_text(bmp, tx, ty, theme().text_bright, label);

            return state;
        }

        // ---------------------------------------------------------------------------
        // Text Input
        // ---------------------------------------------------------------------------

        TextInputState text_input(BITMAP *bmp, int x, int y, int w, int h,
                                  std::string &buffer, int max_len,
                                  bool focused, const InputState &input,
                                  bool password)
        {
            TextInputState state;
            state.focused = focused;
            state.submitted = false;

            int border = focused ? theme().input_focus : theme().input_border;

            rectfill(bmp, x, y, x + w - 1, y + h - 1, theme().input_bg);
            rect(bmp, x, y, x + w - 1, y + h - 1, border);

            // Draw text (or asterisks for password fields)
            std::string display = buffer;

            if (password)
                display = std::string(buffer.size(), '*');

            int tx = x + 4;
            int ty = y + (h - text_height()) / 2;

            draw_text(bmp, tx, ty, theme().text, display.c_str());

            // Blinking cursor
            if (focused)
            {
                int cursor_x = tx + text_width(display.c_str());
                if ((retrace_count / 30) % 2 == 0)
                    vline(bmp, cursor_x + 1, ty, ty + text_height() - 1, theme().text);
            }

            // Handle keyboard input when focused — process ALL keys from this frame
            if (focused)
            {
                for (size_t i = 0; i < input.keys.size(); ++i)
                {
                    int scancode = input.keys[i].scancode;
                    int ascii = input.keys[i].ascii;

                    if (scancode == KEY_ENTER || scancode == KEY_ENTER_PAD)
                        state.submitted = true;
                    else if (scancode == KEY_BACKSPACE)
                        if (!buffer.empty())
                            buffer.erase(buffer.size() - 1);
                    else if (scancode == KEY_TAB)
                    {
                        // Don't consume — let the screen handle tab
                    }
                    else if (ascii >= 32 && ascii < 127)
                        if (static_cast<int>(buffer.size()) < max_len)
                            buffer += static_cast<char>(ascii);
                }
            }

            return state;
        }

        // ---------------------------------------------------------------------------
        // Label
        // ---------------------------------------------------------------------------

        void label(BITMAP *bmp, int x, int y, int color, const char *text)
        {
            draw_text(bmp, x, y, color, text);
        }

        // ---------------------------------------------------------------------------
        // Panel
        // ---------------------------------------------------------------------------

        void panel(BITMAP *bmp, int x, int y, int w, int h, int color)
        {
            rectfill(bmp, x, y, x + w - 1, y + h - 1, color);
        }

        // ---------------------------------------------------------------------------
        // Divider
        // ---------------------------------------------------------------------------

        void divider(BITMAP *bmp, int x, int y, int w)
        {
            hline(bmp, x, y, x + w - 1, theme().divider);
        }

    } // namespace ui
} // namespace launcher
