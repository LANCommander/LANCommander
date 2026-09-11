// gfx_sdl.cpp — SDL3 implementation of the gfx contract.
//
// Architecture note: the launcher does NOT draw into SDL_GetWindowSurface()
// directly. It composites into its own 32-bit ARGB surface and blits that to
// the window once per frame. Two reasons:
//
//   1. On a 16 bpp (or 8 bpp) Win9x desktop the window surface is 16/8 bpp,
//      and every alpha blend and gradient in the UI would degrade.
//   2. It matches what the Allegro backend did (blit_to_hdc), so the swap is
//      behaviour-neutral.
//
// The cost is one full-surface blit per frame, which is what the old code
// already paid.

#include "gfx/gfx.h"

#include <SDL3/SDL.h>

#include <vector>

namespace launcher
{
    namespace gfx
    {

        struct Surface
        {
            SDL_Surface *surf;
            bool owned;
            std::vector<Rect> clips; // back() is active
        };

        namespace
        {
            SDL_Window *s_window = NULL;
            Surface *s_backbuffer = NULL;
            int s_width = 0;
            int s_height = 0;
            bool s_close_requested = false;

            // Everything composites at 32-bit so alpha survives regardless of
            // what the desktop is running at.
            const SDL_PixelFormat SURFACE_FORMAT = SDL_PIXELFORMAT_ARGB8888;

            inline Uint32 pack(SDL_Surface *s, Color c)
            {
                const SDL_PixelFormatDetails *fmt =
                    SDL_GetPixelFormatDetails(s->format);
                return SDL_MapRGBA(fmt, NULL, c.r, c.g, c.b, c.a);
            }

            inline SDL_Rect to_sdl(const Rect &r)
            {
                SDL_Rect out;
                out.x = r.x; out.y = r.y; out.w = r.w; out.h = r.h;
                return out;
            }

            Surface *wrap(SDL_Surface *s, bool owned)
            {
                if (!s)
                    return NULL;

                Surface *out = new Surface;
                out->surf = s;
                out->owned = owned;
                out->clips.push_back(rect(0, 0, s->w, s->h));
                return out;
            }

            void apply_clip(Surface *s)
            {
                const Rect &r = s->clips.back();
                if (r.w <= 0 || r.h <= 0)
                {
                    // SDL treats an empty rect as "clip everything out" only
                    // if we pass a zero-sized rect, which is what we want.
                    SDL_Rect empty = { 0, 0, 0, 0 };
                    SDL_SetSurfaceClipRect(s->surf, &empty);
                    return;
                }
                SDL_Rect sr = to_sdl(r);
                SDL_SetSurfaceClipRect(s->surf, &sr);
            }

            Rect intersect(const Rect &a, const Rect &b)
            {
                int x1 = a.x > b.x ? a.x : b.x;
                int y1 = a.y > b.y ? a.y : b.y;
                int x2 = (a.x + a.w) < (b.x + b.w) ? (a.x + a.w) : (b.x + b.w);
                int y2 = (a.y + a.h) < (b.y + b.h) ? (a.y + a.h) : (b.y + b.h);
                return rect(x1, y1, x2 > x1 ? x2 - x1 : 0, y2 > y1 ? y2 - y1 : 0);
            }

            // Clip a rect against a surface's active clip. SDL_FillSurfaceRect
            // honours the clip rect, but our manual pixel loops do not.
            Rect clipped(Surface *s, const Rect &r)
            {
                return intersect(s->clips.back(), r);
            }

            SDL_Surface *create_raw(int w, int h)
            {
                if (w <= 0 || h <= 0)
                    return NULL;
                return SDL_CreateSurface(w, h, SURFACE_FORMAT);
            }
        } // namespace

        // --- Display ---

