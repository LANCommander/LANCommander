// font_stb.cpp — text via stb_truetype against the bundled TTF.
//
// The DOS backend's font renderer. It exists because font_ttf.cpp reaches
// through SDL_ttf into FreeType, and neither builds for DJGPP without a port
// nobody needs: stb_truetype is one header, has no dependencies, and produces
// the same 8-bit coverage the gfx layer already knows how to composite.
//
// The structure deliberately mirrors font_ttf.cpp — same string-keyed mask
// cache, same white-mask-plus-tint arrangement — so the two stay comparable
// and a bug found in one is findable in the other. It is written against
// gfx.h alone, so nothing here is actually DOS-specific.
//
// The visible difference from FreeType is hinting: stb_truetype does not
// hint at all, so small text is a little softer. At the sizes this UI uses
// that is the whole of it.

#include "ui/font.h"
#include "app/paths.h"

#include "stb_truetype.h"

#include <cstdio>
#include <cstring>
#include <map>
#include <string>
#include <vector>

namespace launcher
{
    namespace ui
    {

        namespace
        {
            const char *const FONT_FILE = "assets/fonts/Inter-Regular.ttf";

            const size_t MAX_CACHED_STRINGS = 512;

            stbtt_fontinfo s_font;
            std::vector<unsigned char> s_font_data;
            bool s_ready = false;

            struct Entry
            {
                gfx::Surface *mask;
                int width;
                unsigned long long last_used;
            };

            // Everything that varies with the rung of the type scale. The
            // face itself does not: stbtt takes the scale per call, so one
            // stbtt_fontinfo over one copy of the TTF serves every size, and
            // only the derived metrics and the caches are per-rung. That
            // matters on DOS, where the alternative — one 400KB buffer per
            // rung — is most of a conventional memory budget.
            struct Rung
            {
                float scale;
                int ascent;  // pixels, positive
                int height;

                std::map<std::string, Entry> cache;
                std::map<std::string, int> widths;

                Rung() : scale(0.0f), ascent(0), height(0) {}
            };

            Rung s_rungs[(int)FontSize::Count];
            unsigned long long s_tick = 0;

            Rung &rung_of(FontSize size)
            {
                const int i = (int)size;
                return s_rungs[(i >= 0 && i < (int)FontSize::Count)
                                   ? i
                                   : (int)FontSize::Body];
            }

            // --- UTF-8 ----------------------------------------------------

            // Decodes one code point, advancing *i. Malformed input yields
            // U+FFFD and consumes one byte, so a bad string cannot loop.
            unsigned int next_codepoint(const char *s, size_t len, size_t *i)
            {
                const unsigned char c = (unsigned char)s[*i];

                if (c < 0x80)
                {
                    ++*i;
                    return c;
                }

                int extra;
                unsigned int cp;
                if ((c & 0xE0) == 0xC0)      { extra = 1; cp = c & 0x1F; }
                else if ((c & 0xF0) == 0xE0) { extra = 2; cp = c & 0x0F; }
                else if ((c & 0xF8) == 0xF0) { extra = 3; cp = c & 0x07; }
                else                         { ++*i; return 0xFFFD; }

                // Truncated sequence at the end of the string.
                if (*i + (size_t)extra >= len)
                {
                    ++*i;
                    return 0xFFFD;
                }

                for (int k = 1; k <= extra; ++k)
                {
                    const unsigned char cc = (unsigned char)s[*i + (size_t)k];
                    if ((cc & 0xC0) != 0x80)
                    {
                        ++*i;
                        return 0xFFFD;
                    }
                    cp = (cp << 6) | (cc & 0x3F);
                }

                *i += (size_t)extra + 1;
                return cp;
            }

            // --- Measurement ----------------------------------------------

            // Total advance of `utf8`, and optionally the byte offset at which
            // the run first exceeds `max_w` along with the width up to there.
            int measure_run(const char *utf8, int max_w, int *out_fit_bytes,
                            int *out_fit_w, FontSize size)
            {
                const float scale = rung_of(size).scale;

                if (!s_ready || !utf8 || !*utf8)
                {
                    if (out_fit_bytes) *out_fit_bytes = 0;
                    if (out_fit_w) *out_fit_w = 0;
                    return 0;
                }

                const size_t len = std::strlen(utf8);
                size_t i = 0;
                float x = 0.0f;
                unsigned int prev = 0;

                int fit_bytes = 0;
                int fit_w = 0;
                bool fit_done = false;

                while (i < len)
                {
                    const size_t start = i;
                    const unsigned int cp = next_codepoint(utf8, len, &i);

                    if (prev)
                        x += scale * (float)stbtt_GetCodepointKernAdvance(
                                         &s_font, (int)prev, (int)cp);

                    int advance = 0, lsb = 0;
                    stbtt_GetCodepointHMetrics(&s_font, (int)cp, &advance, &lsb);
                    const float next_x = x + scale * (float)advance;

                    if (!fit_done && max_w >= 0)
                    {
                        if ((int)(next_x + 0.5f) > max_w)
                        {
                            fit_bytes = (int)start;
                            fit_w = (int)(x + 0.5f);
                            fit_done = true;
                        }
                    }

                    x = next_x;
                    prev = cp;
                }

                if (!fit_done)
                {
                    fit_bytes = (int)len;
                    fit_w = (int)(x + 0.5f);
                }

                if (out_fit_bytes) *out_fit_bytes = fit_bytes;
                if (out_fit_w) *out_fit_w = fit_w;

                return (int)(x + 0.5f);
            }

