#include "test_main.h"

#include "ui/text_edit.h"
#include "ui/clipboard.h"

using namespace launcher::ui;

namespace launcher
{
    namespace ui
    {
        extern const int FAKE_ADVANCE; // font_fake.cpp
    }
}

namespace
{
    // "héllo" — the e-acute is two bytes, so byte offsets and character
    // offsets disagree from index 1 onward. That disagreement is the whole
    // reason text_prev_char / text_next_char exist.
    const char *const UTF8 = "h\xC3\xA9llo";      // 6 bytes, 5 characters
    const char *const EURO = "\xE2\x82\xAC";      // 3 bytes, 1 character

    TextEditState at(int caret, int anchor)
    {
        TextEditState st;
        st.caret = caret;
        st.sel_anchor = anchor;
        return st;
    }

    // The real clipboard backend linked into the tests is clipboard_null.cpp,
    // which keeps a process-local string. That is enough to drive copy, cut
    // and paste end to end without a display server.
    void clip_reset()
    {
        clipboard_set("");
    }

    void test_utf8_motion()
    {
        const std::string s(UTF8);
        CHECK_INT(s.size(), 6);

        // Forward across the multi-byte character skips both of its bytes.
        CHECK_INT(text_next_char(s, 0), 1);
        CHECK_INT(text_next_char(s, 1), 3);
        CHECK_INT(text_next_char(s, 3), 4);

        // Backward lands on the lead byte, never between the two.
        CHECK_INT(text_prev_char(s, 3), 1);
        CHECK_INT(text_prev_char(s, 1), 0);

        // Clamping at both ends.
        CHECK_INT(text_prev_char(s, 0), 0);
        CHECK_INT(text_next_char(s, 6), 6);
        CHECK_INT(text_next_char(s, 99), 6);
        CHECK_INT(text_prev_char(s, -5), 0);

        // Starting from inside a sequence still lands on a boundary.
        CHECK_INT(text_prev_char(s, 2), 1);

        // Three-byte characters too.
        const std::string e(EURO);
        CHECK_INT(text_next_char(e, 0), 3);
        CHECK_INT(text_prev_char(e, 3), 0);

        CHECK_INT(text_char_count(s, 6), 5);
        CHECK_INT(text_char_count(s, 3), 2);
        CHECK_INT(text_char_count(s, 0), 0);
    }

    void test_navigation()
    {
        std::string buf(UTF8);
        TextEditState st;

        // Right from 0 crosses one character at a time, not one byte.
        text_edit_key(buf, 64, st, Key::Right, ModNone, false, NULL, NULL);
        CHECK_INT(st.caret, 1);
        text_edit_key(buf, 64, st, Key::Right, ModNone, false, NULL, NULL);
        CHECK_INT(st.caret, 3);
        CHECK(!text_has_selection(st));

        // End / Home.
        text_edit_key(buf, 64, st, Key::End, ModNone, false, NULL, NULL);
        CHECK_INT(st.caret, 6);
        text_edit_key(buf, 64, st, Key::Home, ModNone, false, NULL, NULL);
        CHECK_INT(st.caret, 0);

        // Motion never reports a buffer change.
        CHECK(!text_edit_key(buf, 64, st, Key::Right, ModNone, false, NULL, NULL));
        CHECK_EQ(buf, std::string(UTF8));
    }

