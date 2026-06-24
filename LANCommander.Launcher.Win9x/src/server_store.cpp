#include "server_store.h"

#include "cJSON.h"

#include <windows.h>
#include <cstdio>

namespace
{
    std::string GetJsonString(cJSON* obj, const char* key)
    {
        cJSON* n = cJSON_GetObjectItem(obj, key);
        if (n && n->type == cJSON_String && n->valuestring)
            return std::string(n->valuestring);
        return std::string();
    }
}

std::string ServerStore::FilePath() const
{
    char buf[MAX_PATH];
    DWORD n = GetModuleFileNameA(NULL, buf, MAX_PATH);
    std::string path = (n == 0) ? std::string(".") : std::string(buf, n);
    size_t slash = path.find_last_of("\\/");
    if (slash != std::string::npos) path = path.substr(0, slash);
    return path + "\\servers.json";
}

void ServerStore::Load()
{
    m_entries.clear();
    FILE* f = fopen(FilePath().c_str(), "rb");
    if (!f) return;
    fseek(f, 0, SEEK_END);
    long sz = ftell(f);
    fseek(f, 0, SEEK_SET);
    std::string body;
    if (sz > 0)
    {
        body.resize((size_t)sz);
        size_t got = fread(&body[0], 1, (size_t)sz, f);
        if (got != (size_t)sz) body.clear();
    }
    fclose(f);
    if (body.empty()) return;

    cJSON* root = cJSON_Parse(body.c_str());
    if (!root || root->type != cJSON_Array) { if (root) cJSON_Delete(root); return; }
    int count = cJSON_GetArraySize(root);
    for (int i = 0; i < count; ++i)
    {
        cJSON* item = cJSON_GetArrayItem(root, i);
        if (!item || item->type != cJSON_Object) continue;
        ServerBookmark b;
        b.name         = GetJsonString(item, "name");
        b.url          = GetJsonString(item, "url");
        b.userName     = GetJsonString(item, "userName");
        b.accessToken  = GetJsonString(item, "accessToken");
        b.refreshToken = GetJsonString(item, "refreshToken");
        b.expiration   = GetJsonString(item, "expiration");
        if (!b.url.empty()) m_entries.push_back(b);
    }
    cJSON_Delete(root);
}

void ServerStore::Save()
{
    cJSON* root = cJSON_CreateArray();
    for (size_t i = 0; i < m_entries.size(); ++i)
    {
        const ServerBookmark& b = m_entries[i];
        cJSON* item = cJSON_CreateObject();
        cJSON_AddStringToObject(item, "name",         b.name.c_str());
        cJSON_AddStringToObject(item, "url",          b.url.c_str());
        cJSON_AddStringToObject(item, "userName",     b.userName.c_str());
        cJSON_AddStringToObject(item, "accessToken",  b.accessToken.c_str());
        cJSON_AddStringToObject(item, "refreshToken", b.refreshToken.c_str());
        cJSON_AddStringToObject(item, "expiration",   b.expiration.c_str());
        cJSON_AddItemToArray(root, item);
    }

    char* text = cJSON_PrintUnformatted(root);
    cJSON_Delete(root);
    if (!text) return;

    FILE* f = fopen(FilePath().c_str(), "wb");
    if (f)
    {
        fwrite(text, 1, strlen(text), f);
        fclose(f);
    }
    cJSON_free(text);
}

const ServerBookmark* ServerStore::Find(const std::string& url) const
{
    for (size_t i = 0; i < m_entries.size(); ++i)
        if (m_entries[i].url == url) return &m_entries[i];
    return NULL;
}

size_t ServerStore::Upsert(const ServerBookmark& entry)
{
    for (size_t i = 0; i < m_entries.size(); ++i)
    {
        if (m_entries[i].url == entry.url)
        {
            m_entries[i] = entry;
            return i;
        }
    }
    m_entries.push_back(entry);
    return m_entries.size() - 1;
}

void ServerStore::Remove(const std::string& url)
{
    for (size_t i = 0; i < m_entries.size(); ++i)
    {
        if (m_entries[i].url == url)
        {
            m_entries.erase(m_entries.begin() + i);
            return;
        }
    }
}

void ServerStore::Clear()
{
    m_entries.clear();
}
