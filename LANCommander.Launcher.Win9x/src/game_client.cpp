#include "game_client.h"

#include "cJSON.h"

#include <cstdio>
#include <cstring>
#include <sstream>

namespace
{
    std::string GetJsonString(cJSON* obj, const char* key)
    {
        cJSON* n = cJSON_GetObjectItem(obj, key);
        if (n && n->type == cJSON_String && n->valuestring)
            return std::string(n->valuestring);
        return std::string();
    }

    std::string GetEither(cJSON* obj, const char* a, const char* b)
    {
        cJSON* n = cJSON_GetObjectItem(obj, a);
        if (!n) n = cJSON_GetObjectItem(obj, b);
        if (n && n->type == cJSON_String && n->valuestring)
            return std::string(n->valuestring);
        return std::string();
    }

    int GetIntEither(cJSON* obj, const char* a, const char* b)
    {
        cJSON* n = cJSON_GetObjectItem(obj, a);
        if (!n) n = cJSON_GetObjectItem(obj, b);
        if (n && n->type == cJSON_Number) return n->valueint;
        return 0;
    }

    bool GetBoolEither(cJSON* obj, const char* a, const char* b)
    {
        cJSON* n = cJSON_GetObjectItem(obj, a);
        if (!n) n = cJSON_GetObjectItem(obj, b);
        if (!n) return false;
        if (n->type == cJSON_True)  return true;
        if (n->type == cJSON_False) return false;
        if (n->type == cJSON_Number) return n->valueint != 0;
        return false;
    }

    std::string JoinNames(cJSON* arr)
    {
        if (!arr || arr->type != cJSON_Array) return std::string();
        std::string out;
        int n = cJSON_GetArraySize(arr);
        for (int i = 0; i < n; ++i)
        {
            cJSON* e = cJSON_GetArrayItem(arr, i);
            if (!e) continue;
            std::string name = GetJsonString(e, "name");
            if (name.empty()) name = GetJsonString(e, "Name");
            if (name.empty()) continue;
            if (!out.empty()) out += ", ";
            out += name;
        }
        return out;
    }
}

GameClient::GameClient(HttpClient& http) : m_http(http) {}

bool GameClient::GetAll(std::vector<GameSummary>* gamesOut, std::string* errorOut)
{
    HttpResponse resp = m_http.Get("/api/Games");
    if (!resp.ok())
    {
        if (errorOut)
        {
            std::ostringstream e;
            e << "GetGames failed (HTTP " << resp.status << ")";
            *errorOut = e.str();
        }
        return false;
    }
    cJSON* root = cJSON_Parse(resp.body.c_str());
    if (!root || root->type != cJSON_Array)
    {
        if (root) cJSON_Delete(root);
        if (errorOut) *errorOut = "GetGames: expected JSON array";
        return false;
    }
    int count = cJSON_GetArraySize(root);
    for (int i = 0; i < count; ++i)
    {
        cJSON* item = cJSON_GetArrayItem(root, i);
        if (!item) continue;
        GameSummary g;
        g.id        = GetEither(item, "id", "Id");
        g.title     = GetEither(item, "title", "Title");
        g.sortTitle = GetEither(item, "sortTitle", "SortTitle");

        std::string released = GetEither(item, "releasedOn", "ReleasedOn");
        if (released.size() >= 4)
        {
            int year = 0;
            for (int k = 0; k < 4 && released[k] >= '0' && released[k] <= '9'; ++k)
                year = year * 10 + (released[k] - '0');
            if (year >= 1970) g.releasedYear = year;
        }

        cJSON* lib = cJSON_GetObjectItem(item, "inLibrary");
        if (!lib) lib = cJSON_GetObjectItem(item, "InLibrary");
        g.inLibrary = (lib && lib->type == cJSON_True);

        g.description = GetEither(item, "description", "Description");

        cJSON* devs = cJSON_GetObjectItem(item, "developers");
        if (!devs) devs = cJSON_GetObjectItem(item, "Developers");
        g.developers = JoinNames(devs);
        cJSON* pubs = cJSON_GetObjectItem(item, "publishers");
        if (!pubs) pubs = cJSON_GetObjectItem(item, "Publishers");
        g.publishers = JoinNames(pubs);
        cJSON* gens = cJSON_GetObjectItem(item, "genres");
        if (!gens) gens = cJSON_GetObjectItem(item, "Genres");
        g.genres = JoinNames(gens);

        cJSON* media = cJSON_GetObjectItem(item, "media");
        if (!media) media = cJSON_GetObjectItem(item, "Media");
        if (media && media->type == cJSON_Array)
        {
            int mc = cJSON_GetArraySize(media);
            for (int pass = 0; pass < 2 && g.coverMediaId.empty(); ++pass)
            {
                for (int j = 0; j < mc; ++j)
                {
                    cJSON* m = cJSON_GetArrayItem(media, j);
                    if (!m) continue;
                    bool isCover = false;
                    cJSON* t = cJSON_GetObjectItem(m, "type");
                    if (!t) t = cJSON_GetObjectItem(m, "Type");
                    if (t && t->type == cJSON_String && t->valuestring)
                        isCover = (strcmp(t->valuestring, "Cover") == 0);
                    else if (t && t->type == cJSON_Number)
                        isCover = (t->valueint == 1);
                    if (pass == 0 && !isCover) continue;
                    std::string mid = GetEither(m, "id", "Id");
                    if (mid.empty()) continue;
                    g.coverMediaId = mid;
                    g.coverCrc32   = GetEither(m, "crc32", "Crc32");
                    break;
                }
            }
        }

        if (!g.id.empty()) gamesOut->push_back(g);
    }
    cJSON_Delete(root);
    return true;
}

