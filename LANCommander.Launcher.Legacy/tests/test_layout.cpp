#include "test_main.h"

#include "ui/layout.h"

using namespace launcher::ui;
namespace gfx = launcher::gfx;

namespace
{
    // The library grid as it was configured before the arithmetic moved into
    // layout.cpp: 130px minimum cover, 12px gaps, 20px pad, 2:3 covers.
    GridLayout library_grid(int avail_w, int count)
    {
        return grid_layout(avail_w, count, 130, 12, 12, 3, 2, 20, 0, 0);
    }

    void test_grid()
    {
        // Column count across the window sizes the launcher actually runs at.
        // 640 is the minimum window; 800x600 is the default.
        CHECK_INT(library_grid(640, 100).cols, 4);
        CHECK_INT(library_grid(800, 100).cols, 5);
        CHECK_INT(library_grid(1280, 100).cols, 8);
        CHECK_INT(library_grid(1920, 100).cols, 13);

        // Items expand to fill the row: the last column must end flush with
        // the right margin, never overhang it and never leave a visible gap.
        {
            const int avail = 800;
            GridLayout g = library_grid(avail, 100);
            gfx::Rect last = grid_cell_rect(g, g.cols - 1, 0, 0, 0);
            const int right_edge = last.x + last.w;
            CHECK(right_edge <= avail - g.pad);
            CHECK(right_edge >= avail - g.pad - g.cols);
        }

        // 2:3 aspect.
        {
            GridLayout g = library_grid(800, 100);
            CHECK_INT(g.item_h, g.item_w * 3 / 2);
        }

        // A handful of items must not stretch across the whole window.
        CHECK_INT(library_grid(1920, 3).cols, 3);
        CHECK_INT(library_grid(1920, 1).cols, 1);

        // Responsive clamps, which the library sidebar layout needs.
        CHECK_INT(grid_layout(1920, 100, 105, 12, 12, 3, 2, 20, 4, 7).cols, 7);
        CHECK_INT(grid_layout(400, 100, 105, 12, 12, 3, 2, 20, 4, 7).cols, 4);

        // Empty and degenerate inputs must not divide by zero or go negative.
        {
            GridLayout g = library_grid(800, 0);
            CHECK_INT(g.rows, 0);
            CHECK_INT(g.content_h, g.pad);
            CHECK(g.cols >= 1);
            CHECK(g.item_w >= 1);
        }
        {
            GridLayout g = library_grid(10, 100); // narrower than the padding
            CHECK(g.cols >= 1);
            CHECK(g.item_w >= 1);
            CHECK(g.item_h >= 1);
        }

        // Partial last row still contributes a full row of height.
        {
            GridLayout g = library_grid(800, 100);
            CHECK_INT(g.cols, 5);
            GridLayout partial = library_grid(800, 21);
            CHECK_INT(partial.rows, 5);
            CHECK_INT(partial.content_h,
                      partial.pad + 5 * (partial.item_h + partial.row_spacing));
        }

        // Cell placement: row-major, and scroll shifts straight up.
        {
            GridLayout g = library_grid(800, 100);
            gfx::Rect a = grid_cell_rect(g, 0, 0, 0, 0);
            gfx::Rect b = grid_cell_rect(g, 1, 0, 0, 0);
            gfx::Rect below = grid_cell_rect(g, g.cols, 0, 0, 0);

            CHECK_INT(a.x, g.pad);
            CHECK_INT(a.y, g.pad);
            CHECK_INT(b.x, g.pad + g.item_w + g.col_spacing);
            CHECK_INT(b.y, a.y);
            CHECK_INT(below.x, a.x);
            CHECK_INT(below.y, a.y + g.item_h + g.row_spacing);

            gfx::Rect scrolled = grid_cell_rect(g, 0, 0, 0, 50);
            CHECK_INT(scrolled.y, a.y - 50);

            // Origin offsets both axes.
            gfx::Rect moved = grid_cell_rect(g, 0, 7, 100, 0);
            CHECK_INT(moved.x, a.x + 7);
            CHECK_INT(moved.y, a.y + 100);
        }

        // Visible range. This is what keeps ImageCache from being asked for
        // every cover in a 400-game library on every frame.
        {
            GridLayout g = library_grid(800, 100);
            int first = 0, last = 0;

            grid_visible_range(g, 100, 0, 600, &first, &last);
            CHECK_INT(first, 0);
            CHECK(last < 100);
            CHECK(last >= g.cols - 1);

            // Scrolled well down, the first visible index moves off row zero.
            const int stride = g.item_h + g.row_spacing;
            grid_visible_range(g, 100, g.pad + stride * 3, 600, &first, &last);
            CHECK_INT(first, 3 * g.cols);

            // Empty grid reports an empty range rather than 0..0.
            grid_visible_range(g, 0, 0, 600, &first, &last);
            CHECK(last < first);

            // Scrolled past the end clamps to the last real index.
            grid_visible_range(g, 100, 1000000, 600, &first, &last);
            CHECK(last <= 99);
        }
    }

