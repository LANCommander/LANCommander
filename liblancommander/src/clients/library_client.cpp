#include "lancommander/clients/library_client.h"
#include "../json/json_helpers.h"

#include <sstream>

namespace lancommander {

LibraryClient::LibraryClient(IHttpClient& http) : m_http(http) {}

Result<std::vector<EntityReference>> LibraryClient::get()
{
    HttpResponse resp = m_http.get("/api/Library");
    if (!resp.ok()) {
        std::ostringstream e;
        e << "GetLibrary failed (HTTP " << resp.status_code << ")";
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
        EntityReference r = json::parse_entity_reference(item);
        if (!r.id.empty()) refs.push_back(std::move(r));
    }
    return Result<std::vector<EntityReference>>::ok(std::move(refs));
}

Result<bool> LibraryClient::add(const std::string& game_id)
{
    HttpResponse resp = m_http.post("/api/Library/AddToLibrary/" + game_id, "");
    if (resp.ok()) return Result<bool>::ok(true);

    std::ostringstream e;
    e << "AddToLibrary failed (HTTP " << resp.status_code << ")";
    return Result<bool>::fail(e.str());
}

Result<bool> LibraryClient::remove(const std::string& game_id)
{
    HttpResponse resp = m_http.post("/api/Library/RemoveFromLibrary/" + game_id, "");
    if (resp.ok()) return Result<bool>::ok(true);

    std::ostringstream e;
    e << "RemoveFromLibrary failed (HTTP " << resp.status_code << ")";
    return Result<bool>::fail(e.str());
}

} // namespace lancommander
