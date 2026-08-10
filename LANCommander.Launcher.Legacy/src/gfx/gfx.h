#ifndef LAUNCHER_GFX_H
#define LAUNCHER_GFX_H

// gfx — the launcher's entire graphics contract.
//
// Everything the UI draws goes through this header. No backend type
// (Allegro BITMAP, SDL_Surface, Win32 HDC) may appear in any other header,
// so swapping backends is a matter of adding a gfx_*.cpp and flipping a
// CMake option.
//
// The model is deliberately the one the launcher already used: a single
// 32-bit software surface that everything composites into, presented once
// per frame. There is no hardware renderer, no shader, and no retained
// scene — screens draw immediately, top to bottom, every frame.
//
// Coordinates are (x, y, w, h) throughout. Allegro's inclusive x1/y1/x2/y2
// convention does not leak past the backend.

namespace launcher
{
    namespace gfx
    {

        // --- Types ---

        // Straight (non-premultiplied) RGBA, 8 bits per channel.
        struct Color
        {
            unsigned char r, g, b, a;
        };

        inline Color rgb(int r, int g, int b)
        {
            Color c;
            c.r = (unsigned char)r; c.g = (unsigned char)g;
            c.b = (unsigned char)b; c.a = 255;
            return c;
        }

        inline Color rgba(int r, int g, int b, int a)
        {
            Color c;
            c.r = (unsigned char)r; c.g = (unsigned char)g;
            c.b = (unsigned char)b; c.a = (unsigned char)a;
            return c;
        }

        // Return `c` with its alpha replaced. Handy for the many places that
        // reuse a theme color at partial opacity.
        inline Color with_alpha(Color c, int a)
        {
            c.a = (unsigned char)a;
            return c;
        }

        struct Rect
        {
            int x, y, w, h;
        };

        inline Rect rect(int x, int y, int w, int h)
        {
            Rect r;
            r.x = x; r.y = y; r.w = w; r.h = h;
            return r;
        }

        inline bool rect_contains(const Rect &r, int px, int py)
        {
            return px >= r.x && px < r.x + r.w &&
                   py >= r.y && py < r.y + r.h;
        }

        // Opaque backend surface. Defined by whichever gfx_*.cpp is compiled.
        struct Surface;

        // --- Display ---
        //
        // The backend owns the window and the presentation path. Keeping
        // these here rather than in App means the Allegro-to-SDL swap does
        // not reach into application code.

        // Create the window and backbuffer. Returns false on failure.
        bool init_display(const char *title, int w, int h);

        void shutdown_display();

        // The surface every screen draws into. Valid until the next
        // resize_display(); do not cache it across frames.
        Surface *backbuffer();

        int display_width();
        int display_height();

        // Push the backbuffer to the window.
        void present();

        // Reallocate the backbuffer after the window changed size.
        void resize_display(int w, int h);

        // True once the OS has asked the window to close (Alt+F4, taskbar,
        // title-bar X). Latches — it stays true once set.
        bool display_close_requested();

        // Native window handle (HWND on Windows), or NULL where the concept
        // does not exist. The one deliberate hole in the abstraction: the
        // custom chrome needs it for frameless-window styling and dragging.
        // Everything that uses it is #ifdef'd on the platform, so it compiles
        // out on DOS.
        void *native_window_handle();

        // --- Surface lifecycle ---

        // Create a blank 32-bit surface. Returns NULL on failure.
        Surface *create_surface(int w, int h);

        // Create a surface from tightly packed RGBA bytes (4 per pixel,
        // row-major) — the format image_decoder.h hands back.
        Surface *surface_from_rgba(const unsigned char *pixels, int w, int h);

        void destroy_surface(Surface *s);

        int surface_width(const Surface *s);
        int surface_height(const Surface *s);

        // --- Drawing ---

        void clear(Surface *s, Color c);

        void fill_rect(Surface *s, const Rect &r, Color c);

        // One-pixel outline, drawn inside `r`.
        void draw_rect(Surface *s, const Rect &r, Color c);

        void hline(Surface *s, int x, int y, int w, Color c);
        void vline(Surface *s, int x, int y, int h, Color c);

        // Alpha-blended fill using c.a. This replaces the hand-rolled
        // getpixel/putpixel blend loops that used to sit in window_chrome,
        // widgets, screen_library and screen_login.
        void fill_rect_alpha(Surface *s, const Rect &r, Color c);

        // Vertical gradient blend from `top` to `bottom`, alpha included.
        // Replaces the per-pixel ramp in screen_game_detail.
        void fill_rect_gradient_v(Surface *s, const Rect &r, Color top, Color bottom);

        // --- Clipping ---

        // Clip stack. push_clip intersects with the current clip, so nested
        // pushes behave the way the screens' save/restore idiom expects.
        // (The old code faked save/restore by re-setting a full-screen rect,
        // which silently clobbered the outer clip when nested.)
        void push_clip(Surface *s, const Rect &r);
        void pop_clip(Surface *s);
        Rect get_clip(const Surface *s);

        // --- Blitting ---

        // Opaque 1:1 copy of the whole source.
        void blit(Surface *dst, const Surface *src, int dx, int dy);

        // Opaque 1:1 copy of a sub-region.
        void blit_region(Surface *dst, const Surface *src, const Rect &src_rect,
                         int dx, int dy);

        // Scaled copy. Used for the login background's aspect-fill.
        void blit_scaled(Surface *dst, const Surface *src, const Rect &src_rect,
                         const Rect &dst_rect);

        // Copy honouring the source's per-pixel alpha.
        void blit_alpha(Surface *dst, const Surface *src, int dx, int dy);

        // --- Glyph compositing ---

        // Blend a coverage-mask surface into `dst`, tinted with `color`.
        // A mask surface is white RGB with alpha carrying coverage — the
        // shape both the GDI text path and SDL_ttf produce.
        //
        // Colour is applied here rather than baked into the mask so the text
        // cache can key on the string alone: the same label drawn in three
        // theme colours costs one cached mask, not three. On SDL this is
        // SDL_SetSurfaceColorMod + a single optimised blit.
        void blit_tinted(Surface *dst, const Surface *src, int x, int y, Color color);

        // Same, from a raw 8-bit coverage buffer. Used by the GDI text path,
        // which rasterises into a DIB rather than a Surface.
        void blend_coverage(Surface *dst, int x, int y, int w, int h,
                            const unsigned char *mask, int mask_pitch, Color color);

        // --- Pixel access ---
        //
        // Slow per-pixel helpers. Present only for the window icon's
        // premultiply pass; prefer the batch operations above.

        Color get_pixel(const Surface *s, int x, int y);
        void put_pixel(Surface *s, int x, int y, Color c);

        // --- Timing ---

        // Milliseconds since backend init. Replaces Allegro's retrace_count,
        // which tied the caret blink to install_timer().
        unsigned int ticks_ms();

        // Sleep. Replaces Allegro's rest().
        void delay_ms(unsigned int ms);

    } // namespace gfx
} // namespace launcher

#endif // LAUNCHER_GFX_H
