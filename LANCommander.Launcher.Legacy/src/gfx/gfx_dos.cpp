// gfx_dos.cpp — MS-DOS implementation of the gfx contract (DJGPP + VESA VBE 2.0).
//
// Same model as the other two backends: everything composites into a 32-bit
// ARGB surface in system RAM and is presented once per frame. The difference
// is that there is no library underneath — DOS has no window system, so this
// file is both the software rasteriser and the display driver.
//
// Display: VBE 2.0 with a linear frame buffer. The mode list is queried and
// the best match for the requested size is picked; 32, 24 and 16 bpp are all
// accepted and present() converts on the way out. Banked (VBE 1.x) modes are
// deliberately not supported — every card that can usefully run a 386-class
// protected mode launcher has VBE 2.0, and bank switching would put a window
// test in the innermost pixel loop.
//
// LFB access takes the near-pointer path when the DPMI host allows it
// (CWSDPMI, DOSBox, plain 98 DOS mode) and falls back to a far pointer
// through its own selector when it does not (a Windows NT-family DOS box).
// The fallback is several times slower but it is correct everywhere, which
// matters more than the frame rate on a machine that is not the target.
//
// The mouse cursor is drawn here rather than by the UI. Every other backend
// gets one from the OS for free, so making the DOS build the only one whose
// screens have to know about a cursor would be the wrong seam.

#include "gfx/gfx.h"
#include "gfx/gfx_dos.h"

#include <dpmi.h>
#include <go32.h>
#include <sys/farptr.h>
#include <sys/nearptr.h>
#include <sys/movedata.h>
#include <time.h>
#include <unistd.h>

#include <cstring>
#include <cstdlib>
#include <vector>

namespace launcher
{
    namespace gfx
    {

        struct Surface
        {
            int w, h;
            unsigned int *px;        // ARGB8888, w*h, row-major, tightly packed
            bool owned;
            std::vector<Rect> clips; // back() is active
        };

        namespace
        {
            // --- Pixel helpers -------------------------------------------

            inline unsigned int pack(Color c)
            {
                return ((unsigned int)c.a << 24) | ((unsigned int)c.r << 16) |
                       ((unsigned int)c.g << 8) | (unsigned int)c.b;
            }

            inline Color unpack(unsigned int p)
            {
                Color c;
                c.a = (unsigned char)(p >> 24);
                c.r = (unsigned char)(p >> 16);
                c.g = (unsigned char)(p >> 8);
                c.b = (unsigned char)p;
                return c;
            }

            // round(x / 255) for x in [0, 65535], without dividing.
            //
            // This is not a micro-optimisation. Every alpha blend needs four
            // of these, and a full-screen scrim is 480,000 pixels: on the
            // 386-class hardware this targets an integer divide is tens of
            // cycles, so the naive form cost ~1.9 million divides a frame and
            // put the whole UI at 2.5 FPS.
            //
            // Exact, not approximate, over the range that matters: the
            // products fed in here are at most 255*255 + 255*0 = 65025.
            inline unsigned int div255(unsigned int x)
            {
                x += 128;
                return (x + (x >> 8)) >> 8;
            }

            // src over dst, both straight (non-premultiplied) ARGB.
            inline unsigned int blend_over(unsigned int dst, unsigned int src,
                                           unsigned int sa)
            {
                if (sa == 0)
                    return dst;
                if (sa == 255)
                    return src;

                const unsigned int ia = 255u - sa;
                const unsigned int dr = (dst >> 16) & 0xFF;
                const unsigned int dg = (dst >> 8) & 0xFF;
                const unsigned int db = dst & 0xFF;
                const unsigned int da = (dst >> 24) & 0xFF;

                const unsigned int sr = (src >> 16) & 0xFF;
                const unsigned int sg = (src >> 8) & 0xFF;
                const unsigned int sb = src & 0xFF;

                // Rounded, so a long chain of partial fills does not drift
                // darker.
                const unsigned int r = div255(sr * sa + dr * ia);
                const unsigned int g = div255(sg * sa + dg * ia);
                const unsigned int b = div255(sb * sa + db * ia);
                unsigned int a = sa + div255(da * ia);
                if (a > 255)
                    a = 255;

                return (a << 24) | (r << 16) | (g << 8) | b;
            }

            Rect intersect(const Rect &a, const Rect &b)
            {
                int x1 = a.x > b.x ? a.x : b.x;
                int y1 = a.y > b.y ? a.y : b.y;
                int x2 = (a.x + a.w) < (b.x + b.w) ? (a.x + a.w) : (b.x + b.w);
                int y2 = (a.y + a.h) < (b.y + b.h) ? (a.y + a.h) : (b.y + b.h);
                return rect(x1, y1, x2 > x1 ? x2 - x1 : 0, y2 > y1 ? y2 - y1 : 0);
            }

