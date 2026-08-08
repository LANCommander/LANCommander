#include "curl_http_client.h"

#include <cstdio>

namespace lancommander {

namespace {

size_t write_string_cb(char* ptr, size_t size, size_t nmemb, void* userdata)
{
    auto* str = static_cast<std::string*>(userdata);
    size_t bytes = size * nmemb;
    str->append(ptr, bytes);
    return bytes;
}

size_t write_file_cb(char* ptr, size_t size, size_t nmemb, void* userdata)
{
    auto* f = static_cast<FILE*>(userdata);
    return fwrite(ptr, size, nmemb, f);
}

struct ProgressData {
    DownloadProgressFn fn;
};

int progress_cb(void* clientp, curl_off_t dltotal, curl_off_t dlnow,
                curl_off_t /*ultotal*/, curl_off_t /*ulnow*/)
{
    auto* pd = static_cast<ProgressData*>(clientp);
    if (pd->fn) {
        if (!pd->fn(static_cast<uint64_t>(dlnow), static_cast<uint64_t>(dltotal)))
            return 1; // abort
    }
    return 0;
}

} // anonymous namespace

CurlHttpClient::CurlHttpClient()
{
    curl_global_init(CURL_GLOBAL_DEFAULT);
}

CurlHttpClient::~CurlHttpClient()
{
    curl_global_cleanup();
}

void CurlHttpClient::set_base_url(const std::string& url)
{
    m_base_url = url;
    while (!m_base_url.empty() && m_base_url.back() == '/')
        m_base_url.pop_back();
}

void CurlHttpClient::set_bearer_token(const std::string& token)
{
    m_bearer = token;
}

void CurlHttpClient::apply_common(CURL* curl, const std::string& url)
{
    curl_easy_setopt(curl, CURLOPT_URL, url.c_str());
    curl_easy_setopt(curl, CURLOPT_FOLLOWLOCATION, 1L);
    curl_easy_setopt(curl, CURLOPT_USERAGENT, "LANCommander-SDK-Cpp/1.0");

    if (!m_bearer.empty()) {
        std::string auth = "Bearer " + m_bearer;
        curl_easy_setopt(curl, CURLOPT_HTTPAUTH, CURLAUTH_BEARER);
        curl_easy_setopt(curl, CURLOPT_XOAUTH2_BEARER, m_bearer.c_str());
    }
}

HttpResponse CurlHttpClient::request(const char* method, const std::string& path,
                                      const std::string& body,
                                      const std::string& content_type)
{
    HttpResponse resp;
    CURL* curl = curl_easy_init();
    if (!curl) return resp;

    std::string url = m_base_url + path;
    apply_common(curl, url);
    curl_easy_setopt(curl, CURLOPT_CUSTOMREQUEST, method);

    struct curl_slist* headers = nullptr;
    if (!content_type.empty()) {
        std::string ct = "Content-Type: " + content_type;
        headers = curl_slist_append(headers, ct.c_str());
    }
    if (!m_bearer.empty()) {
        std::string auth = "Authorization: Bearer " + m_bearer;
        headers = curl_slist_append(headers, auth.c_str());
    }
    if (headers)
        curl_easy_setopt(curl, CURLOPT_HTTPHEADER, headers);

    if (!body.empty()) {
        curl_easy_setopt(curl, CURLOPT_POSTFIELDS, body.c_str());
        curl_easy_setopt(curl, CURLOPT_POSTFIELDSIZE, static_cast<long>(body.size()));
    }

    std::string response_body;
    curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, write_string_cb);
    curl_easy_setopt(curl, CURLOPT_WRITEDATA, &response_body);

    CURLcode res = curl_easy_perform(curl);
    if (res == CURLE_OK) {
        long code = 0;
        curl_easy_getinfo(curl, CURLINFO_RESPONSE_CODE, &code);
        resp.status_code = static_cast<int>(code);
        resp.body = std::move(response_body);
    }

