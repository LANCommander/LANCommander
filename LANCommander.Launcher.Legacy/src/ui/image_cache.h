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
        class ImageCache
        {
        public:
            explicit ImageCache(lancommander::MediaClient &media);
            ~ImageCache();

            // Get (or download + decode on first request) a cover image scaled to
            // fit within max_w x max_h.  Returns NULL if the image could not be
            // loaded (missing media ID, download failure, unsupported format, etc.).
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
            };
            std::map<std::string, Entry> m_cache;

            std::string file_path(const std::string &media_id) const;
        };

    } // namespace ui
} // namespace launcher

#endif // LAUNCHER_UI_IMAGE_CACHE_H