    void test_carousel()
    {
        // Depot "New Releases": 160px covers, 16px gap.
        const int item_w = 160, gap = 16, strip = 800;

        {
            CarouselLayout c = carousel_layout(strip, 20, item_w, gap, 0);
            CHECK_INT(c.first_visible, 0);
            CHECK_INT(c.content_w, 20 * 160 + 19 * 16);
            CHECK(c.last_visible >= 4);   // at least five 160px items in 800px
            CHECK(c.last_visible <= 5);
            CHECK_INT(c.scroll_x, 0);
        }

        // Mid-scroll: the first visible item advances.
        {
            CarouselLayout c = carousel_layout(strip, 20, item_w, gap, 3 * (item_w + gap));
            CHECK_INT(c.first_visible, 3);
        }

        // Scroll is clamped into range rather than trusted.
        {
            CarouselLayout c = carousel_layout(strip, 20, item_w, gap, 999999);
            CHECK_INT(c.scroll_x, c.max_scroll);
            CHECK_INT(c.last_visible, 19);

            CarouselLayout neg = carousel_layout(strip, 20, item_w, gap, -500);
            CHECK_INT(neg.scroll_x, 0);
            CHECK_INT(neg.first_visible, 0);
        }

        // Content narrower than the strip cannot scroll at all.
        {
            CarouselLayout c = carousel_layout(strip, 2, item_w, gap, 0);
            CHECK_INT(c.max_scroll, 0);
            CHECK_INT(c.first_visible, 0);
            CHECK_INT(c.last_visible, 1);
        }

        // Empty carousel reports an empty range, so callers can loop blindly.
        {
            CarouselLayout c = carousel_layout(strip, 0, item_w, gap, 0);
            CHECK(c.last_visible < c.first_visible);
            CHECK_INT(c.content_w, 0);
            CHECK_INT(c.max_scroll, 0);
        }

        // A strip exactly one item wide shows one item, not two.
        {
            CarouselLayout c = carousel_layout(item_w, 10, item_w, gap, 0);
            CHECK_INT(c.first_visible, 0);
            CHECK_INT(c.last_visible, 0);
        }

        // Item positions step by stride and shift with scroll.
        CHECK_INT(carousel_item_x(0, item_w, gap, 0), 0);
        CHECK_INT(carousel_item_x(1, item_w, gap, 0), item_w + gap);
        CHECK_INT(carousel_item_x(1, item_w, gap, 40), item_w + gap - 40);
    }

    void test_list()
    {
        // Library sidebar: 250px wide, ~40px rows.
        const int row_h = 40;

        {
            ListLayout l = list_layout(400, 100, row_h, 0);
            CHECK_INT(l.content_h, 4000);
            CHECK_INT(l.max_scroll, 3600);
            CHECK_INT(l.first_visible, 0);
            CHECK_INT(l.last_visible, 9);
        }

        {
            ListLayout l = list_layout(400, 100, row_h, 100);
            CHECK_INT(l.first_visible, 2);
        }

        // Fewer rows than the viewport: no scroll, everything visible.
        {
            ListLayout l = list_layout(400, 3, row_h, 0);
            CHECK_INT(l.max_scroll, 0);
            CHECK_INT(l.last_visible, 2);
        }

        // Empty list.
        {
            ListLayout l = list_layout(400, 0, row_h, 0);
            CHECK(l.last_visible < l.first_visible);
        }

        // A row taller than the viewport still reports exactly one row.
        {
            ListLayout l = list_layout(20, 10, row_h, 0);
            CHECK_INT(l.first_visible, 0);
            CHECK_INT(l.last_visible, 0);
        }
    }

