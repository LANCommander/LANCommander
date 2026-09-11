// gfx_allegro.cpp — Allegro 4 implementation of the gfx contract.
//
// Transitional. This backend exists so the UI can be moved onto gfx.h
// without the Win9x build ever going red; it is deleted once SDL3 boots on
// real Win9x hardware. Behaviour here is deliberately identical to the code
// it replaces, including the 16-bit mode-set fallback and the blit-to-HDC
// present path.
//
// Allegro declares global functions named rect/blit/hline/vline, which the
// gfx namespace also uses. Every call into Allegro is written with a
// leading :: so the two never get confused.

#include <allegro.h>
#ifdef ALLEGRO_WINDOWS
#include <winalleg.h>
#endif

#include "gfx/gfx.h"

#include "app/worker.h"
#include "app/logger.h"

#include <vector>

namespace launcher
{
    namespace gfx
    {

        struct Surface
        {
            BITMAP *bmp;
            bool owned;               // false for the backbuffer wrapper
            std::vector<Rect> clips;  // clip stack; back() is active
        };

        namespace
        {
            Surface *s_backbuffer = NULL;
            int s_width = 0;
            int s_height = 0;

            // Allegro's close-button callback runs on its own thread, hence
            // the volatile flag + LOCK_FUNCTION dance.
            volatile int s_close_requested = 0;
            void close_button_handler() { s_close_requested = 1; }
            END_OF_STATIC_FUNCTION(close_button_handler)

            // Guards the backbuffer pointer against that same thread.
            //
            // Allegro owns the window, and it owns it from a thread of its
            // own -- the one the close-button callback above arrives on. The
            // chrome's WndProc subclass is therefore also called from there,
            // and its WM_PAINT case calls present(). Meanwhile the main loop
            // calls resize_display(), which frees the very bitmap present()
            // is reading out of. Dragging a window edge fires WM_PAINT
            // continuously, so the two collide in the ordinary case rather
            // than a rare one, and the process dies on a freed BITMAP.
            //
            // Only the main thread ever swaps the pointer; the lock is what
            // stops the window thread from reading it mid-swap.
            void *s_backbuffer_lock = NULL;

            struct BackbufferLock
            {
                BackbufferLock() { worker_mutex_lock(s_backbuffer_lock); }
                ~BackbufferLock() { worker_mutex_unlock(s_backbuffer_lock); }
            };

#ifdef ALLEGRO_WINDOWS
            // The frame's GDI description, built once per size/depth change.
            //
            // present() used to be a straight call to Allegro's
            // blit_to_hdc(). That function is written for occasional use, and
            // on every single call it:
            //
            //   * mallocs a BITMAPINFO plus a 256-entry palette and fills the
            //     palette -- even at 32bpp, where it is never read;
            //   * mallocs a whole second copy of the frame;
            //   * copies the frame into it, row by row;
            //   * blits;
            //   * frees both.
            //
            // At 30 FPS and 800x600x32 that is ~57 MB/s of allocate, copy and
            // free, and a two-megabyte block goes to VirtualAlloc, so the
            // kernel zero-fills every page of it thirty times a second. On a
            // Pentium 4 that is a large fraction of a core spent on nothing,
            // and it is charged to the whole machine, not just the launcher.
            //
            // It is worse at 16bpp, the depth the Win9x path falls back to:
            // Allegro's DIB is 5-5-5, the bitmap is 5-6-5, so the copy above
            // becomes a per-pixel conversion loop over every pixel of every
            // frame -- and it quietly drops a bit of green on the way.
            //
            // GDI can read the bitmap where it already lies. All that is
            // needed is a BITMAPINFO that describes it, which does not change
            // between frames, and BI_BITFIELDS to state 5-6-5 rather than let
            // GDI assume 5-5-5.
            struct DibDesc
            {
                BITMAPINFOHEADER header;
                DWORD masks[3]; // Only read when biCompression is BI_BITFIELDS.
            };

            DibDesc s_dib;
            bool s_dib_valid = false;
            int s_dib_w = 0;
            int s_dib_h = 0;
            int s_dib_depth = 0;

