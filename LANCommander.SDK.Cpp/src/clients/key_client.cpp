#include "lancommander/clients/key_client.h"
#include "../json/json_helpers.h"

#include <sstream>

namespace lancommander {

KeyClient::KeyClient(IHttpClient& http, IMachineInfo& machine)
    : m_http(http), m_machine(machine) {}

Result<std::string> KeyClient::post_key_request(const std::string& route,
                                                const std::string& game_id)
{
    cJSON* req = cJSON_CreateObject();
    cJSON_AddStringToObject(req, "GameId",       game_id.c_str());
    cJSON_AddStringToObject(req, "MacAddress",   m_machine.get_mac_address().c_str());
    cJSON_AddStringToObject(req, "IpAddress",    m_machine.get_ip_address().c_str());
    cJSON_AddStringToObject(req, "ComputerName", m_machine.get_computer_name().c_str());
    char* body = cJSON_PrintUnformatted(req);
    std::string payload = body ? body : "{}";
    cJSON_Delete(req);
    cJSON_free(body);

    HttpResponse resp = m_http.post(route, payload);
    if (!resp.ok()) {
        std::ostringstream e;
        e << "Key request failed (HTTP " << resp.status_code << ")";
        return Result<std::string>::fail(e.str());
    }

    json::JsonDoc doc(resp.body);
    if (!doc) return Result<std::string>::ok("");

    std::string value = json::get_string(doc.root, "value", "Value");
    return Result<std::string>::ok(std::move(value));
}

Result<std::string> KeyClient::get_allocated(const std::string& game_id)
{
    return post_key_request("/api/Keys/GetAllocated/" + game_id, game_id);
}

Result<std::string> KeyClient::allocate(const std::string& game_id)
{
    return post_key_request("/api/Keys/Allocate/" + game_id, game_id);
}

} // namespace lancommander
