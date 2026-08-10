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
                gfx::destroy_surface(it->second.surf);
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
            // Find the entry with the lowest last_access that has a surface
            // (NULL entries are tiny — prefer evicting real surfaces first).
            std::map<std::string, Entry>::iterator victim = m_cache.end();
            unsigned long long oldest = (unsigned long long)-1;

            for (std::map<std::string, Entry>::iterator it = m_cache.begin();
                 it != m_cache.end(); ++it)
            {
                if (it->second.surf && it->second.last_access < oldest)
                {
                    oldest = it->second.last_access;
                    victim = it;
                }
            }

            // If no surface entries found, evict any NULL entry.
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
                gfx::destroy_surface(victim->second.surf);
                m_cache.erase(victim);
            }
        }

        gfx::Surface *ImageCache::get(const std::string &media_id, int max_w, int max_h)
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
                    return it->second.surf;

                // Size changed — return the stale surface while we wait for
                // a decode slot.  Only re-decode when budget allows.
                if (m_decodes_this_frame >= MAX_DECODES_PER_FRAME)
                    return it->second.surf;

                // Budget available — discard old surface and fall through
                // to re-decode at the new size.
                gfx::destroy_surface(it->second.surf);
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
                    e.surf = NULL;
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
                e.surf = NULL;
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

            // Surfaces are always 32-bit, so alpha survives even on a
            // 16-bit display.
            gfx::Surface *surf = gfx::surface_from_rgba(img.pixels, img.width, img.height);
            free_decoded_image(&img);

            Entry e;
            e.surf = surf;
            e.max_w = max_w;
            e.max_h = max_h;
            e.last_access = m_access_counter;
            m_cache[media_id] = e;
            return surf;
        }

    } // namespace ui
} // namespace launcher
