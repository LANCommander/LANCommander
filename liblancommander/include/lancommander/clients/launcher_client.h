#ifndef LANCOMMANDER_CLIENTS_LAUNCHER_CLIENT_H
#define LANCOMMANDER_CLIENTS_LAUNCHER_CLIENT_H

#include <string>

#include "../http/http_client.h"
#include "../models/update_info.h"
#include "../types.h"

namespace lancommander {

class LauncherClient {
public:
    explicit LauncherClient(IHttpClient& http);

    Result<CheckForUpdateResponse> check_for_update();
    Result<bool> download(const std::string& dest_path,
                          DownloadProgressFn progress = nullptr);

private:
    IHttpClient& m_http;
};

} // namespace lancommander

#endif // LANCOMMANDER_CLIENTS_LAUNCHER_CLIENT_H
