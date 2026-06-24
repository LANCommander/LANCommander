#include "http_client.h"

#include <windows.h>
#include <wininet.h>
#include <string>

namespace
{
    std::string ReadAll(HINTERNET req)
    {
        std::string out;
        char buf[4096];
        DWORD read = 0;
        while (InternetReadFile(req, buf, sizeof(buf), &read) && read > 0)
            out.append(buf, read);
        return out;
    }

    long ReadStatus(HINTERNET req)
    {
        DWORD status = 0;
        DWORD len = sizeof(status);
        HttpQueryInfoA(req, HTTP_QUERY_STATUS_CODE | HTTP_QUERY_FLAG_NUMBER,
                       &status, &len, NULL);
        return static_cast<long>(status);
    }

    unsigned long ReadContentLength(HINTERNET req)
    {
        DWORD len = 0;
        DWORD bufLen = sizeof(len);
        if (HttpQueryInfoA(req, HTTP_QUERY_CONTENT_LENGTH | HTTP_QUERY_FLAG_NUMBER,
                           &len, &bufLen, NULL))
            return static_cast<unsigned long>(len);
        return 0;
    }
}

HttpClient::HttpClient()
    : m_session(NULL)
{
    m_session = InternetOpenA("LANCommander-Win9x/0.1",
                              INTERNET_OPEN_TYPE_PRECONFIG,
                              NULL, NULL, 0);
}

HttpClient::~HttpClient()
{
    if (m_session)
        InternetCloseHandle(static_cast<HINTERNET>(m_session));
}

void HttpClient::SetBaseUrl(const std::string& baseUrl)
{
    m_baseUrl = baseUrl;
    while (!m_baseUrl.empty() && m_baseUrl[m_baseUrl.size() - 1] == '/')
        m_baseUrl.erase(m_baseUrl.size() - 1);
}

void HttpClient::SetBearerToken(const std::string& token)
{
    m_bearer = token;
}

void* HttpClient::OpenRequest(const char* verb, const std::string& path,
                              void** connOut)
{
    *connOut = NULL;
    if (!m_session)
        return NULL;

    std::string url = m_baseUrl + path;

    URL_COMPONENTSA parts;
    ZeroMemory(&parts, sizeof(parts));
    parts.dwStructSize = sizeof(parts);
    char host[256] = {0};
    char object[1024] = {0};
    parts.lpszHostName = host;
    parts.dwHostNameLength = sizeof(host);
    parts.lpszUrlPath = object;
    parts.dwUrlPathLength = sizeof(object);

    if (!InternetCrackUrlA(url.c_str(), 0, 0, &parts))
        return NULL;

    INTERNET_PORT port = parts.nPort ? parts.nPort : INTERNET_DEFAULT_HTTP_PORT;
    DWORD reqFlags = 0;
    if (parts.nScheme == INTERNET_SCHEME_HTTPS)
    {
        port = parts.nPort ? parts.nPort : INTERNET_DEFAULT_HTTPS_PORT;
        reqFlags = INTERNET_FLAG_SECURE;
    }

    HINTERNET conn = InternetConnectA(static_cast<HINTERNET>(m_session),
                                      host, port, NULL, NULL,
                                      INTERNET_SERVICE_HTTP, 0, 0);
    if (!conn)
        return NULL;

    const char* acceptTypes[] = { "*/*", NULL };
    HINTERNET req = HttpOpenRequestA(conn, verb, object, NULL, NULL,
                                     acceptTypes,
                                     reqFlags | INTERNET_FLAG_RELOAD |
                                     INTERNET_FLAG_NO_CACHE_WRITE,
                                     0);
    if (!req)
    {
        InternetCloseHandle(conn);
        return NULL;
    }

    *connOut = conn;
    return req;
}

HttpResponse HttpClient::Get(const std::string& path)
{
    return Request("GET", path, std::string(), NULL);
}

HttpResponse HttpClient::PostJson(const std::string& path, const std::string& json)
{
    return Request("POST", path, json, "application/json");
}

HttpResponse HttpClient::PutJson(const std::string& path, const std::string& json)
{
    return Request("PUT", path, json, "application/json");
}