        bool init_display(const char *title, int w, int h)
        {
            if (!SDL_Init(SDL_INIT_VIDEO))
                return false;

            // Borderless + resizable: the launcher draws its own title bar,
            // so this replaces the WS_POPUP restyle the Win32 code did by
            // hand. Resize edges come from SDL_SetWindowHitTest, installed by
            // window_chrome.
            s_window = SDL_CreateWindow(title, w, h,
                                        SDL_WINDOW_BORDERLESS | SDL_WINDOW_RESIZABLE);
            if (!s_window)
            {
                SDL_Quit();
                return false;
            }

            SDL_SetWindowMinimumSize(s_window, 640, 480);

            SDL_Surface *bb = create_raw(w, h);
            if (!bb)
            {
                SDL_DestroyWindow(s_window);
                s_window = NULL;
                SDL_Quit();
                return false;
            }

            s_backbuffer = wrap(bb, true);
            s_width = w;
            s_height = h;
            s_close_requested = false;

            // Without this no SDL_EVENT_TEXT_INPUT is delivered.
            SDL_StartTextInput(s_window);

            return true;
        }

        void shutdown_display()
        {
            if (s_backbuffer)
            {
                destroy_surface(s_backbuffer);
                s_backbuffer = NULL;
            }
            if (s_window)
            {
                SDL_StopTextInput(s_window);
                SDL_DestroyWindow(s_window);
                s_window = NULL;
            }
            SDL_Quit();
        }

        Surface *backbuffer() { return s_backbuffer; }
        int display_width() { return s_width; }
        int display_height() { return s_height; }

        bool display_close_requested() { return s_close_requested; }

        bool display_minimized()
        {
            return s_window &&
                   (SDL_GetWindowFlags(s_window) & SDL_WINDOW_MINIMIZED) != 0;
        }

        // Set by the event pump (input_sdl.cpp) on SDL_EVENT_QUIT.
        void display_set_close_requested() { s_close_requested = true; }

        SDL_Window *display_window() { return s_window; }

        // Take ownership of an SDL_Surface produced elsewhere (SDL_ttf glyph
        // masks). Converts to the compositing format if needed so blits stay
        // on SDL's fast paths. Backend-internal; not in gfx.h.
        Surface *surface_adopt_sdl(SDL_Surface *s)
        {
            if (!s)
                return NULL;

            if (s->format != SURFACE_FORMAT)
            {
                SDL_Surface *conv = SDL_ConvertSurface(s, SURFACE_FORMAT);
                SDL_DestroySurface(s);
                s = conv;
                if (!s)
                    return NULL;
            }

            return wrap(s, true);
        }

        void *native_window_handle()
        {
#ifdef SDL_PLATFORM_WIN32
            if (!s_window)
                return NULL;
            return SDL_GetPointerProperty(SDL_GetWindowProperties(s_window),
                                          SDL_PROP_WINDOW_WIN32_HWND_POINTER, NULL);
#else
            return NULL;
#endif
        }

        void present()
        {
            if (!s_window || !s_backbuffer)
                return;

            // Must be re-queried every frame: SDL invalidates the returned
            // pointer on resize.
            SDL_Surface *win = SDL_GetWindowSurface(s_window);
            if (!win)
                return; // minimised

            SDL_BlitSurface(s_backbuffer->surf, NULL, win, NULL);
            SDL_UpdateWindowSurface(s_window);
        }

        void resize_display(int w, int h)
        {
            if (w <= 0 || h <= 0)
                return;
            if (w == s_width && h == s_height)
                return;

            SDL_Surface *bb = create_raw(w, h);
            if (!bb)
                return;

            destroy_surface(s_backbuffer);
            s_backbuffer = wrap(bb, true);
            s_width = w;
            s_height = h;
        }

        // --- Surface lifecycle ---

        Surface *create_surface(int w, int h)
        {
            return wrap(create_raw(w, h), true);
        }

