#ifndef LANCOMMANDER_WIN9X_AUTH_CLIENT_H
#define LANCOMMANDER_WIN9X_AUTH_CLIENT_H

#include <string>

#include "http_client.h"

struct AuthTokens
{
    std::string accessToken;
    std::string refreshToken;
    std::string expiration;
};

// Mirrors LANCommander.SDK.Clients.AuthenticationClient. Owns the bearer
// token state on the shared HttpClient: Login() sets it, Logout()/failures
// clear it. Refresh() rotates the token in place.
class AuthenticationClient
{
public:
    explicit AuthenticationClient(HttpClient& http);

    bool Login(const std::string& username, const std::string& password,
               AuthTokens* tokensOut, std::string* errorOut);
    bool Validate(std::string* errorOut);
    bool Refresh(const AuthTokens& in, AuthTokens* out, std::string* errorOut);
    void Logout();

private:
    HttpClient& m_http;
};

#endif
