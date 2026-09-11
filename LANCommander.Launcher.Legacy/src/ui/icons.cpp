#include "ui/icons.h"
#include "ui/image_decoder.h"

#include <map>
#include <string>

namespace launcher
{
    namespace ui
    {

        namespace
        {
            const char *asset_name(Icon icon)
            {
                switch (icon)
                {
                case Icon::ArrowLeft:  return "icons/arrow-left.png";
                case Icon::CaretLeft:  return "icons/caret-left.png";
                case Icon::CaretRight: return "icons/caret-right.png";
                case Icon::CaretDown:  return "icons/caret-down.png";
                case Icon::CaretUp:    return "icons/caret-up.png";
                case Icon::Refresh:    return "icons/refresh.png";
                case Icon::Library:    return "icons/library.png";
                case Icon::Depot:      return "icons/depot.png";
                case Icon::Download:   return "icons/download.png";
                case Icon::Play:       return "icons/play.png";
                case Icon::Stop:       return "icons/stop.png";
                case Icon::Plus:       return "icons/plus.png";
                case Icon::Close:      return "icons/close.png";
                case Icon::Minimize:   return "icons/minimize.png";
                case Icon::Maximize:   return "icons/maximize.png";
                case Icon::User:       return "icons/user.png";
                case Icon::Search:     return "icons/search.png";
                case Icon::Settings:   return "icons/settings.png";
                case Icon::Folder:     return "icons/folder.png";
                case Icon::Trash:      return "icons/trash.png";
                case Icon::Install:    return "icons/install.png";
                case Icon::Check:      return "icons/check.png";
                default:               return NULL;
                }
            }

            // Keyed on icon AND size: the same caret is drawn at 12px in a
            // split button and 20px in a carousel gutter, and decoding one
            // from the other would thrash exactly the way the image cache
            // used to before its key grew a size.
            struct Key
            {
                int icon;
                int size;

                bool operator<(const Key &o) const
                {
                    if (icon != o.icon) return icon < o.icon;
                    return size < o.size;
                }
            };

            // NULL means "tried and failed"; the entry is kept so a missing
            // file is not re-read from disk on every frame.
            std::map<Key, gfx::Surface *> s_cache;

            // Bring a downscaled mask back up to a full-strength stroke.
            //
            // The assets are stored once at 64px with the stroke baked in at
            // Phosphor's own relative weight, so it resolves to exactly 1px
            // at a 16px icon. Below that it goes sub-pixel and the resampler
            // spreads it into a grey smear — a 10px caret came out at about
            // 40% coverage, which on the title bar read as disabled.
            //
            // Avalonia does not have this problem because its StrokeThickness
            // is 1 *device* pixel at every icon size, so its small icons are
            // as solid as its large ones. Scaling the coverage so the darkest
            // pixel lands back at opaque reproduces that: the stroke returns
            // to full strength and its antialiased flanks come up with it,
            // which is the same shape a 1px pen would have drawn.
            //
            // Gain is capped so an asset that is genuinely faint — or an
            // empty one — is not amplified into noise.
            void restore_stroke_weight(DecodedImage *img)
            {
                if (!img || !img->pixels)
                    return;

                const size_t count = (size_t)img->width * (size_t)img->height;

                unsigned char peak = 0;
                for (size_t i = 0; i < count; ++i)
                {
                    const unsigned char a = img->pixels[i * 4 + 3];
                    if (a > peak)
                        peak = a;
                }

                // Already solid, or nothing to work with.
                if (peak >= 250 || peak < 48)
                    return;

                for (size_t i = 0; i < count; ++i)
                {
                    const int a = img->pixels[i * 4 + 3] * 255 / peak;
                    img->pixels[i * 4 + 3] = (unsigned char)(a > 255 ? 255 : a);
                }
            }

            gfx::Surface *mask_for(Icon icon, int size)
            {
                const char *name = asset_name(icon);
                if (!name || size <= 0)
                    return NULL;

                Key key;
                key.icon = (int)icon;
                key.size = size;

                std::map<Key, gfx::Surface *>::iterator it = s_cache.find(key);
                if (it != s_cache.end())
                    return it->second;

                gfx::Surface *surf = NULL;

                DecodedImage img;
                if (decode_image_asset(name, size, size, &img))
                {
                    restore_stroke_weight(&img);
                    surf = gfx::surface_from_rgba(img.pixels, img.width, img.height);
                    free_decoded_image(&img);
                }

                s_cache[key] = surf;
                return surf;
            }
        } // namespace

        void icons_shutdown()
        {
            for (std::map<Key, gfx::Surface *>::iterator it = s_cache.begin();
                 it != s_cache.end(); ++it)
            {
                if (it->second)
                    gfx::destroy_surface(it->second);
            }
            s_cache.clear();
        }

        bool icon_available(Icon icon)
        {
            return mask_for(icon, ICON_MD) != NULL;
        }

        void draw_icon(gfx::Surface *s, int x, int y, int size,
                       gfx::Color color, Icon icon)
        {
            gfx::Surface *mask = mask_for(icon, size);
            if (!mask)
                return;

            // The decode fits WITHIN size x size and the source is square, so
            // in practice this is exact; the centring is here for the case
            // where a future icon is not square.
            const int mw = gfx::surface_width(mask);
            const int mh = gfx::surface_height(mask);

            gfx::blit_tinted(s, mask, x + (size - mw) / 2, y + (size - mh) / 2, color);
        }

        void draw_icon_centered(gfx::Surface *s, const gfx::Rect &r, int size,
                                gfx::Color color, Icon icon)
        {
            draw_icon(s, r.x + (r.w - size) / 2, r.y + (r.h - size) / 2,
                      size, color, icon);
        }

    } // namespace ui
} // namespace launcher
