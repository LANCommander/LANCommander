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

namespace {

// Both custom-field endpoints speak JSON strings, not raw text: the server
// returns TypedResults.Ok(value) and binds the update with [FromBody] string,
// so the wire form is a quoted, escaped JSON string in each direction.
std::string to_json_string(const std::string& value)
{
    cJSON* node = cJSON_CreateString(value.c_str());
    char* text = node ? cJSON_PrintUnformatted(node) : NULL;

    std::string out = text ? text : "\"\"";

    if (text)
        cJSON_free(text);
    if (node)
        cJSON_Delete(node);

    return out;
}

bool from_json_string(const std::string& body, std::string* out)
{
    cJSON* node = cJSON_Parse(body.c_str());
    if (!node)
        return false;

    bool ok = false;
    if ((node->type & 0xFF) == cJSON_String && node->valuestring) {
        *out = node->valuestring;
        ok = true;
    } else if ((node->type & 0xFF) == cJSON_NULL) {
        out->clear();
        ok = true;
    }

    cJSON_Delete(node);
    return ok;
}

} // namespace

Result<std::string> ProfileClient::get_custom_field(const std::string& name)
{
    HttpResponse resp = m_http.get("/api/Profile/CustomField/" + name);

    // The server answers 404 for a field that was never set, which is a normal
    // outcome rather than a failure — the script just gets an empty value.
    if (resp.status_code == 404)
        return Result<std::string>::ok(std::string());

    if (!resp.ok()) {
        std::ostringstream e;
        e << "GetCustomField failed (HTTP " << resp.status_code << ")";
        return Result<std::string>::fail(e.str());
    }

    std::string value;
    if (!from_json_string(resp.body, &value)) {
        // Tolerate a server that answers with a bare string.
        value = resp.body;
    }

    return Result<std::string>::ok(value);
}

Result<std::string> ProfileClient::update_custom_field(const std::string& name,
                                                       const std::string& value)
{
    HttpResponse resp = m_http.put("/api/Profile/CustomField/" + name,
                                   to_json_string(value));

    if (!resp.ok()) {
        std::ostringstream e;
        e << "UpdateCustomField failed (HTTP " << resp.status_code << ")";
        return Result<std::string>::fail(e.str());
    }

    std::string updated;
    if (!from_json_string(resp.body, &updated))
        updated = value;

    return Result<std::string>::ok(updated);
}

Result<std::vector<unsigned char> > ProfileClient::get_avatar()
{
    typedef std::vector<unsigned char> Bytes;

    HttpResponse resp = m_http.get("/api/Profile/Avatar");

    if (!resp.ok()) {
        std::ostringstream e;
        e << "GetAvatar failed (HTTP " << resp.status_code << ")";
        return Result<Bytes>::fail(e.str());
    }

    // HttpResponse::body is length-delimited rather than NUL-terminated, so it
    // carries image bytes intact.
    Bytes bytes;
    bytes.reserve(resp.body.size());
    for (std::size_t i = 0; i < resp.body.size(); ++i)
        bytes.push_back((unsigned char)resp.body[i]);

    return Result<Bytes>::ok(bytes);
}

Result<bool> ProfileClient::download_avatar(const std::string& dest_path)
{
    if (!m_http.download("/api/Profile/Avatar", dest_path))
        return Result<bool>::fail("Avatar download failed");
    return Result<bool>::ok(true);
}

} // namespace lancommander
