// gfx_null.cpp — headless implementation of the gfx contract.
//
// Draws nothing, but tracks everything the UI can observe: surface sizes and
// the clip stack. That is enough for the layout and hit-testing logic to run
// unchanged, which is what lets those be tested on a machine with no display
// (and in CI on Linux).
//
// Not selectable as a launcher backend — it is linked only by launcher_tests.

#include "gfx/gfx.h"

#include <cstring>
#include <vector>

namespace launcher
{
    namespace gfx
    {

        struct Surface
        {
            int w, h;
            std::vector<Rect> clips; // back() is active
        };

        namespace
        {
            Surface *s_backbuffer = NULL;
            int s_width = 0;
            int s_height = 0;
            unsigned int s_ticks = 0;

            Surface *make(int w, int h)
            {
                if (w <= 0 || h <= 0)
                    return NULL;
                Surface *s = new Surface;
                s->w = w;
                s->h = h;
                s->clips.push_back(rect(0, 0, w, h));
                return s;
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
            (void)title;
            s_backbuffer = make(w, h);
            if (!s_backbuffer)
                return false;
            s_width = w;
            s_height = h;
            return true;
        }

        void shutdown_display()
        {
            destroy_surface(s_backbuffer);
            s_backbuffer = NULL;
        }

        Surface *backbuffer() { return s_backbuffer; }
        int display_width() { return s_width; }
        int display_height() { return s_height; }
        bool display_close_requested() { return false; }
        bool display_minimized() { return false; }
        void *native_window_handle() { return NULL; }
        void present() {}

        void resize_display(int w, int h)
        {
            if (w <= 0 || h <= 0)
                return;
            destroy_surface(s_backbuffer);
            s_backbuffer = make(w, h);
            s_width = w;
            s_height = h;
        }

        // --- Surface lifecycle ---

        Surface *create_surface(int w, int h) { return make(w, h); }

        Surface *surface_from_rgba(const unsigned char *pixels, int w, int h)
        {
            (void)pixels;
            return make(w, h);
        }

        void destroy_surface(Surface *s) { delete s; }

        int surface_width(const Surface *s) { return s ? s->w : 0; }
        int surface_height(const Surface *s) { return s ? s->h : 0; }

        // --- Drawing (all no-ops) ---

        void clear(Surface *, Color) {}
        void fill_rect(Surface *, const Rect &, Color) {}
        void draw_rect(Surface *, const Rect &, Color) {}
        void hline(Surface *, int, int, int, Color) {}
        void vline(Surface *, int, int, int, Color) {}
        void fill_rect_alpha(Surface *, const Rect &, Color) {}
        void fill_rect_gradient_v(Surface *, const Rect &, Color, Color) {}
        void blit(Surface *, const Surface *, int, int) {}
        void blit_region(Surface *, const Surface *, const Rect &, int, int) {}
        void blit_scaled(Surface *, const Surface *, const Rect &, const Rect &) {}
        void blit_alpha(Surface *, const Surface *, int, int) {}
        void blit_tinted(Surface *, const Surface *, int, int, Color) {}
        void blend_coverage(Surface *, int, int, int, int,
                            const unsigned char *, int, Color) {}
        void put_pixel(Surface *, int, int, Color) {}

        Color get_pixel(const Surface *, int, int) { return rgba(0, 0, 0, 0); }

        // --- Clipping (tracked, because screens branch on it) ---

        void push_clip(Surface *s, const Rect &r)
        {
            if (!s)
                return;
            s->clips.push_back(intersect(s->clips.back(), r));
        }

        void pop_clip(Surface *s)
        {
            if (!s || s->clips.size() <= 1)
                return;
            s->clips.pop_back();
        }

        Rect get_clip(const Surface *s)
        {
            return s ? s->clips.back() : rect(0, 0, 0, 0);
        }

        // --- Timing ---
        //
        // A counter rather than a clock, so anything time-dependent (the text
        // caret blink) is reproducible instead of flaky.

        unsigned int ticks_ms() { return s_ticks; }
        void delay_ms(unsigned int ms) { s_ticks += ms; }

    } // namespace gfx
} // namespace launcher
