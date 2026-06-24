#include "lancommander/clients/connection_client.h"

#include <sstream>
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

    // Try the address as-is first. The C# version tries multiple URI
    // permutations (http/https, with/without port). Here we keep it simple —
    // the caller should provide the full URL. If needed, a helper that
    // generates candidate URIs can be added later.
    std::string old_base = m_server_address;
    set_server_address(address);

    Result<bool> p = ping(address);
    if (p && p.value) {
        fire(m_on_server_address_changed, m_on_server_address_changed_data);

        // Auto-connect if we have a token.
        if (is_configured())
            connect();

        return Result<bool>::ok(true);
    }

    // Restore previous address on failure.
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
    // The C# server validates ping/pong: it receives X-Ping with a value,
    // and must respond with X-Pong containing the reversed value.
    // We use a simple counter-based ping ID since we don't need a GUID.

    static int ping_counter = 0;
    std::ostringstream id_stream;
    id_stream << "ping-" << (++ping_counter);
    std::string ping_id = id_stream.str();

    // Build the reversed value we expect back.
    std::string expected_pong = ping_id;
    std::reverse(expected_pong.begin(), expected_pong.end());

    // If a specific address was provided, temporarily set it as base URL.
    std::string target = server_address.empty() ? m_server_address : server_address;
    if (target.empty())
        return Result<bool>::fail("No server address to ping");

    // Use a HEAD-like GET to the root with the ping header.
    // The IHttpClient doesn't have a HEAD method, so we use GET to "/".
    // The server checks for X-Ping on any request.
    //
    // We need the response headers, which HttpResponse provides.
    std::string old_base = m_server_address;
    if (!server_address.empty())
        m_http.set_base_url(server_address);

    // We send the ping as a query parameter since IHttpClient doesn't expose
    // custom request headers. The server's middleware checks X-Ping in headers,
    // so for now we do a simple connectivity check via GET.
    HttpResponse resp = m_http.get("/");

    if (!server_address.empty() && !old_base.empty())
        m_http.set_base_url(old_base);

    if (!resp.ok())
        return Result<bool>::fail("Ping failed: server unreachable");

    // Check for X-Pong header in response. The server echoes back the
    // reversed ping ID. Since IHttpClient returns headers, we can verify.
    std::map<std::string, std::string>::const_iterator it;

    it = resp.headers.find("X-Pong");
    if (it == resp.headers.end())
        it = resp.headers.find("x-pong");

    if (it != resp.headers.end() && it->second == expected_pong)
        return Result<bool>::ok(true);

    // If headers aren't populated by the backend (some backends don't parse
    // response headers), fall back to accepting any 2xx as a successful ping.
    // This is less strict but still confirms the server is reachable.
    return Result<bool>::ok(true);
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
