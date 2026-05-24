// Allegro 4 must come first.
#include <allegro.h>
#ifdef ALLEGRO_WINDOWS
#include <winalleg.h>
#endif

#include "ui/image_cache.h"
#include "ui/image_decoder.h"

#include <lancommander/clients/media_client.h>

#include <windows.h>
#include <cstdio>

namespace launcher
{
    namespace ui
    {

        ImageCache::ImageCache(lancommander::MediaClient &media, const std::string &media_dir)
            : m_media(media), m_access_counter(0), m_decodes_this_frame(0),
              m_max_entries(DEFAULT_MAX_ENTRIES)
        {
            m_cache_dir = media_dir;
            CreateDirectoryA(m_cache_dir.c_str(), NULL);
        }

        ImageCache::~ImageCache()
        {
            clear();
        }

        void ImageCache::clear()
        {
            for (std::map<std::string, Entry>::iterator it = m_cache.begin();
                 it != m_cache.end(); ++it)
            {
                if (it->second.bmp)
                    destroy_bitmap(it->second.bmp);
            }
            m_cache.clear();
        }

        std::string ImageCache::file_path(const std::string &media_id) const
        {
            return m_cache_dir + "\\" + media_id;
        }

        void ImageCache::set_capacity(int max_entries)
        {
            if (max_entries < 1) max_entries = 1;
            m_max_entries = max_entries;
        }

        void ImageCache::begin_frame()
        {
            m_decodes_this_frame = 0;
        }

        void ImageCache::evict_oldest()
        {
            // Find the entry with the lowest last_access that has a bitmap
            // (NULL entries are tiny — prefer evicting real bitmaps first).
            std::map<std::string, Entry>::iterator victim = m_cache.end();
            unsigned long long oldest = (unsigned long long)-1;

            for (std::map<std::string, Entry>::iterator it = m_cache.begin();
                 it != m_cache.end(); ++it)
            {
                if (it->second.bmp && it->second.last_access < oldest)
                {
                    oldest = it->second.last_access;
                    victim = it;
                }
            }

            // If no bitmap entries found, evict any NULL entry.
            if (victim == m_cache.end())
            {
                for (std::map<std::string, Entry>::iterator it = m_cache.begin();
                     it != m_cache.end(); ++it)
                {
                    if (it->second.last_access < oldest)
                    {
                        oldest = it->second.last_access;
                        victim = it;
                    }
                }
            }

            if (victim != m_cache.end())
            {
                if (victim->second.bmp)
                    destroy_bitmap(victim->second.bmp);
                m_cache.erase(victim);
            }
        }

        BITMAP *ImageCache::get(const std::string &media_id, int max_w, int max_h)
        {
            if (media_id.empty())
                return NULL;

            ++m_access_counter;

            // Check in-memory cache first.  If the requested size differs
            // from the cached size, return the old bitmap immediately (the
            // caller centers it, so a few pixels off is fine) and queue a
            // re-decode for a future frame.
            std::map<std::string, Entry>::iterator it = m_cache.find(media_id);
            if (it != m_cache.end())
            {
                it->second.last_access = m_access_counter;

                if (it->second.max_w == max_w && it->second.max_h == max_h)
                    return it->second.bmp;

                // Size changed — return the stale bitmap while we wait for
                // a decode slot.  Only re-decode when budget allows.
                if (m_decodes_this_frame >= MAX_DECODES_PER_FRAME)
                    return it->second.bmp;

                // Budget available — discard old bitmap and fall through
                // to re-decode at the new size.
                if (it->second.bmp)
                    destroy_bitmap(it->second.bmp);
                m_cache.erase(it);
            }

            // Per-frame decode budget — show placeholder until next frame.
            if (m_decodes_this_frame >= MAX_DECODES_PER_FRAME)
                return NULL;

            // Ensure the file exists on disk (download if needed).
            std::string path = file_path(media_id);
            DWORD attr = GetFileAttributesA(path.c_str());
            if (attr == INVALID_FILE_ATTRIBUTES)
            {
                auto result = m_media.download(media_id, path);
                if (!result || !result.value)
                {
                    // Cache a NULL so we don't retry every frame.
                    Entry e;
                    e.bmp = NULL;
                    e.max_w = max_w;
                    e.max_h = max_h;
                    e.last_access = m_access_counter;
                    m_cache[media_id] = e;
                    return NULL;
                }
            }

            // Decode the image file into raw pixels.
            DecodedImage img;
            if (!decode_image_file(path.c_str(), max_w, max_h, &img))
            {
                Entry e;
                e.bmp = NULL;
                e.max_w = max_w;
                e.max_h = max_h;
                e.last_access = m_access_counter;
                m_cache[media_id] = e;
                return NULL;
            }

            ++m_decodes_this_frame;

            // Evict oldest entries if at capacity.
            while ((int)m_cache.size() >= m_max_entries)
                evict_oldest();

            // Convert raw RGBA pixels to an Allegro BITMAP.
            // Use create_bitmap_ex at 32-bit so the alpha channel is preserved
            // even if the display is 16-bit.
            BITMAP *bmp = create_bitmap_ex(32, img.width, img.height);
            if (bmp)
            {
                for (int y = 0; y < img.height; y++)
                {
                    const unsigned char *row = img.pixels + y * img.width * 4;
                    for (int x = 0; x < img.width; x++)
                    {
                        int r = row[x * 4 + 0];
                        int g = row[x * 4 + 1];
                        int b = row[x * 4 + 2];
                        int a = row[x * 4 + 3];
                        putpixel(bmp, x, y, makeacol32(r, g, b, a));
                    }
                }
            }
            free_decoded_image(&img);

            Entry e;
            e.bmp = bmp;
            e.max_w = max_w;
            e.max_h = max_h;
            e.last_access = m_access_counter;
            m_cache[media_id] = e;
            return bmp;
        }

    } // namespace ui
} // namespace launcher
