#include "ui/theme.h"
#include "ui/gdi_font.h"

#include <allegro.h>

namespace launcher
{
    namespace ui
    {

        static Theme g_theme;
        static bool g_gdi_font_active = false;

        void theme_init()
        {
            // Color palette from the Avalonia launcher (ColorPalette.axaml).
            g_theme.bg             = makecol(0x00, 0x00, 0x00); // Gray1 — BackgroundLayout
            g_theme.surface        = makecol(0x14, 0x14, 0x14); // Gray2 — BackgroundBase
            g_theme.panel          = makecol(0x1F, 0x1F, 0x1F); // Gray3
            g_theme.panel_hover    = makecol(0x28, 0x28, 0x28); // Gray4
            g_theme.primary        = makecol(0x16, 0x77, 0xFF); // ColorPrimary / Primary6
            g_theme.primary_hover  = makecol(0x40, 0x96, 0xFF); // Primary5
            g_theme.primary_active = makecol(0x09, 0x58, 0xD9); // Primary7
            g_theme.text           = makecol(0xD9, 0xD9, 0xD9); // ~87% white
            g_theme.text_dim       = makecol(0xA6, 0xA6, 0xA6); // ~65% white
            g_theme.text_disabled  = makecol(0x59, 0x59, 0x59); // Gray8 / ~25% white
            g_theme.text_bright    = makecol(0xFF, 0xFF, 0xFF); // Gray13
            g_theme.input_bg       = makecol(0x14, 0x14, 0x14); // Gray2
            g_theme.input_border   = makecol(0x42, 0x42, 0x42); // Gray7
            g_theme.input_focus    = makecol(0x16, 0x77, 0xFF); // Primary6
            g_theme.divider        = makecol(0x30, 0x30, 0x30); // Gray5
            g_theme.error          = makecol(0xDC, 0x44, 0x46); // ColorError
            g_theme.error_hover    = makecol(0xE9, 0x62, 0x63); // Error hover
            g_theme.success        = makecol(0x49, 0xAA, 0x19); // ColorSuccess
            g_theme.warning        = makecol(0xD8, 0x96, 0x14); // ColorWarning
            g_theme.footer         = makecol(0x00, 0x00, 0x00); // Black

            // TrueType font: Inter → Segoe UI → Arial, 13pt.
            gdi_font_init(13);
            g_gdi_font_active = (gdi_font_height() > 0);
        }

        const Theme &theme()
        {
            return g_theme;
        }

        void draw_text(BITMAP *bmp, int x, int y, int color, const char *text)
        {
            if (g_gdi_font_active)
                gdi_font_draw(bmp, x, y, color, text);
            else
                textout_ex(bmp, font, text, x, y, color, -1);
        }

        void draw_text_center(BITMAP *bmp, int cx, int y, int color, const char *text)
        {
            if (g_gdi_font_active)
                gdi_font_draw_center(bmp, cx, y, color, text);
            else
                textout_centre_ex(bmp, font, text, cx, y, color, -1);
        }

        void draw_text_right(BITMAP *bmp, int rx, int y, int color, const char *text)
        {
            if (g_gdi_font_active)
                gdi_font_draw_right(bmp, rx, y, color, text);
            else
                textout_right_ex(bmp, font, text, rx, y, color, -1);
        }

        int text_width(const char *text)
        {
            if (g_gdi_font_active)
                return gdi_font_text_width(text);
            return text_length(font, text);
        }

        int text_height()
        {
            if (g_gdi_font_active)
                return gdi_font_height();
            return ::text_height(font);
        }

    } // namespace ui
} // namespace launcher
