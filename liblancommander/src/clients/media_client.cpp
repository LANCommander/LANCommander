#include "lancommander/clients/media_client.h"
#include "../json/json_helpers.h"

#include <sstream>

namespace lancommander {

MediaClient::MediaClient(IHttpClient& http) : m_http(http) {}

Result<bool> MediaClient::download_thumbnail(const std::string& media_id,
                                             const std::string& dest_path)
{
    if (!m_http.download("/api/Media/" + media_id + "/Thumbnail", dest_path))
        return Result<bool>::fail("Thumbnail download failed");
    return Result<bool>::ok(true);
}

Result<bool> MediaClient::download(const std::string& media_id,
                                   const std::string& dest_path,
                                   DownloadProgressFn progress)
{
    if (!m_http.download("/api/Media/" + media_id + "/Download", dest_path, progress))
        return Result<bool>::fail("Media download failed");
    return Result<bool>::ok(true);
}

Result<std::vector<MediaRef>> MediaClient::get_for_game(const std::string& game_id)
{
    HttpResponse resp = m_http.get("/api/Games/" + game_id);
    if (!resp.ok()) {
        std::ostringstream e;
        e << "GetGameMedia failed (HTTP " << resp.status_code << ")";
        return Result<std::vector<MediaRef>>::fail(e.str());
    }

    json::JsonDoc doc(resp.body);
    if (!doc) return Result<std::vector<MediaRef>>::fail("Invalid JSON response");

    std::vector<MediaRef> refs;
    cJSON* media = json::get_child(doc.root, "media", "Media");
    if (media && media->type == cJSON_Array) {
        int n = cJSON_GetArraySize(media);
        for (int i = 0; i < n; ++i) {
            cJSON* m = cJSON_GetArrayItem(media, i);
            if (!m) continue;
            MediaRef r = json::parse_media_ref(m);
            if (!r.id.empty()) refs.push_back(std::move(r));
        }
    }
    return Result<std::vector<MediaRef>>::ok(std::move(refs));
}

} // namespace lancommander
