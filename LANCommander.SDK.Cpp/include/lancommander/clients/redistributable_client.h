#ifndef LANCOMMANDER_CLIENTS_REDISTRIBUTABLE_CLIENT_H
#define LANCOMMANDER_CLIENTS_REDISTRIBUTABLE_CLIENT_H

#include <string>

#include "../http/http_client.h"
#include "../models/redistributable.h"
#include "../types.h"

namespace lancommander {

class RedistributableClient {
public:
    explicit RedistributableClient(IHttpClient& http);

    Result<bool> download(const std::string& redist_id, const std::string& dest_path,
                          DownloadProgressFn progress = nullptr);

private:
    IHttpClient& m_http;
};

} // namespace lancommander

#endif // LANCOMMANDER_CLIENTS_REDISTRIBUTABLE_CLIENT_H
