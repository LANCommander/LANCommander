#ifndef LAUNCHER_UI_WIDGETS_COLLECTION_H
#define LAUNCHER_UI_WIDGETS_COLLECTION_H

#include <string>

#include "gfx/gfx.h"
#include "image_cache.h"
#include "input.h"
#include "widgets.h"

// Horizontal carousels and vertical lists.
//
// Both follow the same split as the rest of the widget layer: the arithmetic
// is in layout.cpp and unit-tested, and what lives here is drawing and hit
// testing. Both are also "begin/end" rather than a single call, because the
// CALLER draws the items — a hero card, a cover and a genre tile have nothing
// in common but their rectangle, and a callback mechanism would be inventing
// machinery this codebase does not otherwise have.

namespace launcher
{
    namespace ui
    {

        // --- Cover tile -------------------------------------------------------

        // One cell of a cover grid or carousel, drawn the way the Avalonia
        // Cover / CoverButton pair draws it.
        //
        // The library and the depot each had their own copy of this and the
        // two had drifted apart from the Avalonia original in different ways:
        // a flat grey placeholder instead of the purple-to-navy gradient, and
        // a 1px blue outline on hover where Avalonia has a white glass sheen
        // and no border at all.
        struct CoverStyle
        {
            bool hovered;
            bool in_library;  // the depot's "already in your library" tick

            CoverStyle() : hovered(false), in_library(false) {}
        };

        void cover_tile(gfx::Surface *s, const gfx::Rect &r, ImageCache &cache,
                        const std::string &cover_id, const char *title,
                        const CoverStyle &style);

        // --- Carousel ---------------------------------------------------------

        struct CarouselState
        {
            int scroll_x;    // where the strip is now, in pixels
            int target_x;    // where it is heading; equal when settled
            int focus_index; // -1 when nothing is focused

            CarouselState() : scroll_x(0), target_x(0), focus_index(-1) {}
        };

        struct CarouselResult
        {
            // Inclusive, and last < first when empty, so callers can loop
            // unconditionally. Requesting images only inside this range is
            // what keeps six carousels from asking the image cache for a
            // hundred covers every frame.
            int first_visible;
            int last_visible;

            int hovered_index;
            int clicked_index;   // -1 when nothing was clicked
            bool see_all_clicked;

            int header_h;        // title row
            int total_h;         // header + items, for stacking sections
            int content_x;
            int content_y;
            int strip_w;

            CarouselResult()
                : first_visible(0), last_visible(-1), hovered_index(-1),
                  clicked_index(-1), see_all_clicked(false), header_h(0),
                  total_h(0), content_x(0), content_y(0), strip_w(0) {}
        };

        // Draws the title row, an optional "See all", the prev/next arrows and
        // the clip; advances the eased scroll; hit-tests the items.
        //
        // `w` is the width of the whole widget INCLUDING the arrow gutters on
        // either side; the item strip is narrower by carousel_gutter() * 2 and
        // starts at CarouselResult::content_x. The title and "See all" align
        // with the strip, not with the arrows.
        //
        // The wheel is not consumed here — it belongs to the page underneath.
        //
        // Must be paired with carousel_end(), which pops the clip.
        CarouselResult carousel_begin(gfx::Surface *s, int x, int y, int w,
                                      const char *title, int item_count,
                                      int item_w, int item_h, int gap,
                                      CarouselState &state, const InputState &input,
                                      bool show_see_all);

        gfx::Rect carousel_item_rect(const CarouselResult &r, int index,
                                     int item_w, int item_h, int gap,
                                     const CarouselState &state);

        void carousel_end(gfx::Surface *s);

        // Height a carousel will occupy, without drawing it. Lets a scrolled
        // page decide a section is off-screen and skip it entirely.
        int carousel_height(int item_h);

        // Width reserved for the arrow on each side. Callers that put a
        // non-carousel row into a stack of carousels — the depot's search box
        // — use this to line it up with the item strips above and below it,
        // the way the Avalonia views share a CarouselNav column group.
        int carousel_gutter();

        // --- List -------------------------------------------------------------

        struct ListState
        {
            ScrollState scroll;
            int selected;   // -1 when nothing is selected

            ListState() : selected(-1) {}
        };

        struct ListResult
        {
            int first_visible;
            int last_visible;
            int hovered_index;
            int clicked_index;
            int content_h;

            ListResult()
                : first_visible(0), last_visible(-1), hovered_index(-1),
                  clicked_index(-1), content_h(0) {}
        };

        // Handles wheel scrolling, hit testing and the clip. Row backgrounds
        // for hover and selection are drawn here because every list wants
        // them; row CONTENT is the caller's.
        ListResult list_begin(gfx::Surface *s, int x, int y, int w, int h,
                              int item_count, int row_h,
                              ListState &state, const InputState &input);

        gfx::Rect list_row_rect(const ListResult &r, int x, int y, int w,
                                int index, int row_h, const ListState &state);

        // Pops the clip and draws the scrollbar.
        void list_end(gfx::Surface *s, int x, int y, int w, int h,
                      int item_count, int row_h,
                      ListState &state, const InputState &input);

    } // namespace ui
} // namespace launcher

#endif // LAUNCHER_UI_WIDGETS_COLLECTION_H
