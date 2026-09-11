#ifndef LAUNCHER_UI_FONT_H
#define LAUNCHER_UI_FONT_H

#include "gfx/gfx.h"

// Text rasterisation. One implementation per graphics backend:
//   font_ttf.cpp — SDL_ttf + FreeType against a bundled TTF
//   font_gdi.cpp — GDI TrueType from the host font collection (transitional)
//   font_stb.cpp — stb_truetype against the same bundled TTF (DOS)
//   font_fake.cpp — fixed metrics, no rasterisation (headless tests)
//
// theme.cpp is the only caller; screens go through draw_text() there.

namespace launcher
{
    namespace ui
    {

        // The type scale, as a ladder rather than free pixel sizes.
        //
        // The launcher used to render every label at one size, which is the
        // main reason its screens do not read like the Avalonia launcher's:
        // that UI has a real hierarchy — 11px for a transfer rate, 16px for
        // body copy, 32px for a game title — and flattening it loses the
        // shape of every page.
        //
        // The rungs are the Avalonia sizes scaled by 0.8, which is the ratio
        // the two launchers already sit at: Avalonia's base FontSize is 16 to
        // this one's 13, and its title bar is 40px to this one's 32. Sizes
        // that land within a pixel of each other after scaling share a rung,
        // because at these sizes they would not be distinguishable anyway.
        //
        //   Rung      here   Avalonia   used for
        //   Caption     9        11     transfer rates, timestamps, counts
        //   Small      10      12-13    badges, title bar, list captions
        //   Body       13        16     default; everything unmarked
        //   Section    15      18-20    section and page headers
        //   Title      19        24     settings header, banner title
        //   Display    26        32     game detail title
        enum class FontSize
        {
            Caption = 0,
            Small,
            Body,
            Section,
            Title,
            Display,
            Count
        };

        // Pixel size of a rung. Inline so the four backends map to a point
        // size or a cell height from one table rather than four.
        inline int font_px(FontSize size)
        {
            switch (size)
            {
            case FontSize::Caption: return 9;
            case FontSize::Small:   return 10;
            case FontSize::Section: return 15;
            case FontSize::Title:   return 19;
            case FontSize::Display: return 26;
            default:                return 13; // Body
            }
        }

        // Open every rung. False if any of them could not be opened, which is
        // fatal to App::init for the reason theme_init() documents.
        bool font_init();
        void font_shutdown();

        // Line height of a rung. Every vertical measurement in the UI derives
        // from one of these.
        int font_height(FontSize size);

        // Rendered width of a UTF-8 string at `size`.
        int font_measure(const char *utf8, FontSize size);

        // How many bytes of `utf8` fit within `max_w` pixels, and how wide
        // that prefix is. Both backends have a native primitive for this
        // (TTF_MeasureString / GetTextExtentExPoint), which is what makes
        // word wrap one measurement per line instead of one per character.
        int font_fit(const char *utf8, int max_w, int *out_w, FontSize size);

        // Draw `utf8` with its top-left at (x, y).
        void font_draw(gfx::Surface *dst, int x, int y, gfx::Color color,
                       const char *utf8, FontSize size);

    } // namespace ui
} // namespace launcher

#endif // LAUNCHER_UI_FONT_H
