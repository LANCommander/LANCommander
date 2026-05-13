#include "profile_client.h"

#include "cJSON.h"

#include <sstream>

namespace
{
    std::string GetEither(cJSON* obj, const char* a, const char* b)
    {
        cJSON* n = cJSON_GetObjectItem(obj, a);
        if (!n) n = cJSON_GetObjectItem(obj, b);
        if (n && n->type == cJSON_String && n->valuestring)
            return std::string(n->valuestring);
        return std::string();
    }
}

bool ProfileClient::GetAlias(std::string* aliasOut, std::string* errorOut)
{
    HttpResponse resp = m_http.Get("/api/Profile");
    if (!resp.ok())
    {
        if (errorOut)
        {
            std::ostringstream e;
            e << "GetAlias failed (HTTP " << resp.status << ")";
            *errorOut = e.str();
        }
        return false;
    }
    cJSON* root = cJSON_Parse(resp.body.c_str());
    if (!root)
    {
        if (errorOut) *errorOut = "GetAlias: invalid JSON";
        return false;
    }
    std::string alias = GetEither(root, "alias", "Alias");
    if (alias.empty())
        alias = GetEither(root, "userName", "UserName");
    cJSON_Delete(root);
    if (aliasOut) *aliasOut = alias;
    return true;
}

bool ProfileClient::ChangeAlias(const std::string& alias,
                                std::string* errorOut)
{
    cJSON* req = cJSON_CreateObject();
    cJSON_AddStringToObject(req, "Alias", alias.c_str());
    char* body = cJSON_PrintUnformatted(req);
    std::string payload = body ? body : "{}";
    cJSON_Delete(req);
    cJSON_free(body);

    HttpResponse resp = m_http.PutJson("/api/Profile/ChangeAlias", payload);
    if (!resp.ok())
    {
        if (errorOut)
        {
            std::ostringstream e;
            e << "ChangeAlias failed (HTTP " << resp.status << ")";
            *errorOut = e.str();
        }
        return false;
    }
    return true;
}
