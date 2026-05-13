#include "key_client.h"

#include "cJSON.h"

#include <sstream>

#include <windows.h>
#include <winsock.h>

namespace
{
    std::string GetComputerName_()
    {
        char buf[MAX_COMPUTERNAME_LENGTH + 1];
        DWORD n = sizeof(buf);
        if (!GetComputerNameA(buf, &n)) return std::string();
        return std::string(buf, n);
    }

    // First IPv4 address bound to this host (or empty if Winsock can't resolve
    // ourselves). Uses Winsock 1.1 calls so it works on plain Win9x.
    std::string GetLocalIpv4()
    {
        WSADATA wsa;
        if (WSAStartup(MAKEWORD(1, 1), &wsa) != 0) return std::string();
        char host[256];
        if (gethostname(host, sizeof(host)) != 0)
        {
            WSACleanup();
            return std::string();
        }
        struct hostent* he = gethostbyname(host);
        std::string out;
        if (he && he->h_addr_list && he->h_addr_list[0])
        {
            struct in_addr a;
            memcpy(&a, he->h_addr_list[0], sizeof(a));
            out = inet_ntoa(a);
        }
        WSACleanup();
        return out;
    }

    std::string ExtractKeyValue(const std::string& body)
    {
        cJSON* root = cJSON_Parse(body.c_str());
        if (!root) return std::string();
        cJSON* v = cJSON_GetObjectItem(root, "value");
        if (!v) v = cJSON_GetObjectItem(root, "Value");
        std::string out;
        if (v && v->type == cJSON_String && v->valuestring)
            out = v->valuestring;
        cJSON_Delete(root);
        return out;
    }
}

bool KeyClient::PostKeyRequest(const std::string& route,
                               const std::string& gameId,
                               std::string* keyOut, std::string* errorOut)
{
    cJSON* req = cJSON_CreateObject();
    cJSON_AddStringToObject(req, "GameId",       gameId.c_str());
    cJSON_AddStringToObject(req, "MacAddress",   "");
    cJSON_AddStringToObject(req, "IpAddress",    GetLocalIpv4().c_str());
    cJSON_AddStringToObject(req, "ComputerName", GetComputerName_().c_str());
    char* body = cJSON_PrintUnformatted(req);
    std::string payload = body ? body : "{}";
    cJSON_Delete(req);
    cJSON_free(body);

    HttpResponse resp = m_http.PostJson(route, payload);
    if (!resp.ok())
    {
        if (errorOut)
        {
            std::ostringstream e;
            e << "Key request failed (HTTP " << resp.status << ")";
            *errorOut = e.str();
        }
        return false;
    }
    if (keyOut) *keyOut = ExtractKeyValue(resp.body);
    return true;
}

bool KeyClient::GetAllocated(const std::string& gameId, std::string* keyOut,
                             std::string* errorOut)
{
    return PostKeyRequest("/api/Keys/GetAllocated/" + gameId, gameId,
                          keyOut, errorOut);
}

bool KeyClient::Allocate(const std::string& gameId, std::string* keyOut,
                         std::string* errorOut)
{
    return PostKeyRequest("/api/Keys/Allocate/" + gameId, gameId,
                          keyOut, errorOut);
}