            // Bytes a pixel of `depth` occupies. Allegro spells this
            // BYTES_PER_PIXEL, but that macro lives in its internal headers;
            // the arithmetic is the same and 15bpp correctly gives 2.
            inline int bytes_per_pixel(int depth) { return (depth + 7) / 8; }

            // True when the bitmap's rows sit back to back in one allocation
            // at `stride` bytes apart, which is what lets GDI read them in
            // place. Allegro's memory bitmaps are laid out that way, but it
            // is a property of the allocator rather than a documented
            // guarantee, so it is checked rather than assumed -- present()
            // falls back to blit_to_hdc() if it ever stops holding.
            bool rows_are_contiguous(BITMAP *bmp, int stride)
            {
                if (bmp->h <= 0)
                    return false;
                if (bmp->h == 1)
                    return true;

                const unsigned char *first = (const unsigned char *)bmp->line[0];
                const unsigned char *last = (const unsigned char *)bmp->line[bmp->h - 1];

                // Two checks rather than a loop over every row: a uniform
                // stride between the first two rows plus the right total span
                // to the last one cannot hold for a scattered allocation.
                if ((const unsigned char *)bmp->line[1] - first != stride)
                    return false;

                return last - first == (long)stride * (bmp->h - 1);
            }

            // Describes `bmp` for GDI, reusing the last description when the
            // frame's shape has not changed. Returns false for a depth this
            // path does not handle, which sends present() to Allegro.
            bool describe_for_gdi(BITMAP *bmp, int depth)
            {
                if (s_dib_valid && s_dib_w == bmp->w && s_dib_h == bmp->h &&
                    s_dib_depth == depth)
                    return true;

                ZeroMemory(&s_dib, sizeof(s_dib));
                s_dib.header.biSize = sizeof(BITMAPINFOHEADER);
                s_dib.header.biPlanes = 1;
                s_dib.header.biWidth = bmp->w;

                // Negative: a top-down DIB, matching the order Allegro's rows
                // are actually in. Allegro's own path does the same.
                s_dib.header.biHeight = -bmp->h;

                switch (depth)
                {
                case 32:
                    s_dib.header.biBitCount = 32;
                    s_dib.header.biCompression = BI_RGB;
                    break;

                case 16:
                case 15:
                    // The masks come from Allegro's own shifts rather than
                    // hardcoded 5-6-5, because they are what the drawing code
                    // packed the pixels with.
                    s_dib.header.biBitCount = 16;
                    s_dib.header.biCompression = BI_BITFIELDS;
                    if (depth == 16)
                    {
                        s_dib.masks[0] = (DWORD)0x1F << _rgb_r_shift_16;
                        s_dib.masks[1] = (DWORD)0x3F << _rgb_g_shift_16;
                        s_dib.masks[2] = (DWORD)0x1F << _rgb_b_shift_16;
                    }
                    else
                    {
                        s_dib.masks[0] = (DWORD)0x1F << _rgb_r_shift_15;
                        s_dib.masks[1] = (DWORD)0x1F << _rgb_g_shift_15;
                        s_dib.masks[2] = (DWORD)0x1F << _rgb_b_shift_15;
                    }
                    break;

                default:
                    // 8bpp needs the palette shipped alongside and 24bpp is
                    // never what the backbuffer is. Neither is worth carrying
                    // a second code path for.
                    s_dib_valid = false;
                    return false;
                }

                s_dib_valid = true;
                s_dib_w = bmp->w;
                s_dib_h = bmp->h;
                s_dib_depth = depth;
                return true;
            }
#endif // ALLEGRO_WINDOWS

            // Pack a gfx::Color for a specific surface's colour depth. The
            // backbuffer may be 16-bit on Win9x while image surfaces are
            // always 32-bit, so depth is per-surface, not global.
            inline int pack(const Surface *s, Color c)
            {
                return makecol_depth(bitmap_color_depth(s->bmp), c.r, c.g, c.b);
            }

