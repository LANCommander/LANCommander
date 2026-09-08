#ifndef LAUNCHER_UI_THEME_H
#define LAUNCHER_UI_THEME_H

#include "gfx/gfx.h"

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
            gfx::Color primary;       // Primary accent (buttons, selected items)
            gfx::Color primary_hover; // Button hover
            gfx::Color primary_active;// Button pressed
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
        };

        // Initialize the font. Colors need no initialization.
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

        // Draw text at (x, y) with the given color.
        void draw_text(gfx::Surface *s, int x, int y, gfx::Color color, const char *text);

        // Draw text centered horizontally at (cx, y).
        void draw_text_center(gfx::Surface *s, int cx, int y, gfx::Color color, const char *text);

        // Draw text right-aligned to (rx, y).
        void draw_text_right(gfx::Surface *s, int rx, int y, gfx::Color color, const char *text);

        // Get text width in pixels.
        int text_width(const char *text);

        // Font height in pixels.
        int text_height();

        // Draw word-wrapped text within a given width. Returns the total
        // height consumed (pixels). If s is NULL, only measures without drawing.
        int draw_text_wrap(gfx::Surface *s, int x, int y, int max_w, gfx::Color color,
                           const char *text, int line_spacing = 2);

        // Draw word-wrapped text centered horizontally within a region.
        // Returns the total height consumed. If s is NULL, only measures.
        int draw_text_wrap_center(gfx::Surface *s, int cx, int y, int max_w, gfx::Color color,
                                  const char *text, int line_spacing = 2);

    } // namespace ui
} // namespace launcher

#endif // LAUNCHER_UI_THEME_H
