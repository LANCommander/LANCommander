#include "ui/text_edit.h"
#include "ui/font.h"

namespace launcher
{
    namespace ui
    {

        namespace
        {
            bool is_continuation(unsigned char c)
            {
                return (c & 0xC0) == 0x80;
            }

            int clamp_pos(const std::string &s, int pos)
            {
                if (pos < 0)
                    return 0;
                if (pos > (int)s.size())
                    return (int)s.size();
                return pos;
            }

            // Snap backwards onto a lead byte. Used everywhere an offset could
            // have come from arithmetic rather than from one of the motion
            // helpers.
            int snap_to_boundary(const std::string &s, int pos)
            {
                pos = clamp_pos(s, pos);
                while (pos > 0 && pos < (int)s.size() &&
                       is_continuation((unsigned char)s[pos]))
                    --pos;
                return pos;
            }

            void touch(TextEditState &st)
            {
                st.blink_base_ms = 0; // widget re-stamps this with ticks_ms()
            }

            // Erases the selection and leaves the caret where it began.
            bool delete_selection(std::string &buf, TextEditState &st)
            {
                if (!text_has_selection(st))
                    return false;

                const int b = text_sel_begin(st);
                const int e = text_sel_end(st);

                buf.erase((size_t)b, (size_t)(e - b));
                st.caret = b;
                st.sel_anchor = b;
                return true;
            }
        } // namespace

        // -------------------------------------------------------------------
        // UTF-8 cursor motion
        // -------------------------------------------------------------------

        int text_prev_char(const std::string &s, int pos)
        {
            pos = clamp_pos(s, pos);
            if (pos == 0)
                return 0;

            --pos;
            while (pos > 0 && is_continuation((unsigned char)s[pos]))
                --pos;
            return pos;
        }

        int text_next_char(const std::string &s, int pos)
        {
            pos = clamp_pos(s, pos);
            const int n = (int)s.size();
            if (pos >= n)
                return n;

            ++pos;
            while (pos < n && is_continuation((unsigned char)s[pos]))
                ++pos;
            return pos;
        }

        int text_char_count(const std::string &s, int byte_pos)
        {
            byte_pos = clamp_pos(s, byte_pos);

            int count = 0;
            for (int i = 0; i < byte_pos; ++i)
            {
                if (!is_continuation((unsigned char)s[i]))
                    ++count;
            }
            return count;
        }

        // -------------------------------------------------------------------
        // Selection
        // -------------------------------------------------------------------

        bool text_has_selection(const TextEditState &st)
        {
            return st.caret != st.sel_anchor;
        }

        int text_sel_begin(const TextEditState &st)
        {
            return st.caret < st.sel_anchor ? st.caret : st.sel_anchor;
        }

        int text_sel_end(const TextEditState &st)
        {
            return st.caret > st.sel_anchor ? st.caret : st.sel_anchor;
        }

        void text_clear_selection(TextEditState &st)
        {
            st.sel_anchor = st.caret;
        }

        void text_set_caret(TextEditState &st, int pos, bool extend)
        {
            st.caret = pos;
            if (!extend)
                st.sel_anchor = pos;
        }

        // -------------------------------------------------------------------
        // Editing
        // -------------------------------------------------------------------

        bool text_edit_insert(std::string &buf, int max_len, TextEditState &st,
                              const std::string &utf8)
        {
            if (utf8.empty())
                return false;

            const bool had_selection = delete_selection(buf, st);

            st.caret = snap_to_boundary(buf, st.caret);

            // How many bytes will actually fit. Truncating here rather than
            // per-byte is what stops a paste from leaving half a character at
            // the limit.
            int room = max_len - (int)buf.size();
            if (room <= 0)
            {
                touch(st);
                return had_selection;
            }

            std::string add = utf8;
            if ((int)add.size() > room)
            {
                // Walk back from the cut point to the nearest lead byte. Every
                // byte before a lead byte belongs to a complete sequence, so
                // that offset is always a safe place to truncate. Snapping in
                // the source rather than after erasing matters: at the cut
                // point there is still a byte to inspect.
                int cut = room;
                while (cut > 0 && is_continuation((unsigned char)add[cut]))
                    --cut;
                add.erase((size_t)cut);
            }

            if (add.empty())
            {
                touch(st);
                return had_selection;
            }

            buf.insert((size_t)st.caret, add);
            st.caret += (int)add.size();
            st.sel_anchor = st.caret;
            touch(st);
            return true;
        }

