#include "ui/widgets_collection.h"
#include "ui/icons.h"
#include "ui/layout.h"
#include "ui/theme.h"

namespace launcher
{
    namespace ui
    {

        // ---------------------------------------------------------------------
        // Cover tile
        // ---------------------------------------------------------------------

        namespace
        {
            // Avalonia's Cover fallback gradient, #3D1A6B -> #0F3460.
            //
            // Drawn top-to-bottom rather than corner-to-corner. The original
            // runs along the tile's diagonal, but gfx has one gradient
            // primitive and it is a span per row; a true diagonal is a write
            // per pixel, which is not a trade worth making on the DOS target
            // for a tilt on two colours this close in value.
            const gfx::Color COVER_FALLBACK_TOP = gfx::rgb(0x3D, 0x1A, 0x6B);
            const gfx::Color COVER_FALLBACK_BOTTOM = gfx::rgb(0x0F, 0x34, 0x60);

            // Avalonia's GlassOverlay: white at 0x45 falling to 0x10 at 45%
            // of the way down and to nothing at the bottom. Two ramps rather
            // than one, so the three stops survive.
            const int GLASS_TOP_A = 0x45;
            const int GLASS_MID_A = 0x10;
            const int GLASS_MID_AT = 45;   // percent

            // The depot's in-library tick: a 16px black square inset 6 with a
            // 9px check, at 0.8.
            const int LIBRARY_BADGE = 13;
            const int LIBRARY_BADGE_INSET = 5;
            const int LIBRARY_BADGE_ICON = 7;
        } // namespace

        void cover_tile(gfx::Surface *s, const gfx::Rect &r, ImageCache &cache,
                        const std::string &cover_id, const char *title,
                        const CoverStyle &style)
        {
            // object-fit: cover, as Avalonia's Stretch="UniformToFill" does.
            // Covers are nominally 2:3 and the cells are too, but real artwork
            // is not always exactly that, and fitting left a letterbox down
            // two edges of the odd one out.
            if (!draw_image_cover(s, cache, r, cover_id))
            {
                gfx::fill_rect_gradient_v(s, r, COVER_FALLBACK_TOP,
                                          COVER_FALLBACK_BOTTOM);

                if (title && *title)
                {
                    // Avalonia sizes this title at min(w, h) * 0.09, floored
                    // at 8px, so a carousel tile and a grid cell carry
                    // proportionally the same lettering.
                    const int minor = r.w < r.h ? r.w : r.h;
                    const FontSize fs = font_size_near(minor * 9 / 100);

                    const int margin = 8;   // Avalonia's Margin="10", at 0.8
                    const int block_h = draw_text_wrap_center(
                        NULL, 0, 0, r.w - margin * 2, theme().text_bright, title, fs);

                    draw_text_wrap_center(s, r.x + r.w / 2,
                                          r.y + (r.h - block_h) / 2,
                                          r.w - margin * 2, theme().text_bright,
                                          title, fs);
                }
            }

            if (style.hovered)
            {
                // No outline. Avalonia's hover is a sheen and a scale-up, and
                // reserves a border for keyboard focus alone; the blue 1px
                // rectangle this used to draw was not in that vocabulary.
                const int mid = r.h * GLASS_MID_AT / 100;

                if (mid > 0)
                    gfx::fill_rect_gradient_v(
                        s, gfx::rect(r.x, r.y, r.w, mid),
                        gfx::rgba(255, 255, 255, GLASS_TOP_A),
                        gfx::rgba(255, 255, 255, GLASS_MID_A));

                if (r.h - mid > 0)
                    gfx::fill_rect_gradient_v(
                        s, gfx::rect(r.x, r.y + mid, r.w, r.h - mid),
                        gfx::rgba(255, 255, 255, GLASS_MID_A),
                        gfx::rgba(255, 255, 255, 0));
            }

            if (style.in_library)
            {
                const gfx::Rect badge =
                    gfx::rect(r.x + r.w - LIBRARY_BADGE - LIBRARY_BADGE_INSET,
                              r.y + LIBRARY_BADGE_INSET,
                              LIBRARY_BADGE, LIBRARY_BADGE);

                gfx::fill_rect_alpha(s, badge, gfx::rgba(0, 0, 0, 0xCC));
                draw_icon_centered(s, badge, LIBRARY_BADGE_ICON,
                                   theme().text_bright, Icon::Check);
            }
        }

        namespace
        {
            const int HEADER_PAD = 6;   // between the title row and the items

            // Carousel and section titles. Avalonia sets these at 18-20
            // against a base of 16 (DepotView, LibraryRowView, Carousel.axaml).
            const FontSize SECTION_FONT = FontSize::Section;

