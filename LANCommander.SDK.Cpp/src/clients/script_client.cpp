#include "lancommander/clients/script_client.h"
#include "../json/json_helpers.h"

#include <sstream>

namespace lancommander {

ScriptClient::ScriptClient(IHttpClient& http) : m_http(http) {}

Result<std::vector<Script>> ScriptClient::fetch_and_parse(const std::string& route)
{
    HttpResponse resp = m_http.get(route);

    // 404 = no scripts; treat as empty rather than an error.
    if (resp.status_code == 404)
        return Result<std::vector<Script>>::ok({});

    if (!resp.ok()) {
        std::ostringstream e;
        e << "GetScripts failed (HTTP " << resp.status_code << ")";
        return Result<std::vector<Script>>::fail(e.str());
    }

    json::JsonDoc doc(resp.body);
    if (!doc || doc.root->type != cJSON_Array)
        return Result<std::vector<Script>>::fail("Expected JSON array");

    std::vector<Script> scripts;
    int n = cJSON_GetArraySize(doc.root);
    for (int i = 0; i < n; ++i) {
        cJSON* item = cJSON_GetArrayItem(doc.root, i);
        if (!item) continue;
        Script s = json::parse_script(item);
        if (s.type == ScriptType::Unknown) continue;
        if (s.contents.empty()) continue;
        scripts.push_back(std::move(s));
    }
    return Result<std::vector<Script>>::ok(std::move(scripts));
}

Result<std::vector<Script>> ScriptClient::get_game_scripts(const std::string& game_id)
{
    return fetch_and_parse("/api/Games/" + game_id + "/Scripts");
}

Result<std::vector<Script>> ScriptClient::get_redistributable_scripts(const std::string& redist_id)
{
    return fetch_and_parse("/api/Redistributables/" + redist_id + "/Scripts");
}

} // namespace lancommander
