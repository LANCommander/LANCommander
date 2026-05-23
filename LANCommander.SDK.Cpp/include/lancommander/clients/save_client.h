#ifndef LANCOMMANDER_CLIENTS_SAVE_CLIENT_H
#define LANCOMMANDER_CLIENTS_SAVE_CLIENT_H

#include <string>
#include <vector>

#include "../http/http_client.h"
#include "../models/save.h"
#include "../types.h"

namespace lancommander {

class SaveClient {
public:
    explicit SaveClient(IHttpClient& http);

    Result<std::vector<GameSave>> get(const std::string& game_id);
    Result<GameSave> get_latest(const std::string& game_id);
    Result<bool> download_latest(const std::string& game_id, const std::string& dest_path,
                                 DownloadProgressFn progress = nullptr);
    Result<bool> upload(const std::string& game_id, const std::string& zip_path);

private:
    IHttpClient& m_http;
};

} // namespace lancommander

#endif // LANCOMMANDER_CLIENTS_SAVE_CLIENT_H
