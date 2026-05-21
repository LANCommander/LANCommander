#ifndef LANCOMMANDER_CLIENTS_AUTHENTICATION_CLIENT_H
#define LANCOMMANDER_CLIENTS_AUTHENTICATION_CLIENT_H

#include <string>
#include <vector>

#include "../http/http_client.h"
#include "../models/auth.h"
#include "../types.h"

namespace lancommander {

class AuthenticationClient {
public:
    explicit AuthenticationClient(IHttpClient& http);

    Result<AuthToken> login(const std::string& username, const std::string& password);
    Result<bool> validate();
    Result<AuthToken> refresh(const AuthToken& current);
    void logout();

    Result<std::vector<AuthenticationProvider>> get_providers();

private:
    IHttpClient& m_http;
};

} // namespace lancommander

#endif // LANCOMMANDER_CLIENTS_AUTHENTICATION_CLIENT_H
