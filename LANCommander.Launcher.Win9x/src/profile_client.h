#ifndef LANCOMMANDER_WIN9X_PROFILE_CLIENT_H
#define LANCOMMANDER_WIN9X_PROFILE_CLIENT_H

#include <string>

#include "http_client.h"

// Mirrors a slice of LANCommander.SDK.Clients.ProfileClient. The Win9x
// launcher needs only the player's alias (for the NameChange script) and a
// way to push a new one back to the server.
class ProfileClient
{
public:
    explicit ProfileClient(HttpClient& http) : m_http(http) {}

    // GETs /api/Profile and pulls out Alias (falls back to UserName if Alias
    // is empty, matching the SDK's GetAliasAsync behaviour).
    bool GetAlias(std::string* aliasOut, std::string* errorOut);

    // PUTs /api/Profile/ChangeAlias with a JSON body { Alias: "..." }.
    bool ChangeAlias(const std::string& alias, std::string* errorOut);

private:
    HttpClient& m_http;
};

#endif
