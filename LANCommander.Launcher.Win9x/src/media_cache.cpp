#include "media_cache.h"

#include <windows.h>
#include <direct.h>

#include <cstdio>

MediaCache::MediaCache(MediaClient* api)
    : m_api(api)
{
}

std::string MediaCache::EnsureDir()
{
    if (!m_dir.empty()) return m_dir;
    char buf[MAX_PATH];
    DWORD n = GetModuleFileNameA(NULL, buf, MAX_PATH);
    std::string base = (n == 0) ? std::string(".") : std::string(buf, n);
    size_t slash = base.find_last_of("\\/");
    if (slash != std::string::npos) base = base.substr(0, slash);
    m_dir = base + "\\media";
    _mkdir(m_dir.c_str());
    return m_dir;
}

std::string MediaCache::BuildPath(const std::string& key)
{
    return EnsureDir() + "\\" + key + ".img";
}

std::string MediaCache::GetThumbnail(const std::string& mediaId,
                                     const std::string& crc32)
{
    if (mediaId.empty()) return std::string();
    std::string key = crc32.empty() ? mediaId : crc32;
    std::string path = BuildPath(key);

    DWORD attrs = GetFileAttributesA(path.c_str());
    if (attrs != INVALID_FILE_ATTRIBUTES && !(attrs & FILE_ATTRIBUTE_DIRECTORY))
    {
        // Treat zero-byte files as misses to recover from earlier failures.
        WIN32_FILE_ATTRIBUTE_DATA info;
        if (GetFileAttributesExA(path.c_str(), GetFileExInfoStandard, &info) &&
            (info.nFileSizeLow != 0 || info.nFileSizeHigh != 0))
            return path;
        ::remove(path.c_str());
    }

    std::string err;
    if (!m_api->DownloadThumbnail(mediaId, path, &err))
        return std::string();
    return path;
}