        Surface *surface_from_rgba(const unsigned char *pixels, int w, int h)
        {
            Surface *s = create_surface(w, h);
            if (!s || !pixels)
                return s;

            SDL_Surface *dst = s->surf;
            if (!SDL_LockSurface(dst))
                return s;

            for (int y = 0; y < h; ++y)
            {
                const unsigned char *src = pixels + (size_t)y * w * 4;
                Uint32 *out = (Uint32 *)((Uint8 *)dst->pixels + (size_t)y * dst->pitch);

                for (int x = 0; x < w; ++x)
                {
                    // Source is straight RGBA; ARGB8888 is 0xAARRGGBB.
                    out[x] = ((Uint32)src[x * 4 + 3] << 24) |
                             ((Uint32)src[x * 4 + 0] << 16) |
                             ((Uint32)src[x * 4 + 1] << 8) |
                             ((Uint32)src[x * 4 + 2]);
                }
            }

            SDL_UnlockSurface(dst);
            return s;
        }

        void destroy_surface(Surface *s)
        {
            if (!s)
                return;
            if (s->owned && s->surf)
                SDL_DestroySurface(s->surf);
            delete s;
        }

        int surface_width(const Surface *s) { return s ? s->surf->w : 0; }
        int surface_height(const Surface *s) { return s ? s->surf->h : 0; }

        // --- Drawing ---

        void clear(Surface *s, Color c)
        {
            if (!s)
                return;
            // Ignore the clip rect: clear() means the whole surface.
            SDL_Rect saved;
            SDL_GetSurfaceClipRect(s->surf, &saved);
            SDL_SetSurfaceClipRect(s->surf, NULL);
            SDL_FillSurfaceRect(s->surf, NULL, pack(s->surf, c));
            SDL_SetSurfaceClipRect(s->surf, &saved);
        }

        void fill_rect(Surface *s, const Rect &r, Color c)
        {
            if (!s || r.w <= 0 || r.h <= 0)
                return;
            SDL_Rect sr = to_sdl(r);
            SDL_FillSurfaceRect(s->surf, &sr, pack(s->surf, c));
        }

        void draw_rect(Surface *s, const Rect &r, Color c)
        {
            if (!s || r.w <= 0 || r.h <= 0)
                return;

            const Uint32 col = pack(s->surf, c);
            SDL_Rect top    = { r.x,             r.y,             r.w, 1 };
            SDL_Rect bottom = { r.x,             r.y + r.h - 1,   r.w, 1 };
            SDL_Rect left   = { r.x,             r.y,             1,   r.h };
            SDL_Rect right  = { r.x + r.w - 1,   r.y,             1,   r.h };

            SDL_FillSurfaceRect(s->surf, &top, col);
            SDL_FillSurfaceRect(s->surf, &bottom, col);
            SDL_FillSurfaceRect(s->surf, &left, col);
            SDL_FillSurfaceRect(s->surf, &right, col);
        }

        void hline(Surface *s, int x, int y, int w, Color c)
        {
            if (!s || w <= 0)
                return;
            SDL_Rect r = { x, y, w, 1 };
            SDL_FillSurfaceRect(s->surf, &r, pack(s->surf, c));
        }

        void vline(Surface *s, int x, int y, int h, Color c)
        {
            if (!s || h <= 0)
                return;
            SDL_Rect r = { x, y, 1, h };
            SDL_FillSurfaceRect(s->surf, &r, pack(s->surf, c));
        }

        void fill_rect_alpha(Surface *s, const Rect &r, Color c)
        {
            if (!s || r.w <= 0 || r.h <= 0 || c.a == 0)
                return;

            if (c.a >= 255)
            {
                fill_rect(s, r, c);
                return;
            }

            const Rect cr = clipped(s, r);
            if (cr.w <= 0 || cr.h <= 0)
                return;

            SDL_Surface *dst = s->surf;
            if (!SDL_LockSurface(dst))
                return;

            const unsigned a = c.a;
            const unsigned ia = 255u - a;

            for (int y = 0; y < cr.h; ++y)
            {
                Uint32 *row = (Uint32 *)((Uint8 *)dst->pixels +
                                         (size_t)(cr.y + y) * dst->pitch);
                for (int x = 0; x < cr.w; ++x)
                {
                    Uint32 p = row[cr.x + x];
                    unsigned dr = (p >> 16) & 0xFF;
                    unsigned dg = (p >> 8) & 0xFF;
                    unsigned db = p & 0xFF;

                    row[cr.x + x] = 0xFF000000u
                                  | (((c.r * a + dr * ia) / 255u) << 16)
                                  | (((c.g * a + dg * ia) / 255u) << 8)
                                  | ((c.b * a + db * ia) / 255u);
                }
            }

            SDL_UnlockSurface(dst);
        }

