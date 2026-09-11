#ifndef LAUNCHER_UI_THEME_H
#define LAUNCHER_UI_THEME_H

#include "gfx/gfx.h"
#include "ui/font.h"

namespace launcher
{
    namespace ui
    {

        // Color palette and font settings for the launcher UI.
        //
        // Colors are backend-independent gfx::Color values, so unlike the
        // old Allegro packed ints they carry no dependency on the display
        // color depth having been chosen first.
        struct Theme
        {
            gfx::Color bg;            // Window / layout background
            gfx::Color surface;       // Elevated surface (cards, panels)
            gfx::Color panel;         // Panel/card background
            gfx::Color panel_hover;   // Panel hover highlight
            gfx::Color primary;       // Primary accent (selection, progress, focus)
            // Buttons have their own ramp rather than reusing `primary`.
            // Avalonia's ButtonPrimary* tokens are a slightly desaturated blue
            // and, importantly, go DARKER on hover — this used to reach for
            // Primary5 and got lighter, which is the opposite gesture.
            gfx::Color button_primary;        // Primary button fill
            gfx::Color button_primary_hover;  // Primary button hover
            gfx::Color button_primary_active; // Primary button pressed
            // The neutral button, which is what an unclassed Avalonia Button
            // is. Every button here used to be painted primary blue, so a
            // dialog's Cancel shouted as loudly as its Install.
            gfx::Color button_bg;             // Default button fill
            gfx::Color button_bg_hover;       // Default button hover
            gfx::Color button_bg_active;      // Default button pressed
            gfx::Color text;          // Normal text (87% white)
            gfx::Color text_dim;      // Secondary text (65% white)
            gfx::Color text_disabled; // Disabled text (25% white)
            gfx::Color text_bright;   // Bright text (100% white)
            gfx::Color input_bg;      // Text input background
            gfx::Color input_border;  // Text input border
            gfx::Color input_focus;   // Text input focused border
            gfx::Color divider;       // Separator lines
            gfx::Color error;         // Error/danger
            gfx::Color error_hover;   // Error hover
            gfx::Color success;       // Success
            gfx::Color warning;       // Warning
            gfx::Color footer;        // Footer background
            gfx::Color ghost_hover;   // Text/ghost button hover wash (alpha)
            gfx::Color ghost_active;  // Text/ghost button pressed wash (alpha)
        };

        // Initialize the fonts. Colors need no initialization.
        // False when the bundled font could not be loaded, which leaves the
        // UI drawing every label as nothing at all. Fatal to App::init
        // rather than silent: a launcher with no text is not usable, and
        // "blank panels" is a much harder symptom to diagnose than a
        // startup error naming the file.
        bool theme_init();
        void theme_shutdown();

        // Access the current theme.
        const Theme &theme();

        // --- Text helpers ---
        //
        // Every one of these takes a rung of the type scale, defaulting to
        // Body. The default is what the whole UI used to render at, so a call
        // site that does not care reads exactly as it did; a call site that
        // wants the hierarchy the Avalonia launcher has — a Caption transfer
        // rate, a Display game title — names its rung and the measurement
        // helpers agree with it.
        //
        // The trap the default guards against is measuring at one rung and
        // drawing at another: a row laid out with text_height() and drawn
        // with draw_text(..., FontSize::Section) will overlap its neighbour.
        // Pass the same rung to both.

        // Draw text at (x, y) with the given color.
        void draw_text(gfx::Surface *s, int x, int y, gfx::Color color, const char *text,
                       FontSize size = FontSize::Body);

        // Draw text centered horizontally at (cx, y).
        void draw_text_center(gfx::Surface *s, int cx, int y, gfx::Color color, const char *text,
                              FontSize size = FontSize::Body);

        // Draw text right-aligned to (rx, y).
        void draw_text_right(gfx::Surface *s, int rx, int y, gfx::Color color, const char *text,
                             FontSize size = FontSize::Body);

        // The rung closest to `px`, for the handful of places that compute a
        // size rather than choose one — the cover fallback title, whose
        // Avalonia counterpart is `max(8, min(w, h) * 0.09)` of the tile.
        FontSize font_size_near(int px);

        // Get text width in pixels.
        int text_width(const char *text, FontSize size = FontSize::Body);

        // Font line height in pixels.
        int text_height(FontSize size = FontSize::Body);

        // Draw word-wrapped text within a given width. Returns the total
        // height consumed (pixels). If s is NULL, only measures without drawing.
        int draw_text_wrap(gfx::Surface *s, int x, int y, int max_w, gfx::Color color,
                           const char *text, FontSize size = FontSize::Body,
                           int line_spacing = 2);

        // Draw word-wrapped text centered horizontally within a region.
        // Returns the total height consumed. If s is NULL, only measures.
        int draw_text_wrap_center(gfx::Surface *s, int cx, int y, int max_w, gfx::Color color,
                                  const char *text, FontSize size = FontSize::Body,
                                  int line_spacing = 2);

    } // namespace ui
} // namespace launcher

#endif // LAUNCHER_UI_THEME_H
