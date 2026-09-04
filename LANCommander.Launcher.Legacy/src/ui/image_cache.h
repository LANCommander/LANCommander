#ifndef LAUNCHER_UI_IMAGE_CACHE_H
#define LAUNCHER_UI_IMAGE_CACHE_H

#include <map>
#include <string>

#include "gfx/gfx.h"

// Forward declaration — avoids pulling SDK headers into callers.
namespace lancommander
{
    class MediaClient;
}

namespace launcher
{
    class MediaPrefetch;
}

namespace launcher
{
    namespace ui
    {

        // How a decoded image is fitted to the box the caller asks for.
        //
        // Fit is CSS `object-fit: contain`: the whole image, letterboxed. Fill
        // is `object-fit: cover`: the box is filled edge to edge and the
        // overflow is cropped. Cards that use art as a BACKGROUND want Fill —
        // a 2:3 cover letterboxed into a 4:3 tile is mostly empty tile.
        //
        // Fill decodes so that both dimensions are at least the requested
        // size; the caller crops with gfx::blit_region. draw_image_cover() in
        // widgets.h does both and is what screens should reach for.
        enum class ImageFit
        {
            Fit,     // shrink to fit; never enlarge. Photos and screenshots.
            Fill,    // cover the box; the overflow is cropped. Card artwork.
            Contain  // fit the box exactly, enlarging if small. Logos.
        };

        // Downloads, decodes, and caches game media as gfx surfaces.
        // Uses LRU eviction and a per-frame decode budget to limit memory.
        class ImageCache
        {
        public:
            static const int DEFAULT_MAX_ENTRIES = 64;
            static const int MAX_DECODES_PER_FRAME = 4;

            ImageCache(lancommander::MediaClient &media, const std::string &media_dir);
            ~ImageCache();

            // Set the maximum number of cached entries.  The library screen
            // calls this each frame based on how many covers fit in the window.
            void set_capacity(int max_entries);

            // Current capacity, so a screen can raise a budget another screen
            // set without lowering one that is already generous enough.
            int capacity() const;

            // Call once per frame before any get() calls to reset the decode budget.
            void begin_frame();

            // Hand missing files to a background downloader instead of
            // fetching them inline. Optional: with no prefetch set, get()
            // falls back to downloading on the calling thread, which is what
            // it always used to do.
            void set_prefetch(MediaPrefetch *prefetch);

            // Get a media image sized against max_w x max_h, per `fit`.
            //
            // Returns NULL when the image is not available YET — queued for
            // download, or waiting for a decode slot — as well as when it
            // genuinely failed. Callers draw a placeholder either way and try
            // again next frame.
            gfx::Surface *get(const std::string &media_id, int max_w, int max_h,
                              ImageFit fit = ImageFit::Fit);

            // Release all cached surfaces.
            void clear();

        private:
            lancommander::MediaClient &m_media;
            MediaPrefetch *m_prefetch;
            std::string m_cache_dir;

            struct Entry
            {
                gfx::Surface *surf;
                unsigned long long last_access;
            };

            // Keyed on media id, requested size AND fit mode.
            //
            // One entry per id meant the same cover requested at two sizes —
            // the library grid at its responsive width and the Recently
            // Played carousel at 160x240 — thrashed: each request saw the
            // other's dimensions, threw the surface away and re-decoded, every
            // frame, forever, consuming the whole per-frame budget.
            std::map<std::string, Entry> m_cache;
            unsigned long long m_access_counter;
            int m_decodes_this_frame;
            int m_max_entries;

            std::string file_path(const std::string &media_id) const;
            static std::string cache_key(const std::string &media_id, int w, int h,
                                         ImageFit fit);
            void evict_oldest();
        };

        // Draw `media_id` so it fills `r` edge to edge, cropping whatever
        // overhangs — CSS `object-fit: cover`.
        //
        // Returns false when the image is not ready or failed, in which case
        // nothing was drawn and the caller should paint its own placeholder.
        // Lives here rather than in widgets.cpp so the widget layer does not
        // have to know what an ImageCache is.
        bool draw_image_cover(gfx::Surface *s, ImageCache &cache,
                              const gfx::Rect &r, const std::string &media_id);

    } // namespace ui
} // namespace launcher

#endif // LAUNCHER_UI_IMAGE_CACHE_H
