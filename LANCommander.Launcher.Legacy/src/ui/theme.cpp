#include "ui/theme.h"
#include "ui/font.h"

#include <string>

namespace launcher
{
    namespace ui
    {

        namespace
        {
            // Color palette from the Avalonia launcher (ColorPalette.axaml).
            // A plain constant table — the old version had to run after
            // set_color_depth() because it called Allegro's makecol().
            const Theme g_theme = {
                gfx::rgb(0x00, 0x00, 0x00), // bg             Gray1 — BackgroundLayout
                gfx::rgb(0x14, 0x14, 0x14), // surface        Gray2 — BackgroundBase
                gfx::rgb(0x1F, 0x1F, 0x1F), // panel          Gray3
                gfx::rgb(0x28, 0x28, 0x28), // panel_hover    Gray4
                gfx::rgb(0x16, 0x77, 0xFF), // primary        ColorPrimary / Primary6
                gfx::rgb(0x17, 0x7D, 0xDC), // button_primary        ButtonPrimaryBg
                gfx::rgb(0x09, 0x5C, 0xB5), // button_primary_hover  ButtonPrimaryHover
                gfx::rgb(0x09, 0x58, 0xD9), // button_primary_active ButtonPrimaryActive
                gfx::rgb(0x30, 0x30, 0x30), // button_bg             ButtonDefaultBg
                gfx::rgb(0x3A, 0x3A, 0x3A), // button_bg_hover       ButtonDefaultHover
                gfx::rgb(0x42, 0x42, 0x42), // button_bg_active      ButtonDefaultActive
                gfx::rgb(0xD9, 0xD9, 0xD9), // text           ~87% white
                gfx::rgb(0xA6, 0xA6, 0xA6), // text_dim       ~65% white
                gfx::rgb(0x40, 0x40, 0x40), // text_disabled  TextDisabled, 25% white on black
                gfx::rgb(0xFF, 0xFF, 0xFF), // text_bright    Gray13
                gfx::rgb(0x14, 0x14, 0x14), // input_bg       Gray2
                gfx::rgb(0x42, 0x42, 0x42), // input_border   Gray7
                gfx::rgb(0x16, 0x77, 0xFF), // input_focus    Primary6
                gfx::rgb(0x30, 0x30, 0x30), // divider        Gray5
                gfx::rgb(0xDC, 0x44, 0x46), // error          ColorError
                gfx::rgb(0xE9, 0x62, 0x63), // error_hover
                gfx::rgb(0x49, 0xAA, 0x19), // success        ColorSuccess
                gfx::rgb(0xD8, 0x96, 0x14), // warning        ColorWarning
                gfx::rgb(0x00, 0x00, 0x00), // footer         Black
                gfx::rgba(0xFF, 0xFF, 0xFF, 0x1A), // ghost_hover   ButtonTextHoverBg
                gfx::rgba(0xFF, 0xFF, 0xFF, 0x2A), // ghost_active  ButtonTextPressedBg
            };

            enum Align { ALIGN_LEFT, ALIGN_CENTER };

            // How many bytes of `seg` to put on one line, breaking at a word
            // boundary where possible.
            //
            // The old version called text_width() once per character, building
            // a fresh std::string each time — O(n^2) per line, on text that is
            // often measured without ever being drawn. font_fit() answers
            // "how much fits in max_w" in a single call on both backends, so
            // this is now one measurement per line.
            size_t line_break(const std::string &seg, int max_w, FontSize size)
            {
                if (seg.empty())
                    return 0;

                int w = 0;
                int fit = font_fit(seg.c_str(), max_w, &w, size);

                if (fit >= (int)seg.size())
                    return seg.size();

                // Always consume at least one byte, or a glyph wider than
                // max_w would loop forever.
                if (fit <= 0)
                    return 1;

                // Back up to the last space at or before the fit point, so we
                // break between words. Matches the previous behaviour: the
                // break lands *before* the space, and the next line skips it.
                for (int i = fit; i > 0; --i)
                {
                    if (seg[i] == ' ')
                        return (size_t)i;
                }

                // Single word longer than the line — hard break.
                return (size_t)fit;
            }

            // Shared implementation behind draw_text_wrap and
            // draw_text_wrap_center, which were byte-identical apart from
            // the draw call.
            int wrap_impl(gfx::Surface *s, int x, int y, int max_w, gfx::Color color,
                          const char *text, FontSize size, int line_spacing,
                          Align align)
            {
                if (!text || !*text)
                    return 0;

                const int th = text_height(size);
                int total_h = 0;
                const char *p = text;

                while (*p)
                {
                    // Skip leading spaces (except at the very start).
                    if (total_h > 0)
                        while (*p == ' ') p++;

                    if (!*p) break;

                    // Handle explicit newlines.
                    if (*p == '\n') { total_h += th + line_spacing; p++; continue; }
                    if (*p == '\r') { p++; continue; }

                    // Measure only up to the next hard break.
                    const char *nl = p;
                    while (*nl && *nl != '\n' && *nl != '\r') ++nl;

                    std::string seg(p, (size_t)(nl - p));
                    size_t take = line_break(seg, max_w, size);
                    if (take == 0)
                        take = seg.size();

                    if (s)
                    {
                        std::string row = seg.substr(0, take);
                        if (align == ALIGN_CENTER)
                            draw_text_center(s, x, y + total_h, color, row.c_str(), size);
                        else
                            draw_text(s, x, y + total_h, color, row.c_str(), size);
                    }

                    total_h += th + line_spacing;
                    p += take;
                }

                return total_h;
            }
        } // namespace

        bool theme_init()
        {
            return font_init();
        }

        void theme_shutdown()
        {
            font_shutdown();
        }

        const Theme &theme()
        {
            return g_theme;
        }

        void draw_text(gfx::Surface *s, int x, int y, gfx::Color color, const char *text,
                       FontSize size)
        {
            font_draw(s, x, y, color, text, size);
        }

        void draw_text_center(gfx::Surface *s, int cx, int y, gfx::Color color, const char *text,
                              FontSize size)
        {
            font_draw(s, cx - font_measure(text, size) / 2, y, color, text, size);
        }

        void draw_text_right(gfx::Surface *s, int rx, int y, gfx::Color color, const char *text,
                             FontSize size)
        {
            font_draw(s, rx - font_measure(text, size), y, color, text, size);
        }

        FontSize font_size_near(int px)
        {
            FontSize best = FontSize::Caption;
            int best_d = -1;

            for (int i = 0; i < (int)FontSize::Count; ++i)
            {
                const int d = font_px((FontSize)i) - px;
                const int ad = d < 0 ? -d : d;
                if (best_d < 0 || ad < best_d)
                {
                    best_d = ad;
                    best = (FontSize)i;
                }
            }

            return best;
        }

        int text_width(const char *text, FontSize size)
        {
            return font_measure(text, size);
        }

        int text_height(FontSize size)
        {
            return font_height(size);
        }

        int draw_text_wrap(gfx::Surface *s, int x, int y, int max_w, gfx::Color color,
                           const char *text, FontSize size, int line_spacing)
        {
            return wrap_impl(s, x, y, max_w, color, text, size, line_spacing, ALIGN_LEFT);
        }

        int draw_text_wrap_center(gfx::Surface *s, int cx, int y, int max_w, gfx::Color color,
                                  const char *text, FontSize size, int line_spacing)
        {
            return wrap_impl(s, cx, y, max_w, color, text, size, line_spacing, ALIGN_CENTER);
        }

    } // namespace ui
} // namespace launcher
