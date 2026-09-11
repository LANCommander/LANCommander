#include "ui/widgets.h"
#include "ui/icons.h"
#include "ui/theme.h"

namespace launcher
{
    namespace ui
    {

        // ---------------------------------------------------------------------------
        // Overlay input gating
        // ---------------------------------------------------------------------------

        InputState input_blocked(const InputState &in)
        {
            InputState out;

            // Deliberately keep the window-level facts: a resize or a close
            // request is the OS talking, and an open dialog does not get to
            // veto either of those.
            out.quit_requested = in.quit_requested;
            out.resized = in.resized;
            out.resize_w = in.resize_w;
            out.resize_h = in.resize_h;

            // Everything a widget can react to goes away. The pointer is moved
            // far off-screen rather than left at 0,0, because 0,0 is inside
            // any widget anchored at the origin.
            out.mouse.x = -100000;
            out.mouse.y = -100000;

            return out;
        }

        // ---------------------------------------------------------------------------
        // Rounded rectangles
        // ---------------------------------------------------------------------------

        const int BUTTON_RADIUS = 2;
        const int BUTTON_PAD_X = 10;
        const int BUTTON_PAD_Y = 5;

        const int BUTTON_LARGE_PAD_X = 19;
        const int BUTTON_LARGE_PAD_Y = 11;

        namespace
        {
            // Horizontal inset of the rounded corner at row `dy` (0 = the
            // outermost row of the corner), for a quarter-circle of radius r
            // whose centre sits r pixels in from both edges.
            //
            // The smallest dx whose PIXEL CENTRE falls inside the circle:
            //
            //     (r - dx - 0.5)^2 + (r - dy - 0.5)^2 <= r^2
            //
            // doubled through so it stays integer arithmetic.
            int corner_inset(int r, int dy)
            {
                const int diameter_sq = 4 * r * r;
                const int ky = 2 * r - 2 * dy - 1;

                int dx = 0;
                while (dx < r)
                {
                    const int kx = 2 * r - 2 * dx - 1;
                    if (kx * kx + ky * ky <= diameter_sq)
                        break;
                    ++dx;
                }
                return dx;
            }

            int clamp_radius(const gfx::Rect &r, int radius)
            {
                if (radius < 0) radius = 0;
                const int half = (r.w < r.h ? r.w : r.h) / 2;
                return radius > half ? half : radius;
            }

            // Shared by the opaque and alpha fills, which differ only in the
            // span primitive they call.
            template <typename FillSpan>
            void rounded_spans(const gfx::Rect &r, int radius, FillSpan fill)
            {
                radius = clamp_radius(r, radius);

                if (radius <= 0)
                {
                    fill(r.x, r.y, r.w, r.h);
                    return;
                }

                for (int dy = 0; dy < radius; ++dy)
                {
                    const int inset = corner_inset(radius, dy);
                    const int w = r.w - inset * 2;
                    if (w <= 0)
                        continue;

                    fill(r.x + inset, r.y + dy, w, 1);
                    fill(r.x + inset, r.y + r.h - 1 - dy, w, 1);
                }

                const int mid_h = r.h - radius * 2;
                if (mid_h > 0)
                    fill(r.x, r.y + radius, r.w, mid_h);
            }
        } // namespace

        void fill_rounded_rect(gfx::Surface *s, const gfx::Rect &r, int radius,
                               gfx::Color c)
        {
            rounded_spans(r, radius, [s, c](int x, int y, int w, int h)
                          { gfx::fill_rect(s, gfx::rect(x, y, w, h), c); });
        }

        void fill_rounded_rect_alpha(gfx::Surface *s, const gfx::Rect &r, int radius,
                                     gfx::Color c)
        {
            rounded_spans(r, radius, [s, c](int x, int y, int w, int h)
                          { gfx::fill_rect_alpha(s, gfx::rect(x, y, w, h), c); });
        }