            Surface *wrap(BITMAP *bmp, bool owned)
            {
                if (!bmp)
                    return NULL;

                Surface *s = new Surface;
                s->bmp = bmp;
                s->owned = owned;
                s->clips.push_back(rect(0, 0, bmp->w, bmp->h));
                return s;
            }

            void apply_clip(Surface *s)
            {
                const Rect &r = s->clips.back();
                if (r.w <= 0 || r.h <= 0)
                {
                    // Degenerate clip — Allegro has no empty-clip concept, so
                    // park it off-surface.
                    ::set_clip_rect(s->bmp, 0, 0, -1, -1);
                    return;
                }
                ::set_clip_rect(s->bmp, r.x, r.y, r.x + r.w - 1, r.y + r.h - 1);
            }

            Rect intersect(const Rect &a, const Rect &b)
            {
                int x1 = a.x > b.x ? a.x : b.x;
                int y1 = a.y > b.y ? a.y : b.y;
                int x2 = (a.x + a.w) < (b.x + b.w) ? (a.x + a.w) : (b.x + b.w);
                int y2 = (a.y + a.h) < (b.y + b.h) ? (a.y + a.h) : (b.y + b.h);
                return rect(x1, y1, x2 > x1 ? x2 - x1 : 0, y2 > y1 ? y2 - y1 : 0);
            }
        } // namespace

        // --- Display ---

        bool init_display(const char *title, int w, int h)
        {
            // Before set_gfx_mode: that is what creates the window, and the
            // window thread can call present() from the moment it exists.
            if (!s_backbuffer_lock)
                s_backbuffer_lock = worker_mutex_create();

            if (allegro_init() != 0)
                return false;

            install_keyboard();
            install_mouse();
            install_timer();

            set_color_depth(32);

            if (set_gfx_mode(GFX_AUTODETECT_WINDOWED, w, h, 0, 0) != 0)
            {
                // Fall back to 16-bit if 32-bit isn't available (Win9x).
                set_color_depth(16);

                if (set_gfx_mode(GFX_AUTODETECT_WINDOWED, w, h, 0, 0) != 0)
                    return false;
            }

            set_window_title(title);

            // Don't use show_mouse(screen) — it fights with backbuffer
            // blitting. The cursor is drawn onto the backbuffer instead.
            show_mouse(NULL);

            BITMAP *bmp = create_bitmap(w, h);
            if (!bmp)
                return false;

            s_backbuffer = wrap(bmp, true);
            s_width = w;
            s_height = h;

            LOCK_FUNCTION(close_button_handler);
            set_close_button_callback(close_button_handler);

            return true;
        }

        void shutdown_display()
        {
            Surface *dead = NULL;
            {
                BackbufferLock guard;
                dead = s_backbuffer;
                s_backbuffer = NULL;
                s_width = 0;
                s_height = 0;
            }

            // Outside the lock, and before allegro_exit(): a present() that
            // was already inside the blit has released the lock by now, and
            // one that starts later sees a null backbuffer and returns.
            destroy_surface(dead);

            allegro_exit();

            // Last, because allegro_exit() tears down the window thread and
            // that thread can take the lock on its way out.
            if (s_backbuffer_lock)
            {
                worker_mutex_destroy(s_backbuffer_lock);
                s_backbuffer_lock = NULL;
            }
        }

        Surface *backbuffer() { return s_backbuffer; }
        int display_width() { return s_width; }
        int display_height() { return s_height; }

        bool display_close_requested() { return s_close_requested != 0; }

        bool display_minimized()
        {
#ifdef ALLEGRO_WINDOWS
            HWND hwnd = win_get_window();
            return hwnd && IsIconic(hwnd);
#else
            return false;
#endif
        }

        void *native_window_handle()
        {
#ifdef ALLEGRO_WINDOWS
            return (void *)win_get_window();
#else
            return NULL;
#endif
        }

