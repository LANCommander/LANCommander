#ifndef LANCOMMANDER_HTTP_CLIENT_H
#define LANCOMMANDER_HTTP_CLIENT_H

#include <map>
#include <string>

#include "http_response.h"
#include "../types.h"

namespace lancommander {

// Abstract HTTP client interface. Consumers must provide a concrete backend
// (e.g. WinInetHttpClient, CurlHttpClient) that implements these methods.
class IHttpClient {
public:
    virtual ~IHttpClient() = default;

    virtual void set_base_url(const std::string& url) = 0;
    virtual void set_bearer_token(const std::string& token) = 0;

    // Client version reported to the server on every request, as the
    // "X-API-Version" header the .NET SDK already sends. The library has no
    // version of its own to report here — the consuming launcher owns the
    // product version and passes it in.
    //
    // Non-pure with a no-op default so existing backends keep compiling; a
    // backend that does not implement it simply sends no version header.
    virtual void set_client_version(const std::string& version)
    {
        (void)version;
    }

    virtual void set_timeout_ms(int connect_ms, int recv_ms)
    {
        (void)connect_ms;
        (void)recv_ms;
    }

    // HEAD with caller-supplied request headers, and response headers filled
    // in. Needed for the X-Ping / X-Pong handshake that distinguishes a
    // LANCommander server from any other host that answers 2xx on "/".
    virtual HttpResponse head(const std::string& path,
                              const std::map<std::string, std::string>& extra_headers)
    {
        (void)path;
        (void)extra_headers;
        return HttpResponse();
    }

    virtual HttpResponse get(const std::string& path) = 0;
    virtual HttpResponse post(const std::string& path,
                              const std::string& body,
                              const std::string& content_type = "application/json") = 0;
    virtual HttpResponse put(const std::string& path,
                             const std::string& body,
                             const std::string& content_type = "application/json") = 0;
    virtual HttpResponse del(const std::string& path) = 0;

    // Download to a file on disk. Returns true on success.
    virtual bool download(const std::string& path,
                          const std::string& dest_path,
                          DownloadProgressFn progress = nullptr) = 0;

    // Upload a file as multipart/form-data.
    virtual HttpResponse post_multipart_file(const std::string& path,
                                             const std::string& field_name,
                                             const std::string& file_path) = 0;
};

} // namespace lancommander

#endif // LANCOMMANDER_HTTP_CLIENT_H
