#ifndef LAUNCHER_UI_TEXT_EDIT_H
#define LAUNCHER_UI_TEXT_EDIT_H

#include <string>

#include "input.h"

// The editing model behind a text box: caret, selection, clipboard, and the
// horizontal scroll that keeps the caret visible.
//
// Split out of the widget for the same reason chrome_geometry was split out of
// the chrome: this is fiddly, UTF-8 aware, and full of boundary cases that are
// miserable to check by clicking around a window. Nothing here draws. The only
// thing it borrows from the rest of the UI is font metrics, which in tests
// come from font_fake.cpp.
//
// Offsets are byte offsets into the buffer, and every one of them is required
// to land on a UTF-8 sequence boundary. A caret that can sit inside a
// multi-byte sequence turns one wrong keystroke into a corrupted string.

namespace launcher
{
    namespace ui
    {

        struct TextEditState
        {
            int caret;       // byte offset of the insertion point
            int sel_anchor;  // byte offset; equal to caret means no selection
            int scroll_x;    // pixels the field is scrolled by
            bool dragging;   // a click-drag selection is in progress

            // ticks_ms() at the last edit. The caret is drawn solid rather
            // than blinking for a moment after each keystroke, which is what
            // every native text box does and what makes typing feel steady.
            unsigned blink_base_ms;

            TextEditState()
                : caret(0), sel_anchor(0), scroll_x(0),
                  dragging(false), blink_base_ms(0) {}
        };

        // --- UTF-8 cursor motion ---
        //
        // Both clamp into [0, size] and always land on a lead byte.

        int text_prev_char(const std::string &s, int pos);
        int text_next_char(const std::string &s, int pos);

        // Number of characters (not bytes) in s[0, byte_pos).
        int text_char_count(const std::string &s, int byte_pos);

        // --- Selection ---

        bool text_has_selection(const TextEditState &st);
        int text_sel_begin(const TextEditState &st);
        int text_sel_end(const TextEditState &st);

        // Collapses the selection to the caret without moving it.
        void text_clear_selection(TextEditState &st);

        // Puts the caret at `pos`, extending the selection when `extend` is
        // set and collapsing it otherwise.
        void text_set_caret(TextEditState &st, int pos, bool extend);

        // --- Editing ---
        //
        // The clipboard arrives as function pointers rather than a direct call
        // into clipboard.h, so the tests can drive paste and copy without a
        // platform backend and without a global.

        typedef bool (*ClipboardGetFn)(std::string *out);
        typedef bool (*ClipboardSetFn)(const char *utf8);

        // Applies one key event. Returns true when the buffer changed.
        //
        // `password` suppresses copy and cut: the contents of a password field
        // must not be reachable through the clipboard.
        bool text_edit_key(std::string &buf, int max_len, TextEditState &st,
                           Key k, unsigned mods, bool password,
                           ClipboardGetFn clip_get, ClipboardSetFn clip_set);

        // Inserts committed text, replacing any selection. Truncates at
        // `max_len` on a UTF-8 boundary rather than mid-sequence.
        bool text_edit_insert(std::string &buf, int max_len, TextEditState &st,
                              const std::string &utf8);

        // --- Hit testing and scrolling ---
        //
        // Both need font metrics, so they live here rather than in the widget,
        // and both take the field-relative pixel position with scroll already
        // applied by the caller.

        // Byte offset of the character boundary nearest `px`.
        int text_edit_hit(const std::string &buf, int px, bool password);

        // Adjusts scroll_x so the caret is inside [0, field_w).
        void text_edit_scroll_to_caret(const std::string &buf, TextEditState &st,
                                       int field_w, bool password);

        // Pixel x of the caret within the text, before scrolling.
        int text_edit_caret_px(const std::string &buf, const TextEditState &st,
                               bool password);

        // The string actually rendered: the buffer, or one asterisk per
        // character for a password field.
        std::string text_edit_display(const std::string &buf, bool password);

    } // namespace ui
} // namespace launcher

#endif // LAUNCHER_UI_TEXT_EDIT_H
