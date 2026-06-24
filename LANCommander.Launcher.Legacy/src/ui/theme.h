#ifndef LAUNCHER_UI_THEME_H
#define LAUNCHER_UI_THEME_H

struct BITMAP;

namespace launcher
{
    namespace ui
    {

        // Color palette and font settings for the launcher UI.
        // Colors are stored as Allegro packed color values (set after set_color_depth).
        struct Theme
        {
            int bg;            // Window / layout background
            int surface;       // Elevated surface (cards, panels)
            int panel;         // Panel/card background
            int panel_hover;   // Panel hover highlight
            int primary;       // Primary accent (buttons, selected items)
            int primary_hover; // Button hover
            int primary_active;// Button pressed
            int text;          // Normal text (87% white)
            int text_dim;      // Secondary text (65% white)
            int text_disabled; // Disabled text (25% white)
            int text_bright;   // Bright text (100% white)
            int input_bg;      // Text input background
            int input_border;  // Text input border
            int input_focus;   // Text input focused border
            int divider;       // Separator lines
            int error;         // Error/danger
            int error_hover;   // Error hover
            int success;       // Success
            int warning;       // Warning
            int footer;        // Footer background
        };

        // Initialize theme colors. Must be called after set_color_depth / set_gfx_mode.
        void theme_init();

        // Access the current theme.
        const Theme &theme();

        // --- Text helpers ---

        // Draw text at (x, y) with the given color. Uses Allegro's built-in font.
        void draw_text(BITMAP *bmp, int x, int y, int color, const char *text);

        // Draw text centered horizontally at (cx, y).
        void draw_text_center(BITMAP *bmp, int cx, int y, int color, const char *text);

        // Draw text right-aligned to (rx, y).
        void draw_text_right(BITMAP *bmp, int rx, int y, int color, const char *text);

        // Get text width in pixels.
        int text_width(const char *text);

        // Font height in pixels.
        int text_height();

        // Draw word-wrapped text within a given width. Returns the total
        // height consumed (pixels). If bmp is NULL, only measures without drawing.
        int draw_text_wrap(BITMAP *bmp, int x, int y, int max_w, int color,
                           const char *text, int line_spacing = 2);

        // Draw word-wrapped text centered horizontally within a region.
        // Returns the total height consumed. If bmp is NULL, only measures.
        int draw_text_wrap_center(BITMAP *bmp, int cx, int y, int max_w, int color,
                                  const char *text, int line_spacing = 2);

    } // namespace ui
} // namespace launcher

#endif // LAUNCHER_UI_THEME_H
