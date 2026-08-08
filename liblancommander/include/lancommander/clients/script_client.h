#ifndef LANCOMMANDER_CLIENTS_SCRIPT_CLIENT_H
#define LANCOMMANDER_CLIENTS_SCRIPT_CLIENT_H

#include <string>
#include <vector>

#include "../http/http_client.h"
#include "../models/script.h"
#include "../types.h"

namespace lancommander {

class ScriptClient {
public:
    explicit ScriptClient(IHttpClient& http);

    Result<std::vector<Script>> get_game_scripts(const std::string& game_id);
    Result<std::vector<Script>> get_redistributable_scripts(const std::string& redist_id);

private:
    Result<std::vector<Script>> fetch_and_parse(const std::string& route);

    IHttpClient& m_http;
};

} // namespace lancommander

#endif // LANCOMMANDER_CLIENTS_SCRIPT_CLIENT_H