            // --- Rasterisation --------------------------------------------

            // Renders `utf8` into an 8-bit coverage buffer sized w*h.
            void rasterise(const char *utf8, int w, int h,
                           std::vector<unsigned char> *cov, FontSize size)
            {
                const Rung &rung = rung_of(size);
                const float scale = rung.scale;

                cov->assign((size_t)w * (size_t)h, 0);

                const size_t len = std::strlen(utf8);
                size_t i = 0;
                float x = 0.0f;
                unsigned int prev = 0;

                while (i < len)
                {
                    const unsigned int cp = next_codepoint(utf8, len, &i);

                    if (prev)
                        x += scale * (float)stbtt_GetCodepointKernAdvance(
                                         &s_font, (int)prev, (int)cp);

                    // Sub-pixel positioning off the fractional part of the
                    // pen: without it, accumulated rounding makes the spacing
                    // of a long label visibly uneven.
                    const int ix = (int)x;
                    const float frac = x - (float)ix;

                    int gx0, gy0, gx1, gy1;
                    stbtt_GetCodepointBitmapBoxSubpixel(&s_font, (int)cp,
                                                        scale, scale,
                                                        frac, 0.0f,
                                                        &gx0, &gy0, &gx1, &gy1);

                    const int gw = gx1 - gx0;
                    const int gh = gy1 - gy0;

                    if (gw > 0 && gh > 0)
                    {
                        const int dx = ix + gx0;
                        const int dy = rung.ascent + gy0;

                        // Clipped to the buffer rather than assumed to fit:
                        // a glyph's ink can overhang its advance on either
                        // side, and the box is padded but not unbounded.
                        if (dx < w && dy < h && dx + gw > 0 && dy + gh > 0)
                        {
                            std::vector<unsigned char> tmp((size_t)gw * (size_t)gh, 0);
                            stbtt_MakeCodepointBitmapSubpixel(
                                &s_font, &tmp[0], gw, gh, gw,
                                scale, scale, frac, 0.0f, (int)cp);

                            for (int yy = 0; yy < gh; ++yy)
                            {
                                const int ty = dy + yy;
                                if (ty < 0 || ty >= h)
                                    continue;
                                for (int xx = 0; xx < gw; ++xx)
                                {
                                    const int tx = dx + xx;
                                    if (tx < 0 || tx >= w)
                                        continue;

                                    unsigned char &dstv = (*cov)[(size_t)ty * w + tx];
                                    const unsigned char srcv = tmp[(size_t)yy * gw + xx];
                                    // Max, not add: overlapping glyphs (an
                                    // italic tail, a kerned pair) would
                                    // otherwise saturate into a blob.
                                    if (srcv > dstv)
                                        dstv = srcv;
                                }
                            }
                        }
                    }

                    int advance = 0, lsb = 0;
                    stbtt_GetCodepointHMetrics(&s_font, (int)cp, &advance, &lsb);
                    x += scale * (float)advance;
                    prev = cp;
                }
            }

            void evict_oldest(Rung &rung)
            {
                std::map<std::string, Entry>::iterator victim = rung.cache.end();
                unsigned long long oldest = (unsigned long long)-1;

                for (std::map<std::string, Entry>::iterator it = rung.cache.begin();
                     it != rung.cache.end(); ++it)
                {
                    if (it->second.last_used < oldest)
                    {
                        oldest = it->second.last_used;
                        victim = it;
                    }
                }

                if (victim != rung.cache.end())
                {
                    gfx::destroy_surface(victim->second.mask);
                    rung.cache.erase(victim);
                }
            }

