#include "lancommander/clients/launcher_client.h"
#include "../json/json_helpers.h"

#include <sstream>

namespace lancommander {

LauncherClient::LauncherClient(IHttpClient& http) : m_http(http) {}

Result<CheckForUpdateResponse> LauncherClient::check_for_update()
{
    HttpResponse resp = m_http.get("/api/Launcher/CheckForUpdate");
    if (!resp.ok()) {
        std::ostringstream e;
        e << "CheckForUpdate failed (HTTP " << resp.status_code << ")";
        return Result<CheckForUpdateResponse>::fail(e.str());
    }

    json::JsonDoc doc(resp.body);
    if (!doc) return Result<CheckForUpdateResponse>::fail("Invalid JSON response");

    CheckForUpdateResponse r = json::parse_check_for_update_response(doc.root);
    return Result<CheckForUpdateResponse>::ok(std::move(r));
}

Result<bool> LauncherClient::download(const std::string& dest_path,
                                      DownloadProgressFn progress)
{
    if (!m_http.download("/api/Launcher/Download", dest_path, progress))
        return Result<bool>::fail("Launcher download failed");
    return Result<bool>::ok(true);
}

} // namespace lancommander
