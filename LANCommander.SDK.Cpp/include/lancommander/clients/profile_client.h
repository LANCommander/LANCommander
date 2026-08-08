#ifndef LANCOMMANDER_CLIENTS_PROFILE_CLIENT_H
#define LANCOMMANDER_CLIENTS_PROFILE_CLIENT_H

#include <string>
#include <vector>

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

    // The avatar's bytes, rather than writing them to a file. Backs the
    // Out-PlayerAvatar cmdlet, which hands them to a script.
    Result<std::vector<unsigned char> > get_avatar();

    // Per-user custom fields, as used by the Get-UserCustomField and
    // Update-UserCustomField cmdlets. A field that was never set comes back as
    // an empty string rather than an error.
    Result<std::string> get_custom_field(const std::string& name);
    Result<std::string> update_custom_field(const std::string& name,
                                            const std::string& value);

private:
    IHttpClient& m_http;
};

} // namespace lancommander

#endif // LANCOMMANDER_CLIENTS_PROFILE_CLIENT_H
