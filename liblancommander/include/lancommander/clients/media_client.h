#ifndef LANCOMMANDER_CLIENTS_MEDIA_CLIENT_H
#define LANCOMMANDER_CLIENTS_MEDIA_CLIENT_H

#include <string>
#include <vector>

#include "../http/http_client.h"
#include "../models/game.h"
#include "../types.h"

namespace lancommander {

class MediaClient {
public:
    explicit MediaClient(IHttpClient& http);

    Result<bool> download_thumbnail(const std::string& media_id, const std::string& dest_path);
    Result<bool> download(const std::string& media_id, const std::string& dest_path,
                          DownloadProgressFn progress = nullptr);
    Result<std::vector<MediaRef>> get_for_game(const std::string& game_id);

private:
    IHttpClient& m_http;
};

} // namespace lancommander

#endif // LANCOMMANDER_CLIENTS_MEDIA_CLIENT_H
