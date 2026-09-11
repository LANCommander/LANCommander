// widgets_text.cpp — the text box.
//
// Split out of widgets.cpp when it grew a caret, a selection and a clipboard.
// All of the model lives in text_edit.cpp; what is left here is drawing, hit
// testing against the field rectangle, and deciding which frame events reach
// the model.

#include "ui/widgets.h"
#include "ui/text_edit.h"
#include "ui/clipboard.h"
#include "ui/theme.h"

namespace launcher
{
    namespace ui
    {

        namespace
        {
            // Inset of the text from the field border, on both sides.
            const int TEXT_PAD = 4;

            // How long the caret stays solid after a keystroke. Blinking
            // straight through typing makes a field feel like it is dropping
            // input even when it is not.
            const unsigned BLINK_HOLD_MS = 500;
        } // namespace

        TextInputState text_input(gfx::Surface *s, int x, int y, int w, int h,
                                  std::string &buffer, int max_len,
                                  bool focused, const InputState &input,
                                  TextEditState &edit,
                                  bool password)
        {
            TextInputState state;
            state.focused = focused;
            state.submitted = false;

            const int th = text_height();
            const int tx = x + TEXT_PAD;
            const int ty = y + (h - th) / 2;
            const int field_w = w - TEXT_PAD * 2;

            // ---------------------------------------------------------------
            // Input
            // ---------------------------------------------------------------
            const bool over = (input.mouse.x >= x && input.mouse.x < x + w &&
                               input.mouse.y >= y && input.mouse.y < y + h);

            if (focused)
            {
                // Click positions the caret; dragging from there selects.
                if (over && input.mouse.pressed)
                {
                    const int px = input.mouse.x - tx + edit.scroll_x;
                    text_set_caret(edit, text_edit_hit(buffer, px, password), false);
                    edit.dragging = true;
                }

                if (edit.dragging)
                {
                    if (input.mouse.buttons & 1)
                    {
                        const int px = input.mouse.x - tx + edit.scroll_x;
                        // extend == true: the anchor stays where the drag began.
                        text_set_caret(edit, text_edit_hit(buffer, px, password), true);
                    }
                    else
                    {
                        edit.dragging = false;
                    }
                }

                bool consumed_ctrl = false;

                for (size_t i = 0; i < input.keys.size(); ++i)
                {
                    const Key k = input.keys[i].key;
                    const unsigned mods = input.keys[i].mods;

                    if (k == Key::Enter || k == Key::KeypadEnter)
                    {
                        state.submitted = true;
                        continue;
                    }

                    // Tab is not consumed — the screen handles focus movement.
                    if (k == Key::Tab)
                        continue;

                    if (mods & ModCtrl)
                        consumed_ctrl = true;

                    if (text_edit_key(buffer, max_len, edit, k, mods, password,
                                      clipboard_get, clipboard_set))
                        edit.blink_base_ms = gfx::ticks_ms();
                }

                // Committed characters arrive separately from key events, so
                // layout and IME stay the platform layer's problem.
                //
                // Skipped entirely on a frame that handled a Ctrl chord: a
                // backend that also emits a character for Ctrl+V would
                // otherwise append a literal "v" after the pasted text.
                if (!consumed_ctrl && !input.text.empty())
                {
                    if (text_edit_insert(buffer, max_len, edit, input.text))
                        edit.blink_base_ms = gfx::ticks_ms();
                }

                text_edit_scroll_to_caret(buffer, edit, field_w, password);
            }
            else
            {
                edit.dragging = false;
            }

            // ---------------------------------------------------------------
            // Draw
            // ---------------------------------------------------------------
            const gfx::Color border = focused ? theme().input_focus : theme().input_border;

            gfx::fill_rect(s, gfx::rect(x, y, w, h), theme().input_bg);
            gfx::draw_rect(s, gfx::rect(x, y, w, h), border);

            const std::string display = text_edit_display(buffer, password);

            // Clip to the inside of the field so a value longer than the box
            // scrolls rather than spilling over the border and into whatever
            // is drawn next to it.
            gfx::push_clip(s, gfx::rect(x + 1, y + 1, w - 2, h - 2));

            if (focused && text_has_selection(edit))
            {
                TextEditState a = edit;
                a.caret = text_sel_begin(edit);
                TextEditState b = edit;
                b.caret = text_sel_end(edit);

                const int x0 = text_edit_caret_px(buffer, a, password) - edit.scroll_x;
                const int x1 = text_edit_caret_px(buffer, b, password) - edit.scroll_x;

                gfx::fill_rect(s, gfx::rect(tx + x0, y + 2, x1 - x0, h - 4),
                               theme().primary);
            }

            draw_text(s, tx - edit.scroll_x, ty, theme().text, display.c_str());

            if (focused)
            {
                const unsigned now = gfx::ticks_ms();
                const bool solid = (now - edit.blink_base_ms) < BLINK_HOLD_MS;

                if (solid || (now / 500) % 2 == 0)
                {
                    TextEditState c = edit;
                    const int cx = tx + text_edit_caret_px(buffer, c, password) - edit.scroll_x;
                    gfx::vline(s, cx, ty, th, theme().text);
                }
            }

            gfx::pop_clip(s);

            return state;
        }

    } // namespace ui
} // namespace launcher
