#include "ui/widgets_overlay.h"
#include "ui/icons.h"
#include "ui/widgets.h"
#include "ui/theme.h"
#include "ui/layout.h"

#include <cstdio>

namespace launcher
{
    namespace ui
    {

        gfx::Rect dialog_begin(gfx::Surface *s, int screen_w, int screen_h,
                               int w, int h)
        {
            modal_backdrop(s, screen_w, screen_h);

            const int x = (screen_w - w) / 2;
            const int y = (screen_h - h) / 2;

            gfx::fill_rect(s, gfx::rect(x, y, w, h), theme().panel);
            gfx::draw_rect(s, gfx::rect(x, y, w, h), theme().divider);

            // Clipping to the panel means a dialog whose content outgrows it
            // spills nothing onto the screen behind.
            gfx::push_clip(s, gfx::rect(x, y, w, h));

            return gfx::rect(x, y, w, h);
        }

        void dialog_end(gfx::Surface *s)
        {
            gfx::pop_clip(s);
        }

        namespace
        {
            const int LB_BTN = 34;

            bool draw_lb_button(gfx::Surface *s, const gfx::Rect &r,
                                Icon icon, bool enabled,
                                const InputState &input)
            {
                const bool over = enabled &&
                                  gfx::rect_contains(r, input.mouse.x, input.mouse.y);

                fill_rounded_rect_alpha(s, r, BUTTON_RADIUS,
                                        gfx::rgba(0, 0, 0, over ? 210 : 140));
                draw_rounded_rect(s, r, BUTTON_RADIUS,
                                  enabled ? theme().divider : theme().panel);

                draw_icon_centered(s, r, ICON_LG,
                                   enabled ? theme().text_bright : theme().text_disabled,
                                   icon);

                return over && input.mouse.clicked;
            }
        } // namespace

        LightboxAction lightbox(gfx::Surface *s, const gfx::Rect &area,
                                gfx::Surface *image, int index, int count,
                                const InputState &input)
        {
            // Almost opaque rather than the dialog's 170: this is a viewer,
            // and the page behind it should not compete with the picture.
            gfx::fill_rect_alpha(s, area, gfx::rgba(0, 0, 0, 238));

            const LightboxChrome c = lightbox_chrome(area.x, area.y, area.w, area.h,
                                                     LB_BTN, text_height());

            // --- Image -------------------------------------------------------
            if (image)
            {
                // Already decoded to fit: the caller asks ImageCache for the
                // viewport size, and decode_image_file scales on the way in.
                // Fitting again here would only re-derive the same rectangle.
                gfx::blit(s, image,
                          area.x + (area.w - gfx::surface_width(image)) / 2,
                          area.y + (area.h - gfx::surface_height(image)) / 2);
            }
            else
            {
                draw_text_center(s, area.x + area.w / 2, area.y + area.h / 2,
                                 theme().text_dim, "Loading...");
            }

            // --- Counter -----------------------------------------------------
            if (count > 1)
            {
                char buf[32];
                std::sprintf(buf, "%d / %d", index + 1, count);
                draw_text_center(s, area.x + area.w / 2, c.counter.y,
                                 theme().text_dim, buf);
            }

            // --- Controls ----------------------------------------------------
            LightboxAction action = LightboxAction::None;

            if (draw_lb_button(s, c.close, Icon::Close, true, input))
                action = LightboxAction::Close;

            if (count > 1)
            {
                if (draw_lb_button(s, c.prev, Icon::CaretLeft, index > 0, input))
                    action = LightboxAction::Prev;
                if (draw_lb_button(s, c.next, Icon::CaretRight, index < count - 1, input))
                    action = LightboxAction::Next;
            }

            // --- Keys --------------------------------------------------------
            if (input.key_pressed(Key::Escape))
                action = LightboxAction::Close;
            else if (input.key_pressed(Key::Left) && index > 0)
                action = LightboxAction::Prev;
            else if (input.key_pressed(Key::Right) && index < count - 1)
                action = LightboxAction::Next;

            return action;
        }

    } // namespace ui
} // namespace launcher
