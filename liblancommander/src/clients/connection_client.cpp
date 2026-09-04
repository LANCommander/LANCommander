#include "lancommander/clients/connection_client.h"

#include <sstream>
#include <map>
#include <vector>

#include "lancommander/util/uri.h"
#include <algorithm>

namespace lancommander {

ConnectionClient::ConnectionClient(IHttpClient& http)
    : m_http(http)
    , m_connected(false)
    , m_offline_mode(false)
    , m_on_connect(0), m_on_connect_data(0)
    , m_on_disconnect(0), m_on_disconnect_data(0)
    , m_on_server_address_changed(0), m_on_server_address_changed_data(0)
    , m_on_offline_mode_enabled(0), m_on_offline_mode_enabled_data(0)
{
}

// ---------------------------------------------------------------------------
// State queries
// ---------------------------------------------------------------------------

bool ConnectionClient::is_connected() const { return m_connected; }

bool ConnectionClient::is_configured() const
{
    return has_server_address() && !m_access_token.empty();
}

bool ConnectionClient::is_offline_mode() const { return m_offline_mode; }

bool ConnectionClient::has_server_address() const { return !m_server_address.empty(); }

std::string ConnectionClient::get_server_address() const { return m_server_address; }

std::string ConnectionClient::get_access_token() const { return m_access_token; }

// ---------------------------------------------------------------------------
// State mutations
// ---------------------------------------------------------------------------

void ConnectionClient::set_server_address(const std::string& address)
{
    m_server_address = address;
    m_http.set_base_url(address);
}

void ConnectionClient::set_access_token(const std::string& token)
{
    m_access_token = token;
    m_http.set_bearer_token(token);
}

Result<bool> ConnectionClient::update_server_address(const std::string& address)
{
    if (address.empty())
        return Result<bool>::fail("Server address cannot be blank");

    const std::vector<std::string> candidates = suggest_probe_uris(address);
    if (candidates.empty())
        return Result<bool>::fail("Server address cannot be blank");

    const std::string old_base = m_server_address;

    for (size_t i = 0; i < candidates.size(); ++i) {
        Result<bool> p = ping(candidates[i]);
        if (!p || !p.value)
            continue;

        set_server_address(candidates[i]);
        fire(m_on_server_address_changed, m_on_server_address_changed_data);

        if (is_configured())
            connect();

        return Result<bool>::ok(true);
    }

    if (!old_base.empty())
        set_server_address(old_base);

    return Result<bool>::fail("Could not find a server at that address");
}

Result<bool> ConnectionClient::connect()
{
    if (!is_configured())
        return Result<bool>::fail("Client is not configured (missing address or token)");

    m_connected = true;
    m_offline_mode = false;
    fire(m_on_connect, m_on_connect_data);
    return Result<bool>::ok(true);
}

Result<bool> ConnectionClient::disconnect()
{
    m_connected = false;
    fire(m_on_disconnect, m_on_disconnect_data);
    return Result<bool>::ok(true);
}

void ConnectionClient::enable_offline_mode()
{
    disconnect();
    m_offline_mode = true;
    fire(m_on_offline_mode_enabled, m_on_offline_mode_enabled_data);
}

void ConnectionClient::disable_offline_mode()
{
    m_offline_mode = false;
}

Result<bool> ConnectionClient::ping(const std::string& server_address)
{
    const std::string target = server_address.empty() ? m_server_address : server_address;
    if (target.empty())
        return Result<bool>::fail("No server address to ping");

    static int ping_counter = 0;
    std::ostringstream id_stream;
    id_stream << "ping-" << (++ping_counter);
    const std::string ping_id = id_stream.str();

    std::string expected_pong = ping_id;
    std::reverse(expected_pong.begin(), expected_pong.end());

    const std::string old_base = m_server_address;
    if (!server_address.empty())
        m_http.set_base_url(server_address);

    std::map<std::string, std::string> headers;
    headers["X-Ping"] = ping_id;

    HttpResponse resp = m_http.head("/", headers);

    if (!server_address.empty() && !old_base.empty())
        m_http.set_base_url(old_base);

    if (!resp.ok())
        return Result<bool>::fail("Ping failed: server unreachable");

    // read_headers lowercases keys, but a backend that does not is still
    // allowed to exist, so check both spellings before giving up.
    std::map<std::string, std::string>::const_iterator it = resp.headers.find("x-pong");
    if (it == resp.headers.end())
        it = resp.headers.find("X-Pong");

    if (it == resp.headers.end())
        return Result<bool>::fail("Not a LANCommander server (no X-Pong header)");

    if (it->second != expected_pong)
        return Result<bool>::fail("Not a LANCommander server (X-Pong mismatch)");

    return Result<bool>::ok(true);
}

Result<bool> ConnectionClient::probe_candidate(const std::string& uri)
{
    if (uri.empty())
        return Result<bool>::fail("Empty candidate");

    return ping(uri);
}

// ---------------------------------------------------------------------------
// Event callbacks
// ---------------------------------------------------------------------------

void ConnectionClient::set_on_connect(ConnectionEventFn fn, void* user_data)
{
    m_on_connect = fn;
    m_on_connect_data = user_data;
}

void ConnectionClient::set_on_disconnect(ConnectionEventFn fn, void* user_data)
{
    m_on_disconnect = fn;
    m_on_disconnect_data = user_data;
}

void ConnectionClient::set_on_server_address_changed(ConnectionEventFn fn, void* user_data)
{
    m_on_server_address_changed = fn;
    m_on_server_address_changed_data = user_data;
}

void ConnectionClient::set_on_offline_mode_enabled(ConnectionEventFn fn, void* user_data)
{
    m_on_offline_mode_enabled = fn;
    m_on_offline_mode_enabled_data = user_data;
}

void ConnectionClient::fire(ConnectionEventFn fn, void* data)
{
    if (fn) fn(data);
}

} // namespace lancommander