            // Width of the gutter reserved either side of the strip for the
            // prev/next arrows.
            //
            // The arrows used to be drawn INSIDE the strip, before the items,
            // which meant every card painted straight over them and the
            // carousels looked like they had no navigation at all. The
            // Avalonia CarouselControl puts its buttons in their own grid
            // columns beside the clipped item panel; doing the same here means
            // an arrow cannot be covered by definition, rather than relying on
            // draw order.
            // 20px matches the page padding the screens already use, so a
            // caller can hand carousel_begin() the full content band and have
            // the item strip land exactly where its grid and body text do,
            // with the arrows sitting in the margin.
            const int ARROW_GUTTER = 20;

            // Pixels the eased scroll closes per frame, as a fraction of the
            // remaining distance. Integer maths, so a minimum step is needed
            // or the last few pixels never arrive.
            int ease(int current, int target)
            {
                const int delta = target - current;
                if (delta == 0)
                    return current;

                int step = delta / 4;
                if (step == 0)
                    step = (delta > 0) ? 1 : -1;

                return current + step;
            }

            // One arrow. `enabled` is false at the ends of the strip, where it
            // is drawn dimmed rather than removed — a control that disappears
            // as you use it is harder to aim at than one that greys out, and
            // it keeps the strip from resizing underneath the pointer.
            bool arrow(gfx::Surface *s, const gfx::Rect &r, Icon icon,
                       bool enabled, const InputState &input)
            {
                const bool hovered = enabled &&
                                     gfx::rect_contains(r, input.mouse.x, input.mouse.y);

                if (hovered)
                    fill_rounded_rect_alpha(s, r, BUTTON_RADIUS,
                                            gfx::rgba(255, 255, 255, 30));

                const gfx::Color c = !enabled  ? theme().text_disabled
                                     : hovered ? theme().text_bright
                                               : theme().text_dim;

                draw_icon_centered(s, r, ICON_MD, c, icon);

                return hovered && input.mouse.clicked;
            }
        } // namespace

        int carousel_height(int item_h)
        {
            return text_height(SECTION_FONT) + HEADER_PAD + item_h;
        }

        int carousel_gutter() { return ARROW_GUTTER; }

        CarouselResult carousel_begin(gfx::Surface *s, int x, int y, int w,
                                      const char *title, int item_count,
                                      int item_w, int item_h, int gap,
                                      CarouselState &state, const InputState &input,
                                      bool show_see_all)
        {
            CarouselResult r;

            const int th = text_height(SECTION_FONT);

            // The strip is inset by a gutter on each side; the header lines up
            // with the strip, not with the arrows, which is how the Avalonia
            // template aligns its title against the shared nav columns.
            const int strip_x = x + ARROW_GUTTER;
            int strip_w = w - ARROW_GUTTER * 2;
            if (strip_w < 1)
                strip_w = 1;

            r.header_h = th + HEADER_PAD;
            r.total_h = r.header_h + item_h;
            r.content_x = strip_x;
            r.content_y = y + r.header_h;
            r.strip_w = strip_w;

            // --- Header ------------------------------------------------------
            if (title && *title)
                draw_text(s, strip_x, y, theme().text_bright, title, SECTION_FONT);

            if (show_see_all)
            {
                // The section title is a heading and "See all" is not: in the
                // Avalonia views the pair is 18-20px against 16px body, which
                // is what makes a page of carousels read as sections rather
                // than as one undifferentiated column of rows.
                const char *label = "See all";
                const int lw = text_width(label);
                const int lx = strip_x + strip_w - lw;

                const bool hovered = (input.mouse.x >= lx && input.mouse.x < lx + lw &&
                                      input.mouse.y >= y && input.mouse.y < y + th);

                draw_text(s, lx, y + (th - text_height()) / 2,
                          hovered ? theme().text_bright : theme().text_dim, label);
                r.see_all_clicked = hovered && input.mouse.clicked;
            }

            if (item_count <= 0)
            {
                // Still push a clip so the caller can pair begin/end
                // unconditionally.
                gfx::push_clip(s, gfx::rect(strip_x, r.content_y, strip_w, item_h));
                return r;
            }

            // --- Scroll ------------------------------------------------------
            CarouselLayout probe =
                carousel_layout(strip_w, item_count, item_w, gap, state.target_x);
            const bool scrollable = probe.max_scroll > 0;

            // Arrows only where there is somewhere to go.
            //
            // Deliberately NOT wheel-driven. A wheel over a horizontal strip
            // used to scroll it sideways, which meant the pointer sitting
            // anywhere in a page of carousels swallowed the wheel and the page
            // itself would not move. The wheel now always belongs to the page;
            // the strip is moved with the arrows.
            if (scrollable)
            {
                const int page = item_w + gap;

                const gfx::Rect left_r = gfx::rect(x, r.content_y, ARROW_GUTTER, item_h);
                const gfx::Rect right_r = gfx::rect(strip_x + strip_w, r.content_y,
                                                    ARROW_GUTTER, item_h);

                if (arrow(s, left_r, Icon::CaretLeft, state.target_x > 0, input))
                    state.target_x -= page;

                if (arrow(s, right_r, Icon::CaretRight,
                          state.target_x < probe.max_scroll, input))
                    state.target_x += page;
            }
            else
            {
                state.target_x = 0;
            }

            if (state.target_x < 0) state.target_x = 0;
            if (state.target_x > probe.max_scroll) state.target_x = probe.max_scroll;

            state.scroll_x = ease(state.scroll_x, state.target_x);

            const CarouselLayout lay =
                carousel_layout(strip_w, item_count, item_w, gap, state.scroll_x);
            state.scroll_x = lay.scroll_x;

            r.first_visible = lay.first_visible;
            r.last_visible = lay.last_visible;

            // --- Hit testing --------------------------------------------------
            //
            // Confined to the strip. A part-scrolled item extends under the
            // arrow gutter, and without this a click on the left arrow would
            // land on that item as well — scrolling the strip and opening a
            // game in the same frame.
            const bool over_strip = (input.mouse.x >= strip_x &&
                                     input.mouse.x < strip_x + strip_w);

            for (int i = r.first_visible; over_strip && i <= r.last_visible; ++i)
            {
                const int ix = strip_x + carousel_item_x(i, item_w, gap, state.scroll_x);
                if (input.mouse.x >= ix && input.mouse.x < ix + item_w &&
                    input.mouse.y >= r.content_y && input.mouse.y < r.content_y + item_h)
                {
                    r.hovered_index = i;
                    if (input.mouse.clicked)
                        r.clicked_index = i;
                    break;
                }
            }

            gfx::push_clip(s, gfx::rect(strip_x, r.content_y, strip_w, item_h));
            return r;
        }