        void present()
        {
            // Held across the whole blit, not just the null check: the window
            // thread gets here through WM_PAINT while the main thread may be
            // in resize_display().
            BackbufferLock guard;

            if (!s_backbuffer || !s_backbuffer->bmp)
                return;

            // Size taken from the bitmap rather than from s_width/s_height.
            // Those are assigned in a separate statement from the pointer
            // swap, so a reader can catch the pair disagreeing -- and neither
            // blit_to_hdc nor ::blit clips its SOURCE rectangle, so a stale
            // larger size reads off the end of a freshly shrunk bitmap.
            const int w = s_backbuffer->bmp->w;
            const int h = s_backbuffer->bmp->h;

            // Blit directly to the window DC rather than to Allegro's `screen`
            // bitmap, which stays stuck at the initial size after a resize.
#ifdef ALLEGRO_WINDOWS
            HWND hwnd = win_get_window();
            if (!hwnd)
                return;

            // Nothing to show, and the work below is not free. GDI would
            // clip a minimised window's blit away, but only after the frame
            // had been described and handed to it.
            if (IsIconic(hwnd))
                return;

            HDC hdc = GetDC(hwnd);
            if (!hdc)
                return;

            BITMAP *bmp = s_backbuffer->bmp;
            const int depth = bitmap_color_depth(bmp);
            const int bytes = bytes_per_pixel(depth);
            int stride = bmp->w * bytes;
            stride = (stride + 3) & ~3; // GDI wants rows dword-aligned.

            const bool direct = stride == bmp->w * bytes &&
                                rows_are_contiguous(bmp, stride) &&
                                describe_for_gdi(bmp, depth);

            // Once per size/depth change, not per frame. Which path present()
            // takes is the difference between a blit and a full-frame malloc,
            // copy and free, and it is decided by properties of the bitmap
            // that cannot be read off the target machine any other way.
            static int logged_w = 0, logged_h = 0, logged_depth = 0;
            static bool logged_direct = false;
            if (logged_w != w || logged_h != h || logged_depth != depth ||
                logged_direct != direct)
            {
                logged_w = w;
                logged_h = h;
                logged_depth = depth;
                logged_direct = direct;
                log_info("present: %dx%d %dbpp, %s", w, h, depth,
                         direct ? "blitting in place"
                                : "falling back to blit_to_hdc (copies every frame)");
            }

            if (direct)
            {
                // Same call Allegro would have made, with the same arguments
                // -- but reading the frame where it already is, and with a
                // BITMAPINFO that outlives the frame.
                StretchDIBits(hdc, 0, 0, w, h,
                              0, 0, w, h,
                              bmp->line[0], (const BITMAPINFO *)&s_dib,
                              DIB_RGB_COLORS, SRCCOPY);
            }
            else
            {
                // A depth or layout the fast path does not cover. Correct,
                // just as expensive as it always was.
                blit_to_hdc(bmp, hdc, 0, 0, 0, 0, w, h);
            }

            ReleaseDC(hwnd, hdc);
#else
            ::blit(s_backbuffer->bmp, screen, 0, 0, 0, 0, w, h);
#endif
        }

        void resize_display(int w, int h)
        {
            if (w <= 0 || h <= 0)
                return;
            if (w == s_width && h == s_height)
                return;

            // Allocated before the lock is taken so the window thread, which
            // is inside a modal resize loop and repainting as fast as it can,
            // never waits on a bitmap allocation.
            BITMAP *bmp = create_bitmap(w, h);
            if (!bmp)
                return;

            Surface *fresh = wrap(bmp, true);
            if (!fresh)
            {
                destroy_bitmap(bmp);
                return;
            }

            Surface *old = NULL;
            {
                BackbufferLock guard;
                old = s_backbuffer;
                s_backbuffer = fresh;
                s_width = w;
                s_height = h;
#ifdef ALLEGRO_WINDOWS
                // Under the lock with the swap: describe_for_gdi() keys off
                // the frame's dimensions, and the window thread must not be
                // able to see the new bitmap through the old description.
                s_dib_valid = false;
#endif
            }

            // Safe outside the lock: any present() still holding `old` had
            // the lock, so it has finished; any that starts now sees `fresh`.
            destroy_surface(old);
        }

        // --- Surface lifecycle ---

        Surface *create_surface(int w, int h)
        {
            // Always 32-bit so alpha survives even on a 16-bit display.
            return wrap(create_bitmap_ex(32, w, h), true);
        }

