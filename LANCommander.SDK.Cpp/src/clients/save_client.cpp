#include "lancommander/clients/save_client.h"
#include "../json/json_helpers.h"

#include <sstream>

namespace lancommander {

SaveClient::SaveClient(IHttpClient& http) : m_http(http) {}

Result<std::vector<GameSave>> SaveClient::get(const std::string& game_id)
{
    HttpResponse resp = m_http.get("/api/Saves/Game/" + game_id);
    if (!resp.ok()) {
        std::ostringstream e;
        e << "GetSaves failed (HTTP " << resp.status_code << ")";
        return Result<std::vector<GameSave>>::fail(e.str());
    }

    json::JsonDoc doc(resp.body);
    if (!doc || doc.root->type != cJSON_Array)
        return Result<std::vector<GameSave>>::fail("Expected JSON array");

    std::vector<GameSave> saves;
    int n = cJSON_GetArraySize(doc.root);
    for (int i = 0; i < n; ++i) {
        cJSON* item = cJSON_GetArrayItem(doc.root, i);
        if (!item) continue;
        saves.push_back(json::parse_game_save(item));
    }
    return Result<std::vector<GameSave>>::ok(std::move(saves));
}

Result<GameSave> SaveClient::get_latest(const std::string& game_id)
{
    HttpResponse resp = m_http.get("/api/Saves/Game/" + game_id + "/Latest");
    if (!resp.ok()) {
        std::ostringstream e;
        e << "GetLatestSave failed (HTTP " << resp.status_code << ")";
        return Result<GameSave>::fail(e.str());
    }

    json::JsonDoc doc(resp.body);
    if (!doc) return Result<GameSave>::fail("Invalid JSON response");

    return Result<GameSave>::ok(json::parse_game_save(doc.root));
}

Result<bool> SaveClient::download_latest(const std::string& game_id,
                                         const std::string& dest_path,
                                         DownloadProgressFn progress)
{
    if (!m_http.download("/api/Saves/Game/" + game_id + "/Latest/Download",
                         dest_path, progress))
        return Result<bool>::fail("Save download failed");
    return Result<bool>::ok(true);
}

Result<bool> SaveClient::upload(const std::string& game_id, const std::string& zip_path)
{
    HttpResponse resp = m_http.post_multipart_file(
        "/api/Saves/Game/" + game_id + "/Upload", "file", zip_path);
    if (resp.ok()) return Result<bool>::ok(true);

    std::ostringstream e;
    e << "Save upload failed (HTTP " << resp.status_code << ")";
    return Result<bool>::fail(e.str());
}

} // namespace lancommander
