#ifndef LAUNCHER_UI_FONT_H
#define LAUNCHER_UI_FONT_H

#include "gfx/gfx.h"

// Text rasterisation. One implementation per graphics backend:
//   font_ttf.cpp — SDL_ttf + FreeType against a bundled TTF
//   font_gdi.cpp — GDI TrueType from the host font collection (transitional)
//
// theme.cpp is the only caller; screens go through draw_text() there.

namespace launcher
{
    namespace ui
    {

        // `px_size` is the nominal character height the UI was designed
        // around (13). Each backend maps it to whatever its own API wants.
        bool font_init(int px_size);
        void font_shutdown();

        // Line height. Every vertical measurement in the UI derives from this.
        int font_height();

        // Rendered width of a UTF-8 string.
        int font_measure(const char *utf8);

        // How many bytes of `utf8` fit within `max_w` pixels, and how wide
        // that prefix is. Both backends have a native primitive for this
        // (TTF_MeasureString / GetTextExtentExPoint), which is what makes
        // word wrap one measurement per line instead of one per character.
        int font_fit(const char *utf8, int max_w, int *out_w);

        // Draw `utf8` with its top-left at (x, y).
        void font_draw(gfx::Surface *dst, int x, int y, gfx::Color color,
                       const char *utf8);

    } // namespace ui
} // namespace launcher

#endif // LAUNCHER_UI_FONT_H
