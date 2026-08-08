#ifndef LANCOMMANDER_CLIENTS_CONNECTION_CLIENT_H
#define LANCOMMANDER_CLIENTS_CONNECTION_CLIENT_H

#include <string>

#include "../http/http_client.h"
#include "../types.h"

namespace lancommander {

// Callback types for connection events.
typedef void (*ConnectionEventFn)(void* user_data);

// Manages the connection lifecycle to a LANCommander server.
//
// This is a simplified port of the C# ConnectionClient. The C# version uses
// SignalR for real-time RPC — that is not yet ported, so "connected" here
// means "server is reachable and we have a valid token." An RPC layer can
// be plugged in later.
class ConnectionClient {
public:
    explicit ConnectionClient(IHttpClient& http);

    // --- State queries ---

    bool is_connected() const;
    bool is_configured() const;
    bool is_offline_mode() const;
    bool has_server_address() const;
    std::string get_server_address() const;
    std::string get_access_token() const;

    // --- State mutations ---

    // Set the server address. Validates via ping before accepting.
    // Returns true if the server was found and the address was set.
    Result<bool> update_server_address(const std::string& address);

    // Set the server address directly without validation.
    void set_server_address(const std::string& address);

    // Store an authentication token (called after login).
    void set_access_token(const std::string& token);

    // Mark the connection as established.
    // Checks that the client is configured (has address + token).
    Result<bool> connect();

    // Mark the connection as disconnected.
    Result<bool> disconnect();

    // Enable offline mode (disconnects first).
    void enable_offline_mode();

    // Disable offline mode.
    void disable_offline_mode();

    // Ping the server with X-Ping / X-Pong header validation.
    // If server_address is empty, uses the stored address.
    Result<bool> ping(const std::string& server_address = "");

    // --- Event callbacks ---
    // Set to non-null to receive notifications. user_data is passed through.

    void set_on_connect(ConnectionEventFn fn, void* user_data = 0);
    void set_on_disconnect(ConnectionEventFn fn, void* user_data = 0);
    void set_on_server_address_changed(ConnectionEventFn fn, void* user_data = 0);
    void set_on_offline_mode_enabled(ConnectionEventFn fn, void* user_data = 0);

private:
    IHttpClient& m_http;
    std::string m_server_address;
    std::string m_access_token;
    bool m_connected;
    bool m_offline_mode;

    ConnectionEventFn m_on_connect;
    void* m_on_connect_data;
    ConnectionEventFn m_on_disconnect;
    void* m_on_disconnect_data;
    ConnectionEventFn m_on_server_address_changed;
    void* m_on_server_address_changed_data;
    ConnectionEventFn m_on_offline_mode_enabled;
    void* m_on_offline_mode_enabled_data;

    void fire(ConnectionEventFn fn, void* data);
};

} // namespace lancommander

#endif // LANCOMMANDER_CLIENTS_CONNECTION_CLIENT_H
