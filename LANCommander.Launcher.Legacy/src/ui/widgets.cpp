#include "ui/widgets.h"
#include "ui/theme.h"

namespace launcher
{
    namespace ui
    {

        namespace
        {
            // Remove one whole UTF-8 sequence from the end of `s`.
            // Plain erase(size()-1) would leave a truncated multi-byte
            // sequence behind once text arrives from SDL_EVENT_TEXT_INPUT.
            void utf8_pop_back(std::string &s)
            {
                if (s.empty())
                    return;

                size_t i = s.size();
                while (i > 0)
                {
                    --i;
                    // Continuation bytes are 10xxxxxx; stop at the lead byte.
                    if (((unsigned char)s[i] & 0xC0) != 0x80)
                        break;
                }
                s.erase(i);
            }
        } // namespace

        // ---------------------------------------------------------------------------
        // Button
        // ---------------------------------------------------------------------------

        ButtonState button(gfx::Surface *s, int x, int y, int w, int h, const char *label,
                           const InputState &input)
        {
            ButtonState state;
            state.hovered = (input.mouse.x >= x && input.mouse.x < x + w &&
                             input.mouse.y >= y && input.mouse.y < y + h);
            state.clicked = state.hovered && input.mouse.clicked;

            gfx::Color bg = state.hovered ? theme().primary_hover : theme().primary;
            gfx::fill_rect(s, gfx::rect(x, y, w, h), bg);

            int tx = x + (w - text_width(label)) / 2;
            int ty = y + (h - text_height()) / 2;
            draw_text(s, tx, ty, theme().text_bright, label);

            return state;
        }

        // ---------------------------------------------------------------------------
        // Text Input
        // ---------------------------------------------------------------------------

        TextInputState text_input(gfx::Surface *s, int x, int y, int w, int h,
                                  std::string &buffer, int max_len,
                                  bool focused, const InputState &input,
                                  bool password)
        {
            TextInputState state;
            state.focused = focused;
            state.submitted = false;

            gfx::Color border = focused ? theme().input_focus : theme().input_border;

            gfx::fill_rect(s, gfx::rect(x, y, w, h), theme().input_bg);
            gfx::draw_rect(s, gfx::rect(x, y, w, h), border);

            // Draw text (or asterisks for password fields)
            std::string display = buffer;

            if (password)
                display = std::string(buffer.size(), '*');

            int tx = x + 4;
            int ty = y + (h - text_height()) / 2;

            draw_text(s, tx, ty, theme().text, display.c_str());

            // Blinking cursor
            if (focused)
            {
                int cursor_x = tx + text_width(display.c_str());
                if ((gfx::ticks_ms() / 500) % 2 == 0)
                    gfx::vline(s, cursor_x + 1, ty, text_height(), theme().text);
            }

            // Handle keyboard input when focused — process ALL keys from this frame
            if (focused)
            {
                for (size_t i = 0; i < input.keys.size(); ++i)
                {
                    Key k = input.keys[i].key;

                    if (k == Key::Enter || k == Key::KeypadEnter)
                        state.submitted = true;
                    else if (k == Key::Backspace)
                        utf8_pop_back(buffer);
                    // Tab is not consumed — the screen handles focus movement.
                }

                // Committed characters arrive separately from key events, so
                // layout and IME are the platform layer's problem, not ours.
                for (size_t i = 0; i < input.text.size(); ++i)
                {
                    if (static_cast<int>(buffer.size()) < max_len)
                        buffer += input.text[i];
                }
            }


            return state;
        }

        // ---------------------------------------------------------------------------
        // Label
        // ---------------------------------------------------------------------------

        void label(gfx::Surface *s, int x, int y, gfx::Color color, const char *text)
        {
            draw_text(s, x, y, color, text);
        }

        // ---------------------------------------------------------------------------
        // Panel
        // ---------------------------------------------------------------------------

        void panel(gfx::Surface *s, int x, int y, int w, int h, gfx::Color color)
        {
            gfx::fill_rect(s, gfx::rect(x, y, w, h), color);
        }

        // ---------------------------------------------------------------------------
        // Divider
        // ---------------------------------------------------------------------------

        void divider(gfx::Surface *s, int x, int y, int w)
        {
            gfx::hline(s, x, y, w, theme().divider);
        }

        // ---------------------------------------------------------------------------
        // Scrollbar
        // ---------------------------------------------------------------------------

        // Drag state persists across frames.
        static bool s_sb_dragging = false;
        static int  s_sb_drag_offset = 0; // mouse offset from thumb top

        void scrollbar(gfx::Surface *s, int x, int y, int h,
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
            gfx::fill_rect(s, gfx::rect(x, y, track_w, h), theme().panel);

            // Thumb
            gfx::Color thumb_color = s_sb_dragging ? theme().text
                                   : hovered       ? theme().text_dim
                                   :                 theme().text_disabled;
            gfx::fill_rect(s, gfx::rect(x, thumb_y, track_w, thumb_h), thumb_color);
        }

        // ---------------------------------------------------------------------------
        // Modal backdrop
        // ---------------------------------------------------------------------------

        void modal_backdrop(gfx::Surface *s, int sw, int sh)
        {
            gfx::fill_rect_alpha(s, gfx::rect(0, 0, sw, sh), gfx::rgba(0, 0, 0, 170));
        }

        // ---------------------------------------------------------------------------
        // Checkbox
        // ---------------------------------------------------------------------------

        bool checkbox(gfx::Surface *s, int x, int y, const char *label_text,
                      bool &checked, const InputState &input)
        {
            int cb_size = 16;
            int th = text_height();
            int row_h = (th > cb_size) ? th : cb_size;
            int cb_y = y + (row_h - cb_size) / 2;

            gfx::draw_rect(s, gfx::rect(x, cb_y, cb_size, cb_size), theme().input_border);

            if (checked)
                gfx::fill_rect(s, gfx::rect(x + 3, cb_y + 3, cb_size - 6, cb_size - 6),
                               theme().primary);

            int lx = x + cb_size + 8;
            draw_text(s, lx, y + (row_h - th) / 2, theme().text, label_text);

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