            const Entry *acquire(const char *utf8, FontSize size)
            {
                Rung &rung = rung_of(size);

                if (!s_ready || !utf8 || !*utf8)
                    return NULL;

                const std::string key(utf8);

                std::map<std::string, Entry>::iterator it = rung.cache.find(key);
                if (it != rung.cache.end())
                {
                    it->second.last_used = ++s_tick;
                    return &it->second;
                }

                const int width = measure_run(utf8, -1, NULL, NULL, size);
                if (width <= 0)
                    return NULL;

                // Padded: glyph ink routinely extends a little past the
                // advance width, and descenders past the line height.
                const int pad = 2;
                const int w = width + pad * 2;
                const int h = rung.height + pad * 2;

                std::vector<unsigned char> cov;
                rasterise(utf8, w, h, &cov, size);

                // White RGB with coverage in alpha — the shape blit_tinted
                // expects, so the same mask serves every theme colour.
                std::vector<unsigned char> rgba((size_t)w * (size_t)h * 4);
                for (size_t i = 0, n = cov.size(); i < n; ++i)
                {
                    rgba[i * 4 + 0] = 255;
                    rgba[i * 4 + 1] = 255;
                    rgba[i * 4 + 2] = 255;
                    rgba[i * 4 + 3] = cov[i];
                }

                gfx::Surface *mask = gfx::surface_from_rgba(&rgba[0], w, h);
                if (!mask)
                    return NULL;

                while (rung.cache.size() >= MAX_CACHED_STRINGS)
                    evict_oldest(rung);

                Entry e;
                e.mask = mask;
                e.width = width;
                e.last_used = ++s_tick;
                rung.cache[key] = e;

                return &rung.cache[key];
            }
        } // namespace

        bool font_init()
        {
            if (s_ready)
                return true;

            const std::string path = app_path(FONT_FILE);

            std::FILE *f = std::fopen(path.c_str(), "rb");
            if (!f)
                return false;

            std::fseek(f, 0, SEEK_END);
            const long size = std::ftell(f);
            std::fseek(f, 0, SEEK_SET);

            if (size <= 0)
            {
                std::fclose(f);
                return false;
            }

            s_font_data.resize((size_t)size);
            const size_t read = std::fread(&s_font_data[0], 1, (size_t)size, f);
            std::fclose(f);

            if (read != (size_t)size)
                return false;

            const int offset = stbtt_GetFontOffsetForIndex(&s_font_data[0], 0);
            if (offset < 0 || !stbtt_InitFont(&s_font, &s_font_data[0], offset))
                return false;

            int ascent = 0, descent = 0, line_gap = 0;
            stbtt_GetFontVMetrics(&s_font, &ascent, &descent, &line_gap);

            for (int i = 0; i < (int)FontSize::Count; ++i)
            {
                const int px = font_px((FontSize)i);
                Rung &rung = s_rungs[i];

                // FreeType at 72 dpi maps a point size straight to that many
                // pixels per em, which is what SDL_ttf's ptsize means in
                // font_ttf.cpp. Mapping the em the same way keeps the two
                // backends at the same nominal size on every rung.
                rung.scale = stbtt_ScaleForMappingEmToPixels(&s_font, (float)px);

                rung.ascent = (int)(rung.scale * (float)ascent + 0.5f);
                rung.height = (int)(rung.scale *
                                        (float)(ascent - descent + line_gap) +
                                    0.5f);
                if (rung.height <= 0)
                    rung.height = px;
            }

            s_ready = true;
            return true;
        }

        void font_shutdown()
        {
            for (int i = 0; i < (int)FontSize::Count; ++i)
            {
                Rung &rung = s_rungs[i];

                for (std::map<std::string, Entry>::iterator it = rung.cache.begin();
                     it != rung.cache.end(); ++it)
                {
                    gfx::destroy_surface(it->second.mask);
                }
                rung.cache.clear();
                rung.widths.clear();
            }

            s_font_data.clear();
            s_ready = false;
        }

        int font_height(FontSize size) { return rung_of(size).height; }

        int font_measure(const char *utf8, FontSize size)
        {
            Rung &rung = rung_of(size);

            if (!s_ready || !utf8 || !*utf8)
                return 0;

            const std::string key(utf8);
            std::map<std::string, int>::iterator it = rung.widths.find(key);
            if (it != rung.widths.end())
                return it->second;

            const int w = measure_run(utf8, -1, NULL, NULL, size);

            // Word wrap measures far more text than it ever draws, so this
            // cache is separate from the mask cache and is not size-limited —
            // the entries are a few bytes each.
            rung.widths[key] = w;
            return w;
        }

        int font_fit(const char *utf8, int max_w, int *out_w, FontSize size)
        {
            if (out_w)
                *out_w = 0;
            if (!s_ready || !utf8 || !*utf8 || max_w <= 0)
                return 0;

            int bytes = 0, w = 0;
            measure_run(utf8, max_w, &bytes, &w, size);

            if (out_w)
                *out_w = w;
            return bytes;
        }

        void font_draw(gfx::Surface *dst, int x, int y, gfx::Color color,
                       const char *utf8, FontSize size)
        {
            const Entry *e = acquire(utf8, size);
            if (!e)
                return;

            // The mask is padded on every side; shift back so the caller's
            // (x, y) still means the top-left of the text itself.
            gfx::blit_tinted(dst, e->mask, x - 2, y - 2, color);
        }

    } // namespace ui
} // namespace launcher