            inline Rect clipped(Surface *s, const Rect &r)
            {
                return intersect(s->clips.back(), r);
            }

            Surface *make(int w, int h)
            {
                if (w <= 0 || h <= 0)
                    return NULL;

                Surface *s = new Surface;
                s->w = w;
                s->h = h;
                s->px = (unsigned int *)std::calloc((size_t)w * (size_t)h,
                                                    sizeof(unsigned int));
                if (!s->px)
                {
                    delete s;
                    return NULL;
                }
                s->owned = true;
                s->clips.push_back(rect(0, 0, w, h));
                return s;
            }

            // --- VBE ------------------------------------------------------

            // Mode-info fields are read out of the DOS transfer buffer by
            // offset rather than through a struct, so nothing depends on how
            // the compiler would lay one out. Offsets are from the VBE 2.0
            // specification.
            enum
            {
                MODEINFO_ATTRIBUTES   = 0x00, // bit 7 = LFB available
                MODEINFO_BYTES_PER_SL = 0x10,
                MODEINFO_XRES         = 0x12,
                MODEINFO_YRES         = 0x14,
                MODEINFO_BPP          = 0x19,
                MODEINFO_MEMORY_MODEL = 0x1B, // 6 = direct colour
                MODEINFO_RED_MASK     = 0x1F,
                MODEINFO_RED_POS      = 0x20,
                MODEINFO_GREEN_MASK   = 0x21,
                MODEINFO_GREEN_POS    = 0x22,
                MODEINFO_BLUE_MASK    = 0x23,
                MODEINFO_BLUE_POS     = 0x24,
                MODEINFO_PHYS_BASE    = 0x28
            };

            struct VbeMode
            {
                int number;
                int w, h, bpp;
                int pitch;
                unsigned long phys_base;
                int r_pos, g_pos, b_pos;
                int r_size, g_size, b_size;
            };

            struct Display
            {
                bool active;
                VbeMode mode;
                int old_mode;

                // Exactly one of these is used; see map_framebuffer().
                unsigned char *near_ptr; // NULL when near pointers are refused
                int selector;
                __dpmi_meminfo mapping;
                bool mapped;
            };

            Display s_display;
            Surface *s_backbuffer = NULL;
            bool s_close_requested = false;

            int s_cursor_x = 0, s_cursor_y = 0;
            bool s_cursor_visible = false;

            // Scratch for the converted scanline handed to the far pointer
            // path. Sized once at mode set.
            std::vector<unsigned char> s_scanline;

            // Runs a VBE call with the DOS transfer buffer as ES:DI.
            bool vbe_call(int ax, int cx, unsigned long tb_offset)
            {
                __dpmi_regs r;
                std::memset(&r, 0, sizeof(r));
                r.x.ax = (unsigned short)ax;
                r.x.cx = (unsigned short)cx;
                r.x.es = (unsigned short)((__tb + tb_offset) >> 4);
                r.x.di = (unsigned short)((__tb + tb_offset) & 0x0F);

                if (__dpmi_int(0x10, &r) == -1)
                    return false;

                return r.x.ax == 0x004F;
            }

            int current_video_mode()
            {
                __dpmi_regs r;
                std::memset(&r, 0, sizeof(r));
                r.x.ax = 0x0F00;
                if (__dpmi_int(0x10, &r) == -1)
                    return -1;
                return r.h.al;
            }

            void set_video_mode(int mode)
            {
                __dpmi_regs r;
                std::memset(&r, 0, sizeof(r));
                r.x.ax = (unsigned short)(mode < 0 ? 0x0003 : (mode & 0xFF));
                __dpmi_int(0x10, &r);
            }