        void draw_rounded_rect(gfx::Surface *s, const gfx::Rect &r, int radius,
                               gfx::Color c)
        {
            radius = clamp_radius(r, radius);

            if (radius <= 0)
            {
                gfx::draw_rect(s, r, c);
                return;
            }

            // Straight runs first, then one pixel per corner row.
            gfx::hline(s, r.x + radius, r.y, r.w - radius * 2, c);
            gfx::hline(s, r.x + radius, r.y + r.h - 1, r.w - radius * 2, c);
            gfx::vline(s, r.x, r.y + radius, r.h - radius * 2, c);
            gfx::vline(s, r.x + r.w - 1, r.y + radius, r.h - radius * 2, c);

            for (int dy = 0; dy < radius; ++dy)
            {
                const int inset = corner_inset(radius, dy);
                gfx::fill_rect(s, gfx::rect(r.x + inset, r.y + dy, 1, 1), c);
                gfx::fill_rect(s, gfx::rect(r.x + r.w - 1 - inset, r.y + dy, 1, 1), c);
                gfx::fill_rect(s, gfx::rect(r.x + inset, r.y + r.h - 1 - dy, 1, 1), c);
                gfx::fill_rect(s, gfx::rect(r.x + r.w - 1 - inset,
                                            r.y + r.h - 1 - dy, 1, 1), c);
            }
        }

        InputState input_without_pointer(const InputState &in)
        {
            InputState out = in;

            // Off-screen rather than 0,0: the origin is inside any widget
            // anchored there, which is the trap input_blocked() documents.
            out.mouse.x = -100000;
            out.mouse.y = -100000;
            out.mouse.buttons = 0;
            out.mouse.wheel_delta = 0;
            out.mouse.clicked = false;
            out.mouse.pressed = false;

            return out;
        }

        void round_rect_corners(gfx::Surface *s, const gfx::Rect &r, int radius,
                                gfx::Color bg)
        {
            radius = clamp_radius(r, radius);
            if (radius <= 0)
                return;

            for (int dy = 0; dy < radius; ++dy)
            {
                const int inset = corner_inset(radius, dy);
                if (inset <= 0)
                    continue;

                gfx::fill_rect(s, gfx::rect(r.x, r.y + dy, inset, 1), bg);
                gfx::fill_rect(s, gfx::rect(r.x + r.w - inset, r.y + dy, inset, 1), bg);
                gfx::fill_rect(s, gfx::rect(r.x, r.y + r.h - 1 - dy, inset, 1), bg);
                gfx::fill_rect(s, gfx::rect(r.x + r.w - inset, r.y + r.h - 1 - dy,
                                            inset, 1), bg);
            }
        }

        // ---------------------------------------------------------------------------
        // Button
        // ---------------------------------------------------------------------------

        namespace
        {
            // Avalonia's IconButton spaces its icon from its label by
            // (Size / 2) + 2, which is 10 at the 16px icon the UI uses.
            const int ICON_LABEL_GAP = 8;

            struct Fill
            {
                gfx::Color base;
                gfx::Color hover;
                gfx::Color active;
            };

            Fill fill_for(ButtonStyle style)
            {
                Fill f;
                switch (style)
                {
                case ButtonStyle::Primary:
                    f.base = theme().button_primary;
                    f.hover = theme().button_primary_hover;
                    f.active = theme().button_primary_active;
                    break;
                case ButtonStyle::Error:
                    f.base = theme().error;
                    f.hover = theme().error_hover;
                    f.active = theme().error_hover;
                    break;
                default:
                    f.base = theme().button_bg;
                    f.hover = theme().button_bg_hover;
                    f.active = theme().button_bg_active;
                    break;
                }
                return f;
            }
        }

        int button_height()
        {
            return text_height() + BUTTON_PAD_Y * 2;
        }

        int button_height_large()
        {
            return text_height() + BUTTON_LARGE_PAD_Y * 2;
        }

        namespace
        {
            int width_at(const char *label, Icon icon, int pad_x)
            {
                int w = pad_x * 2;
                if (label && *label)
                    w += text_width(label);
                if (icon != Icon::None)
                {
                    w += ICON_MD;
                    if (label && *label)
                        w += ICON_LABEL_GAP;
                }
                return w;
            }
        }

        int button_width(const char *label, Icon icon)
        {
            return width_at(label, icon, BUTTON_PAD_X);
        }

        int button_width_large(const char *label, Icon icon)
        {
            return width_at(label, icon, BUTTON_LARGE_PAD_X);
        }

