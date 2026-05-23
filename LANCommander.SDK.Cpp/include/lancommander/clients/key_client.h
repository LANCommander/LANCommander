#ifndef LANCOMMANDER_CLIENTS_KEY_CLIENT_H
#define LANCOMMANDER_CLIENTS_KEY_CLIENT_H

#include <string>

#include "../http/http_client.h"
#include "../types.h"

namespace lancommander {

// Provides machine identification for key allocation requests.
// Consumers must implement this to supply platform-specific info.
class IMachineInfo {
public:
    virtual ~IMachineInfo() = default;
    virtual std::string get_computer_name() = 0;
    virtual std::string get_ip_address() = 0;
    virtual std::string get_mac_address() { return ""; }
};

class KeyClient {
public:
    KeyClient(IHttpClient& http, IMachineInfo& machine);

    Result<std::string> get_allocated(const std::string& game_id);
    Result<std::string> allocate(const std::string& game_id);

private:
    Result<std::string> post_key_request(const std::string& route, const std::string& game_id);

    IHttpClient& m_http;
    IMachineInfo& m_machine;
};

} // namespace lancommander

#endif // LANCOMMANDER_CLIENTS_KEY_CLIENT_H
