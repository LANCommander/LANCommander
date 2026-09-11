#ifndef LANCOMMANDER_HTTP_CLIENT_H
#define LANCOMMANDER_HTTP_CLIENT_H

#include <stdint.h>

#include <map>
#include <string>

#include "http_response.h"
#include "../types.h"

namespace lancommander {

// Where a download's wall clock went.
//
// A download is a loop of "read from the socket, write to disk", and the two
// happen one after the other on one thread, so the total is the sum of them.
// That makes the split the only thing worth knowing when a transfer is slower
// than the link should allow: a slow network and a slow disk look identical
// from the outside, and the fix for one does nothing for the other.
//
// Times are milliseconds and come from a coarse system tick, so any single
// read or write is quantised badly. The split across thousands of them is
// still sound -- the two accumulators partition the same interval with
// nothing in between, so the quantisation error is a sampling artefact that
// averages out rather than a bias.
struct DownloadTiming {
    uint64_t bytes;

    unsigned long total_ms;      // First read to last write.
    unsigned long socket_ms;     // Inside the transport's read call.
    unsigned long write_ms;      // Inside the write call.
    unsigned long flush_ms;      // The final flush and close.

    unsigned long reads;         // Read calls made.
    unsigned long longest_write_ms; // Worst single write, i.e. the worst stall.

    DownloadTiming()
        : bytes(0), total_ms(0), socket_ms(0), write_ms(0), flush_ms(0),
          reads(0), longest_write_ms(0) {}
};

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

    // How the last download() spent its time. Non-pure with a zeroed default,
    // like set_client_version above, so a backend that does not measure keeps
    // compiling; a caller reads total_ms == 0 as "not measured".
    virtual DownloadTiming last_download_timing() const
    {
        return DownloadTiming();
    }

    // Upload a file as multipart/form-data.
    virtual HttpResponse post_multipart_file(const std::string& path,
                                             const std::string& field_name,
                                             const std::string& file_path) = 0;
};

} // namespace lancommander

#endif // LANCOMMANDER_HTTP_CLIENT_H
