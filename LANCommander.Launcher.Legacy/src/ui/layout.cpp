#include "ui/layout.h"

namespace launcher
{
    namespace ui
    {

        // ---------------------------------------------------------------------
        // Uniform grid
        // ---------------------------------------------------------------------

        GridLayout grid_layout(int avail_w, int count,
                               int min_item_w, int col_gap, int row_gap,
                               int aspect_num, int aspect_den, int pad,
                               int min_cols, int max_cols)
        {
            GridLayout g;
            g.pad = pad;
            g.col_spacing = col_gap;
            g.row_spacing = row_gap;

            if (min_item_w < 1) min_item_w = 1;
            if (aspect_den < 1) aspect_den = 1;

            int usable_w = avail_w - pad * 2;
            if (usable_w < 1) usable_w = 1;

            // How many columns fit at the minimum width, counting the gaps
            // between columns but not after the last one.
            int cols = (usable_w + col_gap) / (min_item_w + col_gap);
            if (cols < 1) cols = 1;

            // Responsive clamps. 0 means unset, which is what the library grid
            // used before it had a range.
            if (min_cols > 0 && cols < min_cols) cols = min_cols;
            if (max_cols > 0 && cols > max_cols) cols = max_cols;

            // Never stretch a handful of items across the full width.
            if (count > 0 && cols > count) cols = count;

            // Expand items to fill the row, then push the leftover pixels back
            // into the spacing so the row ends flush with the right margin.
            int total_gap = (cols > 1) ? (cols - 1) * col_gap : 0;
            int item_w = (usable_w - total_gap) / cols;
            if (item_w < 1) item_w = 1;

            int col_spacing = col_gap;
            if (cols > 1)
            {
                int leftover = usable_w - (cols * item_w);
                col_spacing = leftover / (cols - 1);
                if (col_spacing < 0) col_spacing = 0;
            }

            g.cols = cols;
            g.item_w = item_w;
            g.item_h = item_w * aspect_num / aspect_den;
            if (g.item_h < 1) g.item_h = 1;
            g.col_spacing = col_spacing;

            g.rows = (count > 0) ? ((count + cols - 1) / cols) : 0;
            g.content_h = pad + g.rows * (g.item_h + row_gap);

            return g;
        }

        gfx::Rect grid_cell_rect(const GridLayout &g, int index,
                                 int origin_x, int origin_y, int scroll_y)
        {
            const int col = (g.cols > 0) ? (index % g.cols) : 0;
            const int row = (g.cols > 0) ? (index / g.cols) : 0;

            return gfx::rect(origin_x + g.pad + col * (g.item_w + g.col_spacing),
                             origin_y + g.pad + row * (g.item_h + g.row_spacing) - scroll_y,
                             g.item_w, g.item_h);
        }

        void grid_visible_range(const GridLayout &g, int count,
                                int scroll_y, int viewport_h,
                                int *out_first, int *out_last)
        {
            if (count <= 0 || g.cols <= 0)
            {
                *out_first = 0;
                *out_last = -1;
                return;
            }

            const int stride = g.item_h + g.row_spacing;
            if (stride < 1)
            {
                *out_first = 0;
                *out_last = count - 1;
                return;
            }

            int first_row = (scroll_y - g.pad) / stride;
            if (first_row < 0) first_row = 0;

            int last_row = (scroll_y - g.pad + viewport_h) / stride;
            if (last_row < first_row) last_row = first_row;
            if (last_row > g.rows - 1) last_row = g.rows - 1;

            int first = first_row * g.cols;
            int last = last_row * g.cols + (g.cols - 1);
            if (last > count - 1) last = count - 1;

            if (first > count - 1)
            {
                *out_first = 0;
                *out_last = -1;
                return;
            }

            *out_first = first;
            *out_last = last;
        }

        // ---------------------------------------------------------------------
        // Carousel
        // ---------------------------------------------------------------------

        CarouselLayout carousel_layout(int strip_w, int item_count,
                                       int item_w, int gap, int scroll_x)
        {
            CarouselLayout c;
            c.first_visible = 0;
            c.last_visible = -1;
            c.content_w = 0;
            c.max_scroll = 0;
            c.scroll_x = 0;

            if (item_count <= 0 || item_w < 1)
                return c;

            const int stride = item_w + gap;

            c.content_w = item_count * item_w + (item_count - 1) * gap;
            c.max_scroll = c.content_w - strip_w;
            if (c.max_scroll < 0) c.max_scroll = 0;

            if (scroll_x < 0) scroll_x = 0;
            if (scroll_x > c.max_scroll) scroll_x = c.max_scroll;
            c.scroll_x = scroll_x;

            if (strip_w < 1)
                return c;

            int first = scroll_x / stride;
            if (first < 0) first = 0;
            if (first > item_count - 1) first = item_count - 1;

            // The -1 keeps a strip exactly one item wide reporting one item
            // rather than two.
            int last = (scroll_x + strip_w - 1) / stride;
            if (last > item_count - 1) last = item_count - 1;
            if (last < first) last = first;

            c.first_visible = first;
            c.last_visible = last;
            return c;
        }

        int carousel_item_x(int index, int item_w, int gap, int scroll_x)
        {
            return index * (item_w + gap) - scroll_x;
        }