            bool read_mode_info(int number, VbeMode *out)
            {
                // 0x200 keeps the mode info block clear of the 512-byte
                // controller info block, which stays live in the transfer
                // buffer while its mode list is being walked.
                const unsigned long off = 0x200;

                if (!vbe_call(0x4F01, number, off))
                    return false;

                const unsigned long base = __tb + off;
                const unsigned int attrs = _farpeekw(_dos_ds, base + MODEINFO_ATTRIBUTES);

                // bit 0 = supported, bit 4 = graphics, bit 7 = LFB.
                if (!(attrs & 0x01) || !(attrs & 0x10) || !(attrs & 0x80))
                    return false;

                if (_farpeekb(_dos_ds, base + MODEINFO_MEMORY_MODEL) != 6)
                    return false; // direct colour only

                out->number    = number;
                out->w         = (int)_farpeekw(_dos_ds, base + MODEINFO_XRES);
                out->h         = (int)_farpeekw(_dos_ds, base + MODEINFO_YRES);
                out->bpp       = (int)_farpeekb(_dos_ds, base + MODEINFO_BPP);
                out->pitch     = (int)_farpeekw(_dos_ds, base + MODEINFO_BYTES_PER_SL);
                out->phys_base = _farpeekl(_dos_ds, base + MODEINFO_PHYS_BASE);
                out->r_size    = (int)_farpeekb(_dos_ds, base + MODEINFO_RED_MASK);
                out->r_pos     = (int)_farpeekb(_dos_ds, base + MODEINFO_RED_POS);
                out->g_size    = (int)_farpeekb(_dos_ds, base + MODEINFO_GREEN_MASK);
                out->g_pos     = (int)_farpeekb(_dos_ds, base + MODEINFO_GREEN_POS);
                out->b_size    = (int)_farpeekb(_dos_ds, base + MODEINFO_BLUE_MASK);
                out->b_pos     = (int)_farpeekb(_dos_ds, base + MODEINFO_BLUE_POS);

                if (out->w <= 0 || out->h <= 0 || out->pitch <= 0 ||
                    out->phys_base == 0)
                    return false;

                if (out->r_size <= 0 || out->r_size > 8 ||
                    out->g_size <= 0 || out->g_size > 8 ||
                    out->b_size <= 0 || out->b_size > 8)
                    return false;

                return out->bpp == 32 || out->bpp == 24 || out->bpp == 16 ||
                       out->bpp == 15;
            }

            // Lower is better. An exact size match wins outright; otherwise
            // the smallest mode that still covers the request, then the
            // largest that does not, with deeper colour preferred at equal
            // size.
            long mode_score(const VbeMode &m, int want_w, int want_h)
            {
                const long area = (long)m.w * (long)m.h;
                const long want = (long)want_w * (long)want_h;

                long score;
                if (m.w == want_w && m.h == want_h)
                    score = 0;
                else if (m.w >= want_w && m.h >= want_h)
                    score = 1000000L + (area - want);
                else
                    score = 3000000L - area;

                // Tie-break on depth: 32 bpp needs no conversion at all.
                score *= 4;
                if (m.bpp == 32)
                    score += 0;
                else if (m.bpp == 24)
                    score += 1;
                else
                    score += 2;

                return score;
            }

            bool find_mode(int want_w, int want_h, VbeMode *out)
            {
                // Ask as VBE2 so the mode list is the card's own rather than
                // the standard subset.
                _farpokel(_dos_ds, __tb, 0x32454256UL); // 'VBE2'

                __dpmi_regs r;
                std::memset(&r, 0, sizeof(r));
                r.x.ax = 0x4F00;
                r.x.es = (unsigned short)(__tb >> 4);
                r.x.di = (unsigned short)(__tb & 0x0F);
                if (__dpmi_int(0x10, &r) == -1 || r.x.ax != 0x004F)
                    return false;

                if (_farpeekl(_dos_ds, __tb) != 0x41534556UL) // 'VESA'
                    return false;

                if (_farpeekw(_dos_ds, __tb + 0x04) < 0x0200)
                    return false; // VBE 1.x: no linear frame buffer

                // The mode list is a real-mode far pointer at offset 0x0E.
                const unsigned int list_off = _farpeekw(_dos_ds, __tb + 0x0E);
                const unsigned int list_seg = _farpeekw(_dos_ds, __tb + 0x10);
                const unsigned long list = ((unsigned long)list_seg << 4) + list_off;

                bool found = false;
                long best = 0;
                VbeMode candidate;

                for (int i = 0; i < 512; ++i)
                {
                    const unsigned int number =
                        _farpeekw(_dos_ds, list + (unsigned long)i * 2);
                    if (number == 0xFFFF)
                        break;

                    if (!read_mode_info((int)number, &candidate))
                        continue;

                    const long score = mode_score(candidate, want_w, want_h);
                    if (!found || score < best)
                    {
                        best = score;
                        *out = candidate;
                        found = true;
                    }
                }

                return found;
            }

            bool map_framebuffer(Display *d)
            {
                const unsigned long size =
                    (unsigned long)d->mode.pitch * (unsigned long)d->mode.h;

                d->mapping.address = d->mode.phys_base;
                d->mapping.size = size;
                if (__dpmi_physical_address_mapping(&d->mapping) != 0)
                    return false;
                d->mapped = true;

                // Fast path: with near pointers enabled the frame buffer is
                // reachable as plain memory. Refused by DPMI hosts that will
                // not drop the segment limit (the NT-family DOS box), which
                // is what the selector below is for.
                if (__djgpp_nearptr_enable())
                {
                    d->near_ptr = (unsigned char *)d->mapping.address +
                                  __djgpp_conventional_base;
                    d->selector = 0;
                    return true;
                }

                d->near_ptr = NULL;
                d->selector = __dpmi_allocate_ldt_descriptors(1);
                if (d->selector < 0)
                {
                    __dpmi_free_physical_address_mapping(&d->mapping);
                    d->mapped = false;
                    d->selector = 0;
                    return false;
                }

                __dpmi_set_segment_base_address(d->selector, d->mapping.address);
                __dpmi_set_segment_limit(d->selector, size - 1);
                return true;
            }

