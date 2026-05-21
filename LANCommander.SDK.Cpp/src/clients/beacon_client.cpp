#include "lancommander/clients/beacon_client.h"
#include "../json/json_helpers.h"

#include <cstring>

#ifdef _WIN32
    #include <winsock2.h>
    #include <ws2tcpip.h>
    #pragma comment(lib, "ws2_32.lib")
    using socket_t = SOCKET;
    #define INVALID_SOCK INVALID_SOCKET
    #define CLOSE_SOCKET closesocket
#else
    #include <sys/socket.h>
    #include <sys/select.h>
    #include <netinet/in.h>
    #include <arpa/inet.h>
    #include <unistd.h>
    #include <sys/time.h>
    using socket_t = int;
    #define INVALID_SOCK (-1)
    #define CLOSE_SOCKET close
#endif

namespace lancommander {

namespace {

#ifdef _WIN32
struct WinsockInit {
    bool ok;
    WinsockInit() {
        WSADATA wsa;
        ok = (WSAStartup(MAKEWORD(2, 2), &wsa) == 0);
    }
    ~WinsockInit() {
        if (ok) WSACleanup();
    }
};
#endif

} // anonymous namespace

Result<std::vector<DiscoveredServer>> BeaconClient::discover(unsigned int timeout_ms, int port)
{
#ifdef _WIN32
    WinsockInit wsa;
    if (!wsa.ok)
        return Result<std::vector<DiscoveredServer>>::fail("Winsock startup failed");
#endif

    socket_t s = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP);
    if (s == INVALID_SOCK)
        return Result<std::vector<DiscoveredServer>>::fail("socket() failed");

    int broadcast_on = 1;
    setsockopt(s, SOL_SOCKET, SO_BROADCAST,
               reinterpret_cast<const char*>(&broadcast_on), sizeof(broadcast_on));

    sockaddr_in local;
    memset(&local, 0, sizeof(local));
    local.sin_family      = AF_INET;
    local.sin_addr.s_addr = htonl(INADDR_ANY);
    local.sin_port        = 0;
    if (bind(s, reinterpret_cast<sockaddr*>(&local), sizeof(local)) != 0) {
        CLOSE_SOCKET(s);
        return Result<std::vector<DiscoveredServer>>::fail("bind() failed");
    }

    sockaddr_in dest;
    memset(&dest, 0, sizeof(dest));
    dest.sin_family      = AF_INET;
    dest.sin_addr.s_addr = htonl(INADDR_BROADCAST);
    dest.sin_port        = htons(static_cast<uint16_t>(port));

    const char* probe = "LANCommanderProbe";
    int probe_len = static_cast<int>(strlen(probe));

    if (sendto(s, probe, probe_len, 0,
               reinterpret_cast<sockaddr*>(&dest), sizeof(dest)) < 0) {
        CLOSE_SOCKET(s);
        return Result<std::vector<DiscoveredServer>>::fail("sendto() failed");
    }

    std::vector<DiscoveredServer> servers;
    char buf[4096];

    // Collect replies until timeout
#ifdef _WIN32
    DWORD start = GetTickCount();
#else
    struct timeval tv_start;
    gettimeofday(&tv_start, nullptr);
#endif

    while (true) {
        unsigned int elapsed;
#ifdef _WIN32
        elapsed = static_cast<unsigned int>(GetTickCount() - start);
#else
        struct timeval tv_now;
        gettimeofday(&tv_now, nullptr);
        elapsed = static_cast<unsigned int>(
            (tv_now.tv_sec - tv_start.tv_sec) * 1000 +
            (tv_now.tv_usec - tv_start.tv_usec) / 1000);
#endif
        if (elapsed >= timeout_ms) break;
        unsigned int remaining = timeout_ms - elapsed;

        struct timeval tv;
        tv.tv_sec  = remaining / 1000;
        tv.tv_usec = (remaining % 1000) * 1000;

        fd_set rs;
        FD_ZERO(&rs);
        FD_SET(s, &rs);
        int rv = select(static_cast<int>(s) + 1, &rs, nullptr, nullptr, &tv);
        if (rv <= 0) break;

        sockaddr_in from;
        socklen_t from_len = sizeof(from);
        int n = recvfrom(s, buf, sizeof(buf) - 1, 0,
                         reinterpret_cast<sockaddr*>(&from), &from_len);
        if (n <= 0) continue;
        buf[n] = '\0';

        DiscoveredServer srv;
        srv.remote_ip = inet_ntoa(from.sin_addr);

        json::JsonDoc doc(std::string(buf, n));
        if (doc) {
            srv = json::parse_discovered_server(doc.root);
            srv.remote_ip = inet_ntoa(from.sin_addr);
        }

        // De-dup by address or remote IP
        std::string key = srv.address.empty() ? srv.remote_ip : srv.address;
        bool dup = false;
        for (const auto& existing : servers) {
            std::string ek = existing.address.empty() ? existing.remote_ip : existing.address;
            if (ek == key) { dup = true; break; }
        }
        if (!dup) servers.push_back(std::move(srv));
    }

    CLOSE_SOCKET(s);
    return Result<std::vector<DiscoveredServer>>::ok(std::move(servers));
}

} // namespace lancommander
