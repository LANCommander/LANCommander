#ifndef LAUNCHER_UI_WIDGETS_OVERLAY_H
#define LAUNCHER_UI_WIDGETS_OVERLAY_H

#include "gfx/gfx.h"
#include "input.h"

// Things that cover the whole screen and take input away from what is under
// them: modal dialogs today, the screenshot lightbox in a later phase.
//
// The input half of "modal" is not here — it is input_blocked() in widgets.h,
// which the screen applies to everything it draws underneath. Splitting it
// that way means an overlay does not have to be drawn before the content it
// blocks, which immediate mode cannot do in one pass.

namespace launcher
{
    namespace ui
    {

        // Draws the dimmed backdrop, a centred panel with a one-pixel border,
        // and pushes a clip to the panel. Returns the panel rect.
        //
        // The caller lays out its own content inside that rect, including the
        // title: the two dialogs in the launcher disagree about whether there
        // is a subtitle and whether the title is formatted, and pushing that
        // into a shared function would only produce a pile of flags.
        gfx::Rect dialog_begin(gfx::Surface *s, int screen_w, int screen_h,
                               int w, int h);

        void dialog_end(gfx::Surface *s);

        // --- Lightbox ---------------------------------------------------------

        enum class LightboxAction
        {
            None,
            Close,
            Prev,
            Next
        };

        struct LightboxState
        {
            bool open;
            int index;

            LightboxState() : open(false), index(0) {}
        };

        // Full-screen image viewer: dimmed backdrop, aspect-fitted image,
        // prev/next/close and an "i / n" counter.
        //
        // `image` may be NULL while the picture is still downloading, in which
        // case a placeholder is drawn and the controls still work.
        //
        // Prev and Next are disabled at the ends rather than wrapping, which
        // is what the Avalonia LightboxOverlay does; wrapping from the last
        // shot back to the first reads as a glitch when there are only three.
        // `area` is the region to fill, which the caller sets to the space
        // between the title bar and the footer. Filling the whole window
        // would put the close button under the title bar and the counter
        // under the footer, since both are drawn after the screen.
        LightboxAction lightbox(gfx::Surface *s, const gfx::Rect &area,
                                gfx::Surface *image, int index, int count,
                                const InputState &input);

    } // namespace ui
} // namespace launcher

#endif // LAUNCHER_UI_WIDGETS_OVERLAY_H
