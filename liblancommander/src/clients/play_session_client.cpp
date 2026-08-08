#include "lancommander/clients/play_session_client.h"
#include "../json/json_helpers.h"

#include <sstream>

namespace lancommander {

PlaySessionClient::PlaySessionClient(IHttpClient& http) : m_http(http) {}

Result<std::vector<EntityReference>> PlaySessionClient::get()
{
    HttpResponse resp = m_http.get("/api/PlaySessions");
    if (!resp.ok()) {
        std::ostringstream e;
        e << "GetPlaySessions failed (HTTP " << resp.status_code << ")";
        return Result<std::vector<EntityReference>>::fail(e.str());
    }

    json::JsonDoc doc(resp.body);
    if (!doc || doc.root->type != cJSON_Array)
        return Result<std::vector<EntityReference>>::fail("Expected JSON array");

    std::vector<EntityReference> refs;
    int n = cJSON_GetArraySize(doc.root);
    for (int i = 0; i < n; ++i) {
        cJSON* item = cJSON_GetArrayItem(doc.root, i);
        if (!item) continue;
        refs.push_back(json::parse_entity_reference(item));
    }
    return Result<std::vector<EntityReference>>::ok(std::move(refs));
}

Result<std::vector<PlaySession>> PlaySessionClient::get_for_game(const std::string& game_id)
{
    HttpResponse resp = m_http.get("/api/PlaySessions/" + game_id);
    if (!resp.ok()) {
        std::ostringstream e;
        e << "GetPlaySessions failed (HTTP " << resp.status_code << ")";
        return Result<std::vector<PlaySession>>::fail(e.str());
    }

    json::JsonDoc doc(resp.body);
    if (!doc || doc.root->type != cJSON_Array)
        return Result<std::vector<PlaySession>>::fail("Expected JSON array");

    std::vector<PlaySession> sessions;
    int n = cJSON_GetArraySize(doc.root);
    for (int i = 0; i < n; ++i) {
        cJSON* item = cJSON_GetArrayItem(doc.root, i);
        if (!item) continue;
        sessions.push_back(json::parse_play_session(item));
    }
    return Result<std::vector<PlaySession>>::ok(std::move(sessions));
}

} // namespace lancommander