        bool text_edit_key(std::string &buf, int max_len, TextEditState &st,
                           Key k, unsigned mods, bool password,
                           ClipboardGetFn clip_get, ClipboardSetFn clip_set)
        {
            const bool shift = (mods & ModShift) != 0;
            const bool ctrl = (mods & ModCtrl) != 0;

            st.caret = snap_to_boundary(buf, st.caret);
            st.sel_anchor = snap_to_boundary(buf, st.sel_anchor);

            // --- Clipboard ---------------------------------------------------
            if (ctrl && k == Key::A)
            {
                st.sel_anchor = 0;
                st.caret = (int)buf.size();
                return false;
            }

            if (ctrl && (k == Key::C || k == Key::X))
            {
                // A password field must not leak through the clipboard.
                if (password || !text_has_selection(st) || !clip_set)
                    return false;

                const int b = text_sel_begin(st);
                const int e = text_sel_end(st);
                const std::string sel = buf.substr((size_t)b, (size_t)(e - b));

                if (!clip_set(sel.c_str()))
                    return false;

                if (k == Key::X)
                {
                    delete_selection(buf, st);
                    touch(st);
                    return true;
                }
                return false;
            }

            if (ctrl && k == Key::V)
            {
                if (!clip_get)
                    return false;

                std::string pasted;
                if (!clip_get(&pasted) || pasted.empty())
                    return false;

                // A clipboard carrying newlines is being pasted into a
                // single-line field; take the first line rather than
                // embedding a control character.
                const size_t nl = pasted.find_first_of("\r\n");
                if (nl != std::string::npos)
                    pasted.erase(nl);

                return text_edit_insert(buf, max_len, st, pasted);
            }

            // --- Motion ------------------------------------------------------
            if (k == Key::Left)
            {
                if (text_has_selection(st) && !shift)
                    text_set_caret(st, text_sel_begin(st), false);
                else
                    text_set_caret(st, text_prev_char(buf, st.caret), shift);
                return false;
            }

            if (k == Key::Right)
            {
                if (text_has_selection(st) && !shift)
                    text_set_caret(st, text_sel_end(st), false);
                else
                    text_set_caret(st, text_next_char(buf, st.caret), shift);
                return false;
            }

            if (k == Key::Home)
            {
                text_set_caret(st, 0, shift);
                return false;
            }

            if (k == Key::End)
            {
                text_set_caret(st, (int)buf.size(), shift);
                return false;
            }

            // --- Deletion ----------------------------------------------------
            if (k == Key::Backspace)
            {
                if (delete_selection(buf, st))
                {
                    touch(st);
                    return true;
                }
                if (st.caret == 0)
                    return false;

                const int prev = text_prev_char(buf, st.caret);
                buf.erase((size_t)prev, (size_t)(st.caret - prev));
                st.caret = prev;
                st.sel_anchor = prev;
                touch(st);
                return true;
            }

            if (k == Key::Delete)
            {
                if (delete_selection(buf, st))
                {
                    touch(st);
                    return true;
                }
                if (st.caret >= (int)buf.size())
                    return false;

                const int next = text_next_char(buf, st.caret);
                buf.erase((size_t)st.caret, (size_t)(next - st.caret));
                touch(st);
                return true;
            }

            return false;
        }

        // -------------------------------------------------------------------
        // Hit testing and scrolling
        // -------------------------------------------------------------------

        std::string text_edit_display(const std::string &buf, bool password)
        {
            if (!password)
                return buf;

            // One asterisk per character, not per byte, or a multi-byte
            // password would render wider than it is long.
            return std::string((size_t)text_char_count(buf, (int)buf.size()), '*');
        }

        int text_edit_caret_px(const std::string &buf, const TextEditState &st,
                               bool password)
        {
            const int caret = clamp_pos(buf, st.caret);

            if (password)
            {
                const std::string stars((size_t)text_char_count(buf, caret), '*');
                return font_measure(stars.c_str(), FontSize::Body);
            }

            return font_measure(buf.substr(0, (size_t)caret).c_str(), FontSize::Body);
        }

        int text_edit_hit(const std::string &buf, int px, bool password)
        {
            if (px <= 0)
                return 0;

            const std::string display = text_edit_display(buf, password);
            if (display.empty())
                return 0;

            int fitted_w = 0;
            int fitted = font_fit(display.c_str(), px, &fitted_w, FontSize::Body);
            if (fitted < 0)
                fitted = 0;
            if (fitted > (int)display.size())
                fitted = (int)display.size();

            // font_fit stops at the last boundary that fits. If the pointer is
            // past the midpoint of the next glyph, the user meant the far side
            // of it — this is what makes clicking feel like it lands where you
            // aimed rather than always one character short.
            if (fitted < (int)display.size())
            {
                const int next = password ? fitted + 1
                                          : text_next_char(display, fitted);
                const int next_w =
                    font_measure(display.substr(0, (size_t)next).c_str(),
                                 FontSize::Body);
                if (px >= (fitted_w + next_w) / 2)
                    fitted = next;
            }

            if (!password)
                return snap_to_boundary(buf, fitted);

            // In a password field the display index counts characters, so it
            // has to be walked back into a byte offset in the real buffer.
            int bytes = 0;
            for (int i = 0; i < fitted; ++i)
                bytes = text_next_char(buf, bytes);
            return bytes;
        }

        void text_edit_scroll_to_caret(const std::string &buf, TextEditState &st,
                                       int field_w, bool password)
        {
            if (field_w < 1)
            {
                st.scroll_x = 0;
                return;
            }

            const int caret_px = text_edit_caret_px(buf, st, password);

            if (caret_px - st.scroll_x > field_w)
                st.scroll_x = caret_px - field_w;
            if (caret_px - st.scroll_x < 0)
                st.scroll_x = caret_px;

            // Never leave blank space on the right while text is scrolled off
            // to the left: deleting the tail of a long value should pull the
            // rest back into view.
            const std::string display = text_edit_display(buf, password);
            const int total_w = font_measure(display.c_str(), FontSize::Body);
            int max_scroll = total_w - field_w;
            if (max_scroll < 0)
                max_scroll = 0;

            if (st.scroll_x > max_scroll)
                st.scroll_x = max_scroll;
            if (st.scroll_x < 0)
                st.scroll_x = 0;
        }

    } // namespace ui
} // namespace launcher