        // ---------------------------------------------------------------------
        // List
        // ---------------------------------------------------------------------

        ListLayout list_layout(int viewport_h, int item_count,
                               int row_h, int scroll_y)
        {
            ListLayout l;
            l.first_visible = 0;
            l.last_visible = -1;
            l.content_h = 0;
            l.max_scroll = 0;
            l.scroll_y = 0;

            if (item_count <= 0 || row_h < 1)
                return l;

            l.content_h = item_count * row_h;
            l.max_scroll = l.content_h - viewport_h;
            if (l.max_scroll < 0) l.max_scroll = 0;

            if (scroll_y < 0) scroll_y = 0;
            if (scroll_y > l.max_scroll) scroll_y = l.max_scroll;
            l.scroll_y = scroll_y;

            if (viewport_h < 1)
                return l;

            int first = scroll_y / row_h;
            if (first < 0) first = 0;
            if (first > item_count - 1) first = item_count - 1;

            int last = (scroll_y + viewport_h - 1) / row_h;
            if (last > item_count - 1) last = item_count - 1;
            if (last < first) last = first;

            l.first_visible = first;
            l.last_visible = last;
            return l;
        }

        // ---------------------------------------------------------------------
        // Context menu
        // ---------------------------------------------------------------------

        MenuLayout menu_layout(int anchor_x, int anchor_y,
                               int screen_w, int screen_h,
                               int menu_w, int visible_rows, int visible_seps,
                               int row_h, int sep_h, int pad)
        {
            MenuLayout m;
            m.w = menu_w;
            m.h = pad * 2 + visible_rows * row_h + visible_seps * sep_h;

            m.x = anchor_x;
            m.y = anchor_y;

            // Right edge: hang the menu left of the anchor instead.
            if (m.x + m.w > screen_w)
                m.x = screen_w - m.w;
            if (m.x < 0)
                m.x = 0;

            // Bottom edge: rise above the anchor, but only when there is
            // actually more room up there. A menu taller than the screen
            // should not jump for no benefit.
            if (m.y + m.h > screen_h && anchor_y - m.h >= 0)
                m.y = anchor_y - m.h;
            if (m.y + m.h > screen_h)
                m.y = screen_h - m.h;
            if (m.y < 0)
                m.y = 0;

            return m;
        }

        int menu_filter(const bool *visible, const bool *is_separator, int count,
                        int *out_indices, int cap)
        {
            int n = 0;
            bool pending_sep = false;

            for (int i = 0; i < count; ++i)
            {
                if (!visible[i])
                    continue;

                if (is_separator[i])
                {
                    // Never leading, never doubled: hold it back until a real
                    // item earns it.
                    if (n > 0)
                        pending_sep = true;
                    continue;
                }

                if (pending_sep)
                {
                    pending_sep = false;
                    if (n < cap)
                        out_indices[n++] = -1; // caller renders a separator
                }

                if (n < cap)
                    out_indices[n++] = i;
            }

            // A separator still pending here would be trailing, so drop it.
            return n;
        }

        // ---------------------------------------------------------------------
        // Split button
        // ---------------------------------------------------------------------

        SplitButtonLayout split_button_layout(int x, int y, int w, int h,
                                              int caret_w)
        {
            SplitButtonLayout s;

            if (caret_w < 0) caret_w = 0;
            if (caret_w > w) caret_w = w;

            s.primary = gfx::rect(x, y, w - caret_w, h);
            s.caret = gfx::rect(x + w - caret_w, y, caret_w, h);
            return s;
        }

        // ---------------------------------------------------------------------
        // Lightbox
        // ---------------------------------------------------------------------

        gfx::Rect lightbox_fit(int img_w, int img_h, int box_w, int box_h)
        {
            if (img_w < 1 || img_h < 1 || box_w < 1 || box_h < 1)
                return gfx::rect(box_w / 2, box_h / 2, 0, 0);

            // Same shrink-to-fit as the fit_size helper in image_decoder.cpp:
            // scale down when oversized, and leave a small image at its
            // natural size rather than blowing it up into a blurry fullscreen.
            int w = img_w;
            int h = img_h;

            if (w > box_w)
            {
                h = h * box_w / w;
                w = box_w;
            }
            if (h > box_h)
            {
                w = w * box_h / h;
                h = box_h;
            }

            if (w < 1) w = 1;
            if (h < 1) h = 1;

            return gfx::rect((box_w - w) / 2, (box_h - h) / 2, w, h);
        }

        LightboxChrome lightbox_chrome(int x, int y, int w, int h,
                                       int btn, int text_h)
        {
            LightboxChrome c;

            const int margin = 12;

            c.close = gfx::rect(x + w - margin - btn, y + margin, btn, btn);
            c.prev = gfx::rect(x + margin, y + (h - btn) / 2, btn, btn);
            c.next = gfx::rect(x + w - margin - btn, y + (h - btn) / 2, btn, btn);

            // Full-width strip so the counter can be centred without the
            // caller re-deriving the middle.
            c.counter = gfx::rect(x, y + h - margin - text_h, w, text_h);

            return c;
        }

    } // namespace ui
} // namespace launcher
