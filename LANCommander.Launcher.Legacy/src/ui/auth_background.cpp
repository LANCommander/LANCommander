#include "ui/auth_background.h"
#include "ui/image_decoder.h"

#include <cstdlib>
#include <ctime>

namespace launcher
{
    namespace ui
    {

        namespace
        {
            gfx::Surface *s_bg = NULL;
            bool s_loaded = false;

            // The background scaled to the screen with the scrim already
            // blended in, ready to be blitted straight out.
            //
            // Without this the login screens paid an aspect-fill scale *and*
            // a full-screen alpha blend on every single frame, for a picture
            // that only changes when the window resizes or a new one is
            // picked. On DOS that was 291 ms of a 327 ms frame -- about 3 FPS
            // and a mouse pointer that lurched. It is wasted work on every
            // backend; DOS is just where it stopped being survivable.
            gfx::Surface *s_composed = NULL;
            int s_composed_w = 0;
            int s_composed_h = 0;

            const char *const BG_ASSET_NAMES[] = {
                "backgrounds/aoe2.jpg",
                "backgrounds/bfme2.jpg",
                "backgrounds/css.jpg",
                "backgrounds/ns2.jpg",
                "backgrounds/soldat2.jpg",
                "backgrounds/ut2004.jpg",
            };
            const int BG_COUNT = sizeof(BG_ASSET_NAMES) / sizeof(BG_ASSET_NAMES[0]);

            gfx::Surface *load_background(int max_w, int max_h)
            {
                static bool seeded = false;
                if (!seeded)
                {
                    srand((unsigned)time(NULL));
                    seeded = true;
                }

                const int idx = rand() % BG_COUNT;

                DecodedImage img = {};
                if (!decode_image_asset(BG_ASSET_NAMES[idx], max_w, max_h, &img))
                    return NULL;

                gfx::Surface *s = gfx::surface_from_rgba(img.pixels, img.width, img.height);
                free_decoded_image(&img);
                return s;
            }

            // Aspect-fill the loaded background across `dst` and lay the
            // scrim over it. This is what used to run every frame; it now
            // runs once per screen size, into the cache.
            void draw_into(gfx::Surface *dst, int sw, int sh)
            {
                // Cropping the overhanging axis rather than letterboxing or
                // squashing.
                int src_x = 0, src_y = 0;
                int src_w = gfx::surface_width(s_bg);
                int src_h = gfx::surface_height(s_bg);

                if (src_w * sh > src_h * sw)
                {
                    const int scaled_w = src_h * sw / sh;
                    src_x = (src_w - scaled_w) / 2;
                    src_w = scaled_w;
                }
                else
                {
                    const int scaled_h = src_w * sh / sw;
                    src_y = (src_h - scaled_h) / 2;
                    src_h = scaled_h;
                }

                gfx::blit_scaled(dst, s_bg,
                                 gfx::rect(src_x, src_y, src_w, src_h),
                                 gfx::rect(0, 0, sw, sh));

                gfx::fill_rect_alpha(dst, gfx::rect(0, 0, sw, sh),
                                     gfx::rgba(0, 0, 0, 140));
            }
        } // namespace

        void auth_background_reset()
        {
            if (s_bg)
                gfx::destroy_surface(s_bg);
            s_bg = NULL;
            s_loaded = false;

            if (s_composed)
                gfx::destroy_surface(s_composed);
            s_composed = NULL;
            s_composed_w = 0;
            s_composed_h = 0;
        }

        namespace
        {
            // Builds s_composed for the current screen size. Returns false if
            // the surface could not be allocated, in which case the caller
            // falls back to compositing straight into the destination.
            bool compose(int sw, int sh)
            {
                if (s_composed && s_composed_w == sw && s_composed_h == sh)
                    return true;

                if (s_composed)
                    gfx::destroy_surface(s_composed);

                s_composed = gfx::create_surface(sw, sh);
                s_composed_w = sw;
                s_composed_h = sh;

                if (!s_composed)
                {
                    s_composed_w = 0;
                    s_composed_h = 0;
                    return false;
                }

                draw_into(s_composed, sw, sh);
                return true;
            }
        } // namespace

        void auth_background_draw(gfx::Surface *s, int sw, int sh)
        {
            if (!s_loaded)
            {
                s_bg = load_background(sw, sh);
                s_loaded = true;
            }

            if (!s_bg)
                return;

            if (compose(sw, sh))
            {
                // Opaque: the scrim is already blended in, so this is a
                // straight copy rather than a blend.
                gfx::blit(s, s_composed, 0, 0);
                return;
            }

            // Out of memory for the cache. Correct, just slow.
            draw_into(s, sw, sh);
        }


        // ---------------------------------------------------------------
        // Wordmark
        // ---------------------------------------------------------------

        namespace
        {
            // Cached on the width it was decoded for. The two auth cards are
            // different widths, so a single cached surface would be rescaled
            // every time the user moved between them.
            gfx::Surface *s_logo = NULL;
            int s_logo_w = 0;

            gfx::Surface *logo_surface(int max_w)
            {
                if (max_w <= 0)
                    return NULL;

                if (s_logo && s_logo_w == max_w)
                    return s_logo;

                if (s_logo)
                {
                    gfx::destroy_surface(s_logo);
                    s_logo = NULL;
                }

                s_logo_w = max_w;

                // Height is left unbounded — the asset is much wider than it
                // is tall, so width is always the binding constraint and a
                // height cap would only ever letterbox it by a pixel.
                DecodedImage img;
                if (decode_image_asset("logo.png", max_w, max_w, &img))
                {
                    s_logo = gfx::surface_from_rgba(img.pixels, img.width, img.height);
                    free_decoded_image(&img);
                }

                return s_logo;
            }
        } // namespace

        int auth_logo_height(int max_w)
        {
            gfx::Surface *logo = logo_surface(max_w);
            return logo ? gfx::surface_height(logo) : 0;
        }

        int auth_logo_draw(gfx::Surface *s, int cx, int y, int max_w)
        {
            gfx::Surface *logo = logo_surface(max_w);
            if (!logo)
                return 0;

            const int lw = gfx::surface_width(logo);

            // Alpha, not a plain blit: the mark is a pair of skewed slabs, so
            // the corners of its box are transparent and a plain blit would
            // punch black wedges into the card.
            gfx::blit_alpha(s, logo, cx - lw / 2, y);

            return gfx::surface_height(logo);
        }

    } // namespace ui
} // namespace launcher