bool GameClient::DownloadGame(const std::string& gameId,
                              const std::string& destPath,
                              DownloadProgressFn progress, void* userData,
                              std::string* errorOut)
{
    FILE* f = fopen(destPath.c_str(), "wb");
    if (!f)
    {
        if (errorOut) *errorOut = "Could not open destination file";
        return false;
    }
    long status = m_http.Download("/api/Games/" + gameId + "/Download",
                                  f, progress, userData);
    fclose(f);
    if (status < 200 || status >= 300)
    {
        if (errorOut)
        {
            std::ostringstream e;
            e << "Download failed (HTTP " << status << ")";
            *errorOut = e.str();
        }
        return false;
    }
    return true;
}

bool GameClient::FetchManifestJson(const std::string& gameId,
                                   std::string* jsonOut, std::string* errorOut)
{
    HttpResponse resp = m_http.Get("/api/Games/" + gameId + "/Manifest");
    if (!resp.ok())
    {
        if (errorOut)
        {
            std::ostringstream e;
            e << "GetManifest failed (HTTP " << resp.status << ")";
            *errorOut = e.str();
        }
        return false;
    }
    if (jsonOut) *jsonOut = resp.body;
    return true;
}

bool GameClient::GetManifest(const std::string& gameId, GameManifest* out,
                             std::string* errorOut)
{
    std::string body;
    if (!FetchManifestJson(gameId, &body, errorOut)) return false;
    return ParseManifestJson(body, out, errorOut);
}

bool ParseManifestJson(const std::string& json, GameManifest* out,
                       std::string* errorOut)
{
    cJSON* root = cJSON_Parse(json.c_str());
    if (!root)
    {
        if (errorOut) *errorOut = "Manifest: invalid JSON";
        return false;
    }

    out->id      = GetEither(root, "id", "Id");
    out->title   = GetEither(root, "title", "Title");
    out->version = GetEither(root, "version", "Version");

    cJSON* actions = cJSON_GetObjectItem(root, "actions");
    if (!actions) actions = cJSON_GetObjectItem(root, "Actions");
    if (actions && actions->type == cJSON_Array)
    {
        int n = cJSON_GetArraySize(actions);
        for (int i = 0; i < n; ++i)
        {
            cJSON* a = cJSON_GetArrayItem(actions, i);
            if (!a) continue;
            ManifestAction ma;
            ma.name             = GetEither(a, "name", "Name");
            ma.path             = GetEither(a, "path", "Path");
            ma.arguments        = GetEither(a, "arguments", "Arguments");
            ma.workingDirectory = GetEither(a, "workingDirectory", "WorkingDirectory");
            ma.isPrimary        = GetBoolEither(a, "isPrimaryAction", "IsPrimaryAction");
            ma.sortOrder        = GetIntEither(a, "sortOrder", "SortOrder");

            cJSON* vars = cJSON_GetObjectItem(a, "variables");
            if (!vars) vars = cJSON_GetObjectItem(a, "Variables");
            if (vars && vars->type == cJSON_Object)
            {
                for (cJSON* v = vars->child; v; v = v->next)
                {
                    if (v->string && v->type == cJSON_String && v->valuestring)
                        ma.variables[v->string] = v->valuestring;
                }
            }
            out->actions.push_back(ma);
        }
    }

    cJSON* sps = cJSON_GetObjectItem(root, "savePaths");
    if (!sps) sps = cJSON_GetObjectItem(root, "SavePaths");
    if (sps && sps->type == cJSON_Array)
    {
        int n = cJSON_GetArraySize(sps);
        for (int i = 0; i < n; ++i)
        {
            cJSON* s = cJSON_GetArrayItem(sps, i);
            if (!s) continue;
            ManifestSavePath sp;
            sp.id               = GetEither(s, "id", "Id");
            sp.path             = GetEither(s, "path", "Path");
            sp.workingDirectory = GetEither(s, "workingDirectory", "WorkingDirectory");
            sp.isRegex          = GetBoolEither(s, "isRegex", "IsRegex");

            cJSON* t = cJSON_GetObjectItem(s, "type");
            if (!t) t = cJSON_GetObjectItem(s, "Type");
            if (t && t->type == cJSON_String && t->valuestring)
                sp.isFile = (strcmp(t->valuestring, "File") == 0);
            else if (t && t->type == cJSON_Number)
                sp.isFile = (t->valueint == 0);
            else
                sp.isFile = true;

            out->savePaths.push_back(sp);
        }
    }
    cJSON_Delete(root);
    return true;
}

