#ifndef LANCOMMANDER_WIN9X_LIBRARY_CLIENT_H
#define LANCOMMANDER_WIN9X_LIBRARY_CLIENT_H

#include <string>

#include "http_client.h"

// Mirrors LANCommander.SDK.Clients.LibraryClient.
class LibraryClient
{
public:
    explicit LibraryClient(HttpClient& http) : m_http(http) {}

    bool AddToLibrary(const std::string& gameId, std::string* errorOut);
    bool RemoveFromLibrary(const std::string& gameId, std::string* errorOut);

private:
    HttpClient& m_http;
};

#endif