    if (headers) curl_slist_free_all(headers);
    curl_easy_cleanup(curl);
    return resp;
}

HttpResponse CurlHttpClient::get(const std::string& path)
{
    return request("GET", path, "", "");
}

HttpResponse CurlHttpClient::post(const std::string& path,
                                   const std::string& body,
                                   const std::string& content_type)
{
    return request("POST", path, body, content_type);
}

HttpResponse CurlHttpClient::put(const std::string& path,
                                  const std::string& body,
                                  const std::string& content_type)
{
    return request("PUT", path, body, content_type);
}

HttpResponse CurlHttpClient::del(const std::string& path)
{
    return request("DELETE", path, "", "");
}

bool CurlHttpClient::download(const std::string& path,
                               const std::string& dest_path,
                               DownloadProgressFn progress_fn)
{
    FILE* f = fopen(dest_path.c_str(), "wb");
    if (!f) return false;

    CURL* curl = curl_easy_init();
    if (!curl) { fclose(f); return false; }

    std::string url = m_base_url + path;
    apply_common(curl, url);

    struct curl_slist* headers = nullptr;
    if (!m_bearer.empty()) {
        std::string auth = "Authorization: Bearer " + m_bearer;
        headers = curl_slist_append(headers, auth.c_str());
        curl_easy_setopt(curl, CURLOPT_HTTPHEADER, headers);
    }

    curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, write_file_cb);
    curl_easy_setopt(curl, CURLOPT_WRITEDATA, f);

    ProgressData pd;
    pd.fn = progress_fn;
    if (progress_fn) {
        curl_easy_setopt(curl, CURLOPT_XFERINFOFUNCTION, progress_cb);
        curl_easy_setopt(curl, CURLOPT_XFERINFODATA, &pd);
        curl_easy_setopt(curl, CURLOPT_NOPROGRESS, 0L);
    }

    CURLcode res = curl_easy_perform(curl);
    bool ok = false;
    if (res == CURLE_OK) {
        long code = 0;
        curl_easy_getinfo(curl, CURLINFO_RESPONSE_CODE, &code);
        ok = (code >= 200 && code < 300);
    }

    if (headers) curl_slist_free_all(headers);
    curl_easy_cleanup(curl);
    fclose(f);

    if (!ok) remove(dest_path.c_str());
    return ok;
}

HttpResponse CurlHttpClient::post_multipart_file(const std::string& path,
                                                   const std::string& field_name,
                                                   const std::string& file_path)
{
    HttpResponse resp;
    CURL* curl = curl_easy_init();
    if (!curl) return resp;

    std::string url = m_base_url + path;
    apply_common(curl, url);

    struct curl_slist* headers = nullptr;
    if (!m_bearer.empty()) {
        std::string auth = "Authorization: Bearer " + m_bearer;
        headers = curl_slist_append(headers, auth.c_str());
        curl_easy_setopt(curl, CURLOPT_HTTPHEADER, headers);
    }

    curl_mime* mime = curl_mime_init(curl);
    curl_mimepart* part = curl_mime_addpart(mime);
    curl_mime_name(part, field_name.c_str());
    curl_mime_filedata(part, file_path.c_str());

    curl_easy_setopt(curl, CURLOPT_MIMEPOST, mime);

    std::string response_body;
    curl_easy_setopt(curl, CURLOPT_WRITEFUNCTION, write_string_cb);
    curl_easy_setopt(curl, CURLOPT_WRITEDATA, &response_body);

    CURLcode res = curl_easy_perform(curl);
    if (res == CURLE_OK) {
        long code = 0;
        curl_easy_getinfo(curl, CURLINFO_RESPONSE_CODE, &code);
        resp.status_code = static_cast<int>(code);
        resp.body = std::move(response_body);
    }

    curl_mime_free(mime);
    if (headers) curl_slist_free_all(headers);
    curl_easy_cleanup(curl);
    return resp;
}

} // namespace lancommander