    void test_menu()
    {
        const int row_h = 22, sep_h = 7, pad = 4, menu_w = 200;

        // Ordinary placement hangs down and right from the anchor.
        {
            MenuLayout m = menu_layout(100, 100, 800, 600, menu_w, 6, 2, row_h, sep_h, pad);
            CHECK_INT(m.x, 100);
            CHECK_INT(m.y, 100);
            CHECK_INT(m.h, pad * 2 + 6 * row_h + 2 * sep_h);
        }

        // Near the right edge it flips to hang left.
        {
            MenuLayout m = menu_layout(750, 100, 800, 600, menu_w, 6, 2, row_h, sep_h, pad);
            CHECK_INT(m.x, 600);
            CHECK(m.x + m.w <= 800);
        }

        // Near the bottom it rises above the anchor.
        {
            MenuLayout m = menu_layout(100, 580, 800, 600, menu_w, 6, 2, row_h, sep_h, pad);
            const int h = pad * 2 + 6 * row_h + 2 * sep_h;
            CHECK_INT(m.y, 580 - h);
            CHECK(m.y >= 0);
        }

        // Both at once, in the bottom-right corner.
        {
            MenuLayout m = menu_layout(790, 590, 800, 600, menu_w, 6, 2, row_h, sep_h, pad);
            CHECK(m.x + m.w <= 800);
            CHECK(m.y + m.h <= 600);
            CHECK(m.x >= 0);
            CHECK(m.y >= 0);
        }

        // A menu taller than the screen must still start on-screen rather
        // than flipping to a negative y.
        {
            MenuLayout m = menu_layout(100, 300, 800, 200, menu_w, 20, 0, row_h, sep_h, pad);
            CHECK(m.y >= 0);
            CHECK(m.x >= 0);
        }
    }

    void test_menu_filter()
    {
        // The classic table-driven-menu defect: once invisible entries are
        // dropped, the separators they surrounded must not end up leading,
        // trailing or doubled.
        int out[16];

        // Leading separator is dropped.
        {
            const bool vis[] = { true, true };
            const bool sep[] = { true, false };
            const int n = menu_filter(vis, sep, 2, out, 16);
            CHECK_INT(n, 1);
            CHECK_INT(out[0], 1);
        }

        // Trailing separator is dropped.
        {
            const bool vis[] = { true, true };
            const bool sep[] = { false, true };
            const int n = menu_filter(vis, sep, 2, out, 16);
            CHECK_INT(n, 1);
            CHECK_INT(out[0], 0);
        }

        // Two separators in a row collapse to one.
        {
            const bool vis[] = { true, true, true, true };
            const bool sep[] = { false, true, true, false };
            const int n = menu_filter(vis, sep, 4, out, 16);
            CHECK_INT(n, 3);
            CHECK_INT(out[0], 0);
            CHECK_INT(out[1], -1);
            CHECK_INT(out[2], 3);
        }

        // A separator orphaned by hiding the item after it is dropped.
        {
            const bool vis[] = { true, true, false };
            const bool sep[] = { false, true, false };
            const int n = menu_filter(vis, sep, 3, out, 16);
            CHECK_INT(n, 1);
            CHECK_INT(out[0], 0);
        }

        // A separator between two surviving items is kept.
        {
            const bool vis[] = { true, true, true };
            const bool sep[] = { false, true, false };
            const int n = menu_filter(vis, sep, 3, out, 16);
            CHECK_INT(n, 3);
            CHECK_INT(out[1], -1);
        }

        // Everything hidden yields an empty menu, not a lone separator.
        {
            const bool vis[] = { false, false, false };
            const bool sep[] = { false, true, false };
            CHECK_INT(menu_filter(vis, sep, 3, out, 16), 0);
        }

        // The cap is respected.
        {
            const bool vis[] = { true, true, true, true };
            const bool sep[] = { false, false, false, false };
            CHECK_INT(menu_filter(vis, sep, 4, out, 2), 2);
        }
    }

    void test_split_button()
    {
        SplitButtonLayout s = split_button_layout(10, 20, 140, 32, 28);
        CHECK_INT(s.primary.x, 10);
        CHECK_INT(s.primary.w, 112);
        CHECK_INT(s.caret.x, 122);
        CHECK_INT(s.caret.w, 28);
        CHECK_INT(s.primary.x + s.primary.w, s.caret.x);
        CHECK_INT(s.caret.x + s.caret.w, 150);

        // A caret wider than the button must not produce a negative primary.
        SplitButtonLayout narrow = split_button_layout(0, 0, 20, 32, 100);
        CHECK(narrow.primary.w >= 0);
        CHECK_INT(narrow.caret.w, 20);
    }

