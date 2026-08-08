#ifndef LANCOMMANDER_CLIENTS_LIBRARY_CLIENT_H
#define LANCOMMANDER_CLIENTS_LIBRARY_CLIENT_H

#include <string>
#include <vector>

#include "../http/http_client.h"
#include "../models/library.h"
#include "../types.h"

namespace lancommander {

class LibraryClient {
public:
    explicit LibraryClient(IHttpClient& http);

    Result<std::vector<EntityReference>> get();
    Result<bool> add(const std::string& game_id);
    Result<bool> remove(const std::string& game_id);

private:
    IHttpClient& m_http;
};

} // namespace lancommander

#endif // LANCOMMANDER_CLIENTS_LIBRARY_CLIENT_H