            void unmap_framebuffer(Display *d)
            {
                if (d->near_ptr)
                {
                    __djgpp_nearptr_disable();
                    d->near_ptr = NULL;
                }
                if (d->selector > 0)
                {
                    __dpmi_free_ldt_descriptor(d->selector);
                    d->selector = 0;
                }
                if (d->mapped)
                {
                    __dpmi_free_physical_address_mapping(&d->mapping);
                    d->mapped = false;
                }
            }

            // --- Cursor ---------------------------------------------------

            // ' ' transparent, 'X' black outline, '#' white fill.
            const char *const CURSOR[] = {
                "X           ",
                "XX          ",
                "X#X         ",
                "X##X        ",
                "X###X       ",
                "X####X      ",
                "X#####X     ",
                "X######X    ",
                "X#######X   ",
                "X########X  ",
                "X#########X ",
                "X######XXXXX",
                "X###X##X    ",
                "X##X X##X   ",
                "X#X   X##X  ",
                "XX    X##X  ",
                "X      X##X ",
                "        XX  "
            };
            const int CURSOR_H = (int)(sizeof(CURSOR) / sizeof(CURSOR[0]));
            const int CURSOR_W = 12;

            // Drawn straight into the backbuffer just before conversion and
            // undone afterwards, so the frame the UI composited is never
            // permanently altered by something it does not know about.
            void stamp_cursor(std::vector<unsigned int> *saved)
            {
                saved->clear();
                if (!s_cursor_visible || !s_backbuffer)
                    return;

                saved->reserve((size_t)CURSOR_W * (size_t)CURSOR_H);

                for (int cy = 0; cy < CURSOR_H; ++cy)
                {
                    const int y = s_cursor_y + cy;
                    for (int cx = 0; cx < CURSOR_W; ++cx)
                    {
                        const int x = s_cursor_x + cx;
                        const char g = CURSOR[cy][cx];

                        if (x < 0 || y < 0 || x >= s_backbuffer->w ||
                            y >= s_backbuffer->h || g == ' ')
                        {
                            saved->push_back(0);
                            continue;
                        }

                        unsigned int *p =
                            &s_backbuffer->px[(size_t)y * s_backbuffer->w + x];
                        saved->push_back(*p);
                        *p = (g == 'X') ? 0xFF000000u : 0xFFFFFFFFu;
                    }
                }
            }

            void unstamp_cursor(const std::vector<unsigned int> &saved)
            {
                if (saved.empty() || !s_backbuffer)
                    return;

                size_t i = 0;
                for (int cy = 0; cy < CURSOR_H; ++cy)
                {
                    const int y = s_cursor_y + cy;
                    for (int cx = 0; cx < CURSOR_W; ++cx, ++i)
                    {
                        const int x = s_cursor_x + cx;
                        if (x < 0 || y < 0 || x >= s_backbuffer->w ||
                            y >= s_backbuffer->h || CURSOR[cy][cx] == ' ')
                            continue;
                        s_backbuffer->px[(size_t)y * s_backbuffer->w + x] = saved[i];
                    }
                }
            }

            // --- Presentation ---------------------------------------------

            inline unsigned int to_native(const VbeMode &m, unsigned int argb)
            {
                const unsigned int r = (argb >> 16) & 0xFF;
                const unsigned int g = (argb >> 8) & 0xFF;
                const unsigned int b = argb & 0xFF;

                return ((r >> (8 - m.r_size)) << m.r_pos) |
                       ((g >> (8 - m.g_size)) << m.g_pos) |
                       ((b >> (8 - m.b_size)) << m.b_pos);
            }

            inline int bytes_per_pixel(const VbeMode &m)
            {
                return m.bpp == 32 ? 4 : (m.bpp == 24 ? 3 : 2);
            }

            // Converts one backbuffer row into `dst` in the mode's format.
            void convert_row(const VbeMode &m, const unsigned int *src, int n,
                             unsigned char *dst)
            {
                if (m.bpp == 32)
                {
                    unsigned int *out = (unsigned int *)dst;
                    // The overwhelmingly common case: the only difference
                    // from our own layout is the ignored alpha byte.
                    if (m.r_pos == 16 && m.g_pos == 8 && m.b_pos == 0 &&
                        m.r_size == 8 && m.g_size == 8 && m.b_size == 8)
                        std::memcpy(out, src, (size_t)n * 4);
                    else
                        for (int i = 0; i < n; ++i)
                            out[i] = to_native(m, src[i]);
                }
                else if (m.bpp == 24)
                {
                    for (int i = 0; i < n; ++i)
                    {
                        const unsigned int v = to_native(m, src[i]);
                        dst[i * 3 + 0] = (unsigned char)v;
                        dst[i * 3 + 1] = (unsigned char)(v >> 8);
                        dst[i * 3 + 2] = (unsigned char)(v >> 16);
                    }
                }
                else // 15/16 bpp
                {
                    unsigned short *out = (unsigned short *)dst;
                    for (int i = 0; i < n; ++i)
                        out[i] = (unsigned short)to_native(m, src[i]);
                }
            }

