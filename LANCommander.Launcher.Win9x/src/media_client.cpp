#include "media_client.h"

#include "cJSON.h"

#include <cstdio>
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
}

bool MediaClient::DownloadThumbnail(const std::string& mediaId,
                                    const std::string& destPath,
                                    std::string* errorOut)
{
    FILE* f = fopen(destPath.c_str(), "wb");
    if (!f)
    {
        if (errorOut) *errorOut = "Could not open destination file";
        return false;
    }
    long status = m_http.Download("/api/Media/" + mediaId + "/Thumbnail",
                                  f, NULL, NULL);
    fclose(f);
    if (status < 200 || status >= 300)
    {
        ::remove(destPath.c_str());
        if (errorOut)
        {
            std::ostringstream e;
            e << "Thumbnail download failed (HTTP " << status << ")";
            *errorOut = e.str();
        }
        return false;
    }
    return true;
}

bool MediaClient::GetMediaForGame(const std::string& gameId,
                                  std::vector<MediaRef>* out,
                                  std::string* errorOut)
{
    HttpResponse resp = m_http.Get("/api/Games/" + gameId);
    if (!resp.ok())
    {
        if (errorOut)
        {
            std::ostringstream e;
            e << "GetGameMedia failed (HTTP " << resp.status << ")";
            *errorOut = e.str();
        }
        return false;
    }
    cJSON* root = cJSON_Parse(resp.body.c_str());
    if (!root)
    {
        if (errorOut) *errorOut = "GetGameMedia: invalid JSON";
        return false;
    }
    cJSON* media = cJSON_GetObjectItem(root, "media");
    if (!media) media = cJSON_GetObjectItem(root, "Media");
    if (media && media->type == cJSON_Array)
    {
        int n = cJSON_GetArraySize(media);
        for (int i = 0; i < n; ++i)
        {
            cJSON* m = cJSON_GetArrayItem(media, i);
            if (!m) continue;
            MediaRef r;
            r.id = GetJsonString(m, "id");
            if (r.id.empty()) r.id = GetJsonString(m, "Id");
            if (r.id.empty()) continue;

            cJSON* t = cJSON_GetObjectItem(m, "type");
            if (!t) t = cJSON_GetObjectItem(m, "Type");
            if (t && t->type == cJSON_String && t->valuestring)
                r.type = t->valuestring;
            else if (t && t->type == cJSON_Number)
            {
                static const char* kNames[] = {
                    "Icon", "Cover", "Background", "Avatar", "Logo",
                    "Manual", "Thumbnail", "PageImage", "Grid",
                    "Screenshot", "Video"
                };
                int v = t->valueint;
                if (v >= 0 && v < (int)(sizeof(kNames) / sizeof(kNames[0])))
                    r.type = kNames[v];
            }

            r.crc32 = GetJsonString(m, "crc32");
            if (r.crc32.empty()) r.crc32 = GetJsonString(m, "Crc32");
            out->push_back(r);
        }
    }
    cJSON_Delete(root);
    return true;
}