        Surface *surface_from_rgba(const unsigned char *pixels, int w, int h)
        {
            Surface *s = create_surface(w, h);
            if (!s || !pixels)
                return s;

            for (int y = 0; y < h; ++y)
            {
                const unsigned char *row = pixels + (size_t)y * w * 4;
                for (int x = 0; x < w; ++x)
                {
                    ::putpixel(s->bmp, x, y,
                               makeacol32(row[x * 4 + 0], row[x * 4 + 1],
                                          row[x * 4 + 2], row[x * 4 + 3]));
                }
            }
            return s;
        }

        void destroy_surface(Surface *s)
        {
            if (!s)
                return;
            if (s->owned && s->bmp)
                destroy_bitmap(s->bmp);
            delete s;
        }

        int surface_width(const Surface *s) { return s ? s->bmp->w : 0; }
        int surface_height(const Surface *s) { return s ? s->bmp->h : 0; }

        // --- Drawing ---

        void clear(Surface *s, Color c)
        {
            if (s) ::clear_to_color(s->bmp, pack(s, c));
        }

        void fill_rect(Surface *s, const Rect &r, Color c)
        {
            if (!s || r.w <= 0 || r.h <= 0)
                return;
            ::rectfill(s->bmp, r.x, r.y, r.x + r.w - 1, r.y + r.h - 1, pack(s, c));
        }

        void draw_rect(Surface *s, const Rect &r, Color c)
        {
            if (!s || r.w <= 0 || r.h <= 0)
                return;
            ::rect(s->bmp, r.x, r.y, r.x + r.w - 1, r.y + r.h - 1, pack(s, c));
        }

        void hline(Surface *s, int x, int y, int w, Color c)
        {
            if (!s || w <= 0)
                return;
            ::hline(s->bmp, x, y, x + w - 1, pack(s, c));
        }

        void vline(Surface *s, int x, int y, int h, Color c)
        {
            if (!s || h <= 0)
                return;
            ::vline(s->bmp, x, y, y + h - 1, pack(s, c));
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

            ::drawing_mode(DRAW_MODE_TRANS, NULL, 0, 0);
            ::set_trans_blender(0, 0, 0, c.a);
            ::rectfill(s->bmp, r.x, r.y, r.x + r.w - 1, r.y + r.h - 1, pack(s, c));
            ::drawing_mode(DRAW_MODE_SOLID, NULL, 0, 0);
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
            ::blit(src->bmp, dst->bmp, 0, 0, dx, dy, src->bmp->w, src->bmp->h);
        }

        void blit_region(Surface *dst, const Surface *src, const Rect &sr,
                         int dx, int dy)
        {
            if (!dst || !src || sr.w <= 0 || sr.h <= 0)
                return;
            ::blit(src->bmp, dst->bmp, sr.x, sr.y, dx, dy, sr.w, sr.h);
        }

        void blit_scaled(Surface *dst, const Surface *src, const Rect &sr,
                         const Rect &dr)
        {
            if (!dst || !src || sr.w <= 0 || sr.h <= 0 || dr.w <= 0 || dr.h <= 0)
                return;
            ::stretch_blit(src->bmp, dst->bmp,
                           sr.x, sr.y, sr.w, sr.h,
                           dr.x, dr.y, dr.w, dr.h);
        }

        void blit_alpha(Surface *dst, const Surface *src, int dx, int dy)
        {
            if (!dst || !src)
                return;
            ::set_alpha_blender();
            ::draw_trans_sprite(dst->bmp, src->bmp, dx, dy);
            ::drawing_mode(DRAW_MODE_SOLID, NULL, 0, 0);
        }

        // --- Glyph compositing ---