            // --- Timing ---------------------------------------------------

            // uclock() ticks at 1.19 MHz off the 8253, which is the only
            // clock DOS offers that is finer than the 55 ms BIOS tick — and
            // 55 ms is too coarse for the caret blink and frame pacing.
            uclock_t s_start = 0;
        } // namespace

        // --- Display ---

        bool init_display(const char *title, int w, int h)
        {
            (void)title; // no window manager to tell

            std::memset(&s_display, 0, sizeof(s_display));
            s_start = uclock();

            if (!find_mode(w, h, &s_display.mode))
                return false;

            s_display.old_mode = current_video_mode();

            // bit 14 = use the linear frame buffer.
            __dpmi_regs r;
            std::memset(&r, 0, sizeof(r));
            r.x.ax = 0x4F02;
            r.x.bx = (unsigned short)(s_display.mode.number | 0x4000);
            if (__dpmi_int(0x10, &r) == -1 || r.x.ax != 0x004F)
                return false;

            if (!map_framebuffer(&s_display))
            {
                set_video_mode(s_display.old_mode);
                return false;
            }

            s_display.active = true;
            s_scanline.resize((size_t)s_display.mode.pitch);

            s_backbuffer = make(s_display.mode.w, s_display.mode.h);
            if (!s_backbuffer)
            {
                shutdown_display();
                return false;
            }

            s_close_requested = false;
            return true;
        }

        void shutdown_display()
        {
            if (s_backbuffer)
            {
                destroy_surface(s_backbuffer);
                s_backbuffer = NULL;
            }

            if (s_display.active)
            {
                unmap_framebuffer(&s_display);
                set_video_mode(s_display.old_mode);
                s_display.active = false;
            }
        }

        Surface *backbuffer() { return s_backbuffer; }

        int display_width() { return s_backbuffer ? s_backbuffer->w : 0; }
        int display_height() { return s_backbuffer ? s_backbuffer->h : 0; }

        void present()
        {
            if (!s_display.active || !s_backbuffer)
                return;

            static std::vector<unsigned int> saved;
            stamp_cursor(&saved);

            const VbeMode &m = s_display.mode;
            const int rows = m.h < s_backbuffer->h ? m.h : s_backbuffer->h;
            const int cols = m.w < s_backbuffer->w ? m.w : s_backbuffer->w;
            const size_t row_bytes = (size_t)cols * (size_t)bytes_per_pixel(m);

            // The common case by a wide margin: a 32 bpp mode whose channel
            // layout matches ours, with no padding at the end of a scanline,
            // reachable through a near pointer. Then the whole frame is one
            // memcpy and the per-row loop below is pure overhead.
            if (s_display.near_ptr && m.bpp == 32 &&
                m.r_pos == 16 && m.g_pos == 8 && m.b_pos == 0 &&
                m.r_size == 8 && m.g_size == 8 && m.b_size == 8 &&
                cols == s_backbuffer->w && (size_t)m.pitch == row_bytes)
            {
                std::memcpy(s_display.near_ptr, s_backbuffer->px,
                            row_bytes * (size_t)rows);
                unstamp_cursor(saved);
                return;
            }

            for (int y = 0; y < rows; ++y)
            {
                const unsigned int *src =
                    &s_backbuffer->px[(size_t)y * s_backbuffer->w];
                const unsigned long dst_off =
                    (unsigned long)y * (unsigned long)m.pitch;

                if (s_display.near_ptr)
                {
                    convert_row(m, src, cols, s_display.near_ptr + dst_off);
                }
                else
                {
                    convert_row(m, src, cols, &s_scanline[0]);
                    // One movedata per row rather than per pixel: the far
                    // path is slow enough without paying a selector reload
                    // 800 times a line.
                    movedata(_my_ds(), (unsigned)&s_scanline[0],
                             s_display.selector, (unsigned)dst_off, row_bytes);
                }
            }

            unstamp_cursor(saved);
        }

        void resize_display(int w, int h)
        {
            // The screen is whatever mode was set at startup: there is no
            // window to resize, and the chrome only ever asks for this after
            // a drag DOS cannot deliver.
            (void)w;
            (void)h;
        }

        bool display_close_requested() { return s_close_requested; }