HttpResponse HttpClient::Request(const char* verb, const std::string& path,
                                 const std::string& body, const char* contentType)
{
    HttpResponse resp;
    resp.status = 0;

    void* connV = NULL;
    HINTERNET req = static_cast<HINTERNET>(OpenRequest(verb, path, &connV));
    HINTERNET conn = static_cast<HINTERNET>(connV);
    if (!req)
        return resp;

    std::string headers;
    if (contentType && *contentType)
    {
        headers += "Content-Type: ";
        headers += contentType;
        headers += "\r\n";
    }
    if (!m_bearer.empty())
    {
        headers += "Authorization: Bearer ";
        headers += m_bearer;
        headers += "\r\n";
    }

    void* bodyPtr = body.empty() ? NULL : (void*)body.data();
    DWORD bodyLen = static_cast<DWORD>(body.size());

    if (HttpSendRequestA(req,
                         headers.empty() ? NULL : headers.c_str(),
                         headers.empty() ? 0 : (DWORD)headers.size(),
                         bodyPtr, bodyLen))
    {
        resp.status = ReadStatus(req);
        resp.body = ReadAll(req);
    }

    InternetCloseHandle(req);
    InternetCloseHandle(conn);
    return resp;
}

HttpResponse HttpClient::PostMultipartFile(const std::string& path,
                                           const std::string& fieldName,
                                           const std::string& filePath)
{
    HttpResponse resp;
    resp.status = 0;

    FILE* f = fopen(filePath.c_str(), "rb");
    if (!f) return resp;
    fseek(f, 0, SEEK_END);
    long sz = ftell(f);
    fseek(f, 0, SEEK_SET);

    const char* boundary = "----LANCommanderWin9xBoundary7c3f1a";

    // Derive filename from the path's basename.
    std::string fileName = filePath;
    size_t slash = fileName.find_last_of("\\/");
    if (slash != std::string::npos) fileName = fileName.substr(slash + 1);

    std::string head;
    head += "--"; head += boundary; head += "\r\n";
    head += "Content-Disposition: form-data; name=\"";
    head += fieldName; head += "\"; filename=\"";
    head += fileName; head += "\"\r\n";
    head += "Content-Type: application/octet-stream\r\n\r\n";

    std::string tail;
    tail += "\r\n--"; tail += boundary; tail += "--\r\n";

    std::string body;
    body.reserve(head.size() + (size_t)sz + tail.size());
    body.append(head);
    body.resize(head.size() + (size_t)sz);
    if (sz > 0)
    {
        size_t got = fread(&body[head.size()], 1, (size_t)sz, f);
        if (got != (size_t)sz) { fclose(f); return resp; }
    }
    fclose(f);
    body.append(tail);

    std::string contentType = "multipart/form-data; boundary=";
    contentType += boundary;

    return Request("POST", path, body, contentType.c_str());
}

long HttpClient::Download(const std::string& path, FILE* dest,
                          DownloadProgressFn progress, void* userData)
{
    if (!dest) return 0;

    void* connV = NULL;
    HINTERNET req = static_cast<HINTERNET>(OpenRequest("GET", path, &connV));
    HINTERNET conn = static_cast<HINTERNET>(connV);
    if (!req) return 0;

    std::string headers;
    if (!m_bearer.empty())
    {
        headers += "Authorization: Bearer ";
        headers += m_bearer;
        headers += "\r\n";
    }

    long status = 0;
    if (HttpSendRequestA(req,
                         headers.empty() ? NULL : headers.c_str(),
                         headers.empty() ? 0 : (DWORD)headers.size(),
                         NULL, 0))
    {
        status = ReadStatus(req);
        if (status >= 200 && status < 300)
        {
            unsigned long total = ReadContentLength(req);
            unsigned long received = 0;
            char buf[8192];
            DWORD read = 0;
            bool aborted = false;
            while (InternetReadFile(req, buf, sizeof(buf), &read) && read > 0)
            {
                if (fwrite(buf, 1, read, dest) != read) { aborted = true; break; }
                received += read;
                if (progress && !progress(received, total, userData))
                {
                    aborted = true;
                    break;
                }
            }
            if (aborted) status = 0;
        }
    }

    InternetCloseHandle(req);
    InternetCloseHandle(conn);
    return status;
}
