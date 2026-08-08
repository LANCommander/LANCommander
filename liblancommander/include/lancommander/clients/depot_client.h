#ifndef LANCOMMANDER_CLIENTS_DEPOT_CLIENT_H
#define LANCOMMANDER_CLIENTS_DEPOT_CLIENT_H

#include <string>

#include "../http/http_client.h"
#include "../models/depot.h"
#include "../types.h"

namespace lancommander {

class DepotClient {
public:
    explicit DepotClient(IHttpClient& http);

    Result<DepotResults> get();
    Result<DepotGame> get_game(const std::string& game_id);

private:
    IHttpClient& m_http;
};

} // namespace lancommander

#endif // LANCOMMANDER_CLIENTS_DEPOT_CLIENT_H
