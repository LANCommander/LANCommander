#ifndef LANCOMMANDER_CLIENTS_GAME_CLIENT_H
#define LANCOMMANDER_CLIENTS_GAME_CLIENT_H

#include <string>
#include <vector>

#include "../http/http_client.h"
#include "../models/game.h"
#include "../models/redistributable.h"
#include "../types.h"

namespace lancommander {

class GameClient {
public:
    explicit GameClient(IHttpClient& http);

    Result<std::vector<Game>> get_all();
    Result<Game> get(const std::string& game_id);
    Result<GameManifest> get_manifest(const std::string& game_id);
    Result<std::vector<Action>> get_actions(const std::string& game_id);
    Result<std::vector<Game>> get_addons(const std::string& game_id);
    Result<std::vector<Redistributable>> get_redistributables(const std::string& game_id);
    Result<bool> check_for_update(const std::string& game_id, const std::string& installed_version);
    Result<bool> download(const std::string& game_id, const std::string& dest_path,
                          DownloadProgressFn progress = nullptr);
    void notify_started(const std::string& game_id);
    void notify_stopped(const std::string& game_id);

private:
    IHttpClient& m_http;
};

// Parse a manifest JSON string into a GameManifest struct.
// Exposed so cached manifests can be rehydrated without network access.
bool parse_manifest_json(const std::string& json, GameManifest* out, std::string* error_out);

} // namespace lancommander

#endif // LANCOMMANDER_CLIENTS_GAME_CLIENT_H