        // DOS owns the whole screen; there is nothing to minimise into.
        bool display_minimized() { return false; }

        void *native_window_handle() { return NULL; }

        // --- DOS-only hooks (gfx_dos.h) ---

        void dos_set_cursor(int x, int y, bool visible)
        {
            s_cursor_x = x;
            s_cursor_y = y;
            s_cursor_visible = visible;
        }

        void dos_request_close() { s_close_requested = true; }

        void dos_suspend_display()
        {
            if (!s_display.active)
                return;

            unmap_framebuffer(&s_display);
            set_video_mode(s_display.old_mode);
            s_display.active = false;
        }

        bool dos_resume_display()
        {
            // Already up, or never was: either way there is nothing to do
            // and the caller is not in trouble.
            if (s_display.active || s_backbuffer == NULL)
                return s_display.active;

            __dpmi_regs r;
            std::memset(&r, 0, sizeof(r));
            r.x.ax = 0x4F02;
            r.x.bx = (unsigned short)(s_display.mode.number | 0x4000);
            if (__dpmi_int(0x10, &r) == -1 || r.x.ax != 0x004F)
                return false;

            if (!map_framebuffer(&s_display))
            {
                set_video_mode(s_display.old_mode);
                return false;
            }

            s_display.active = true;
            return true;
        }

        // --- Surface lifecycle ---

        Surface *create_surface(int w, int h) { return make(w, h); }

        Surface *surface_from_rgba(const unsigned char *pixels, int w, int h)
        {
            if (!pixels)
                return NULL;

            Surface *s = make(w, h);
            if (!s)
                return NULL;

            const size_t n = (size_t)w * (size_t)h;
            for (size_t i = 0; i < n; ++i)
            {
                const unsigned char *p = pixels + i * 4;
                s->px[i] = ((unsigned int)p[3] << 24) |
                           ((unsigned int)p[0] << 16) |
                           ((unsigned int)p[1] << 8) | (unsigned int)p[2];
            }
            return s;
        }

        void destroy_surface(Surface *s)
        {
            if (!s)
                return;
            if (s->owned)
                std::free(s->px);
            delete s;
        }

        int surface_width(const Surface *s) { return s ? s->w : 0; }
        int surface_height(const Surface *s) { return s ? s->h : 0; }

        // --- Drawing ---

        void clear(Surface *s, Color c)
        {
            if (!s || s->w <= 0 || s->h <= 0)
                return;

            const unsigned int v = pack(c);
            const size_t row_bytes = (size_t)s->w * sizeof(unsigned int);

            // Fill one row the slow way, then replicate it. memcpy compiles
            // to a `rep movsd` here, which moves several times the bytes per
            // cycle that a scalar store loop does -- and this runs over the
            // whole backbuffer every frame.
            for (int x = 0; x < s->w; ++x)
                s->px[x] = v;

            for (int y = 1; y < s->h; ++y)
                std::memcpy(&s->px[(size_t)y * s->w], s->px, row_bytes);
        }

        void fill_rect(Surface *s, const Rect &r, Color c)
        {
            if (!s)
                return;
            const Rect a = clipped(s, r);
            if (a.w <= 0 || a.h <= 0)
                return;

            const unsigned int v = pack(c);
            for (int y = 0; y < a.h; ++y)
            {
                unsigned int *row = &s->px[(size_t)(a.y + y) * s->w + a.x];
                for (int x = 0; x < a.w; ++x)
                    row[x] = v;
            }
        }

        void draw_rect(Surface *s, const Rect &r, Color c)
        {
            if (!s || r.w <= 0 || r.h <= 0)
                return;
            hline(s, r.x, r.y, r.w, c);
            hline(s, r.x, r.y + r.h - 1, r.w, c);
            vline(s, r.x, r.y, r.h, c);
            vline(s, r.x + r.w - 1, r.y, r.h, c);
        }

        void hline(Surface *s, int x, int y, int w, Color c)
        {
            fill_rect(s, rect(x, y, w, 1), c);
        }

        void vline(Surface *s, int x, int y, int h, Color c)
        {
            fill_rect(s, rect(x, y, 1, h), c);
        }

        void fill_rect_alpha(Surface *s, const Rect &r, Color c)
        {
            if (!s)
                return;
            if (c.a == 255)
            {
                fill_rect(s, r, c);
                return;
            }
            if (c.a == 0)
                return;

            const Rect a = clipped(s, r);
            if (a.w <= 0 || a.h <= 0)
                return;

            const unsigned int src = pack(c);
            for (int y = 0; y < a.h; ++y)
            {
                unsigned int *row = &s->px[(size_t)(a.y + y) * s->w + a.x];
                for (int x = 0; x < a.w; ++x)
                    row[x] = blend_over(row[x], src, c.a);
            }
        }

