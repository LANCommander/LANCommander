#ifndef LANCOMMANDER_CLIENTS_PROFILE_CLIENT_H
#define LANCOMMANDER_CLIENTS_PROFILE_CLIENT_H

#include <string>

#include "../http/http_client.h"
#include "../models/profile.h"
#include "../types.h"

namespace lancommander {

class ProfileClient {
public:
    explicit ProfileClient(IHttpClient& http);

    Result<User> get();
    Result<std::string> get_alias();
    Result<bool> change_alias(const std::string& alias);
    Result<bool> download_avatar(const std::string& dest_path);

private:
    IHttpClient& m_http;
};

} // namespace lancommander

#endif // LANCOMMANDER_CLIENTS_PROFILE_CLIENT_H