bool GameClient::CheckForUpdate(const std::string& gameId,
                                const std::string& installedVersion,
                                bool* updateAvailableOut, std::string* errorOut)
{
    HttpResponse resp = m_http.Get(
        "/api/Games/" + gameId + "/CheckForUpdate?version=" + installedVersion);
    if (!resp.ok())
    {
        if (errorOut)
        {
            std::ostringstream e;
            e << "CheckForUpdate failed (HTTP " << resp.status << ")";
            *errorOut = e.str();
        }
        return false;
    }
    cJSON* root = cJSON_Parse(resp.body.c_str());
    if (!root)
    {
        if (errorOut) *errorOut = "CheckForUpdate: invalid JSON";
        return false;
    }
    bool avail = (root->type == cJSON_True);
    cJSON_Delete(root);
    if (updateAvailableOut) *updateAvailableOut = avail;
    return true;
}

void GameClient::NotifyStarted(const std::string& gameId)
{
    m_http.Get("/api/Games/" + gameId + "/Started");
}

void GameClient::NotifyStopped(const std::string& gameId)
{
    m_http.Get("/api/Games/" + gameId + "/Stopped");
}

bool GameClient::GetAddons(const std::string& gameId,
                           std::vector<GameSummary>* out, std::string* errorOut)
{
    HttpResponse resp = m_http.Get("/api/Games/" + gameId + "/Addons");
    if (!resp.ok())
    {
        if (errorOut)
        {
            std::ostringstream e;
            e << "GetAddons failed (HTTP " << resp.status << ")";
            *errorOut = e.str();
        }
        return false;
    }
    cJSON* root = cJSON_Parse(resp.body.c_str());
    if (!root || root->type != cJSON_Array)
    {
        if (root) cJSON_Delete(root);
        if (errorOut) *errorOut = "GetAddons: expected JSON array";
        return false;
    }
    int n = cJSON_GetArraySize(root);
    for (int i = 0; i < n; ++i)
    {
        cJSON* item = cJSON_GetArrayItem(root, i);
        if (!item) continue;
        GameSummary g;
        g.id    = GetEither(item, "id", "Id");
        g.title = GetEither(item, "title", "Title");
        if (!g.id.empty()) out->push_back(g);
    }
    cJSON_Delete(root);
    return true;
}

bool GameClient::GetRedistributables(const std::string& gameId,
                                     std::vector<RedistributableSummary>* out,
                                     std::string* errorOut)
{
    HttpResponse resp = m_http.Get("/api/Games/" + gameId);
    if (!resp.ok())
    {
        if (errorOut)
        {
            std::ostringstream e;
            e << "GetRedistributables failed (HTTP " << resp.status << ")";
            *errorOut = e.str();
        }
        return false;
    }
    cJSON* root = cJSON_Parse(resp.body.c_str());
    if (!root)
    {
        if (errorOut) *errorOut = "GetRedistributables: invalid JSON";
        return false;
    }
    cJSON* arr = cJSON_GetObjectItem(root, "redistributables");
    if (!arr) arr = cJSON_GetObjectItem(root, "Redistributables");
    if (arr && arr->type == cJSON_Array)
    {
        int n = cJSON_GetArraySize(arr);
        for (int i = 0; i < n; ++i)
        {
            cJSON* item = cJSON_GetArrayItem(arr, i);
            if (!item) continue;
            RedistributableSummary r;
            r.id          = GetEither(item, "id", "Id");
            r.name        = GetEither(item, "name", "Name");
            r.description = GetEither(item, "description", "Description");
            if (!r.id.empty()) out->push_back(r);
        }
    }
    cJSON_Delete(root);
    return true;
}