        gfx::Rect carousel_item_rect(const CarouselResult &r, int index,
                                     int item_w, int item_h, int gap,
                                     const CarouselState &state)
        {
            return gfx::rect(r.content_x + carousel_item_x(index, item_w, gap, state.scroll_x),
                             r.content_y, item_w, item_h);
        }

        void carousel_end(gfx::Surface *s)
        {
            gfx::pop_clip(s);
        }

        // ---------------------------------------------------------------------
        // List
        // ---------------------------------------------------------------------

        ListResult list_begin(gfx::Surface *s, int x, int y, int w, int h,
                              int item_count, int row_h,
                              ListState &state, const InputState &input)
        {
            ListResult r;

            const bool over = (input.mouse.x >= x && input.mouse.x < x + w &&
                               input.mouse.y >= y && input.mouse.y < y + h);

            if (over && input.mouse.wheel_delta != 0)
                state.scroll.offset -= input.mouse.wheel_delta * row_h;

            const ListLayout lay = list_layout(h, item_count, row_h, state.scroll.offset);
            state.scroll.offset = lay.scroll_y;

            r.first_visible = lay.first_visible;
            r.last_visible = lay.last_visible;
            r.content_h = lay.content_h;

            gfx::push_clip(s, gfx::rect(x, y, w, h));

            // Hover and selection backgrounds are drawn here because every
            // list wants them; the row's content is the caller's business.
            for (int i = r.first_visible; i <= r.last_visible; ++i)
            {
                const gfx::Rect row = list_row_rect(r, x, y, w, i, row_h, state);

                const bool hovered = over &&
                                     input.mouse.y >= row.y &&
                                     input.mouse.y < row.y + row.h;

                if (i == state.selected)
                {
                    gfx::fill_rect(s, row, theme().panel_hover);
                    // Accent bar on the selected row, as the Avalonia
                    // CompactListRow style does.
                    gfx::fill_rect(s, gfx::rect(row.x, row.y, 2, row.h), theme().primary);
                }
                else if (hovered)
                {
                    gfx::fill_rect_alpha(s, row, gfx::rgba(255, 255, 255, 20));
                }

                if (hovered)
                {
                    r.hovered_index = i;
                    if (input.mouse.clicked)
                    {
                        r.clicked_index = i;
                        state.selected = i;
                    }
                }
            }

            return r;
        }

        gfx::Rect list_row_rect(const ListResult &r, int x, int y, int w,
                                int index, int row_h, const ListState &state)
        {
            (void)r;
            return gfx::rect(x, y + index * row_h - state.scroll.offset, w, row_h);
        }

        void list_end(gfx::Surface *s, int x, int y, int w, int h,
                      int item_count, int row_h,
                      ListState &state, const InputState &input)
        {
            gfx::pop_clip(s);

            const int content_h = item_count * row_h;
            if (content_h > h)
                scrollbar(s, x + w - 12, y, h, content_h, h, state.scroll, input);
        }

    } // namespace ui
} // namespace launcher
