#ifndef LANCOMMANDER_WIN9X_HTTP_CLIENT_H
#define LANCOMMANDER_WIN9X_HTTP_CLIENT_H

#include <cstdio>
#include <string>

struct HttpResponse
{
    long status;
    std::string body;
    bool ok() const { return status >= 200 && status < 300; }
};

// Return false from the callback to abort the download.
typedef bool (*DownloadProgressFn)(unsigned long received,
                                   unsigned long total,
                                   void* userData);

class HttpClient
{
public:
    HttpClient();
    ~HttpClient();

    void SetBaseUrl(const std::string& baseUrl);
    void SetBearerToken(const std::string& token);

    HttpResponse Get(const std::string& path);
    HttpResponse PostJson(const std::string& path, const std::string& json);
    HttpResponse PutJson(const std::string& path, const std::string& json);

    // POSTs `filePath` as a single multipart/form-data file part named
    // `fieldName` (filename derives from the path).
    HttpResponse PostMultipartFile(const std::string& path,
                                   const std::string& fieldName,
                                   const std::string& filePath);

    // Streams the response body into `dest`. `total` reported to the callback
    // is 0 when the server doesn't send Content-Length.
    long Download(const std::string& path, FILE* dest,
                  DownloadProgressFn progress, void* userData);

private:
    HttpResponse Request(const char* verb, const std::string& path,
                         const std::string& body, const char* contentType);

    void* OpenRequest(const char* verb, const std::string& path,
                      void** connOut);

    std::string m_baseUrl;
    std::string m_bearer;
    void* m_session;
};

#endif