    void test_selection()
    {
        std::string buf("abcdef");

        // Shift+Right builds a selection anchored where it started.
        TextEditState st;
        text_edit_key(buf, 64, st, Key::Right, ModShift, false, NULL, NULL);
        text_edit_key(buf, 64, st, Key::Right, ModShift, false, NULL, NULL);
        CHECK(text_has_selection(st));
        CHECK_INT(text_sel_begin(st), 0);
        CHECK_INT(text_sel_end(st), 2);

        // A plain arrow collapses to the correct END of the selection, rather
        // than moving one character from the caret. Right collapses right...
        text_edit_key(buf, 64, st, Key::Right, ModNone, false, NULL, NULL);
        CHECK(!text_has_selection(st));
        CHECK_INT(st.caret, 2);

        // ...and Left collapses left.
        TextEditState st2 = at(4, 2);
        text_edit_key(buf, 64, st2, Key::Left, ModNone, false, NULL, NULL);
        CHECK(!text_has_selection(st2));
        CHECK_INT(st2.caret, 2);

        // A backwards selection (caret before anchor) reports the same range.
        TextEditState back = at(1, 4);
        CHECK_INT(text_sel_begin(back), 1);
        CHECK_INT(text_sel_end(back), 4);

        // Shift+Home from the middle selects back to the start.
        TextEditState st3 = at(3, 3);
        text_edit_key(buf, 64, st3, Key::Home, ModShift, false, NULL, NULL);
        CHECK_INT(text_sel_begin(st3), 0);
        CHECK_INT(text_sel_end(st3), 3);

        // Ctrl+A selects everything.
        TextEditState st4;
        text_edit_key(buf, 64, st4, Key::A, ModCtrl, false, NULL, NULL);
        CHECK_INT(text_sel_begin(st4), 0);
        CHECK_INT(text_sel_end(st4), 6);
    }

    void test_deletion()
    {
        // Backspace removes a whole multi-byte character.
        {
            std::string buf(UTF8);
            TextEditState st = at(3, 3);
            CHECK(text_edit_key(buf, 64, st, Key::Backspace, ModNone, false, NULL, NULL));
            CHECK_EQ(buf, std::string("hllo"));
            CHECK_INT(st.caret, 1);
        }

        // Delete removes the character in front, also whole.
        {
            std::string buf(UTF8);
            TextEditState st = at(1, 1);
            CHECK(text_edit_key(buf, 64, st, Key::Delete, ModNone, false, NULL, NULL));
            CHECK_EQ(buf, std::string("hllo"));
            CHECK_INT(st.caret, 1);
        }

        // Both are no-ops at the ends, and report no change.
        {
            std::string buf("abc");
            TextEditState st;
            CHECK(!text_edit_key(buf, 64, st, Key::Backspace, ModNone, false, NULL, NULL));
            CHECK_EQ(buf, std::string("abc"));

            TextEditState end = at(3, 3);
            CHECK(!text_edit_key(buf, 64, end, Key::Delete, ModNone, false, NULL, NULL));
            CHECK_EQ(buf, std::string("abc"));
        }

        // Either key deletes a selection rather than a single character.
        {
            std::string buf("abcdef");
            TextEditState st = at(1, 4);
            CHECK(text_edit_key(buf, 64, st, Key::Backspace, ModNone, false, NULL, NULL));
            CHECK_EQ(buf, std::string("aef"));
            CHECK_INT(st.caret, 1);
            CHECK(!text_has_selection(st));
        }
        {
            std::string buf("abcdef");
            TextEditState st = at(4, 1);
            CHECK(text_edit_key(buf, 64, st, Key::Delete, ModNone, false, NULL, NULL));
            CHECK_EQ(buf, std::string("aef"));
            CHECK_INT(st.caret, 1);
        }
    }