        void blit_tinted(Surface *dst, const Surface *src, int x, int y, Color color)
        {
            if (!dst || !src)
                return;

            const int depth = bitmap_color_depth(dst->bmp);
            const int sw = src->bmp->w;
            const int sh = src->bmp->h;

            for (int row = 0; row < sh; ++row)
            {
                const int py = y + row;
                if (py < dst->bmp->ct || py >= dst->bmp->cb)
                    continue;

                for (int col = 0; col < sw; ++col)
                {
                    const int px = x + col;
                    if (px < dst->bmp->cl || px >= dst->bmp->cr)
                        continue;

                    const int sp = ::getpixel(src->bmp, col, row);
                    int a = geta_depth(32, sp);
                    if (a <= 0)
                        continue;

                    a = a * color.a / 255;
                    if (a <= 0)
                        continue;

                    if (a >= 255)
                    {
                        ::putpixel(dst->bmp, px, py,
                                   makecol_depth(depth, color.r, color.g, color.b));
                        continue;
                    }

                    const int bg = ::getpixel(dst->bmp, px, py);
                    const int br = getr_depth(depth, bg);
                    const int bgc = getg_depth(depth, bg);
                    const int bb = getb_depth(depth, bg);

                    ::putpixel(dst->bmp, px, py,
                               makecol_depth(depth,
                                             (color.r * a + br * (255 - a)) / 255,
                                             (color.g * a + bgc * (255 - a)) / 255,
                                             (color.b * a + bb * (255 - a)) / 255));
                }
            }
        }

        void blend_coverage(Surface *dst, int x, int y, int w, int h,
                            const unsigned char *mask, int mask_pitch, Color color)
        {
            if (!dst || !mask || w <= 0 || h <= 0)
                return;

            const int depth = bitmap_color_depth(dst->bmp);

            for (int row = 0; row < h; ++row)
            {
                const int py = y + row;
                if (py < dst->bmp->ct || py >= dst->bmp->cb)
                    continue;

                const unsigned char *mrow = mask + (size_t)row * mask_pitch;

                for (int col = 0; col < w; ++col)
                {
                    const int cov = mrow[col];
                    if (!cov)
                        continue;

                    const int px = x + col;
                    if (px < dst->bmp->cl || px >= dst->bmp->cr)
                        continue;

                    const int a = cov * color.a / 255;
                    if (a <= 0)
                        continue;

                    if (a >= 255)
                    {
                        ::putpixel(dst->bmp, px, py,
                                   makecol_depth(depth, color.r, color.g, color.b));
                        continue;
                    }

                    const int bg = ::getpixel(dst->bmp, px, py);
                    const int br = getr_depth(depth, bg);
                    const int bgc = getg_depth(depth, bg);
                    const int bb = getb_depth(depth, bg);

                    ::putpixel(dst->bmp, px, py,
                               makecol_depth(depth,
                                             (color.r * a + br * (255 - a)) / 255,
                                             (color.g * a + bgc * (255 - a)) / 255,
                                             (color.b * a + bb * (255 - a)) / 255));
                }
            }
        }

        // --- Pixel access ---

        Color get_pixel(const Surface *s, int x, int y)
        {
            Color c = rgba(0, 0, 0, 0);
            if (!s)
                return c;

            const int depth = bitmap_color_depth(s->bmp);
            const int p = ::getpixel(s->bmp, x, y);
            if (p < 0)
                return c;

            c.r = (unsigned char)getr_depth(depth, p);
            c.g = (unsigned char)getg_depth(depth, p);
            c.b = (unsigned char)getb_depth(depth, p);
            c.a = (depth == 32) ? (unsigned char)geta_depth(depth, p) : 255;
            return c;
        }

        void put_pixel(Surface *s, int x, int y, Color c)
        {
            if (!s)
                return;

            if (bitmap_color_depth(s->bmp) == 32)
                ::putpixel(s->bmp, x, y, makeacol32(c.r, c.g, c.b, c.a));
            else
                ::putpixel(s->bmp, x, y, pack(s, c));
        }

        // --- Timing ---

        unsigned int ticks_ms()
        {
#ifdef ALLEGRO_WINDOWS
            return (unsigned int)GetTickCount();
#else
            // Allegro has no wall-clock millisecond source of its own; the
            // retrace counter is the closest portable stand-in.
            return (unsigned int)(retrace_count * 1000 / 70);
#endif
        }

        void delay_ms(unsigned int ms) { rest(ms); }

    } // namespace gfx
} // namespace launcher
