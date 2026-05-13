#include "script_client.h"

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

    Script::Type DecodeType(cJSON* item)
    {
        cJSON* n = cJSON_GetObjectItem(item, "type");
        if (!n) n = cJSON_GetObjectItem(item, "Type");
        if (!n) return Script::TypeUnknown;
        if (n->type == cJSON_Number)
        {
            switch (n->valueint)
            {
            case 0: return Script::TypeInstall;
            case 1: return Script::TypeUninstall;
            case 2: return Script::TypeNameChange;
            case 3: return Script::TypeKeyChange;
            case 7: return Script::TypeBeforeStart;
            case 8: return Script::TypeAfterStop;
            default: return Script::TypeUnknown;
            }
        }
        if (n->type == cJSON_String && n->valuestring)
        {
            std::string s = n->valuestring;
            if (s == "Install")     return Script::TypeInstall;
            if (s == "Uninstall")   return Script::TypeUninstall;
            if (s == "NameChange")  return Script::TypeNameChange;
            if (s == "KeyChange")   return Script::TypeKeyChange;
            if (s == "BeforeStart") return Script::TypeBeforeStart;
            if (s == "AfterStop")   return Script::TypeAfterStop;
        }
        return Script::TypeUnknown;
    }
}

bool ScriptClient::FetchAndParse(const std::string& route,
                                 std::vector<Script>* out,
                                 std::string* errorOut)
{
    HttpResponse resp = m_http.Get(route);
    if (!resp.ok())
    {
        // 404 = no scripts; treat as empty rather than an error so callers
        // don't have to special-case the common "this entity has none" path.
        if (resp.status == 404) return true;
        if (errorOut)
        {
            std::ostringstream e;
            e << "GetScripts failed (HTTP " << resp.status << ")";
            *errorOut = e.str();
        }
        return false;
    }
    cJSON* root = cJSON_Parse(resp.body.c_str());
    if (!root || root->type != cJSON_Array)
    {
        if (root) cJSON_Delete(root);
        if (errorOut) *errorOut = "GetScripts: expected JSON array";
        return false;
    }
    int n = cJSON_GetArraySize(root);
    for (int i = 0; i < n; ++i)
    {
        cJSON* item = cJSON_GetArrayItem(root, i);
        if (!item) continue;
        Script s;
        s.type     = DecodeType(item);
        s.name     = GetEither(item, "name", "Name");
        s.contents = GetEither(item, "contents", "Contents");
        if (s.type == Script::TypeUnknown) continue;
        if (s.contents.empty()) continue;
        out->push_back(s);
    }
    cJSON_Delete(root);
    return true;
}

bool ScriptClient::GetGameScripts(const std::string& gameId,
                                  std::vector<Script>* out,
                                  std::string* errorOut)
{
    return FetchAndParse("/api/Games/" + gameId + "/Scripts", out, errorOut);
}

bool ScriptClient::GetRedistributableScripts(const std::string& redistId,
                                             std::vector<Script>* out,
                                             std::string* errorOut)
{
    return FetchAndParse("/api/Redistributables/" + redistId + "/Scripts",
                         out, errorOut);
}
