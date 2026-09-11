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
            // laid out against — so the Body rung causes no vertical layout
            // shift. The other rungs open at font_px() directly.

            const size_t MAX_CACHED_STRINGS = 512;

            struct Entry
            {
                gfx::Surface *mask;
                int width;
                unsigned long long last_used;
            };

            // One face and one pair of caches per rung.
            //
            // The caches could have been keyed on (string, rung) instead, but
            // a given string is almost always drawn at one rung for the life
            // of the program, so per-rung maps keep the common lookup on the
            // short key it already had.
            struct Face
            {
                TTF_Font *font;
                int height;
                std::map<std::string, Entry> cache;
                std::map<std::string, int> widths; // measure-only, much cheaper

                Face() : font(NULL), height(0) {}
            };

            Face s_faces[(int)FontSize::Count];
            unsigned long long s_tick = 0;

            Face &face_of(FontSize size)
            {
                const int i = (int)size;
                return s_faces[(i >= 0 && i < (int)FontSize::Count)
                                   ? i
                                   : (int)FontSize::Body];
            }

            void evict_oldest(Face &f)
            {
                std::map<std::string, Entry>::iterator victim = f.cache.end();
                unsigned long long oldest = (unsigned long long)-1;

                for (std::map<std::string, Entry>::iterator it = f.cache.begin();
                     it != f.cache.end(); ++it)
                {
                    if (it->second.last_used < oldest)
                    {
                        oldest = it->second.last_used;
                        victim = it;
                    }
                }

                if (victim != f.cache.end())
                {
                    gfx::destroy_surface(victim->second.mask);
                    f.cache.erase(victim);
                }
            }

            const Entry *acquire(const char *utf8, FontSize size)
            {
                Face &f = face_of(size);

                if (!f.font || !utf8 || !*utf8)
                    return NULL;

                std::string key(utf8);

                std::map<std::string, Entry>::iterator it = f.cache.find(key);
                if (it != f.cache.end())
                {
                    it->second.last_used = ++s_tick;
                    return &it->second;
                }

                // White, so colour modulation at blit time can tint it to any
                // theme colour without re-rasterising.
                SDL_Color white = { 255, 255, 255, 255 };
                SDL_Surface *rendered =
                    TTF_RenderText_Blended(f.font, utf8, 0, white);
                if (!rendered)
                    return NULL;

                const int w = rendered->w;

                gfx::Surface *mask = gfx::surface_adopt_sdl(rendered);
                if (!mask)
                    return NULL;

                while (f.cache.size() >= MAX_CACHED_STRINGS)
                    evict_oldest(f);

                Entry e;
                e.mask = mask;
                e.width = w;
                e.last_used = ++s_tick;
                f.cache[key] = e;

                return &f.cache[key];
            }
        } // namespace

        bool font_init()
        {
            if (!TTF_Init())
                return false;

            for (int i = 0; i < (int)FontSize::Count; ++i)
            {
                TTF_Font *font =
                    TTF_OpenFont(FONT_PATH, (float)font_px((FontSize)i));
                if (!font)
                {
                    font_shutdown();
                    return false;
                }

                // FreeType's default hinting is softer than the
                // ANTIALIASED_QUALITY GDI output this replaces; NORMAL keeps
                // small text crisp, which matters most at the Caption rung.
                TTF_SetFontHinting(font, TTF_HINTING_NORMAL);

                s_faces[i].font = font;
                s_faces[i].height = TTF_GetFontHeight(font);
            }

            return true;
        }

        void font_shutdown()
        {
            for (int i = 0; i < (int)FontSize::Count; ++i)
            {
                Face &f = s_faces[i];

                for (std::map<std::string, Entry>::iterator it = f.cache.begin();
                     it != f.cache.end(); ++it)
                {
                    gfx::destroy_surface(it->second.mask);
                }
                f.cache.clear();
                f.widths.clear();

                if (f.font)
                {
                    TTF_CloseFont(f.font);
                    f.font = NULL;
                }
                f.height = 0;
            }
            TTF_Quit();
        }

        int font_height(FontSize size) { return face_of(size).height; }

        int font_measure(const char *utf8, FontSize size)
        {
            Face &f = face_of(size);

            if (!f.font || !utf8 || !*utf8)
                return 0;

            std::string key(utf8);
            std::map<std::string, int>::iterator it = f.widths.find(key);
            if (it != f.widths.end())
                return it->second;

            int w = 0, h = 0;
            if (!TTF_GetStringSize(f.font, utf8, 0, &w, &h))
                return 0;

            // Measurement is used heavily by word wrap on text that is never
            // drawn, so this cache is kept separate from the mask cache and
            // is not size-limited — the entries are a few bytes each.
            f.widths[key] = w;
            return w;
        }

        int font_fit(const char *utf8, int max_w, int *out_w, FontSize size)
        {
            Face &f = face_of(size);

            if (out_w)
                *out_w = 0;
            if (!f.font || !utf8 || !*utf8 || max_w <= 0)
                return 0;

            int measured_w = 0;
            size_t measured_len = 0;

            if (!TTF_MeasureString(f.font, utf8, 0, max_w,
                                   &measured_w, &measured_len))
                return 0;

            if (out_w)
                *out_w = measured_w;
            return (int)measured_len;
        }

        void font_draw(gfx::Surface *dst, int x, int y, gfx::Color color,
                       const char *utf8, FontSize size)
        {
            const Entry *e = acquire(utf8, size);
            if (!e)
                return;
            gfx::blit_tinted(dst, e->mask, x, y, color);
        }

    } // namespace ui
} // namespace launcher
