#include "ui/image_cache.h"
#include "ui/image_decoder.h"
#include "app/media_prefetch.h"

#include <lancommander/clients/media_client.h>

#include <windows.h>
#include <cstdio>

namespace launcher
{
    namespace ui
    {

        ImageCache::ImageCache(lancommander::MediaClient &media, const std::string &media_dir)
            : m_media(media), m_prefetch(NULL), m_access_counter(0),
              m_decodes_this_frame(0), m_max_entries(DEFAULT_MAX_ENTRIES)
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

        namespace
        {
            // True when the path names a file with at least one byte in it.
            // An empty file is treated as absent: see ImageCache::get.
            bool file_has_content(const std::string &path)
            {
                // FindFirstFile rather than GetFileAttributesEx, which is not
                // on Windows 95.
                WIN32_FIND_DATAA find;
                HANDLE h = FindFirstFileA(path.c_str(), &find);
                if (h == INVALID_HANDLE_VALUE)
                    return false;
                FindClose(h);

                if (find.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY)
                    return false;

                if (find.nFileSizeHigh == 0 && find.nFileSizeLow == 0)
                {
                    DeleteFileA(path.c_str());
                    return false;
                }
                return true;
            }
        } // namespace

        std::string ImageCache::file_path(const std::string &media_id) const
        {
            return m_cache_dir + "\\" + media_id;
        }

        void ImageCache::set_prefetch(MediaPrefetch *prefetch)
        {
            m_prefetch = prefetch;
        }

        std::string ImageCache::cache_key(const std::string &media_id, int w, int h,
                                          ImageFit fit)
        {
            char dims[40];
            std::sprintf(dims, ":%dx%d:%d", w, h, (int)fit);
            return media_id + dims;
        }

        void ImageCache::set_capacity(int max_entries)
        {
            if (max_entries < 1) max_entries = 1;
            m_max_entries = max_entries;
        }

        int ImageCache::capacity() const
        {
            return m_max_entries;
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

        gfx::Surface *ImageCache::get(const std::string &media_id, int max_w, int max_h,
                                      ImageFit fit)
        {
            if (media_id.empty())
                return NULL;

            ++m_access_counter;

            // Size and fit are part of the key, so a hit is always the right
            // shape and there is no re-decode path to fall into.
            const std::string key = cache_key(media_id, max_w, max_h, fit);

            std::map<std::string, Entry>::iterator it = m_cache.find(key);
            if (it != m_cache.end())
            {
                it->second.last_access = m_access_counter;
                return it->second.surf;
            }

            // Not on disk yet.
            //
            // "On disk" means a file with bytes in it. The prefetch worker
            // downloads to a .part name and moves the result into place, so a
            // non-empty file here is a complete one; a zero-length file is a
            // leftover from before that was true and is cleared out rather
            // than decoded, failed on, and cached as a permanent miss.
            const std::string path = file_path(media_id);
            const bool on_disk = file_has_content(path);

            if (!on_disk)
            {
                if (m_prefetch)
                {
                    // Hand it to the worker and give up on this frame.
                    //
                    // Deliberately NOT cached as a miss: the file is expected
                    // to appear shortly, and caching NULL here would poison
                    // the entry so the image never rendered no matter how
                    // many times it was requested.
                    m_prefetch->request(media_id);
                    return NULL;
                }

                // No prefetch configured: fall back to the old inline
                // download, which blocks the caller.
                if (m_decodes_this_frame >= MAX_DECODES_PER_FRAME)
                    return NULL;

                auto result = m_media.download(media_id, path);
                if (!result || !result.value)
                {
                    // A genuine failure IS cached, so a missing media id is
                    // not retried on every frame.
                    Entry e;
                    e.surf = NULL;
                    e.last_access = m_access_counter;
                    m_cache[key] = e;
                    return NULL;
                }
            }

            // The file exists; decoding is what costs, so it stays budgeted.
            if (m_decodes_this_frame >= MAX_DECODES_PER_FRAME)
                return NULL;

            DecodedImage img;
            bool decoded = false;
            switch (fit)
            {
            case ImageFit::Fill:
                decoded = decode_image_file_fill(path.c_str(), max_w, max_h, &img);
                break;
            case ImageFit::Contain:
                decoded = decode_image_file_contain(path.c_str(), max_w, max_h, &img);
                break;
            default:
                decoded = decode_image_file(path.c_str(), max_w, max_h, &img);
                break;
            }

            if (!decoded)
            {
                // A file that is still being written is not a failure. The
                // worker may have replaced it between the size check above and
                // this decode; try again next frame rather than caching a miss
                // that would never be revisited.
                if (m_prefetch && m_prefetch->is_pending(media_id))
                    return NULL;

                Entry e;
                e.surf = NULL;
                e.last_access = m_access_counter;
                m_cache[key] = e;
                return NULL;
            }

            ++m_decodes_this_frame;

            while ((int)m_cache.size() >= m_max_entries)
                evict_oldest();

            // Surfaces are always 32-bit, so alpha survives even on a
            // 16-bit display.
            gfx::Surface *surf = gfx::surface_from_rgba(img.pixels, img.width, img.height);
            free_decoded_image(&img);

            Entry e;
            e.surf = surf;
            e.last_access = m_access_counter;
            m_cache[key] = e;
            return surf;
        }

        bool draw_image_cover(gfx::Surface *s, ImageCache &cache,
                              const gfx::Rect &r, const std::string &media_id)
        {
            if (media_id.empty() || r.w <= 0 || r.h <= 0)
                return false;

            gfx::Surface *img = cache.get(media_id, r.w, r.h, ImageFit::Fill);
            if (!img)
                return false;

            const int iw = gfx::surface_width(img);
            const int ih = gfx::surface_height(img);

            // The decode covers the box, so the crop is the centre r.w x r.h
            // of it. Clamped anyway: an upscale limit inside the decoder would
            // otherwise turn into an out-of-bounds source rect here.
            int cw = r.w < iw ? r.w : iw;
            int ch = r.h < ih ? r.h : ih;

            const gfx::Rect src = gfx::rect((iw - cw) / 2, (ih - ch) / 2, cw, ch);

            gfx::blit_region(s, img, src,
                             r.x + (r.w - cw) / 2,
                             r.y + (r.h - ch) / 2);
            return true;
        }

    } // namespace ui
} // namespace launcher
