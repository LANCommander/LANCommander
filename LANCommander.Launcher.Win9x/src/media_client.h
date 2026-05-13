#ifndef LANCOMMANDER_WIN9X_MEDIA_CLIENT_H
#define LANCOMMANDER_WIN9X_MEDIA_CLIENT_H

#include <string>
#include <vector>

#include "http_client.h"

struct MediaRef
{
    std::string id;
    std::string type;  // "Cover", "Background", "Screenshot", "Logo", ...
    std::string crc32;
};

// Mirrors LANCommander.SDK.Clients.MediaClient. We also fold in a /api/Games
// detail-fetch helper used for the screenshots/lightbox flow, since the media
// list is keyed off the game payload — same pattern Avalonia uses where
// MediaService consumes both endpoints.
class MediaClient
{
public:
    explicit MediaClient(HttpClient& http) : m_http(http) {}

    bool DownloadThumbnail(const std::string& mediaId,
                           const std::string& destPath,
                           std::string* errorOut);

    bool GetMediaForGame(const std::string& gameId,
                         std::vector<MediaRef>* out, std::string* errorOut);

private:
    HttpClient& m_http;
};

#endif
