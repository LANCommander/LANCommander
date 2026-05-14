#include "auth_client.h"

#include "cJSON.h"

#include <sstream>

namespace
{
    std::string EscapeJsonString(const std::string& in)
    {
        std::string out;
        out.reserve(in.size() + 2);
        for (size_t i = 0; i < in.size(); ++i)
        {
            char c = in[i];
            switch (c)
            {
                case '"':  out += "\\\""; break;
                case '\\': out += "\\\\"; break;
                case '\b': out += "\\b"; break;
                case '\f': out += "\\f"; break;
                case '\n': out += "\\n"; break;
                case '\r': out += "\\r"; break;
                case '\t': out += "\\t"; break;
                default:   out += c;     break;
            }
        }
        return out;
    }

    std::string GetJsonString(cJSON* obj, const char* key)
    {
        cJSON* n = cJSON_GetObjectItem(obj, key);
        if (n && n->type == cJSON_String && n->valuestring)
            return std::string(n->valuestring);
        return std::string();
    }

    bool ParseAuthResponse(const std::string& body, AuthTokens* out,
                           std::string* errorOut)
    {
        cJSON* root = cJSON_Parse(body.c_str());
        if (!root)
        {
            if (errorOut) *errorOut = "invalid JSON response";
            return false;
        }
        std::string access  = GetJsonString(root, "accessToken");
        if (access.empty())  access  = GetJsonString(root, "AccessToken");
        std::string refresh = GetJsonString(root, "refreshToken");
        if (refresh.empty()) refresh = GetJsonString(root, "RefreshToken");
        std::string exp     = GetJsonString(root, "expiration");
        if (exp.empty())     exp     = GetJsonString(root, "Expiration");
        cJSON_Delete(root);

        if (access.empty())
        {
            if (errorOut) *errorOut = "no access token in response";
            return false;
        }
        out->accessToken  = access;
        out->refreshToken = refresh;
        out->expiration   = exp;
        return true;
    }
}

AuthenticationClient::AuthenticationClient(HttpClient& http)
    : m_http(http) {}

bool AuthenticationClient::Login(const std::string& username,
                                 const std::string& password,
                                 AuthTokens* tokensOut, std::string* errorOut)
{
    std::ostringstream body;
    body << "{\"UserName\":\"" << EscapeJsonString(username)
         << "\",\"Password\":\"" << EscapeJsonString(password) << "\"}";

    HttpResponse resp = m_http.PostJson("/api/Auth/Login", body.str());
    if (!resp.ok())
    {
        if (errorOut)
        {
            std::ostringstream e;
            e << "Login failed (HTTP " << resp.status << ")";
            *errorOut = e.str();
        }
        return false;
    }
    AuthTokens t;
    if (!ParseAuthResponse(resp.body, &t, errorOut)) return false;
    m_http.SetBearerToken(t.accessToken);
    if (tokensOut) *tokensOut = t;
    return true;
}

bool AuthenticationClient::Validate(std::string* errorOut)
{
    HttpResponse resp = m_http.PostJson("/api/Auth/Validate", std::string());
    if (resp.ok()) return true;
    if (errorOut)
    {
        std::ostringstream e;
        e << "Validate failed (HTTP " << resp.status << ")";
        *errorOut = e.str();
    }
    return false;
}

bool AuthenticationClient::Refresh(const AuthTokens& in, AuthTokens* out,
                                   std::string* errorOut)
{
    std::ostringstream body;
    body << "{\"AccessToken\":\""  << EscapeJsonString(in.accessToken)
         << "\",\"RefreshToken\":\"" << EscapeJsonString(in.refreshToken)
         << "\",\"Expiration\":\""   << EscapeJsonString(in.expiration)
         << "\"}";
    HttpResponse resp = m_http.PostJson("/api/Auth/Refresh", body.str());
    if (!resp.ok())
    {
        if (errorOut)
        {
            std::ostringstream e;
            e << "Refresh failed (HTTP " << resp.status << ")";
            *errorOut = e.str();
        }
        return false;
    }
    AuthTokens t;
    if (!ParseAuthResponse(resp.body, &t, errorOut)) return false;
    m_http.SetBearerToken(t.accessToken);
    if (out) *out = t;
    return true;
}

void AuthenticationClient::Logout()
{
    m_http.PostJson("/api/Auth/Logout", std::string());
}
