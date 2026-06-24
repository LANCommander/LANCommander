#include "lancommander/clients/authentication_client.h"
#include "../json/json_helpers.h"

#include <sstream>

namespace lancommander {

AuthenticationClient::AuthenticationClient(IHttpClient& http) : m_http(http) {}

Result<AuthToken> AuthenticationClient::login(const std::string& username,
                                              const std::string& password)
{
    std::string body = "{\"UserName\":\"" + json::escape(username)
                     + "\",\"Password\":\"" + json::escape(password) + "\"}";

    HttpResponse resp = m_http.post("/api/Auth/Login", body);
    if (!resp.ok()) {
        std::ostringstream e;
        e << "Login failed (HTTP " << resp.status_code << ")";
        return Result<AuthToken>::fail(e.str());
    }

    json::JsonDoc doc(resp.body);
    if (!doc) return Result<AuthToken>::fail("Invalid JSON response");

    AuthToken token = json::parse_auth_token(doc.root);
    if (token.access_token.empty())
        return Result<AuthToken>::fail("No access token in response");

    m_http.set_bearer_token(token.access_token);
    return Result<AuthToken>::ok(std::move(token));
}

Result<bool> AuthenticationClient::validate()
{
    HttpResponse resp = m_http.post("/api/Auth/Validate", "");
    if (resp.ok()) return Result<bool>::ok(true);

    std::ostringstream e;
    e << "Validate failed (HTTP " << resp.status_code << ")";
    return Result<bool>::fail(e.str());
}

Result<AuthToken> AuthenticationClient::refresh(const AuthToken& current)
{
    std::string body = "{\"AccessToken\":\""  + json::escape(current.access_token)
                     + "\",\"RefreshToken\":\"" + json::escape(current.refresh_token)
                     + "\",\"Expiration\":\""   + json::escape(current.expiration) + "\"}";

    HttpResponse resp = m_http.post("/api/Auth/Refresh", body);
    if (!resp.ok()) {
        std::ostringstream e;
        e << "Refresh failed (HTTP " << resp.status_code << ")";
        return Result<AuthToken>::fail(e.str());
    }

    json::JsonDoc doc(resp.body);
    if (!doc) return Result<AuthToken>::fail("Invalid JSON response");

    AuthToken token = json::parse_auth_token(doc.root);
    if (token.access_token.empty())
        return Result<AuthToken>::fail("No access token in response");

    m_http.set_bearer_token(token.access_token);
    return Result<AuthToken>::ok(std::move(token));
}

void AuthenticationClient::logout()
{
    m_http.post("/api/Auth/Logout", "");
}

Result<std::vector<AuthenticationProvider>> AuthenticationClient::get_providers()
{
    HttpResponse resp = m_http.get("/api/Auth/AuthenticationProviders");
    if (!resp.ok()) {
        std::ostringstream e;
        e << "GetProviders failed (HTTP " << resp.status_code << ")";
        return Result<std::vector<AuthenticationProvider>>::fail(e.str());
    }

    json::JsonDoc doc(resp.body);
    if (!doc || doc.root->type != cJSON_Array)
        return Result<std::vector<AuthenticationProvider>>::fail("Expected JSON array");

    std::vector<AuthenticationProvider> providers;
    int n = cJSON_GetArraySize(doc.root);
    for (int i = 0; i < n; ++i) {
        cJSON* item = cJSON_GetArrayItem(doc.root, i);
        if (!item) continue;
        AuthenticationProvider p;
        p.name = json::get_string(item, "name", "Name");
        p.type = json::get_string(item, "type", "Type");
        if (!p.name.empty()) providers.push_back(std::move(p));
    }
    return Result<std::vector<AuthenticationProvider>>::ok(std::move(providers));
}

} // namespace lancommander
