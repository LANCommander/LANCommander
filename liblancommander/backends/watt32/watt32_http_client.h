#ifndef LANCOMMANDER_BACKENDS_WATT32_HTTP_CLIENT_H
#define LANCOMMANDER_BACKENDS_WATT32_HTTP_CLIENT_H

#include "lancommander/http/http_client.h"

#include <cstdio>

namespace lancommander {

// HTTP over Watt-32's BSD sockets, for MS-DOS.
//
// Plaintext HTTP only. Watt-32 has no TLS, so an https:// base URL is
// rejected with an error saying so rather than silently downgraded — the
// alternative is sending a bearer token in the clear to a server that was
// asked to be reached securely.
//
// Requests are HTTP/1.0 with `Connection: close`, and the response is read
// until the server closes. That is a deliberate simplification, not an
// oversight: it removes chunked transfer-encoding and connection reuse from
// a client that has one request in flight at a time anyway. Kestrel still
// sends Content-Length on a 1.0 response, so download progress is unaffected.
//
// A live request needs a packet driver, a NIC (real or emulated) and
// addressing from WATTCP.CFG or DHCP. When the stack cannot come up, every
// call fails with the reason rather than aborting the process — Watt-32's
// default is to print and exit(), which this turns off at init.
class Watt32HttpClient : public IHttpClient {
public:
    Watt32HttpClient();
    ~Watt32HttpClient() override;

    void set_base_url(const std::string& url) override;
    void set_bearer_token(const std::string& token) override;
    void set_client_version(const std::string& version) override;

    void set_timeout_ms(int connect_ms, int recv_ms) override;
    HttpResponse head(const std::string& path,
                      const std::map<std::string, std::string>& extra_headers) override;

    HttpResponse get(const std::string& path) override;
    HttpResponse post(const std::string& path,
                      const std::string& body,
                      const std::string& content_type) override;
    HttpResponse put(const std::string& path,
                     const std::string& body,
                     const std::string& content_type) override;
    HttpResponse del(const std::string& path) override;

    bool download(const std::string& path,
                  const std::string& dest_path,
                  DownloadProgressFn progress) override;

    HttpResponse post_multipart_file(const std::string& path,
                                     const std::string& field_name,
                                     const std::string& file_path) override;

private:
    // Where a response body goes. Exactly one of these is set: in-memory for
    // the API verbs, a file for download(), so a multi-gigabyte archive never
    // has to fit in a DPMI heap.
    struct Sink {
        std::string* memory;
        std::FILE* file;
        DownloadProgressFn progress;

        // Bytes the last exchange() wrote. download() truncates the file to
        // this: a redirect hop rewinds rather than truncating, so a short
        // final body would otherwise leave the tail of the redirect's body
        // on the end of the archive.
        unsigned long long written;

        Sink() : memory(nullptr), file(nullptr), written(0) {}
    };

    // One request/response round trip. `extra` is appended verbatim to the
    // request head and must already be CRLF-terminated per line.
    bool exchange(const char* verb, const std::string& url,
                  const std::string& body, const std::string& content_type,
                  const std::string& extra, bool head_only,
                  Sink& sink, HttpResponse& out, std::string& redirect_to);

    // Follows 301/302/303/307/308 up to a small limit, which is what makes a
    // download survive a server that redirects to storage.
    bool perform(const char* verb, const std::string& path,
                 const std::string& body, const std::string& content_type,
                 const std::string& extra, bool head_only,
                 Sink& sink, HttpResponse& out, bool follow_redirects);

    // Brings the stack up on first use. False (with `error`) when there is no
    // packet driver or no address.
    bool ensure_stack(std::string& error);

    std::string default_headers() const;

    std::string m_base_url;
    std::string m_bearer;
    std::string m_client_version;
    int m_connect_timeout_ms;
    int m_recv_timeout_ms;
};

} // namespace lancommander

#endif // LANCOMMANDER_BACKENDS_WATT32_HTTP_CLIENT_H