        void fill_rect_gradient_v(Surface *s, const Rect &r, Color top, Color bottom)
        {
            if (!s || r.w <= 0 || r.h <= 0)
                return;

            const int den = (r.h > 1) ? (r.h - 1) : 1;

            for (int i = 0; i < r.h; ++i)
            {
                const int t = (r.h > 1) ? i : 0;
                Color c;
                c.r = (unsigned char)(top.r + (bottom.r - top.r) * t / den);
                c.g = (unsigned char)(top.g + (bottom.g - top.g) * t / den);
                c.b = (unsigned char)(top.b + (bottom.b - top.b) * t / den);
                c.a = (unsigned char)(top.a + (bottom.a - top.a) * t / den);

                fill_rect_alpha(s, rect(r.x, r.y + i, r.w, 1), c);
            }
        }

        // --- Clipping ---

        void push_clip(Surface *s, const Rect &r)
        {
            if (!s)
                return;
            s->clips.push_back(intersect(s->clips.back(), r));
            apply_clip(s);
        }

        void pop_clip(Surface *s)
        {
            if (!s || s->clips.size() <= 1)
                return;
            s->clips.pop_back();
            apply_clip(s);
        }

        Rect get_clip(const Surface *s)
        {
            return s ? s->clips.back() : rect(0, 0, 0, 0);
        }

        // --- Blitting ---

        void blit(Surface *dst, const Surface *src, int dx, int dy)
        {
            if (!dst || !src)
                return;
            SDL_Surface *s = src->surf;
            SDL_SetSurfaceBlendMode(s, SDL_BLENDMODE_NONE);
            SDL_Rect d = { dx, dy, s->w, s->h };
            SDL_BlitSurface(s, NULL, dst->surf, &d);
        }

        void blit_region(Surface *dst, const Surface *src, const Rect &sr,
                         int dx, int dy)
        {
            if (!dst || !src || sr.w <= 0 || sr.h <= 0)
                return;
            SDL_SetSurfaceBlendMode(src->surf, SDL_BLENDMODE_NONE);
            SDL_Rect s = to_sdl(sr);
            SDL_Rect d = { dx, dy, sr.w, sr.h };
            SDL_BlitSurface(src->surf, &s, dst->surf, &d);
        }

        void blit_scaled(Surface *dst, const Surface *src, const Rect &sr,
                         const Rect &dr)
        {
            if (!dst || !src || sr.w <= 0 || sr.h <= 0 || dr.w <= 0 || dr.h <= 0)
                return;
            SDL_SetSurfaceBlendMode(src->surf, SDL_BLENDMODE_NONE);
            SDL_Rect s = to_sdl(sr);
            SDL_Rect d = to_sdl(dr);
            SDL_BlitSurfaceScaled(src->surf, &s, dst->surf, &d, SDL_SCALEMODE_LINEAR);
        }

        void blit_alpha(Surface *dst, const Surface *src, int dx, int dy)
        {
            if (!dst || !src)
                return;
            SDL_Surface *s = src->surf;
            SDL_SetSurfaceBlendMode(s, SDL_BLENDMODE_BLEND);
            SDL_SetSurfaceColorMod(s, 255, 255, 255);
            SDL_SetSurfaceAlphaMod(s, 255);
            SDL_Rect d = { dx, dy, s->w, s->h };
            SDL_BlitSurface(s, NULL, dst->surf, &d);
        }

        // --- Glyph compositing ---