        void fill_rect_gradient_v(Surface *s, const Rect &r, Color top, Color bottom)
        {
            if (!s || r.h <= 0)
                return;

            const Rect a = clipped(s, r);
            if (a.w <= 0 || a.h <= 0)
                return;

            const int denom = r.h > 1 ? r.h - 1 : 1;

            for (int y = 0; y < a.h; ++y)
            {
                // Interpolated against the requested rect, not the clipped
                // one, so a partly scrolled-off gradient keeps its ramp.
                const int t = (a.y + y) - r.y;

                Color c;
                c.r = (unsigned char)(top.r + (bottom.r - top.r) * t / denom);
                c.g = (unsigned char)(top.g + (bottom.g - top.g) * t / denom);
                c.b = (unsigned char)(top.b + (bottom.b - top.b) * t / denom);
                c.a = (unsigned char)(top.a + (bottom.a - top.a) * t / denom);

                const unsigned int src = pack(c);
                unsigned int *row = &s->px[(size_t)(a.y + y) * s->w + a.x];

                if (c.a == 255)
                {
                    for (int x = 0; x < a.w; ++x)
                        row[x] = src;
                }
                else if (c.a != 0)
                {
                    for (int x = 0; x < a.w; ++x)
                        row[x] = blend_over(row[x], src, c.a);
                }
            }
        }

        // --- Clipping ---

        void push_clip(Surface *s, const Rect &r)
        {
            if (!s)
                return;
            s->clips.push_back(intersect(s->clips.back(), r));
        }

        void pop_clip(Surface *s)
        {
            // Never pop the full-surface rect the surface was created with.
            if (s && s->clips.size() > 1)
                s->clips.pop_back();
        }

        Rect get_clip(const Surface *s)
        {
            if (!s)
                return rect(0, 0, 0, 0);
            return s->clips.back();
        }

        // --- Blitting ---

        namespace
        {
            // Shared setup for the 1:1 blits. Intersects `src_rect` with the
            // source bounds and the landing position with the destination's
            // clip, then hands back the region actually to be copied.
            bool prepare_blit(Surface *dst, const Surface *src,
                              const Rect &src_rect, int dx, int dy,
                              Rect *out_src, Rect *out_dst)
            {
                if (!dst || !src)
                    return false;

                Rect sr = intersect(rect(0, 0, src->w, src->h), src_rect);
                if (sr.w <= 0 || sr.h <= 0)
                    return false;

                // Where the (possibly trimmed) source region would land.
                const int ox = dx + (sr.x - src_rect.x);
                const int oy = dy + (sr.y - src_rect.y);

                const Rect dr = clipped(dst, rect(ox, oy, sr.w, sr.h));
                if (dr.w <= 0 || dr.h <= 0)
                    return false;

                // Clipping off the left/top edge has to advance the source
                // origin by the same amount, or the copy smears.
                sr.x += dr.x - ox;
                sr.y += dr.y - oy;
                sr.w = dr.w;
                sr.h = dr.h;

                *out_src = sr;
                *out_dst = dr;
                return true;
            }
        } // namespace

        void blit(Surface *dst, const Surface *src, int dx, int dy)
        {
            if (!src)
                return;
            blit_region(dst, src, rect(0, 0, src->w, src->h), dx, dy);
        }

        void blit_region(Surface *dst, const Surface *src, const Rect &src_rect,
                         int dx, int dy)
        {
            Rect sr, dr;
            if (!prepare_blit(dst, src, src_rect, dx, dy, &sr, &dr))
                return;

            for (int y = 0; y < dr.h; ++y)
            {
                std::memcpy(&dst->px[(size_t)(dr.y + y) * dst->w + dr.x],
                            &src->px[(size_t)(sr.y + y) * src->w + sr.x],
                            (size_t)dr.w * sizeof(unsigned int));
            }
        }

        void blit_alpha(Surface *dst, const Surface *src, int dx, int dy)
        {
            if (!src)
                return;

            Rect sr, dr;
            if (!prepare_blit(dst, src, rect(0, 0, src->w, src->h), dx, dy,
                              &sr, &dr))
                return;

            for (int y = 0; y < dr.h; ++y)
            {
                unsigned int *d = &dst->px[(size_t)(dr.y + y) * dst->w + dr.x];
                const unsigned int *s = &src->px[(size_t)(sr.y + y) * src->w + sr.x];
                for (int x = 0; x < dr.w; ++x)
                    d[x] = blend_over(d[x], s[x], s[x] >> 24);
            }
        }

