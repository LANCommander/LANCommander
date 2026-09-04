#include "wininet_http_client.h"

#include <cstdio>
#include <cstring>

#pragma comment(lib, "wininet.lib")

namespace lancommander {

namespace {

std::string read_all(HINTERNET req) {
    std::string out;
    char buf[4096];
    DWORD read = 0;
    while (InternetReadFile(req, buf, sizeof(buf), &read) && read > 0)
        out.append(buf, read);
    return out;
}

long read_status(HINTERNET req) {
    DWORD status = 0;
    DWORD len = sizeof(status);
    HttpQueryInfoA(req, HTTP_QUERY_STATUS_CODE | HTTP_QUERY_FLAG_NUMBER,
                   &status, &len, NULL);
    return static_cast<long>(status);
}

unsigned long read_content_length(HINTERNET req) {
    DWORD len = 0;
    DWORD buf_len = sizeof(len);
    if (HttpQueryInfoA(req, HTTP_QUERY_CONTENT_LENGTH | HTTP_QUERY_FLAG_NUMBER,
                       &len, &buf_len, NULL))
        return static_cast<unsigned long>(len);
    return 0;
}

// Pull the whole response header block and split it into the map on
// HttpResponse. This was never populated before, which is why the X-Pong
// check in ConnectionClient::ping() could never fire and always fell through
// to accepting any 2xx.
//
// Keys are lowercased; HTTP header names are case-insensitive and no caller
// should have to guess which casing a server used.
void read_headers(HINTERNET req, std::map<std::string, std::string>* out) {
    DWORD size = 0;
    HttpQueryInfoA(req, HTTP_QUERY_RAW_HEADERS_CRLF, NULL, &size, NULL);
    if (size == 0)
        return;

    std::string raw;
    raw.resize(size + 1);

    DWORD have = size + 1;
    if (!HttpQueryInfoA(req, HTTP_QUERY_RAW_HEADERS_CRLF, &raw[0], &have, NULL))
        return;

    raw.resize(have);

    size_t pos = 0;
    while (pos < raw.size()) {
        size_t end = raw.find("\r\n", pos);
        if (end == std::string::npos)
            end = raw.size();

        const std::string line = raw.substr(pos, end - pos);
        pos = end + 2;

        // The first line is the status line, which has no colon before the
        // HTTP version and would otherwise be stored as a bogus header.
        const size_t colon = line.find(':');
        if (colon == std::string::npos)
            continue;

        std::string key = line.substr(0, colon);
        std::string value = line.substr(colon + 1);

        for (size_t i = 0; i < key.size(); ++i)
            key[i] = static_cast<char>(tolower(static_cast<unsigned char>(key[i])));

        size_t vb = value.find_first_not_of(" \t");
        if (vb == std::string::npos)
            value.clear();
        else
            value = value.substr(vb);

        (*out)[key] = value;
    }
}

} // anonymous namespace

WinInetHttpClient::WinInetHttpClient()
    : m_session(NULL), m_connect_timeout_ms(0), m_recv_timeout_ms(0)
{
    m_session = InternetOpenA("LANCommander-SDK-Cpp/1.0",
                              INTERNET_OPEN_TYPE_PRECONFIG,
                              NULL, NULL, 0);
}

WinInetHttpClient::~WinInetHttpClient()
{
    if (m_session)
        InternetCloseHandle(m_session);
}

void WinInetHttpClient::set_base_url(const std::string& url)
{
    m_base_url = url;
    while (!m_base_url.empty() && m_base_url.back() == '/')
        m_base_url.pop_back();
}

void WinInetHttpClient::set_bearer_token(const std::string& token)
{
    m_bearer = token;
}

HINTERNET WinInetHttpClient::open_request(const char* verb, const std::string& path,
                                           HINTERNET* conn_out, bool follow_redirects)
{
    *conn_out = NULL;
    if (!m_session) return NULL;

    std::string url = m_base_url + path;

    URL_COMPONENTSA parts;
    ZeroMemory(&parts, sizeof(parts));
    parts.dwStructSize = sizeof(parts);
    char host[256] = {0};
    char object[2048] = {0};
    parts.lpszHostName = host;
    parts.dwHostNameLength = sizeof(host);
    parts.lpszUrlPath = object;
    parts.dwUrlPathLength = sizeof(object);

    if (!InternetCrackUrlA(url.c_str(), 0, 0, &parts))
        return NULL;

    INTERNET_PORT port = parts.nPort ? parts.nPort : INTERNET_DEFAULT_HTTP_PORT;
    DWORD req_flags = 0;
    if (parts.nScheme == INTERNET_SCHEME_HTTPS) {
        port = parts.nPort ? parts.nPort : INTERNET_DEFAULT_HTTPS_PORT;
        req_flags = INTERNET_FLAG_SECURE;
    }

    HINTERNET conn = InternetConnectA(m_session, host, port,
                                       NULL, NULL, INTERNET_SERVICE_HTTP, 0, 0);
    if (!conn) return NULL;

    // Redirects are followed only where a redirect is a legitimate part of
    // the protocol, which for LANCommander means file downloads: storage
    // backends hand out a 302 to a signed URL.
    //
    // For API calls it is actively harmful. When a token is missing or
    // expired the server answers 302 to /Login, WinINet follows it, converts
    // the POST to a GET, and returns 200 with an HTML login page. Every
    // status-code check in the SDK then reads that as success — which is how
    // an invalid token used to pass AuthenticationClient::validate() and drop
    // the launcher into a library it could not actually talk to.
    //
    // Refusing to follow also makes the discovery fan-out behave better: a
    // server that redirects http to https now fails its http candidate and
    // succeeds on the https one, so the address that gets stored is the one
    // that actually works.
    DWORD redirect_flag = follow_redirects ? 0 : INTERNET_FLAG_NO_AUTO_REDIRECT;

    const char* accept_types[] = { "*/*", NULL };
    HINTERNET req = HttpOpenRequestA(conn, verb, object, NULL, NULL,
                                      accept_types,
                                      req_flags | INTERNET_FLAG_RELOAD |
                                      INTERNET_FLAG_NO_CACHE_WRITE |
                                      redirect_flag, 0);
    if (!req) {
        InternetCloseHandle(conn);
        return NULL;
    }

    *conn_out = conn;
    return req;
}

HttpResponse WinInetHttpClient::request(const char* verb, const std::string& path,
                                         const std::string& body,
                                         const std::string& content_type)
{
    HttpResponse resp;

    HINTERNET conn = NULL;
    HINTERNET req = open_request(verb, path, &conn, false);
    if (!req) return resp;

    std::string headers;
    if (!content_type.empty()) {
        headers += "Content-Type: " + content_type + "\r\n";
    }
    if (!m_bearer.empty()) {
        headers += "Authorization: Bearer " + m_bearer + "\r\n";
    }

    void* body_ptr = body.empty() ? NULL : const_cast<char*>(body.data());
    DWORD body_len = static_cast<DWORD>(body.size());

    if (HttpSendRequestA(req,
                         headers.empty() ? NULL : headers.c_str(),
                         headers.empty() ? 0 : static_cast<DWORD>(headers.size()),
                         body_ptr, body_len)) {
        resp.status_code = static_cast<int>(read_status(req));
        read_headers(req, &resp.headers);
        resp.body = read_all(req);
    }

    InternetCloseHandle(req);
    InternetCloseHandle(conn);
    return resp;
}

void WinInetHttpClient::set_timeout_ms(int connect_ms, int recv_ms)
{
    m_connect_timeout_ms = connect_ms;
    m_recv_timeout_ms = recv_ms;

    if (!m_session)
        return;

    // Session-level options are inherited by requests opened afterwards.
    DWORD connect_to = static_cast<DWORD>(connect_ms);
    DWORD recv_to = static_cast<DWORD>(recv_ms);

    InternetSetOptionA(m_session, INTERNET_OPTION_CONNECT_TIMEOUT,
                       &connect_to, sizeof(connect_to));
    InternetSetOptionA(m_session, INTERNET_OPTION_RECEIVE_TIMEOUT,
                       &recv_to, sizeof(recv_to));
    InternetSetOptionA(m_session, INTERNET_OPTION_SEND_TIMEOUT,
                       &recv_to, sizeof(recv_to));
}

HttpResponse WinInetHttpClient::head(const std::string& path,
                                     const std::map<std::string, std::string>& extra_headers)
{
    HttpResponse resp;

    HINTERNET conn = NULL;
    HINTERNET req = open_request("HEAD", path, &conn, false);
    if (!req) return resp;

    // Per-request timeouts as well as the session ones: a handle opened
    // before set_timeout_ms was called would otherwise keep the defaults.
    if (m_connect_timeout_ms > 0) {
        DWORD to = static_cast<DWORD>(m_connect_timeout_ms);
        InternetSetOptionA(req, INTERNET_OPTION_CONNECT_TIMEOUT, &to, sizeof(to));
    }
    if (m_recv_timeout_ms > 0) {
        DWORD to = static_cast<DWORD>(m_recv_timeout_ms);
        InternetSetOptionA(req, INTERNET_OPTION_RECEIVE_TIMEOUT, &to, sizeof(to));
        InternetSetOptionA(req, INTERNET_OPTION_SEND_TIMEOUT, &to, sizeof(to));
    }

    std::string headers;
    for (std::map<std::string, std::string>::const_iterator it = extra_headers.begin();
         it != extra_headers.end(); ++it) {
        headers += it->first + ": " + it->second + "\r\n";
    }
    if (!m_bearer.empty()) {
        headers += "Authorization: Bearer " + m_bearer + "\r\n";
    }

    if (HttpSendRequestA(req,
                         headers.empty() ? NULL : headers.c_str(),
                         headers.empty() ? 0 : static_cast<DWORD>(headers.size()),
                         NULL, 0)) {
        resp.status_code = static_cast<int>(read_status(req));
        read_headers(req, &resp.headers);
        // No body is read: this is a HEAD.
    }

    InternetCloseHandle(req);
    InternetCloseHandle(conn);
    return resp;
}

HttpResponse WinInetHttpClient::get(const std::string& path)
{
    return request("GET", path, "", "");
}

HttpResponse WinInetHttpClient::post(const std::string& path,
                                      const std::string& body,
                                      const std::string& content_type)
{
    return request("POST", path, body, content_type);
}

HttpResponse WinInetHttpClient::put(const std::string& path,
                                     const std::string& body,
                                     const std::string& content_type)
{
    return request("PUT", path, body, content_type);
}

HttpResponse WinInetHttpClient::del(const std::string& path)
{
    return request("DELETE", path, "", "");
}

bool WinInetHttpClient::download(const std::string& path,
                                  const std::string& dest_path,
                                  DownloadProgressFn progress)
{
    FILE* f = fopen(dest_path.c_str(), "wb");
    if (!f) return false;

    HINTERNET conn = NULL;
    HINTERNET req = open_request("GET", path, &conn, true);
    if (!req) {
        fclose(f);
        return false;
    }

    std::string headers;
    if (!m_bearer.empty()) {
        headers += "Authorization: Bearer " + m_bearer + "\r\n";
    }

    bool ok = false;
    if (HttpSendRequestA(req,
                         headers.empty() ? NULL : headers.c_str(),
                         headers.empty() ? 0 : static_cast<DWORD>(headers.size()),
                         NULL, 0)) {
        long status = read_status(req);
        if (status >= 200 && status < 300) {
            uint64_t total = read_content_length(req);
            uint64_t received = 0;
            char buf[8192];
            DWORD read_bytes = 0;
            ok = true;
            while (InternetReadFile(req, buf, sizeof(buf), &read_bytes) && read_bytes > 0) {
                if (fwrite(buf, 1, read_bytes, f) != read_bytes) { ok = false; break; }
                received += read_bytes;
                if (progress && !progress(received, total)) { ok = false; break; }
            }
        }
    }

    fclose(f);
    InternetCloseHandle(req);
    InternetCloseHandle(conn);

    if (!ok) {
        remove(dest_path.c_str());
    }
    return ok;
}

HttpResponse WinInetHttpClient::post_multipart_file(const std::string& path,
                                                     const std::string& field_name,
                                                     const std::string& file_path)
{
    HttpResponse resp;

    FILE* f = fopen(file_path.c_str(), "rb");
    if (!f) return resp;
    fseek(f, 0, SEEK_END);
    long sz = ftell(f);
    fseek(f, 0, SEEK_SET);

    const char* boundary = "----LANCommanderCppSDKBoundary7c3f1a";

    // Derive filename from path
    std::string file_name = file_path;
    size_t slash = file_name.find_last_of("\\/");
    if (slash != std::string::npos) file_name = file_name.substr(slash + 1);

    std::string head;
    head += "--"; head += boundary; head += "\r\n";
    head += "Content-Disposition: form-data; name=\"";
    head += field_name; head += "\"; filename=\"";
    head += file_name; head += "\"\r\n";
    head += "Content-Type: application/octet-stream\r\n\r\n";

    std::string tail;
    tail += "\r\n--"; tail += boundary; tail += "--\r\n";

    std::string body;
    body.reserve(head.size() + static_cast<size_t>(sz) + tail.size());
    body.append(head);
    body.resize(head.size() + static_cast<size_t>(sz));
    if (sz > 0) {
        size_t got = fread(&body[head.size()], 1, static_cast<size_t>(sz), f);
        if (got != static_cast<size_t>(sz)) { fclose(f); return resp; }
    }
    fclose(f);
    body.append(tail);

    std::string content_type = "multipart/form-data; boundary=";
    content_type += boundary;

    return request("POST", path, body, content_type);
}

} // namespace lancommander
