#ifndef LAUNCHER_UI_ICONS_H
#define LAUNCHER_UI_ICONS_H

#include "gfx/gfx.h"

// Icons, as tinted raster masks.
//
// The UI used to spell its icons with letters — "X" to close, "v" for a
// dropdown caret, "<" and ">" for the carousel arrows. That reads as debug
// scaffolding, and it puts the icon set at the mercy of whichever font backend
// is compiled in.
//
// These are PNGs instead: Phosphor's Regular weight, the same set and the same
// weight the Avalonia launcher draws, rasterised offline by
// tools/generate-icons.py into assets/icons/. Vector at runtime was the
// alternative and is not worth it here — there is no rasteriser in this stack,
// the UI is fixed-DPI, and stb already decodes PNG for cover art.
//
// Regular, not Fill. Avalonia renders these as a hairline STROKE over the
// Phosphor centreline geometry (App.axaml: Fill="Transparent", Stroke bound to
// Foreground, StrokeThickness="1"), so the icons in that launcher are outlines.
// Filling them here, which is what this used to do, made every icon read two
// weights heavier than its counterpart.
//
// Each file is stored once at 64px and downscaled on demand through the same
// Mitchell filter the image cache uses, so any size is available without a
// size matrix on disk. Files are white RGB with coverage in alpha — the mask
// shape gfx::blit_tinted() takes — so one file serves every theme colour.

namespace launcher
{
    namespace ui
    {

        enum class Icon
        {
            None = 0,
            ArrowLeft,
            CaretLeft,
            CaretRight,
            CaretDown,
            CaretUp,
            Refresh,
            Library,
            Depot,
            Download,
            Play,
            Stop,
            Plus,
            Close,
            Minimize,
            Maximize,
            User,
            Search,
            Settings,
            Folder,
            Trash,
            Install,
            Check
        };

        // Sizes the UI draws at. Nothing enforces these — draw_icon takes any
        // pixel size — but keeping to them means the decode cache stays small.
        //
        // The Avalonia launcher draws 12 / 16 / 20 beside 16px text. This one
        // draws 13px text, so the same icons at the same optical weight are
        // those sizes times 0.8. They used to be copied across literally,
        // which left every icon here a quarter larger than its label.
        const int ICON_SM = 10;  // Avalonia 12
        const int ICON_MD = 13;  // Avalonia 16 — the standard size
        const int ICON_LG = 16;  // Avalonia 20

        // Release every cached mask. Called at shutdown.
        void icons_shutdown();

        // Draw `icon` at `size` x `size` with its top-left at (x, y), tinted
        // with `color`.
        //
        // Silently draws nothing if the asset is missing or has not decoded
        // yet, so a botched deployment degrades to a blank space rather than a
        // crash. Callers that need a visible fallback should test
        // icon_available() once at startup rather than per frame.
        void draw_icon(gfx::Surface *s, int x, int y, int size,
                       gfx::Color color, Icon icon);

        // Centre `icon` in `r`.
        void draw_icon_centered(gfx::Surface *s, const gfx::Rect &r, int size,
                                gfx::Color color, Icon icon);

        // False when the icon's PNG could not be loaded at all.
        bool icon_available(Icon icon);

    } // namespace ui
} // namespace launcher

#endif // LAUNCHER_UI_ICONS_H