    void test_insert()
    {
        // Typing replaces a selection.
        {
            std::string buf("abcdef");
            TextEditState st = at(1, 4);
            CHECK(text_edit_insert(buf, 64, st, "X"));
            CHECK_EQ(buf, std::string("aXef"));
            CHECK_INT(st.caret, 2);
            CHECK(!text_has_selection(st));
        }

        // Insertion at the caret, not the end.
        {
            std::string buf("ace");
            TextEditState st = at(1, 1);
            text_edit_insert(buf, 64, st, "b");
            CHECK_EQ(buf, std::string("abce"));
            CHECK_INT(st.caret, 2);
        }

        // max_len is a byte cap and is honoured.
        {
            std::string buf("abcd");
            TextEditState st = at(4, 4);
            text_edit_insert(buf, 4, st, "e");
            CHECK_EQ(buf, std::string("abcd"));
        }

        // Truncating at max_len must not split a multi-byte character: with
        // one byte of room, a 2-byte character inserts nothing at all rather
        // than a lone lead byte.
        {
            std::string buf("abc");
            TextEditState st = at(3, 3);
            text_edit_insert(buf, 4, st, "\xC3\xA9");
            CHECK_EQ(buf, std::string("abc"));
        }

        // With two bytes of room it fits whole.
        {
            std::string buf("abc");
            TextEditState st = at(3, 3);
            text_edit_insert(buf, 5, st, "\xC3\xA9");
            CHECK_EQ(buf, std::string("abc\xC3\xA9"));
        }

        // A longer paste is cut at a character boundary, never mid-sequence.
        {
            std::string buf;
            TextEditState st;
            text_edit_insert(buf, 5, st, "\xC3\xA9\xC3\xA9\xC3\xA9"); // 3 chars, 6 bytes
            CHECK_INT(buf.size(), 4);                                  // 2 whole chars
            CHECK_EQ(buf, std::string("\xC3\xA9\xC3\xA9"));
        }

        // Empty insert changes nothing.
        {
            std::string buf("abc");
            TextEditState st;
            CHECK(!text_edit_insert(buf, 64, st, ""));
        }
    }

    void test_clipboard()
    {
        clip_reset();

        // Copy leaves the buffer alone; paste brings it back.
        {
            std::string buf("hello world");
            TextEditState st = at(0, 5); // "hello"
            CHECK(!text_edit_key(buf, 64, st, Key::C, ModCtrl, false,
                                 clipboard_get, clipboard_set));
            CHECK_EQ(buf, std::string("hello world"));

            TextEditState dst = at(11, 11);
            std::string target("hello world");
            CHECK(text_edit_key(target, 64, dst, Key::V, ModCtrl, false,
                                clipboard_get, clipboard_set));
            CHECK_EQ(target, std::string("hello worldhello"));
        }

        // Cut removes the selection and reports a change.
        {
            std::string buf("abcdef");
            TextEditState st = at(1, 4); // "bcd"
            CHECK(text_edit_key(buf, 64, st, Key::X, ModCtrl, false,
                                clipboard_get, clipboard_set));
            CHECK_EQ(buf, std::string("aef"));
            CHECK_INT(st.caret, 1);

            std::string got;
            CHECK(clipboard_get(&got));
            CHECK_EQ(got, std::string("bcd"));
        }

        // A password field must not put its contents on the clipboard, by
        // either copy or cut, and cut must not delete anything either.
        {
            clip_reset();
            clipboard_set("sentinel");

            std::string buf("hunter2");
            TextEditState st = at(0, 7);
            CHECK(!text_edit_key(buf, 64, st, Key::C, ModCtrl, true,
                                 clipboard_get, clipboard_set));
            CHECK(!text_edit_key(buf, 64, st, Key::X, ModCtrl, true,
                                 clipboard_get, clipboard_set));
            CHECK_EQ(buf, std::string("hunter2"));

            std::string got;
            clipboard_get(&got);
            CHECK_EQ(got, std::string("sentinel"));
        }

        // Copy with no selection is a no-op rather than copying everything.
        {
            clip_reset();
            clipboard_set("sentinel");

            std::string buf("abc");
            TextEditState st = at(1, 1);
            text_edit_key(buf, 64, st, Key::C, ModCtrl, false,
                          clipboard_get, clipboard_set);

            std::string got;
            clipboard_get(&got);
            CHECK_EQ(got, std::string("sentinel"));
        }

        // Pasting multi-line content into a single-line field takes the first
        // line rather than embedding a newline.
        {
            clipboard_set("first\nsecond");
            std::string buf;
            TextEditState st;
            CHECK(text_edit_key(buf, 64, st, Key::V, ModCtrl, false,
                                clipboard_get, clipboard_set));
            CHECK_EQ(buf, std::string("first"));
        }

        // Paste replaces a selection.
        {
            clipboard_set("XY");
            std::string buf("abcdef");
            TextEditState st = at(1, 4);
            text_edit_key(buf, 64, st, Key::V, ModCtrl, false,
                          clipboard_get, clipboard_set);
            CHECK_EQ(buf, std::string("aXYef"));
        }
    }