        void blit_tinted(Surface *dst, const Surface *src, int x, int y, Color color)
        {
            if (!dst || !src)
                return;

            SDL_Surface *s = src->surf;
            SDL_SetSurfaceBlendMode(s, SDL_BLENDMODE_BLEND);
            SDL_SetSurfaceColorMod(s, color.r, color.g, color.b);
            SDL_SetSurfaceAlphaMod(s, color.a);

            SDL_Rect d = { x, y, s->w, s->h };
            SDL_BlitSurface(s, NULL, dst->surf, &d);
        }

        void blend_coverage(Surface *dst, int x, int y, int w, int h,
                            const unsigned char *mask, int mask_pitch, Color color)
        {
            if (!dst || !mask || w <= 0 || h <= 0)
                return;

            const Rect cr = clipped(dst, rect(x, y, w, h));
            if (cr.w <= 0 || cr.h <= 0)
                return;

            SDL_Surface *surf = dst->surf;
            if (!SDL_LockSurface(surf))
                return;

            for (int row = 0; row < cr.h; ++row)
            {
                const int sy = (cr.y + row) - y;
                const unsigned char *mrow = mask + (size_t)sy * mask_pitch;
                Uint32 *out = (Uint32 *)((Uint8 *)surf->pixels +
                                         (size_t)(cr.y + row) * surf->pitch);

                for (int col = 0; col < cr.w; ++col)
                {
                    const int sx = (cr.x + col) - x;
                    unsigned cov = mrow[sx];
                    if (!cov)
                        continue;

                    unsigned a = cov * color.a / 255u;
                    if (!a)
                        continue;

                    Uint32 *p = &out[cr.x + col];

                    if (a >= 255)
                    {
                        *p = 0xFF000000u | ((Uint32)color.r << 16) |
                             ((Uint32)color.g << 8) | color.b;
                        continue;
                    }

                    const unsigned ia = 255u - a;
                    unsigned dr = (*p >> 16) & 0xFF;
                    unsigned dg = (*p >> 8) & 0xFF;
                    unsigned db = *p & 0xFF;

                    *p = 0xFF000000u
                       | (((color.r * a + dr * ia) / 255u) << 16)
                       | (((color.g * a + dg * ia) / 255u) << 8)
                       | ((color.b * a + db * ia) / 255u);
                }
            }

            SDL_UnlockSurface(surf);
        }

        // --- Pixel access ---

        Color get_pixel(const Surface *s, int x, int y)
        {
            Color c = rgba(0, 0, 0, 0);
            if (!s || x < 0 || y < 0 || x >= s->surf->w || y >= s->surf->h)
                return c;

            SDL_Surface *surf = s->surf;
            if (!SDL_LockSurface(surf))
                return c;

            const Uint32 p = *(Uint32 *)((Uint8 *)surf->pixels +
                                         (size_t)y * surf->pitch + (size_t)x * 4);
            SDL_UnlockSurface(surf);

            c.a = (unsigned char)((p >> 24) & 0xFF);
            c.r = (unsigned char)((p >> 16) & 0xFF);
            c.g = (unsigned char)((p >> 8) & 0xFF);
            c.b = (unsigned char)(p & 0xFF);
            return c;
        }

        void put_pixel(Surface *s, int x, int y, Color c)
        {
            if (!s || x < 0 || y < 0 || x >= s->surf->w || y >= s->surf->h)
                return;

            SDL_Surface *surf = s->surf;
            if (!SDL_LockSurface(surf))
                return;

            *(Uint32 *)((Uint8 *)surf->pixels +
                        (size_t)y * surf->pitch + (size_t)x * 4) =
                ((Uint32)c.a << 24) | ((Uint32)c.r << 16) |
                ((Uint32)c.g << 8) | c.b;

            SDL_UnlockSurface(surf);
        }

        // --- Timing ---

        unsigned int ticks_ms() { return (unsigned int)SDL_GetTicks(); }

        void delay_ms(unsigned int ms) { SDL_Delay(ms); }

    } // namespace gfx
} // namespace launcher
