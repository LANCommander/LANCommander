#include "test_main.h"

#include "ui/theme.h"
#include "ui/font.h"
#include "gfx/gfx.h"

#include <string>
#include <vector>

namespace launcher
{
    namespace ui
    {
        // font_fake.cpp
        std::vector<std::string> &font_fake_drawn();
        extern const int FAKE_ADVANCE;
        extern const int FAKE_HEIGHT;
    }
}

using namespace launcher;

namespace
{
    gfx::Surface *g_surface = NULL;

    // Wrap `text` at `max_w` and return the lines that were actually drawn.
    std::vector<std::string> wrap(const char *text, int max_w)
    {
        ui::font_fake_drawn().clear();
        ui::draw_text_wrap(g_surface, 0, 0, max_w, gfx::rgb(255, 255, 255), text);
        return ui::font_fake_drawn();
    }

    std::string join(const std::vector<std::string> &lines)
    {
        std::string out;
        for (size_t i = 0; i < lines.size(); ++i) {
            if (i)
                out += "|";
            out += lines[i];
        }
        return out;
    }
}

void test_text_wrap()
{
    ui::font_init();
    g_surface = gfx::create_surface(400, 400);

    // Every glyph is 10px wide, so a 100px line fits exactly 10 characters.
    CHECK_INT(ui::text_width("abcde"), 5 * ui::FAKE_ADVANCE);
    CHECK_INT(ui::text_height(), ui::FAKE_HEIGHT);

    // --- Fits on one line ---
    CHECK_EQ(join(wrap("hello", 100)), "hello");

    // --- Breaks at a word boundary, not mid-word ---
    // "hello world" is 11 chars = 110px, so it cannot fit in 100px.
    CHECK_EQ(join(wrap("hello world", 100)), "hello|world");

    // --- The space is consumed, not carried to the next line ---
    {
        std::vector<std::string> lines = wrap("hello world", 100);
        CHECK(lines.size() == 2);
        if (lines.size() == 2) {
            CHECK(lines[1].empty() || lines[1][0] != ' ');
        }
    }

    // --- Exact fit does not spill ---
    // 10 chars at 10px each is exactly 100px.
    CHECK_EQ(join(wrap("abcdefghij", 100)), "abcdefghij");
    // 11 chars must break.
    CHECK(wrap("abcdefghijk", 100).size() == 2);

    // --- A single word longer than the line is force-broken ---
    // Without this the wrap loop makes no progress and hangs.
    {
        std::vector<std::string> lines = wrap("abcdefghijklmnopqrst", 100);
        CHECK(lines.size() == 2);
        CHECK_EQ(join(lines), "abcdefghij|klmnopqrst");
    }

    // --- Explicit newlines ---
    CHECK_EQ(join(wrap("a\nb", 100)), "a|b");
    CHECK_EQ(join(wrap("a\r\nb", 100)), "a|b"); // CRLF must not yield a blank line

    // --- Empty and whitespace input ---
    CHECK(wrap("", 100).empty());
    CHECK_INT(ui::draw_text_wrap(NULL, 0, 0, 100, gfx::rgb(0, 0, 0), ""), 0);

    // --- Measure-only mode draws nothing but still reports height ---
    {
        ui::font_fake_drawn().clear();
        const int h = ui::draw_text_wrap(NULL, 0, 0, 100, gfx::rgb(0, 0, 0),
                                         "hello world");
        CHECK(ui::font_fake_drawn().empty());
        // Two lines, each font_height + default line_spacing of 2.
        CHECK_INT(h, 2 * (ui::FAKE_HEIGHT + 2));
    }

    // --- Measured height matches the number of lines drawn ---
    {
        const char *text = "the quick brown fox jumps over the lazy dog";
        const int h = ui::draw_text_wrap(NULL, 0, 0, 100, gfx::rgb(0, 0, 0), text);
        const size_t drawn = wrap(text, 100).size();
        CHECK_INT(h, (int)drawn * (ui::FAKE_HEIGHT + 2));
    }

    // --- Degenerate width must terminate rather than loop forever ---
    // Every glyph is wider than the line, so each one is force-broken onto
    // its own line. The guarantee under test is that it returns at all.
    {
        std::vector<std::string> lines = wrap("abc", 5);
        CHECK(lines.size() == 3);
    }

    gfx::destroy_surface(g_surface);
    g_surface = NULL;
    ui::font_shutdown();
}
