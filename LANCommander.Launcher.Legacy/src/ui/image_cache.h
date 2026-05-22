#ifndef LAUNCHER_UI_IMAGE_CACHE_H
#define LAUNCHER_UI_IMAGE_CACHE_H

#include <map>
#include <string>

// Forward declarations — avoids pulling Allegro or SDK headers into callers.
struct BITMAP;
namespace lancommander
{
    class MediaClient;
}

namespace launcher
{
    namespace ui
    {

        // Downloads, decodes, and caches game media as Allegro BITMAPs.
        // Uses LRU eviction and a per-frame decode budget to limit memory.
        class ImageCache
        {
        public:
            static const int MAX_ENTRIES = 30;
            static const int MAX_DECODES_PER_FRAME = 2;

            explicit ImageCache(lancommander::MediaClient &media);
            ~ImageCache();

            // Call once per frame before any get() calls to reset the decode budget.
            void begin_frame();

            // Get (or download + decode on first request) a cover image scaled to
            // fit within max_w x max_h.  Returns NULL if the image could not be
            // loaded, the decode budget is exhausted, or the media ID is empty.
            BITMAP *get(const std::string &media_id, int max_w, int max_h);

            // Release all cached bitmaps.
            void clear();

        private:
            lancommander::MediaClient &m_media;
            std::string m_cache_dir;

            struct Entry
            {
                BITMAP *bmp;
                int max_w;
                int max_h;
                unsigned long long last_access;
            };
            std::map<std::string, Entry> m_cache;
            unsigned long long m_access_counter;
            int m_decodes_this_frame;

            std::string file_path(const std::string &media_id) const;
            void evict_oldest();
        };

    } // namespace ui
} // namespace launcher

#endif // LAUNCHER_UI_IMAGE_CACHE_H