        ButtonState icon_button(gfx::Surface *s, int x, int y, int w, int h,
                                Icon icon, const char *label,
                                const InputState &input, ButtonStyle style)
        {
            ButtonState state;
            state.hovered = (input.mouse.x >= x && input.mouse.x < x + w &&
                             input.mouse.y >= y && input.mouse.y < y + h);
            state.clicked = state.hovered && input.mouse.clicked;

            const Fill f = fill_for(style);

            // Pressed beats hover: the pointer is over the button in both
            // states, and Avalonia's :pressed selector wins the same way.
            const gfx::Color bg = !state.hovered  ? f.base
                                  : input.mouse.buttons ? f.active
                                                        : f.hover;

            fill_rounded_rect(s, gfx::rect(x, y, w, h), BUTTON_RADIUS, bg);

            const bool has_label = (label && *label);
            const int lw = has_label ? text_width(label) : 0;
            const int iw = (icon != Icon::None) ? ICON_MD : 0;
            const int gap = (iw && has_label) ? ICON_LABEL_GAP : 0;

            int cx = x + (w - (iw + gap + lw)) / 2;

            // Default buttons carry body text; the coloured ones use white,
            // as ButtonPrimaryText / ButtonErrorText do.
            const gfx::Color fg = (style == ButtonStyle::Default)
                                      ? theme().text
                                      : theme().text_bright;

            if (iw)
            {
                draw_icon(s, cx, y + (h - ICON_MD) / 2, ICON_MD, fg, icon);
                cx += iw + gap;
            }

            if (has_label)
                draw_text(s, cx, y + (h - text_height()) / 2, fg, label);

            return state;
        }

        ButtonState button(gfx::Surface *s, int x, int y, int w, int h, const char *label,
                           const InputState &input, ButtonStyle style)
        {
            return icon_button(s, x, y, w, h, Icon::None, label, input, style);
        }

        // ---------------------------------------------------------------------------
        // Badge
        // ---------------------------------------------------------------------------
        //
        // Mirrors the Avalonia Button.Badge style scaled by 0.8: Padding
        // 10,5 and CornerRadius 2 become 8,4 and radius 2, and its FontSize
        // 12 against a 16 base becomes the Small rung.

        namespace
        {
            const int BADGE_PAD_X = 8;
            const int BADGE_PAD_Y = 4;
            const int BADGE_RADIUS = 2;
            const FontSize BADGE_FONT = FontSize::Small;
        }

        int badge_width(const char *label)
        {
            return text_width(label, BADGE_FONT) + BADGE_PAD_X * 2;
        }

        int badge_height()
        {
            return text_height(BADGE_FONT) + BADGE_PAD_Y * 2;
        }

        ButtonState badge(gfx::Surface *s, int x, int y, const char *label,
                          bool interactive, const InputState &input)
        {
            ButtonState state;
            state.hovered = false;
            state.clicked = false;

            const gfx::Rect r = gfx::rect(x, y, badge_width(label), badge_height());

            if (interactive)
            {
                state.hovered = gfx::rect_contains(r, input.mouse.x, input.mouse.y);
                state.clicked = state.hovered && input.mouse.clicked;
            }

            fill_rounded_rect(s, r, BADGE_RADIUS,
                              state.hovered ? theme().panel_hover : theme().panel);

            draw_text(s, x + BADGE_PAD_X, y + BADGE_PAD_Y,
                      state.hovered ? theme().text_bright : theme().text, label,
                      BADGE_FONT);

            return state;
        }

        // ---------------------------------------------------------------------------
        // Label
        // ---------------------------------------------------------------------------

        void label(gfx::Surface *s, int x, int y, gfx::Color color, const char *text)
        {
            draw_text(s, x, y, color, text);
        }

        // ---------------------------------------------------------------------------
        // Panel
        // ---------------------------------------------------------------------------

        void panel(gfx::Surface *s, int x, int y, int w, int h, gfx::Color color)
        {
            gfx::fill_rect(s, gfx::rect(x, y, w, h), color);
        }

        // ---------------------------------------------------------------------------
        // Divider
        // ---------------------------------------------------------------------------

        void divider(gfx::Surface *s, int x, int y, int w)
        {
            gfx::hline(s, x, y, w, theme().divider);
        }

        // ---------------------------------------------------------------------------
        // Scrollbar
        // ---------------------------------------------------------------------------

