import io

# ---------------------------------------------------------------------------
# 1. The contract.
# ---------------------------------------------------------------------------
p = 'include/lancommander/http/http_client.h'
s = open(p, encoding='utf-8').read()

old = '''#include <map>
#include <string>

#include "http_response.h"
#include "../types.h"

namespace lancommander {
'''
new = '''#include <stdint.h>

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
'''
assert old in s, 'http_client header'
s = s.replace(old, new, 1)

old = '''    // Download to a file on disk. Returns true on success.
    virtual bool download(const std::string& path,
                          const std::string& dest_path,
                          DownloadProgressFn progress = nullptr) = 0;
'''
new = '''    // Download to a file on disk. Returns true on success.
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
'''
assert old in s, 'download decl'
s = s.replace(old, new, 1)
open(p, 'w', encoding='utf-8', newline='').write(s)
print('patched http_client.h')

# ---------------------------------------------------------------------------
# 2. The WinINet backend header.
# ---------------------------------------------------------------------------
p = 'backends/wininet/wininet_http_client.h'
s = open(p, encoding='utf-8').read()

old = '''    bool download(const std::string& path,
                  const std::string& dest_path,
                  DownloadProgressFn progress = nullptr) override;'''
new = '''    bool download(const std::string& path,
                  const std::string& dest_path,
                  DownloadProgressFn progress = nullptr) override;

    DownloadTiming last_download_timing() const override { return m_last_timing; }'''
assert old in s, 'wininet download decl'
s = s.replace(old, new, 1)

old = '''    int m_connect_timeout_ms;'''
new = '''    DownloadTiming m_last_timing;

    int m_connect_timeout_ms;'''
assert old in s, 'wininet members'
s = s.replace(old, new, 1)
open(p, 'w', encoding='utf-8', newline='').write(s)
print('patched wininet_http_client.h')

# ---------------------------------------------------------------------------
# 3. The measurement.
# ---------------------------------------------------------------------------
p = 'backends/wininet/wininet_http_client.cpp'
s = open(p, encoding='utf-8').read()

old = '''    bool ok = false;
    if (HttpSendRequestA(req,
                         headers.empty() ? NULL : headers.c_str(),
                         headers.empty() ? 0 : static_cast<DWORD>(headers.size()),
                         NULL, 0)) {
        long status = read_status(req);
        if (status >= 200 && status < 300) {
            uint64_t total = read_content_length(req);
            uint64_t received = 0;
            DWORD read_bytes = 0;
            ok = true;
            while (InternetReadFile(req, &buf[0],
                                    static_cast<DWORD>(buf.size()),
                                    &read_bytes) && read_bytes > 0) {
                if (fwrite(&buf[0], 1, read_bytes, f) != read_bytes) { ok = false; break; }
                received += read_bytes;
                if (progress && !progress(received, total)) { ok = false; break; }
            }
        }
    }

    // Checked, not just called: with a megabyte of buffering behind it, a
    // full disk now surfaces here rather than on the last fwrite, and a
    // truncated archive that reports success would fail later as a corrupt
    // zip with nothing pointing at the real cause.
    if (fclose(f) != 0)
        ok = false;
'''

new = '''    // Reset here rather than on the way out, so a download that fails partway
    // still leaves behind the timings for however far it got.
    m_last_timing = DownloadTiming();

    bool ok = false;
    if (HttpSendRequestA(req,
                         headers.empty() ? NULL : headers.c_str(),
                         headers.empty() ? 0 : static_cast<DWORD>(headers.size()),
                         NULL, 0)) {
        long status = read_status(req);
        if (status >= 200 && status < 300) {
            uint64_t total = read_content_length(req);
            uint64_t received = 0;
            DWORD read_bytes = 0;
            ok = true;

            const DWORD loop_start = GetTickCount();
            DWORD mark = loop_start;

            for (;;) {
                if (!InternetReadFile(req, &buf[0],
                                      static_cast<DWORD>(buf.size()),
                                      &read_bytes))
                    break;

                const DWORD after_read = GetTickCount();
                m_last_timing.socket_ms += after_read - mark;
                m_last_timing.reads++;
                mark = after_read;

                if (read_bytes == 0)
                    break;

                const bool wrote = fwrite(&buf[0], 1, read_bytes, f) == read_bytes;

                const DWORD after_write = GetTickCount();
                const DWORD this_write = after_write - mark;
                m_last_timing.write_ms += this_write;
                if (this_write > m_last_timing.longest_write_ms)
                    m_last_timing.longest_write_ms = this_write;
                mark = after_write;

                if (!wrote) { ok = false; break; }

                received += read_bytes;

                // Outside the two accumulators on purpose: whatever the
                // caller does in here is its own cost, not the network's or
                // the disk's, and folding it into either would misattribute
                // it. It is charged to total_ms, so a caller doing something
                // expensive per chunk shows up as an unexplained remainder.
                if (progress && !progress(received, total)) { ok = false; break; }
                mark = GetTickCount();
            }

            m_last_timing.bytes = received;
            m_last_timing.total_ms = GetTickCount() - loop_start;
        }
    }

    // Checked, not just called: with a megabyte of buffering behind it, a
    // full disk now surfaces here rather than on the last fwrite, and a
    // truncated archive that reports success would fail later as a corrupt
    // zip with nothing pointing at the real cause.
    //
    // Timed separately from write_ms because it is the one write that is
    // guaranteed to reach the platter rather than the cache.
    const DWORD before_close = GetTickCount();
    if (fclose(f) != 0)
        ok = false;
    m_last_timing.flush_ms = GetTickCount() - before_close;
'''
assert old in s, 'download loop'
s = s.replace(old, new, 1)
open(p, 'w', encoding='utf-8', newline='').write(s)
print('patched wininet_http_client.cpp')
