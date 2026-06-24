#include "discovery.h"

#include <winsock.h>

#include "cJSON.h"

#include <cstdio>
#include <cstring>

namespace
{
    const int kBeaconPort = 35891;
    bool g_winsockReady = false;

    std::string GetJsonStringEither(cJSON* obj, const char* k1, const char* k2)
    {
        cJSON* n = cJSON_GetObjectItem(obj, k1);
        if (!n) n = cJSON_GetObjectItem(obj, k2);
        if (n && n->type == cJSON_String && n->valuestring)
            return std::string(n->valuestring);
        return std::string();
    }
}

bool DiscoveryStartup()
{
    if (g_winsockReady) return true;
    WSADATA wsa;
    // 1.1 is enough for blocking UDP and works on stock Win95.
    if (WSAStartup(MAKEWORD(1, 1), &wsa) != 0) return false;
    g_winsockReady = true;
    return true;
}

void DiscoveryShutdown()
{
    if (!g_winsockReady) return;
    WSACleanup();
    g_winsockReady = false;
}

bool DiscoverServers(unsigned int timeoutMs,
                     std::vector<DiscoveredServer>* out,
                     std::string* errorOut)
{
    if (!DiscoveryStartup())
    {
        if (errorOut) *errorOut = "Winsock startup failed";
        return false;
    }

    SOCKET s = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP);
    if (s == INVALID_SOCKET)
    {
        if (errorOut) *errorOut = "socket() failed";
        return false;
    }

    BOOL broadcastOn = TRUE;
    setsockopt(s, SOL_SOCKET, SO_BROADCAST,
               (const char*)&broadcastOn, sizeof(broadcastOn));

    sockaddr_in local;
    memset(&local, 0, sizeof(local));
    local.sin_family      = AF_INET;
    local.sin_addr.s_addr = htonl(INADDR_ANY);
    local.sin_port        = 0;
    if (bind(s, (sockaddr*)&local, sizeof(local)) == SOCKET_ERROR)
    {
        if (errorOut) *errorOut = "bind() failed";
        closesocket(s);
        return false;
    }

    sockaddr_in dest;
    memset(&dest, 0, sizeof(dest));
    dest.sin_family      = AF_INET;
    dest.sin_addr.s_addr = htonl(INADDR_BROADCAST);
    dest.sin_port        = htons(kBeaconPort);

    // The server's probe handler doesn't validate the contents; any non-empty
    // payload triggers a reply. Send a short ASCII id so packet captures are
    // legible.
    const char* probe = "LANCommanderWin9xProbe";
    int probeLen = (int)strlen(probe);

    if (sendto(s, probe, probeLen, 0, (sockaddr*)&dest, sizeof(dest)) == SOCKET_ERROR)
    {
        if (errorOut) *errorOut = "sendto() failed";
        closesocket(s);
        return false;
    }

    DWORD start = GetTickCount();
    char buf[4096];
    while (true)
    {
        DWORD now = GetTickCount();
        DWORD elapsed = now - start; // wraps cleanly modulo 2^32
        if (elapsed >= timeoutMs) break;
        DWORD remaining = timeoutMs - elapsed;

        TIMEVAL tv;
        tv.tv_sec  = remaining / 1000;
        tv.tv_usec = (remaining % 1000) * 1000;

        fd_set rs;
        FD_ZERO(&rs);
        FD_SET(s, &rs);
        int rv = select(0, &rs, NULL, NULL, &tv);
        if (rv <= 0) break;

        sockaddr_in from;
        int fromLen = sizeof(from);
        int n = recvfrom(s, buf, sizeof(buf) - 1, 0, (sockaddr*)&from, &fromLen);
        if (n <= 0) continue;
        buf[n] = '\0';

        DiscoveredServer srv;
        srv.remoteIp = inet_ntoa(from.sin_addr);

        cJSON* root = cJSON_Parse(buf);
        if (root)
        {
            srv.address = GetJsonStringEither(root, "address", "Address");
            srv.name    = GetJsonStringEither(root, "name",    "Name");
            srv.version = GetJsonStringEither(root, "version", "Version");
            cJSON_Delete(root);
        }

        // De-dup on address (if present) or remoteIp.
        bool dup = false;
        std::string key = srv.address.empty() ? srv.remoteIp : srv.address;
        for (size_t i = 0; i < out->size(); ++i)
        {
            const DiscoveredServer& e = (*out)[i];
            std::string ek = e.address.empty() ? e.remoteIp : e.address;
            if (ek == key) { dup = true; break; }
        }
        if (!dup) out->push_back(srv);
    }

    closesocket(s);
    return true;
}
