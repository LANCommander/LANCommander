#ifndef LANCOMMANDER_BACKENDS_CURL_HTTP_CLIENT_H
#define LANCOMMANDER_BACKENDS_CURL_HTTP_CLIENT_H

#include "lancommander/http/http_client.h"

#include <curl/curl.h>

namespace lancommander {

class CurlHttpClient : public IHttpClient {
public:
    CurlHttpClient();
    ~CurlHttpClient() override;

    void set_base_url(const std::string& url) override;
    void set_bearer_token(const std::string& token) override;
    void set_client_version(const std::string& version) override;

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
    HttpResponse request(const char* method, const std::string& path,
                         const std::string& body, const std::string& content_type);
    void apply_common(CURL* curl, const std::string& url);
    // Appends the Authorization and X-API-Version headers shared by every verb.
    // Takes and returns the list because curl_slist_append reallocates.
    struct curl_slist* append_default_headers(struct curl_slist* headers) const;

    std::string m_base_url;
    std::string m_bearer;
    std::string m_client_version;
};

} // namespace lancommander

#endif // LANCOMMANDER_BACKENDS_CURL_HTTP_CLIENT_H
