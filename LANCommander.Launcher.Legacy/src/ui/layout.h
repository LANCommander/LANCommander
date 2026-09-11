#ifndef LAUNCHER_UI_LAYOUT_H
#define LAUNCHER_UI_LAYOUT_H

// Pure layout arithmetic for the collection widgets.
//
// Same rationale as chrome_geometry.h: this is the half of each widget that
// has to be right, and it is the half that is painful to verify by looking at
// a window. Nothing here touches a surface, a theme colour or a font — text
// metrics arrive as ints — so it compiles into launcher_tests without a
// graphics backend and runs on the Linux CI runner.
//
// gfx.h is included only for Rect and its inline helpers, which are plain
// arithmetic types with no backend dependency.

#include "gfx/gfx.h"

namespace launcher
{
    namespace ui
    {

        // --- Uniform grid ---------------------------------------------------
        //
        // Columns are derived from the available width at a minimum item
        // width, then items expand to fill the row and the leftover pixels go
        // back into the spacing. This is the arithmetic that used to sit
        // inline in screen_library.cpp.

        struct GridLayout
        {
            int cols;
            int rows;
            int item_w;
            int item_h;
            int col_spacing;
            int row_spacing;
            int pad;
            int content_h;
        };

        // `aspect_num`/`aspect_den` give the item height: 3/2 for 2:3 covers.
        // `min_cols`/`max_cols` clamp the responsive range; pass 0 for either
        // to leave it unclamped. The column count is still capped at `count`,
        // so three games do not stretch across seven columns.
        GridLayout grid_layout(int avail_w, int count,
                               int min_item_w, int col_gap, int row_gap,
                               int aspect_num, int aspect_den, int pad,
                               int min_cols, int max_cols);

        gfx::Rect grid_cell_rect(const GridLayout &g, int index,
                                 int origin_x, int origin_y, int scroll_y);

        // Inclusive index range intersecting the viewport. Returns
        // first > last when nothing is visible, so callers can loop
        // unconditionally.
        void grid_visible_range(const GridLayout &g, int count,
                                int scroll_y, int viewport_h,
                                int *out_first, int *out_last);

        // --- Carousel -------------------------------------------------------

        struct CarouselLayout
        {
            int first_visible;
            int last_visible;   // < first_visible when empty
            int content_w;
            int max_scroll;
            int scroll_x;       // input scroll clamped into range
        };

        CarouselLayout carousel_layout(int strip_w, int item_count,
                                       int item_w, int gap, int scroll_x);

        int carousel_item_x(int index, int item_w, int gap, int scroll_x);

        // --- List ------------------------------------------------------------

        struct ListLayout
        {
            int first_visible;
            int last_visible;   // < first_visible when empty
            int content_h;
            int max_scroll;
            int scroll_y;       // input scroll clamped into range
        };

        ListLayout list_layout(int viewport_h, int item_count,
                               int row_h, int scroll_y);

        // --- Context menu -----------------------------------------------------

        struct MenuLayout
        {
            int x, y, w, h;
        };

        // Flips left of the anchor when it would overflow the right edge, and
        // above the anchor when it would overflow the bottom. Both can happen
        // at once near a corner.
        MenuLayout menu_layout(int anchor_x, int anchor_y,
                               int screen_w, int screen_h,
                               int menu_w, int visible_rows, int visible_seps,
                               int row_h, int sep_h, int pad);

        // Drops invisible entries and collapses the separators left behind, so
        // a separator can never end up leading, trailing or doubled. Writes
        // surviving source indices into `out_indices` and returns how many.
        //
        // This is the classic defect in a table-driven menu, and it is only
        // cheap to get right if it is testable in isolation.
        int menu_filter(const bool *visible, const bool *is_separator, int count,
                        int *out_indices, int cap);

        // --- Split button ------------------------------------------------------

        struct SplitButtonLayout
        {
            gfx::Rect primary;
            gfx::Rect caret;
        };

        SplitButtonLayout split_button_layout(int x, int y, int w, int h,
                                              int caret_w);

        // --- Lightbox ----------------------------------------------------------

        // Aspect-preserving fit, centred in the box. Never upscales past the
        // box; a source smaller than the box is centred at its own size.
        gfx::Rect lightbox_fit(int img_w, int img_h, int box_w, int box_h);

        // Where the lightbox controls sit. Close is top-right; prev and next
        // are vertically centred against the left and right edges.
        struct LightboxChrome
        {
            gfx::Rect close;
            gfx::Rect prev;
            gfx::Rect next;
            gfx::Rect counter;  // the "i / n" strip along the bottom
        };

        // `x`/`y`/`w`/`h` are the region the viewer occupies, which is NOT
        // the whole window: the custom title bar and footer are drawn after
        // the screen and would sit on top of anything placed at the very
        // edges.
        LightboxChrome lightbox_chrome(int x, int y, int w, int h,
                                       int btn, int text_h);

    } // namespace ui
} // namespace launcher

#endif // LAUNCHER_UI_LAYOUT_H
