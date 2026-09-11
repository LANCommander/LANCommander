#ifndef LAUNCHER_UI_AUTH_BACKGROUND_H
#define LAUNCHER_UI_AUTH_BACKGROUND_H

#include "gfx/gfx.h"

// The photographic backdrop behind the pre-login screens.
//
// Shared by the server-selection and credentials screens so they look like one
// flow rather than two unrelated pages, and so the randomly chosen image does
// not change when the user moves between them.

namespace launcher
{
    namespace ui
    {

        // Draws the backdrop, aspect-filled and cropped to the window, with the
        // darkening overlay that keeps panel text readable. Picks an image on
        // first call and keeps it for the life of the process.
        void auth_background_draw(gfx::Surface *s, int sw, int sh);

        // Drop the cached image, e.g. on logout, so the next visit gets a
        // different one.
        void auth_background_reset();

        // --- Wordmark ---
        //
        // The brand mark at the top of both auth cards, where the Avalonia
        // views put `<svg:Svg Path="/Assets/logo.svg" Width="350"/>`. It sits
        // here rather than in either screen because both draw it and both want
        // the same decode.
        //
        // Rasterised offline by tools/generate-logo.py from the same
        // logo.svg the Avalonia launcher renders, so the two cannot drift.

        // Height the mark will occupy at `max_w`, without drawing it, so a
        // card can lay out the fields under it. 0 when the asset is missing.
        int auth_logo_height(int max_w);

        // Draw the mark centred on `cx` with its top at `y`, scaled to fit
        // `max_w`. Returns the height consumed, matching auth_logo_height().
        int auth_logo_draw(gfx::Surface *s, int cx, int y, int max_w);

    } // namespace ui
} // namespace launcher

#endif // LAUNCHER_UI_AUTH_BACKGROUND_H