    void test_lightbox()
    {
        // Landscape into a portrait box: width binds.
        {
            gfx::Rect r = lightbox_fit(1920, 1080, 600, 800);
            CHECK_INT(r.w, 600);
            CHECK_INT(r.h, 337);
            CHECK_INT(r.x, 0);
            CHECK_INT(r.y, (800 - 337) / 2);
        }

        // Portrait into a landscape box: height binds.
        {
            gfx::Rect r = lightbox_fit(1080, 1920, 800, 600);
            CHECK_INT(r.h, 600);
            CHECK(r.w <= 800);
            CHECK_INT(r.y, 0);
        }

        // A tiny image is centred at its natural size, not blown up.
        {
            gfx::Rect r = lightbox_fit(1, 1, 800, 600);
            CHECK_INT(r.w, 1);
            CHECK_INT(r.h, 1);
            CHECK_INT(r.x, 399);
            CHECK_INT(r.y, 299);
        }

        // Exactly the box size is left alone.
        {
            gfx::Rect r = lightbox_fit(800, 600, 800, 600);
            CHECK_INT(r.w, 800);
            CHECK_INT(r.h, 600);
            CHECK_INT(r.x, 0);
            CHECK_INT(r.y, 0);
        }

        // Aspect is preserved within rounding in both directions.
        {
            gfx::Rect r = lightbox_fit(1600, 900, 400, 400);
            CHECK_INT(r.w, 400);
            CHECK_INT(r.h, 225);
        }

        // Degenerate inputs must not divide by zero.
        {
            gfx::Rect r = lightbox_fit(0, 0, 800, 600);
            CHECK_INT(r.w, 0);
            CHECK_INT(r.h, 0);
        }
    }

    void test_lightbox_chrome()
    {
        // An offset region puts every control inside it, not at the window
        // edges: the viewer sits between the title bar and the footer.
        {
            const LightboxChrome c = lightbox_chrome(0, 32, 800, 528, 34, 16);
            CHECK(c.close.y >= 32);
            CHECK(c.counter.y + c.counter.h <= 32 + 528);
            CHECK(c.prev.y >= 32);
            CHECK(c.next.y + c.next.h <= 32 + 528);
            CHECK_INT(c.prev.y + c.prev.h / 2, 32 + 528 / 2);
        }

        // The controls must land on-screen and not overlap each other at both
        // the smallest window the launcher allows and a large one.
        const int BTN = 34;
        const int TH = 16;

        {
            const LightboxChrome c = lightbox_chrome(0, 0, 640, 480, BTN, TH);

            // Close is top-right, fully on-screen.
            CHECK(c.close.x + c.close.w <= 640);
            CHECK(c.close.y >= 0);
            CHECK_INT(c.close.w, BTN);

            // Prev and Next hug the edges and are vertically centred.
            CHECK(c.prev.x >= 0);
            CHECK(c.next.x + c.next.w <= 640);
            CHECK_INT(c.prev.y, c.next.y);
            CHECK_INT(c.prev.y + c.prev.h / 2, 480 / 2);

            // They must not overlap each other.
            CHECK(c.prev.x + c.prev.w < c.next.x);

            // The counter sits along the bottom, fully on-screen.
            CHECK(c.counter.y + c.counter.h <= 480);
            CHECK_INT(c.counter.w, 640);
        }

        {
            const LightboxChrome c = lightbox_chrome(0, 0, 1920, 1080, BTN, TH);

            CHECK(c.close.x + c.close.w <= 1920);
            CHECK(c.next.x + c.next.w <= 1920);
            CHECK_INT(c.prev.y + c.prev.h / 2, 1080 / 2);
            CHECK(c.counter.y + c.counter.h <= 1080);

            // Close and Next share the right edge but must not sit on top of
            // one another, since Next is vertically centred.
            CHECK(c.close.y + c.close.h < c.next.y);
        }

        // Close and Next DO share a column at the minimum window size, which
        // is fine as long as they are still vertically separated.
        {
            const LightboxChrome c = lightbox_chrome(0, 0, 640, 480, BTN, TH);
            CHECK_INT(c.close.x, c.next.x);
            CHECK(c.close.y + c.close.h < c.next.y);
        }
    }
} // namespace

void test_layout()
{
    test_grid();
    test_carousel();
    test_list();
    test_menu();
    test_menu_filter();
    test_split_button();
    test_lightbox();
    test_lightbox_chrome();
}
