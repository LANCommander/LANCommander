#ifndef LANCOMMANDER_BACKENDS_WININET_HTTP_CLIENT_H
#define LANCOMMANDER_BACKENDS_WININET_HTTP_CLIENT_H

#include "lancommander/http/http_client.h"

#include <windows.h>
#include <wininet.h>

namespace lancommander {

class WinInetHttpClient : public IHttpClient {
public:
    WinInetHttpClient();
    ~WinInetHttpClient() override;

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
    HttpResponse request(const char* verb, const std::string& path,
                         const std::string& body, const std::string& content_type);
    // `follow_redirects` is true only for downloads; see the comment at the
    // definition for why API calls must not follow a 302.
    HINTERNET open_request(const char* verb, const std::string& path,
                           HINTERNET* conn_out, bool follow_redirects);
    // Appends the Authorization and X-API-Version headers shared by every verb.
    void append_default_headers(std::string& headers) const;

    std::string m_base_url;
    std::string m_bearer;
    std::string m_client_version;
    HINTERNET m_session;
    int m_connect_timeout_ms;
    int m_recv_timeout_ms;
};

} // namespace lancommander

#endif // LANCOMMANDER_BACKENDS_WININET_HTTP_CLIENT_H
