#include "lancommander/clients/profile_client.h"
#include "../json/json_helpers.h"

#include <sstream>

namespace lancommander {

ProfileClient::ProfileClient(IHttpClient& http) : m_http(http) {}

Result<User> ProfileClient::get()
{
    HttpResponse resp = m_http.get("/api/Profile");
    if (!resp.ok()) {
        std::ostringstream e;
        e << "GetProfile failed (HTTP " << resp.status_code << ")";
        return Result<User>::fail(e.str());
    }

    json::JsonDoc doc(resp.body);
    if (!doc) return Result<User>::fail("Invalid JSON response");

    User user = json::parse_user(doc.root);
    return Result<User>::ok(std::move(user));
}

Result<std::string> ProfileClient::get_alias()
{
    HttpResponse resp = m_http.get("/api/Profile");
    if (!resp.ok()) {
        std::ostringstream e;
        e << "GetAlias failed (HTTP " << resp.status_code << ")";
        return Result<std::string>::fail(e.str());
    }

    json::JsonDoc doc(resp.body);
    if (!doc) return Result<std::string>::fail("Invalid JSON response");

    std::string alias = json::get_string(doc.root, "alias", "Alias");
    if (alias.empty())
        alias = json::get_string(doc.root, "userName", "UserName");

    return Result<std::string>::ok(std::move(alias));
}

Result<bool> ProfileClient::change_alias(const std::string& alias)
{
    cJSON* req = cJSON_CreateObject();
    cJSON_AddStringToObject(req, "Alias", alias.c_str());
    char* body = cJSON_PrintUnformatted(req);
    std::string payload = body ? body : "{}";
    cJSON_Delete(req);
    cJSON_free(body);

    HttpResponse resp = m_http.put("/api/Profile/ChangeAlias", payload);
    if (resp.ok()) return Result<bool>::ok(true);

    std::ostringstream e;
    e << "ChangeAlias failed (HTTP " << resp.status_code << ")";
    return Result<bool>::fail(e.str());
}

Result<bool> ProfileClient::download_avatar(const std::string& dest_path)
{
    if (!m_http.download("/api/Profile/Avatar", dest_path))
        return Result<bool>::fail("Avatar download failed");
    return Result<bool>::ok(true);
}

} // namespace lancommander