    void test_hit_and_scroll()
    {
        // font_fake gives every glyph a fixed advance, so the expected
        // boundaries can be written down exactly.
        const int adv = launcher::ui::FAKE_ADVANCE;
        const std::string buf("abcdef");

        // Left of the field is always offset 0.
        CHECK_INT(text_edit_hit(buf, 0, false), 0);
        CHECK_INT(text_edit_hit(buf, -50, false), 0);

        // Past the end clamps to the end.
        CHECK_INT(text_edit_hit(buf, adv * 100, false), 6);

        // Exactly on a boundary picks that boundary.
        CHECK_INT(text_edit_hit(buf, adv * 2, false), 2);

        // Past the midpoint of a glyph rounds to the far side, so a click
        // lands where it looks like it should.
        CHECK_INT(text_edit_hit(buf, adv * 2 + adv / 2 + 1, false), 3);
        CHECK_INT(text_edit_hit(buf, adv * 2 + 1, false), 2);

        // Caret pixel position tracks the offset.
        CHECK_INT(text_edit_caret_px(buf, at(0, 0), false), 0);
        CHECK_INT(text_edit_caret_px(buf, at(3, 3), false), adv * 3);

        // A password field measures asterisks, one per character, so a
        // multi-byte password does not render wider than it is long.
        {
            const std::string pw(UTF8); // 6 bytes, 5 characters
            CHECK_EQ(text_edit_display(pw, true), std::string("*****"));
            CHECK_INT(text_edit_caret_px(pw, at(3, 3), true), adv * 2);

            // Hit testing returns a BYTE offset into the real buffer.
            CHECK_INT(text_edit_hit(pw, adv * 2, true), 3);
        }

        // Scrolling: a value that fits never scrolls.
        {
            TextEditState st = at(6, 6);
            st.scroll_x = 0;
            text_edit_scroll_to_caret(buf, st, adv * 10, false);
            CHECK_INT(st.scroll_x, 0);
        }

        // A caret past the right edge pulls the view along.
        {
            TextEditState st = at(6, 6);
            st.scroll_x = 0;
            text_edit_scroll_to_caret(buf, st, adv * 3, false);
            CHECK_INT(st.scroll_x, adv * 6 - adv * 3);
        }

        // Moving back to the start scrolls back to zero.
        {
            TextEditState st = at(0, 0);
            st.scroll_x = adv * 3;
            text_edit_scroll_to_caret(buf, st, adv * 3, false);
            CHECK_INT(st.scroll_x, 0);
        }

        // Never leave blank space on the right: after deleting the tail, the
        // scroll clamps back so the text stays flush.
        {
            TextEditState st = at(6, 6);
            st.scroll_x = adv * 50;
            text_edit_scroll_to_caret(buf, st, adv * 10, false);
            CHECK_INT(st.scroll_x, 0);
        }

        // A zero-width field must not divide by anything or go negative.
        {
            TextEditState st = at(3, 3);
            st.scroll_x = 17;
            text_edit_scroll_to_caret(buf, st, 0, false);
            CHECK_INT(st.scroll_x, 0);
        }
    }
} // namespace

void test_text_edit()
{
    test_utf8_motion();
    test_navigation();
    test_selection();
    test_deletion();
    test_insert();
    test_clipboard();
    test_hit_and_scroll();
}
