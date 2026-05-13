#ifndef LANCOMMANDER_WIN9X_KEY_CLIENT_H
#define LANCOMMANDER_WIN9X_KEY_CLIENT_H

#include <string>

#include "http_client.h"

// Mirrors LANCommander.SDK.Clients.GameClient's Key endpoints. We split them
// out into their own client to keep GameClient focused on game-shaped
// resources. Server routes live under /api/Keys.
class KeyClient
{
public:
    explicit KeyClient(HttpClient& http) : m_http(http) {}

    // Returns the key already allocated to this machine for the given game
    // (or an empty string if there is none). Identifies the machine via
    // ComputerName/IP — the Win9x launcher does not send a MAC address (the
    // NetBIOS path is non-trivial and the server treats MAC as optional).
    bool GetAllocated(const std::string& gameId, std::string* keyOut,
                      std::string* errorOut);

    // Asks the server to allocate (or rotate to) a fresh key for this
    // machine + game pair. Returns the new key value.
    bool Allocate(const std::string& gameId, std::string* keyOut,
                  std::string* errorOut);

private:
    bool PostKeyRequest(const std::string& route, const std::string& gameId,
                        std::string* keyOut, std::string* errorOut);

    HttpClient& m_http;
};

#endif
