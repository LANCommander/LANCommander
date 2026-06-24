#ifndef LANCOMMANDER_WIN9X_MEDIA_CACHE_H
#define LANCOMMANDER_WIN9X_MEDIA_CACHE_H

#include <string>

#include "media_client.h"

class MediaCache
{
public:
    explicit MediaCache(MediaClient* api);

    // Returns the local path for the given media id, downloading via the API
    // if not already cached. Key prefers crc32 (stable across re-uploads); if
    // empty, falls back to the mediaId. Returns "" on failure.
    std::string GetThumbnail(const std::string& mediaId,
                             const std::string& crc32);

private:
    std::string EnsureDir();
    std::string BuildPath(const std::string& key);

    MediaClient* m_api;
    std::string  m_dir;
};

#endif
