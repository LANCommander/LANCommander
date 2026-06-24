#include "lancommander/clients/game_client.h"
#include "../json/json_helpers.h"

#include <sstream>

namespace lancommander {

GameClient::GameClient(IHttpClient& http) : m_http(http) {}

Result<std::vector<Game>> GameClient::get_all()
{
    HttpResponse resp = m_http.get("/api/Games");
    if (!resp.ok()) {
        std::ostringstream e;
        e << "GetGames failed (HTTP " << resp.status_code << ")";
        return Result<std::vector<Game>>::fail(e.str());
    }

    json::JsonDoc doc(resp.body);
    if (!doc || doc.root->type != cJSON_Array)
        return Result<std::vector<Game>>::fail("Expected JSON array");

    std::vector<Game> games;
    int count = cJSON_GetArraySize(doc.root);
    for (int i = 0; i < count; ++i) {
        cJSON* item = cJSON_GetArrayItem(doc.root, i);
        if (!item) continue;
        Game g = json::parse_game(item);
        if (!g.id.empty()) games.push_back(std::move(g));
    }
    return Result<std::vector<Game>>::ok(std::move(games));
}

Result<Game> GameClient::get(const std::string& game_id)
{
    HttpResponse resp = m_http.get("/api/Games/" + game_id);
    if (!resp.ok()) {
        std::ostringstream e;
        e << "GetGame failed (HTTP " << resp.status_code << ")";
        return Result<Game>::fail(e.str());
    }

    json::JsonDoc doc(resp.body);
    if (!doc) return Result<Game>::fail("Invalid JSON response");

    Game g = json::parse_game(doc.root);
    if (g.id.empty()) return Result<Game>::fail("No game ID in response");
    return Result<Game>::ok(std::move(g));
}

Result<GameManifest> GameClient::get_manifest(const std::string& game_id)
{
    HttpResponse resp = m_http.get("/api/Games/" + game_id + "/Manifest");
    if (!resp.ok()) {
        std::ostringstream e;
        e << "GetManifest failed (HTTP " << resp.status_code << ")";
        return Result<GameManifest>::fail(e.str());
    }

    GameManifest manifest;
    std::string error;
    if (!parse_manifest_json(resp.body, &manifest, &error))
        return Result<GameManifest>::fail(error);

    return Result<GameManifest>::ok(std::move(manifest));
}

Result<std::vector<Action>> GameClient::get_actions(const std::string& game_id)
{
    HttpResponse resp = m_http.get("/api/Games/" + game_id + "/Actions");
    if (!resp.ok()) {
        std::ostringstream e;
        e << "GetActions failed (HTTP " << resp.status_code << ")";
        return Result<std::vector<Action>>::fail(e.str());
    }

    json::JsonDoc doc(resp.body);
    if (!doc || doc.root->type != cJSON_Array)
        return Result<std::vector<Action>>::fail("Expected JSON array");

    std::vector<Action> actions;
    int n = cJSON_GetArraySize(doc.root);
    for (int i = 0; i < n; ++i) {
        cJSON* item = cJSON_GetArrayItem(doc.root, i);
        if (!item) continue;
        ManifestAction ma = json::parse_manifest_action(item);
        Action a;
        a.name              = std::move(ma.name);
        a.path              = std::move(ma.path);
        a.arguments         = std::move(ma.arguments);
        a.working_directory = std::move(ma.working_directory);
        a.is_primary        = ma.is_primary;
        a.sort_order        = ma.sort_order;
        a.variables         = std::move(ma.variables);
        actions.push_back(std::move(a));
    }
    return Result<std::vector<Action>>::ok(std::move(actions));
}

Result<std::vector<Game>> GameClient::get_addons(const std::string& game_id)
{
    HttpResponse resp = m_http.get("/api/Games/" + game_id + "/Addons");
    if (!resp.ok()) {
        std::ostringstream e;
        e << "GetAddons failed (HTTP " << resp.status_code << ")";
        return Result<std::vector<Game>>::fail(e.str());
    }

    json::JsonDoc doc(resp.body);
    if (!doc || doc.root->type != cJSON_Array)
        return Result<std::vector<Game>>::fail("Expected JSON array");

    std::vector<Game> addons;
    int n = cJSON_GetArraySize(doc.root);
    for (int i = 0; i < n; ++i) {
        cJSON* item = cJSON_GetArrayItem(doc.root, i);
        if (!item) continue;
        Game g = json::parse_game(item);
        if (!g.id.empty()) addons.push_back(std::move(g));
    }
    return Result<std::vector<Game>>::ok(std::move(addons));
}

Result<std::vector<Redistributable>> GameClient::get_redistributables(const std::string& game_id)
{
    HttpResponse resp = m_http.get("/api/Games/" + game_id);
    if (!resp.ok()) {
        std::ostringstream e;
        e << "GetRedistributables failed (HTTP " << resp.status_code << ")";
        return Result<std::vector<Redistributable>>::fail(e.str());
    }

    json::JsonDoc doc(resp.body);
    if (!doc) return Result<std::vector<Redistributable>>::fail("Invalid JSON response");

    std::vector<Redistributable> redists;
    cJSON* arr = json::get_child(doc.root, "redistributables", "Redistributables");
    if (arr && arr->type == cJSON_Array) {
        int n = cJSON_GetArraySize(arr);
        for (int i = 0; i < n; ++i) {
            cJSON* item = cJSON_GetArrayItem(arr, i);
            if (!item) continue;
            Redistributable r = json::parse_redistributable(item);
            if (!r.id.empty()) redists.push_back(std::move(r));
        }
    }
    return Result<std::vector<Redistributable>>::ok(std::move(redists));
}

Result<bool> GameClient::check_for_update(const std::string& game_id,
                                          const std::string& installed_version)
{
    HttpResponse resp = m_http.get(
        "/api/Games/" + game_id + "/CheckForUpdate?version=" + installed_version);
    if (!resp.ok()) {
        std::ostringstream e;
        e << "CheckForUpdate failed (HTTP " << resp.status_code << ")";
        return Result<bool>::fail(e.str());
    }

    json::JsonDoc doc(resp.body);
    if (!doc) return Result<bool>::fail("Invalid JSON response");

    bool available = (doc.root->type == cJSON_True);
    return Result<bool>::ok(available);
}

Result<bool> GameClient::download(const std::string& game_id, const std::string& dest_path,
                                  DownloadProgressFn progress)
{
    if (!m_http.download("/api/Games/" + game_id + "/Download", dest_path, progress))
        return Result<bool>::fail("Download failed");
    return Result<bool>::ok(true);
}

void GameClient::notify_started(const std::string& game_id)
{
    m_http.get("/api/Games/" + game_id + "/Started");
}

void GameClient::notify_stopped(const std::string& game_id)
{
    m_http.get("/api/Games/" + game_id + "/Stopped");
}

// Free function — also usable for cached manifest rehydration.
bool parse_manifest_json(const std::string& json_str, GameManifest* out, std::string* error_out)
{
    json::JsonDoc doc(json_str);
    if (!doc) {
        if (error_out) *error_out = "Manifest: invalid JSON";
        return false;
    }

    out->id      = json::get_string(doc.root, "id", "Id");
    out->title   = json::get_string(doc.root, "title", "Title");
    out->version = json::get_string(doc.root, "version", "Version");

    cJSON* actions = json::get_child(doc.root, "actions", "Actions");
    if (actions && actions->type == cJSON_Array) {
        int n = cJSON_GetArraySize(actions);
        for (int i = 0; i < n; ++i) {
            cJSON* a = cJSON_GetArrayItem(actions, i);
            if (!a) continue;
            out->actions.push_back(json::parse_manifest_action(a));
        }
    }

    cJSON* sps = json::get_child(doc.root, "savePaths", "SavePaths");
    if (sps && sps->type == cJSON_Array) {
        int n = cJSON_GetArraySize(sps);
        for (int i = 0; i < n; ++i) {
            cJSON* s = cJSON_GetArrayItem(sps, i);
            if (!s) continue;
            out->save_paths.push_back(json::parse_manifest_save_path(s));
        }
    }

    cJSON* redists = json::get_child(doc.root, "redistributables", "Redistributables");
    if (redists && redists->type == cJSON_Array) {
        int n = cJSON_GetArraySize(redists);
        for (int i = 0; i < n; ++i) {
            cJSON* r = cJSON_GetArrayItem(redists, i);
            if (!r) continue;
            ManifestRedistributable mr;
            mr.id   = json::get_string(r, "id", "Id");
            mr.name = json::get_string(r, "name", "Name");
            out->redistributables.push_back(std::move(mr));
        }
    }

    return true;
}

} // namespace lancommander
