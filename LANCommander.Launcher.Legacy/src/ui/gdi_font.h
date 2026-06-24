#ifndef LAUNCHER_UI_GDI_FONT_H
#define LAUNCHER_UI_GDI_FONT_H

// TrueType font renderer that draws into Allegro BITMAPs via GDI.
// This file must NOT include Allegro headers (BITMAP conflict).

// Initialize the GDI font system. Creates the font with the given face
// name and point size.  Falls back through: Inter → Segoe UI → Arial.
void gdi_font_init(int point_size);
void gdi_font_shutdown();

// Text metrics.
int gdi_font_height();
int gdi_font_text_width(const char *text);

// Render text into an Allegro BITMAP (passed as opaque pointer).
// The color is an Allegro packed color value.
void gdi_font_draw(void *allegro_bmp, int x, int y, int color, const char *text);
void gdi_font_draw_center(void *allegro_bmp, int cx, int y, int color, const char *text);
void gdi_font_draw_right(void *allegro_bmp, int rx, int y, int color, const char *text);

#endif // LAUNCHER_UI_GDI_FONT_H