        void scrollbar(gfx::Surface *s, int x, int y, int h,
                       int content_h, int viewport_h, ScrollState &state,
                       const InputState &input)
        {
            int &scroll_y = state.offset;

            if (content_h <= viewport_h)
                return; // no scrollbar needed

            int track_w = 12;
            int max_scroll = content_h - viewport_h;
            if (max_scroll < 1) max_scroll = 1;

            // Thumb size and position
            int thumb_h = h * viewport_h / content_h;
            if (thumb_h < 24) thumb_h = 24;
            if (thumb_h > h) thumb_h = h;

            int thumb_y = y;
            if (max_scroll > 0)
                thumb_y = y + (h - thumb_h) * scroll_y / max_scroll;

            // --- Interaction ---
            bool in_track = (input.mouse.x >= x && input.mouse.x < x + track_w &&
                             input.mouse.y >= y && input.mouse.y < y + h);
            bool in_thumb = (input.mouse.x >= x && input.mouse.x < x + track_w &&
                             input.mouse.y >= thumb_y && input.mouse.y < thumb_y + thumb_h);

            // Start drag on thumb press
            if (in_thumb && input.mouse.pressed)
            {
                state.dragging = true;
                state.drag_offset = input.mouse.y - thumb_y;
            }

            // Click on track (outside thumb) — jump to that position
            if (in_track && !in_thumb && input.mouse.pressed)
            {
                int target_thumb_y = input.mouse.y - thumb_h / 2;
                if (target_thumb_y < y) target_thumb_y = y;
                if (target_thumb_y > y + h - thumb_h) target_thumb_y = y + h - thumb_h;
                int track_range = h - thumb_h;
                scroll_y = (track_range > 0)
                    ? (target_thumb_y - y) * max_scroll / track_range
                    : 0;
                state.dragging = true;
                state.drag_offset = thumb_h / 2;
            }

            // Continue drag while mouse button is held
            if (state.dragging)
            {
                if (input.mouse.buttons & 1)
                {
                    int target_thumb_y = input.mouse.y - state.drag_offset;
                    if (target_thumb_y < y) target_thumb_y = y;
                    if (target_thumb_y > y + h - thumb_h) target_thumb_y = y + h - thumb_h;
                    int track_range = h - thumb_h;
                    scroll_y = (track_range > 0)
                        ? (target_thumb_y - y) * max_scroll / track_range
                        : 0;
                }
                else
                {
                    state.dragging = false;
                }
            }

            // Clamp
            if (scroll_y < 0) scroll_y = 0;
            if (scroll_y > max_scroll) scroll_y = max_scroll;

            // Recalculate thumb_y after possible scroll change
            thumb_y = y + (h - thumb_h) * scroll_y / max_scroll;

            // --- Draw ---
            bool hovered = in_track || state.dragging;

            // Track
            gfx::fill_rect(s, gfx::rect(x, y, track_w, h), theme().panel);

            // Thumb
            gfx::Color thumb_color = state.dragging ? theme().text
                                   : hovered       ? theme().text_dim
                                   :                 theme().text_disabled;
            gfx::fill_rect(s, gfx::rect(x, thumb_y, track_w, thumb_h), thumb_color);
        }

        // ---------------------------------------------------------------------------
        // Modal backdrop
        // ---------------------------------------------------------------------------

        void modal_backdrop(gfx::Surface *s, int sw, int sh)
        {
            gfx::fill_rect_alpha(s, gfx::rect(0, 0, sw, sh), gfx::rgba(0, 0, 0, 170));
        }

        // ---------------------------------------------------------------------------
        // Checkbox
        // ---------------------------------------------------------------------------

        bool checkbox(gfx::Surface *s, int x, int y, const char *label_text,
                      bool &checked, const InputState &input)
        {
            int cb_size = 16;
            int th = text_height();
            int row_h = (th > cb_size) ? th : cb_size;
            int cb_y = y + (row_h - cb_size) / 2;

            gfx::draw_rect(s, gfx::rect(x, cb_y, cb_size, cb_size), theme().input_border);

            if (checked)
                gfx::fill_rect(s, gfx::rect(x + 3, cb_y + 3, cb_size - 6, cb_size - 6),
                               theme().primary);

            int lx = x + cb_size + 8;
            draw_text(s, lx, y + (row_h - th) / 2, theme().text, label_text);

            int hit_w = cb_size + 8 + text_width(label_text);
            bool hovered = (input.mouse.x >= x && input.mouse.x < x + hit_w &&
                            input.mouse.y >= y && input.mouse.y < y + row_h);
            if (hovered && input.mouse.clicked)
            {
                checked = !checked;
                return true;
            }
            return false;
        }

    } // namespace ui
} // namespace launcher
