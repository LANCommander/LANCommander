#ifndef LANCOMMANDER_CLIENTS_PLAY_SESSION_CLIENT_H
#define LANCOMMANDER_CLIENTS_PLAY_SESSION_CLIENT_H

#include <string>
#include <vector>

#include "../http/http_client.h"
#include "../models/library.h"
#include "../models/play_session.h"
#include "../types.h"

namespace lancommander {

class PlaySessionClient {
public:
    explicit PlaySessionClient(IHttpClient& http);

    Result<std::vector<EntityReference>> get();
    Result<std::vector<PlaySession>> get_for_game(const std::string& game_id);

private:
    IHttpClient& m_http;
};

} // namespace lancommander

#endif // LANCOMMANDER_CLIENTS_PLAY_SESSION_CLIENT_H
