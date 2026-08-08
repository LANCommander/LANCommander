#include "lancommander/clients/redistributable_client.h"

#include <sstream>

namespace lancommander {

RedistributableClient::RedistributableClient(IHttpClient& http) : m_http(http) {}

Result<bool> RedistributableClient::download(const std::string& redist_id,
                                             const std::string& dest_path,
                                             DownloadProgressFn progress)
{
    if (!m_http.download("/api/Redistributables/" + redist_id + "/Download",
                         dest_path, progress))
        return Result<bool>::fail("Redistributable download failed");
    return Result<bool>::ok(true);
}

} // namespace lancommander
