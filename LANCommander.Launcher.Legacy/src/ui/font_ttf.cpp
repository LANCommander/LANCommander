// font_ttf.cpp — text via SDL_ttf + FreeType against a bundled TTF.
//
// Two things this fixes versus the GDI renderer it replaces:
//
//   1. The font ships with the launcher. gdi_font.cpp asked the host for
//      Inter, then Segoe UI, then Arial — so the design only rendered as
//      intended on machines that happened to have Inter installed, and on
//      Windows 95/98 none of the three exist.
//
//   2. Strings are cached. The old path built a DIB section and a device
//      context, rendered, blended per pixel, then destroyed all of it — for
//      every string, every frame. The UI is immediate-mode and redraws the
//      same few dozen labels continuously, so the hit rate after the first
//      frame is essentially 100%.
//
// The cache is keyed on the string alone, not (string, colour). Masks are
// rendered white and tinted at blit time via colour modulation, so the same
// label drawn in three theme colours costs one entry rather than three.

#include "ui/font.h"

#include <SDL3/SDL.h>
#include <SDL3_ttf/SDL_ttf.h>

#include <map>
#include <string>

namespace launcher
{
    namespace gfx
    {
        Surface *surface_adopt_sdl(SDL_Surface *s); // gfx_sdl.cpp
    }

    namespace ui
    {

        namespace
        {
            const char *FONT_PATH = "assets/fonts/Inter-Regular.ttf";

            // Inter at ptsize 13 reports a line height of 16px, matching what
            // GDI's CreateFontA(-13, ...) reported for the faces this UI was
            // laid out against — so no vertical layout shift.
            const float FONT_PTSIZE = 13.0f;

            const size_t MAX_CACHED_STRINGS = 512;

            TTF_Font *s_font = NULL;
            int s_height = 0;

            struct Entry
            {
                gfx::Surface *mask;
                int width;
                unsigned long long last_used;
            };

            std::map<std::string, Entry> s_cache;
            std::map<std::string, int> s_widths; // measure-only, much cheaper
            unsigned long long s_tick = 0;

            void evict_oldest()
            {
                std::map<std::string, Entry>::iterator victim = s_cache.end();
                unsigned long long oldest = (unsigned long long)-1;

                for (std::map<std::string, Entry>::iterator it = s_cache.begin();
                     it != s_cache.end(); ++it)
                {
                    if (it->second.last_used < oldest)
                    {
                        oldest = it->second.last_used;
                        victim = it;
                    }
                }

                if (victim != s_cache.end())
                {
                    gfx::destroy_surface(victim->second.mask);
                    s_cache.erase(victim);
                }
            }

            const Entry *acquire(const char *utf8)
            {
                if (!s_font || !utf8 || !*utf8)
                    return NULL;

                std::string key(utf8);

                std::map<std::string, Entry>::iterator it = s_cache.find(key);
                if (it != s_cache.end())
                {
                    it->second.last_used = ++s_tick;
                    return &it->second;
                }

                // White, so colour modulation at blit time can tint it to any
                // theme colour without re-rasterising.
                SDL_Color white = { 255, 255, 255, 255 };
                SDL_Surface *rendered =
                    TTF_RenderText_Blended(s_font, utf8, 0, white);
                if (!rendered)
                    return NULL;

                const int w = rendered->w;

                gfx::Surface *mask = gfx::surface_adopt_sdl(rendered);
                if (!mask)
                    return NULL;

                while (s_cache.size() >= MAX_CACHED_STRINGS)
                    evict_oldest();

                Entry e;
                e.mask = mask;
                e.width = w;
                e.last_used = ++s_tick;
                s_cache[key] = e;

                return &s_cache[key];
            }
        } // namespace

        bool font_init(int px_size)
        {
            (void)px_size; // FONT_PTSIZE is calibrated, not derived

            if (!TTF_Init())
                return false;

            s_font = TTF_OpenFont(FONT_PATH, FONT_PTSIZE);
            if (!s_font)
            {
                TTF_Quit();
                return false;
            }

            // FreeType's default hinting is softer than the ANTIALIASED_QUALITY
            // GDI output this replaces; NORMAL keeps small text crisp.
            TTF_SetFontHinting(s_font, TTF_HINTING_NORMAL);

            s_height = TTF_GetFontHeight(s_font);
            return true;
        }

        void font_shutdown()
        {
            for (std::map<std::string, Entry>::iterator it = s_cache.begin();
                 it != s_cache.end(); ++it)
            {
                gfx::destroy_surface(it->second.mask);
            }
            s_cache.clear();
            s_widths.clear();

            if (s_font)
            {
                TTF_CloseFont(s_font);
                s_font = NULL;
            }
            TTF_Quit();
        }

        int font_height() { return s_height; }

        int font_measure(const char *utf8)
        {
            if (!s_font || !utf8 || !*utf8)
                return 0;

            std::string key(utf8);
            std::map<std::string, int>::iterator it = s_widths.find(key);
            if (it != s_widths.end())
                return it->second;

            int w = 0, h = 0;
            if (!TTF_GetStringSize(s_font, utf8, 0, &w, &h))
                return 0;

            // Measurement is used heavily by word wrap on text that is never
            // drawn, so this cache is kept separate from the mask cache and
            // is not size-limited — the entries are a few bytes each.
            s_widths[key] = w;
            return w;
        }

        int font_fit(const char *utf8, int max_w, int *out_w)
        {
            if (out_w)
                *out_w = 0;
            if (!s_font || !utf8 || !*utf8 || max_w <= 0)
                return 0;

            int measured_w = 0;
            size_t measured_len = 0;

            if (!TTF_MeasureString(s_font, utf8, 0, max_w,
                                   &measured_w, &measured_len))
                return 0;

            if (out_w)
                *out_w = measured_w;
            return (int)measured_len;
        }

        void font_draw(gfx::Surface *dst, int x, int y, gfx::Color color,
                       const char *utf8)
        {
            const Entry *e = acquire(utf8);
            if (!e)
                return;
            gfx::blit_tinted(dst, e->mask, x, y, color);
        }

    } // namespace ui
} // namespace launcher
