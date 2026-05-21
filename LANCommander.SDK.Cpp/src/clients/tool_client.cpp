#include "lancommander/clients/tool_client.h"
#include "../json/json_helpers.h"

#include <sstream>

namespace lancommander {

ToolClient::ToolClient(IHttpClient& http) : m_http(http) {}

Result<Tool> ToolClient::get(const std::string& tool_id)
{
    HttpResponse resp = m_http.get("/api/Tools/" + tool_id);
    if (!resp.ok()) {
        std::ostringstream e;
        e << "GetTool failed (HTTP " << resp.status_code << ")";
        return Result<Tool>::fail(e.str());
    }

    json::JsonDoc doc(resp.body);
    if (!doc) return Result<Tool>::fail("Invalid JSON response");

    Tool t = json::parse_tool(doc.root);
    if (t.id.empty()) return Result<Tool>::fail("No tool ID in response");
    return Result<Tool>::ok(std::move(t));
}

Result<std::vector<Script>> ToolClient::get_scripts(const std::string& tool_id)
{
    HttpResponse resp = m_http.get("/api/Tool/" + tool_id + "/Scripts");
    if (!resp.ok()) {
        std::ostringstream e;
        e << "GetToolScripts failed (HTTP " << resp.status_code << ")";
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
        scripts.push_back(json::parse_script(item));
    }
    return Result<std::vector<Script>>::ok(std::move(scripts));
}

Result<bool> ToolClient::download(const std::string& tool_id, const std::string& dest_path,
                                  DownloadProgressFn progress)
{
    if (!m_http.download("/api/Tools/" + tool_id + "/Download", dest_path, progress))
        return Result<bool>::fail("Tool download failed");
    return Result<bool>::ok(true);
}

} // namespace lancommander