        void blit_scaled(Surface *dst, const Surface *src, const Rect &src_rect,
                         const Rect &dst_rect)
        {
            if (!dst || !src || dst_rect.w <= 0 || dst_rect.h <= 0)
                return;

            const Rect sr = intersect(rect(0, 0, src->w, src->h), src_rect);
            if (sr.w <= 0 || sr.h <= 0)
                return;

            const Rect dr = clipped(dst, dst_rect);
            if (dr.w <= 0 || dr.h <= 0)
                return;

            // Nearest neighbour. The one caller is the login background's
            // aspect-fill, which is a photograph behind a scrim — bilinear
            // would cost a multiply per channel per pixel on a 386 for a
            // difference nobody can see through it.
            //
            // The source column for a given destination column does not
            // depend on the row, so it is computed once into a table rather
            // than once per pixel. Full screen, that is 800 divides instead
            // of 480,000 — the difference between this being the second most
            // expensive thing in the frame and it not registering.
            //
            // Function-static rather than a local: this runs every frame, and
            // there is one thread on DOS.
            static std::vector<int> col;
            col.resize((size_t)dr.w);

            for (int x = 0; x < dr.w; ++x)
            {
                int sxi = sr.x + (int)((long)(dr.x + x - dst_rect.x) * sr.w /
                                       dst_rect.w);
                if (sxi >= sr.x + sr.w)
                    sxi = sr.x + sr.w - 1;
                col[(size_t)x] = sxi;
            }

            const int *const cols = &col[0];

            for (int y = 0; y < dr.h; ++y)
            {
                int syi = sr.y + (int)((long)(dr.y + y - dst_rect.y) * sr.h /
                                       dst_rect.h);
                if (syi >= sr.y + sr.h)
                    syi = sr.y + sr.h - 1;

                const unsigned int *s = &src->px[(size_t)syi * src->w];
                unsigned int *d = &dst->px[(size_t)(dr.y + y) * dst->w + dr.x];

                for (int x = 0; x < dr.w; ++x)
                    d[x] = s[cols[x]];
            }
        }

        // --- Glyph compositing ---

        void blit_tinted(Surface *dst, const Surface *src, int x, int y, Color color)
        {
            if (!src)
                return;

            Rect sr, dr;
            if (!prepare_blit(dst, src, rect(0, 0, src->w, src->h), x, y,
                              &sr, &dr))
                return;

            const unsigned int tint = pack(color) & 0x00FFFFFFu;

            for (int row = 0; row < dr.h; ++row)
            {
                unsigned int *d = &dst->px[(size_t)(dr.y + row) * dst->w + dr.x];
                const unsigned int *s = &src->px[(size_t)(sr.y + row) * src->w + sr.x];

                for (int col = 0; col < dr.w; ++col)
                {
                    // Coverage is the mask's alpha, scaled by the requested
                    // colour's own alpha the way SDL's colour modulation does.
                    unsigned int a = s[col] >> 24;
                    if (color.a != 255)
                        a = div255(a * color.a);
                    d[col] = blend_over(d[col], tint | (a << 24), a);
                }
            }
        }

        void blend_coverage(Surface *dst, int x, int y, int w, int h,
                            const unsigned char *mask, int mask_pitch, Color color)
        {
            if (!dst || !mask || w <= 0 || h <= 0)
                return;

            const Rect dr = clipped(dst, rect(x, y, w, h));
            if (dr.w <= 0 || dr.h <= 0)
                return;

            const unsigned int tint = pack(color) & 0x00FFFFFFu;

            for (int row = 0; row < dr.h; ++row)
            {
                const unsigned char *m = mask +
                    (size_t)(dr.y + row - y) * (size_t)mask_pitch + (dr.x - x);
                unsigned int *d = &dst->px[(size_t)(dr.y + row) * dst->w + dr.x];

                for (int col = 0; col < dr.w; ++col)
                {
                    unsigned int a = m[col];
                    if (color.a != 255)
                        a = div255(a * color.a);
                    d[col] = blend_over(d[col], tint | (a << 24), a);
                }
            }
        }

        // --- Pixel access ---

        Color get_pixel(const Surface *s, int x, int y)
        {
            if (!s || x < 0 || y < 0 || x >= s->w || y >= s->h)
                return rgba(0, 0, 0, 0);
            return unpack(s->px[(size_t)y * s->w + x]);
        }

        void put_pixel(Surface *s, int x, int y, Color c)
        {
            if (!s || x < 0 || y < 0 || x >= s->w || y >= s->h)
                return;
            s->px[(size_t)y * s->w + x] = pack(c);
        }

        // --- Timing ---

        unsigned int ticks_ms()
        {
            const uclock_t now = uclock();
            return (unsigned int)(((now - s_start) * 1000) / UCLOCKS_PER_SEC);
        }

        void delay_ms(unsigned int ms)
        {
            // usleep() on DJGPP issues the DPMI "release time slice" call, so
            // an idle launcher does not peg the host CPU under DOSBox or a
            // Windows DOS box the way a spin would.
            if (ms)
                usleep(ms * 1000);
        }

    } // namespace gfx
} // namespace launcher
