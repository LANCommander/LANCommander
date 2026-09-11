#include "ui/widgets_menu.h"
#include "ui/icons.h"
#include "ui/layout.h"
#include "ui/theme.h"
#include "ui/widgets.h"

namespace launcher
{
    namespace ui
    {

        namespace
        {
            const int CARET_W = 26;
            const int ROW_H = 22;
            const int SEP_H = 7;
            const int MENU_PAD = 4;
            const int MENU_MIN_W = 170;
            const int LABEL_PAD = 10;
        } // namespace

        SplitButtonResult split_button(gfx::Surface *s, int x, int y, int w, int h,
                                       const char *label, bool enabled,
                                       gfx::Color bg, gfx::Color bg_hover,
                                       const InputState &input)
        {
            SplitButtonResult r;

            const SplitButtonLayout lay = split_button_layout(x, y, w, h, CARET_W);

            const bool over_primary =
                gfx::rect_contains(lay.primary, input.mouse.x, input.mouse.y);
            const bool over_caret =
                gfx::rect_contains(lay.caret, input.mouse.x, input.mouse.y);

            r.hovered = over_primary || over_caret;

            // Drawn as one rounded slab, then the hovered half is repainted
            // over it: that keeps the four outer corners round while the seam
            // down the middle stays square.
            fill_rounded_rect(s, gfx::rect(lay.primary.x, y,
                                           lay.primary.w + lay.caret.w, h),
                              BUTTON_RADIUS,
                              !enabled ? theme().panel : bg);

            if (enabled && over_primary)
            {
                fill_rounded_rect(s, lay.primary, BUTTON_RADIUS, bg_hover);
                gfx::fill_rect(s, gfx::rect(lay.primary.x + lay.primary.w - BUTTON_RADIUS,
                                            y, BUTTON_RADIUS, h), bg_hover);
            }
            else if (enabled && over_caret)
            {
                fill_rounded_rect(s, lay.caret, BUTTON_RADIUS, bg_hover);
                gfx::fill_rect(s, gfx::rect(lay.caret.x, y, BUTTON_RADIUS, h), bg_hover);
            }

            // A one-pixel gap so the two halves read as two targets rather
            // than one wide button.
            gfx::vline(s, lay.caret.x, y + 4, h - 8,
                       gfx::rgba(0, 0, 0, 90));

            const gfx::Color fg = enabled ? theme().text_bright : theme().text_disabled;

            draw_text_center(s, lay.primary.x + lay.primary.w / 2,
                             y + (h - text_height()) / 2, fg, label);

            draw_icon_centered(s, lay.caret, ICON_SM, fg, Icon::CaretDown);

            if (enabled)
            {
                r.primary_clicked = over_primary && input.mouse.clicked;
                r.caret_clicked = over_caret && input.mouse.clicked;
            }

            return r;
        }

        void menu_open(MenuState &state, int x, int y)
        {
            state.open = true;
            state.anchor_x = x;
            state.anchor_y = y;
            state.opened_this_frame = true;
        }

        int context_menu(gfx::Surface *s, int screen_w, int screen_h,
                         const MenuItemDef *items, int count,
                         MenuState &state, const InputState &input)
        {
            if (!state.open || !items || count <= 0)
                return 0;

            if (input.key_pressed(Key::Escape))
            {
                state.open = false;
                return 0;
            }

            // --- Size -----------------------------------------------------
            int rows = 0;
            int seps = 0;
            int widest = MENU_MIN_W;

            for (int i = 0; i < count; ++i)
            {
                if (!items[i].label)
                {
                    ++seps;
                    continue;
                }
                ++rows;
                const int w = text_width(items[i].label) + LABEL_PAD * 2;
                if (w > widest)
                    widest = w;
            }

            const MenuLayout m = menu_layout(state.anchor_x, state.anchor_y,
                                             screen_w, screen_h, widest,
                                             rows, seps, ROW_H, SEP_H, MENU_PAD);

            // --- Draw ------------------------------------------------------
            fill_rounded_rect(s, gfx::rect(m.x, m.y, m.w, m.h), BUTTON_RADIUS,
                              theme().surface);
            draw_rounded_rect(s, gfx::rect(m.x, m.y, m.w, m.h), BUTTON_RADIUS,
                              theme().divider);

            int chosen = 0;
            int iy = m.y + MENU_PAD;

            for (int i = 0; i < count; ++i)
            {
                if (!items[i].label)
                {
                    gfx::hline(s, m.x + 6, iy + SEP_H / 2, m.w - 12, theme().divider);
                    iy += SEP_H;
                    continue;
                }

                const gfx::Rect row = gfx::rect(m.x + 1, iy, m.w - 2, ROW_H);
                const bool over = gfx::rect_contains(row, input.mouse.x, input.mouse.y);

                if (over && items[i].enabled)
                    gfx::fill_rect(s, row, theme().panel_hover);

                draw_text(s, m.x + LABEL_PAD, iy + (ROW_H - text_height()) / 2,
                          items[i].enabled ? theme().text : theme().text_disabled,
                          items[i].label);

                if (over && items[i].enabled && input.mouse.clicked)
                    chosen = items[i].command;

                iy += ROW_H;
            }

            // Any click closes the menu, including one that missed every row.
            // Without this, clicking away would leave it open with the click
            // already consumed by whatever is underneath.
            //
            // Except the click that opened it, which is still live this frame.
            if (state.opened_this_frame)
                state.opened_this_frame = false;
            else if (input.mouse.clicked)
                state.open = false;

            return chosen;
        }

    } // namespace ui
} // namespace launcher
