#ifndef LANCOMMANDER_HTTP_CLIENT_H
#define LANCOMMANDER_HTTP_CLIENT_H

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
