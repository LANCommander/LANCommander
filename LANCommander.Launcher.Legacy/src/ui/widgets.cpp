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
                    {
                        if (!buffer.empty())
                            buffer.erase(buffer.size() - 1);
                    }
                    else if (scancode == KEY_TAB)
                    {
                        // Don't consume — let the screen handle tab
                    }
                    else if (ascii >= 32 && ascii < 127)
                    {
                        if (static_cast<int>(buffer.size()) < max_len)
                            buffer += static_cast<char>(ascii);
                    }
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

        // ---------------------------------------------------------------------------
        // Scrollbar
        // ---------------------------------------------------------------------------

        // Drag state persists across frames.
        static bool s_sb_dragging = false;
        static int  s_sb_drag_offset = 0; // mouse offset from thumb top

        void scrollbar(BITMAP *bmp, int x, int y, int h,
                       int content_h, int viewport_h, int &scroll_y,
                       const InputState &input)
        {
            if (content_h <= viewport_h)
                return; // no scrollbar needed

            int track_w = 12;
            int max_scroll = content_h - viewport_h;
            if (max_scroll < 1) max_scroll = 1;

            // Thumb size and position
            int thumb_h = h * viewport_h / content_h;
            if (thumb_h < 24) thumb_h = 24;
            if (thumb_h > h) thumb_h = h;

            int thumb_y = y;
            if (max_scroll > 0)
                thumb_y = y + (h - thumb_h) * scroll_y / max_scroll;

            // --- Interaction ---
            bool in_track = (input.mouse.x >= x && input.mouse.x < x + track_w &&
                             input.mouse.y >= y && input.mouse.y < y + h);
            bool in_thumb = (input.mouse.x >= x && input.mouse.x < x + track_w &&
                             input.mouse.y >= thumb_y && input.mouse.y < thumb_y + thumb_h);

            // Start drag on thumb press
            if (in_thumb && input.mouse.pressed)
            {
                s_sb_dragging = true;
                s_sb_drag_offset = input.mouse.y - thumb_y;
            }

            // Click on track (outside thumb) — jump to that position
            if (in_track && !in_thumb && input.mouse.pressed)
            {
                int target_thumb_y = input.mouse.y - thumb_h / 2;
                if (target_thumb_y < y) target_thumb_y = y;
                if (target_thumb_y > y + h - thumb_h) target_thumb_y = y + h - thumb_h;
                int track_range = h - thumb_h;
                scroll_y = (track_range > 0)
                    ? (target_thumb_y - y) * max_scroll / track_range
                    : 0;
                s_sb_dragging = true;
                s_sb_drag_offset = thumb_h / 2;
            }

            // Continue drag while mouse button is held
            if (s_sb_dragging)
            {
                if (input.mouse.buttons & 1)
                {
                    int target_thumb_y = input.mouse.y - s_sb_drag_offset;
                    if (target_thumb_y < y) target_thumb_y = y;
                    if (target_thumb_y > y + h - thumb_h) target_thumb_y = y + h - thumb_h;
                    int track_range = h - thumb_h;
                    scroll_y = (track_range > 0)
                        ? (target_thumb_y - y) * max_scroll / track_range
                        : 0;
                }
                else
                {
                    s_sb_dragging = false;
                }
            }

            // Clamp
            if (scroll_y < 0) scroll_y = 0;
            if (scroll_y > max_scroll) scroll_y = max_scroll;

            // Recalculate thumb_y after possible scroll change
            thumb_y = y + (h - thumb_h) * scroll_y / max_scroll;

            // --- Draw ---
            bool hovered = in_track || s_sb_dragging;

            // Track
            rectfill(bmp, x, y, x + track_w - 1, y + h - 1, theme().panel);

            // Thumb
            int thumb_color = s_sb_dragging ? theme().text
                            : hovered       ? theme().text_dim
                            :                 theme().text_disabled;
            rectfill(bmp, x, thumb_y, x + track_w - 1, thumb_y + thumb_h - 1,
                     thumb_color);
        }

        // ---------------------------------------------------------------------------
        // Modal backdrop
        // ---------------------------------------------------------------------------

        void modal_backdrop(BITMAP *bmp, int sw, int sh)
        {
            drawing_mode(DRAW_MODE_TRANS, NULL, 0, 0);
            set_trans_blender(0, 0, 0, 170);
            rectfill(bmp, 0, 0, sw - 1, sh - 1, makecol(0, 0, 0));
            drawing_mode(DRAW_MODE_SOLID, NULL, 0, 0);
        }

        // ---------------------------------------------------------------------------
        // Checkbox
        // ---------------------------------------------------------------------------

        bool checkbox(BITMAP *bmp, int x, int y, const char *label_text,
                      bool &checked, const InputState &input)
        {
            int cb_size = 16;
            int th = text_height();
            int row_h = (th > cb_size) ? th : cb_size;
            int cb_y = y + (row_h - cb_size) / 2;

            rect(bmp, x, cb_y, x + cb_size - 1, cb_y + cb_size - 1,
                 theme().input_border);

            if (checked)
                rectfill(bmp, x + 3, cb_y + 3,
                         x + cb_size - 4, cb_y + cb_size - 4,
                         theme().primary);

            int lx = x + cb_size + 8;
            draw_text(bmp, lx, y + (row_h - th) / 2, theme().text, label_text);

            int hit_w = cb_size + 8 + text_width(label_text);
            bool hovered = (input.mouse.x >= x && input.mouse.x < x + hit_w &&
                            input.mouse.y >= y && input.mouse.y < y + row_h);
            if (hovered && input.mouse.clicked)
            {
                checked = !checked;
                return true;
            }
            return false;
        }

    } // namespace ui
} // namespace launcher
