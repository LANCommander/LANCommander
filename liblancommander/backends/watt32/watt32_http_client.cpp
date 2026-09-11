#include "watt32_http_client.h"

// Watt-32's headers define a full BSD socket API of their own and must come
// before anything that might pull in a system <sys/socket.h>. On DJGPP there
// is no competing one, but the include order is kept explicit so that stays
// true if this is ever built against another DOS libc.
#include <tcp.h>
#include <sys/socket.h>
#include <netinet/in.h>
#include <netdb.h>

#include <cctype>
#include <cstdio>
#include <cstdlib>
#include <cstring>

// ftruncate, for the redirect case in download(). DJGPP implements it as an
// INT 21h seek plus a zero-length write, which is how DOS shortens a file.
#include <unistd.h>

namespace lancommander {

namespace {

const int MAX_REDIRECTS = 5;

// Read buffer. 4 KB rather than something larger because this runs on
// machines where the DPMI heap is measured in megabytes and Watt-32's own
// receive window is 8 KB by default.
const int READ_CHUNK = 4096;

bool g_stack_up = false;

std::string to_lower(const std::string& s)
{
    std::string out(s);
    for (std::string::size_type i = 0; i < out.size(); ++i)
        out[i] = (char)std::tolower((unsigned char)out[i]);
    return out;
}

std::string trim(const std::string& s)
{
    std::string::size_type b = 0;
    std::string::size_type e = s.size();
    while (b < e && std::isspace((unsigned char)s[b])) ++b;
    while (e > b && std::isspace((unsigned char)s[e - 1])) --e;
    return s.substr(b, e - b);
}

struct Url {
    std::string scheme;
    std::string host;
    int port;
    std::string path;

    Url() : port(80) {}
};

// Splits an absolute URL. Returns false for anything that is not
// http:// or https://, including a relative path — callers join those onto
// the base URL before getting here.
bool parse_url(const std::string& url, Url& out)
{
    const std::string::size_type sep = url.find("://");
    if (sep == std::string::npos)
        return false;

    out.scheme = to_lower(url.substr(0, sep));
    if (out.scheme != "http" && out.scheme != "https")
        return false;

    std::string rest = url.substr(sep + 3);

    const std::string::size_type slash = rest.find('/');
    std::string authority;
    if (slash == std::string::npos) {
        authority = rest;
        out.path = "/";
    } else {
        authority = rest.substr(0, slash);
        out.path = rest.substr(slash);
    }

    // Credentials in the authority are not supported and would otherwise be
    // parsed as part of the host name.
    const std::string::size_type at = authority.find('@');
    if (at != std::string::npos)
        authority = authority.substr(at + 1);

    out.port = (out.scheme == "https") ? 443 : 80;

    const std::string::size_type colon = authority.rfind(':');
    if (colon != std::string::npos && authority.find(']') == std::string::npos) {
        out.host = authority.substr(0, colon);
        const int p = std::atoi(authority.c_str() + colon + 1);
        if (p > 0 && p < 65536)
            out.port = p;
    } else {
        out.host = authority;
    }

    return !out.host.empty();
}

// base + path, where `path` may already be absolute (a redirect target).
std::string join_url(const std::string& base, const std::string& path)
{
    if (path.find("://") != std::string::npos)
        return path;

    std::string b = base;
    while (!b.empty() && b[b.size() - 1] == '/')
        b.erase(b.size() - 1);

    if (path.empty())
        return b + "/";
    if (path[0] == '/')
        return b + path;
    return b + "/" + path;
}

void set_timeout(int fd, int option, int ms)
{
    if (ms <= 0)
        return;

    struct timeval tv;
    tv.tv_sec = ms / 1000;
    tv.tv_usec = (ms % 1000) * 1000;

    // Best effort: an older Watt-32 build without the option still works,
    // it just falls back to the stack's own retransmit timers.
    setsockopt(fd, SOL_SOCKET, option, (const char*)&tv, sizeof(tv));
}

// Reads one line's worth of the response head, growing `head` until the
// blank line that ends it. Anything read past that point is returned in
// `overflow`, because a single recv() routinely spans the boundary.
bool read_head(int fd, std::string& head, std::string& overflow, std::string& error)
{
    char buf[READ_CHUNK];

    for (;;) {
        const std::string::size_type end = head.find("\r\n\r\n");
        if (end != std::string::npos) {
            overflow = head.substr(end + 4);
            head.erase(end + 4);
            return true;
        }

        // A server that answers with bare LFs is not conforming, but neither
        // is refusing to talk to it when the intent is unambiguous.
        const std::string::size_type end_lf = head.find("\n\n");
        if (end_lf != std::string::npos) {
            overflow = head.substr(end_lf + 2);
            head.erase(end_lf + 2);
            return true;
        }

        const int got = recv(fd, buf, READ_CHUNK, 0);
        if (got < 0) {
            error = "error reading response";
            return false;
        }
        if (got == 0) {
            error = head.empty() ? "server closed the connection"
                                 : "truncated response head";
            return false;
        }

        head.append(buf, (std::string::size_type)got);

        // A head this large is a malfunctioning or hostile peer, not a real
        // LANCommander server.
        if (head.size() > 64u * 1024u) {
            error = "response head too large";
            return false;
        }
    }
}

int parse_status(const std::string& head)
{
    // "HTTP/1.1 200 OK"
    const std::string::size_type sp = head.find(' ');
    if (sp == std::string::npos)
        return 0;
    return std::atoi(head.c_str() + sp + 1);
}

void parse_headers(const std::string& head, std::map<std::string, std::string>& out)
{
    std::string::size_type pos = head.find('\n');
    if (pos == std::string::npos)
        return;
    ++pos;

    while (pos < head.size()) {
        std::string::size_type eol = head.find('\n', pos);
        if (eol == std::string::npos)
            eol = head.size();

        std::string line = head.substr(pos, eol - pos);
        pos = eol + 1;

        if (!line.empty() && line[line.size() - 1] == '\r')
            line.erase(line.size() - 1);
        if (line.empty())
            break;

        const std::string::size_type colon = line.find(':');
        if (colon == std::string::npos)
            continue;

        // Lower-cased keys: callers look up "content-length" and "x-pong"
        // without having to guess how the server capitalised them.
        out[to_lower(trim(line.substr(0, colon)))] = trim(line.substr(colon + 1));
    }
}

const std::string* find_header(const std::map<std::string, std::string>& h,
                               const char* name)
{
    std::map<std::string, std::string>::const_iterator it = h.find(name);
    return it == h.end() ? nullptr : &it->second;
}

} // namespace

Watt32HttpClient::Watt32HttpClient()
    : m_connect_timeout_ms(0), m_recv_timeout_ms(0)
{
}

Watt32HttpClient::~Watt32HttpClient() = default;

void Watt32HttpClient::set_base_url(const std::string& url)
{
    m_base_url = url;
    while (!m_base_url.empty() && m_base_url[m_base_url.size() - 1] == '/')
        m_base_url.erase(m_base_url.size() - 1);
}

void Watt32HttpClient::set_bearer_token(const std::string& token)
{
    m_bearer = token;
}

void Watt32HttpClient::set_client_version(const std::string& version)
{
    m_client_version = version;
}

void Watt32HttpClient::set_timeout_ms(int connect_ms, int recv_ms)
{
    m_connect_timeout_ms = connect_ms;
    m_recv_timeout_ms = recv_ms;
}

bool Watt32HttpClient::ensure_stack(std::string& error)
{
    if (g_stack_up)
        return true;

    // Watt-32's default failure behaviour is to print a message and exit the
    // process. For a GUI in a VESA mode that would leave the machine on a
    // screen nobody can read, so every one of these turns a hard failure into
    // a return code instead.
    _watt_do_exit = 0;
    survive_eth = 1;
    survive_bootp = 1;
    survive_dhcp = 1;

    const int rc = sock_init();
    if (rc != 0) {
        const char* msg = sock_init_err(rc);
        error = msg ? msg : "could not initialise the TCP/IP stack";
        return false;
    }

    g_stack_up = true;
    return true;
}

std::string Watt32HttpClient::default_headers() const
{
    std::string out;

    if (!m_bearer.empty())
        out += "Authorization: Bearer " + m_bearer + "\r\n";
    if (!m_client_version.empty())
        out += "X-API-Version: " + m_client_version + "\r\n";

    return out;
}

bool Watt32HttpClient::exchange(const char* verb, const std::string& url,
                                const std::string& body,
                                const std::string& content_type,
                                const std::string& extra, bool head_only,
                                Sink& sink, HttpResponse& out,
                                std::string& redirect_to)
{
    redirect_to.clear();

    Url u;
    if (!parse_url(url, u)) {
        out.body = "Malformed server URL: " + url;
        return false;
    }

    if (u.scheme == "https") {
        // Said plainly, because the fix is a server-side one and the user is
        // the only one who can make it.
        out.body = "HTTPS is not available on DOS: Watt-32 has no TLS. "
                   "Connect to this server over http:// instead.";
        return false;
    }

    std::string error;
    if (!ensure_stack(error)) {
        out.body = "No TCP/IP: " + error;
        return false;
    }

    const struct hostent* he = gethostbyname(u.host.c_str());
    if (!he || !he->h_addr_list || !he->h_addr_list[0]) {
        out.body = "Cannot resolve host: " + u.host;
        return false;
    }

    const int fd = socket(AF_INET, SOCK_STREAM, 0);
    if (fd < 0) {
        out.body = "Cannot create socket";
        return false;
    }

    set_timeout(fd, SO_SNDTIMEO, m_connect_timeout_ms);
    set_timeout(fd, SO_RCVTIMEO, m_recv_timeout_ms);

    struct sockaddr_in addr;
    std::memset(&addr, 0, sizeof(addr));
    addr.sin_family = AF_INET;
    addr.sin_port = htons((unsigned short)u.port);
    std::memcpy(&addr.sin_addr, he->h_addr_list[0], (size_t)he->h_length);

    if (connect(fd, (struct sockaddr*)&addr, sizeof(addr)) != 0) {
        closesocket(fd);
        char port_text[16];
        std::sprintf(port_text, "%d", u.port);
        out.body = "Cannot connect to " + u.host + ":" + port_text;
        return false;
    }

    // HTTP/1.0 + Connection: close. See the class comment.
    std::string head;
    head += std::string(verb) + " " + u.path + " HTTP/1.0\r\n";
    head += "Host: " + u.host;
    if (u.port != 80) {
        char port_text[16];
        std::sprintf(port_text, "%d", u.port);
        head += std::string(":") + port_text;
    }
    head += "\r\n";
    head += "Connection: close\r\n";
    head += "Accept: */*\r\n";
    head += default_headers();
    head += extra;

    if (!body.empty() || std::strcmp(verb, "POST") == 0 ||
        std::strcmp(verb, "PUT") == 0) {
        char len_text[32];
        std::sprintf(len_text, "%lu", (unsigned long)body.size());
        head += "Content-Length: " + std::string(len_text) + "\r\n";
        if (!content_type.empty())
            head += "Content-Type: " + content_type + "\r\n";
    }
    head += "\r\n";

    const char* p = head.c_str();
    std::string::size_type remaining = head.size();
    while (remaining > 0) {
        const int sent = send(fd, p, (int)remaining, 0);
        if (sent <= 0) {
            closesocket(fd);
            out.body = "Cannot send request";
            return false;
        }
        p += sent;
        remaining -= (std::string::size_type)sent;
    }

    if (!body.empty()) {
        p = body.c_str();
        remaining = body.size();
        while (remaining > 0) {
            const int sent = send(fd, p, (int)remaining, 0);
            if (sent <= 0) {
                closesocket(fd);
                out.body = "Cannot send request body";
                return false;
            }
            p += sent;
            remaining -= (std::string::size_type)sent;
        }
    }

    std::string raw_head, overflow;
    if (!read_head(fd, raw_head, overflow, error)) {
        closesocket(fd);
        out.body = error;
        return false;
    }

    out.status_code = parse_status(raw_head);
    out.headers.clear();
    parse_headers(raw_head, out.headers);

    if (out.status_code == 301 || out.status_code == 302 ||
        out.status_code == 303 || out.status_code == 307 ||
        out.status_code == 308) {
        const std::string* loc = find_header(out.headers, "location");
        if (loc && !loc->empty())
            redirect_to = join_url(url, *loc);
    }

    if (head_only) {
        closesocket(fd);
        return true;
    }

    unsigned long long total = 0;
    if (const std::string* cl = find_header(out.headers, "content-length"))
        total = (unsigned long long)std::strtoul(cl->c_str(), nullptr, 10);

    unsigned long long received = 0;
    bool aborted = false;

    sink.written = 0;

    // The first bytes of the body usually arrived with the head.
    if (!overflow.empty()) {
        if (sink.memory)
            sink.memory->append(overflow);
        if (sink.file)
            std::fwrite(overflow.data(), 1, overflow.size(), sink.file);
        received += overflow.size();
    }

    char buf[READ_CHUNK];
    for (;;) {
        const int got = recv(fd, buf, READ_CHUNK, 0);
        if (got < 0) {
            closesocket(fd);
            out.body = "Error reading response body";
            return false;
        }
        if (got == 0)
            break;

        if (sink.memory)
            sink.memory->append(buf, (std::string::size_type)got);
        if (sink.file &&
            std::fwrite(buf, 1, (size_t)got, sink.file) != (size_t)got) {
            closesocket(fd);
            out.body = "Could not write to the destination file";
            return false;
        }

        received += (unsigned long long)got;

        if (sink.progress && !sink.progress(received, total)) {
            aborted = true;
            break;
        }
    }

    closesocket(fd);

    sink.written = received;

    if (aborted) {
        out.body = "Cancelled";
        return false;
    }

    // A truncated transfer would otherwise look like a complete one: the
    // connection closing is how a 1.0 response normally ends.
    if (total > 0 && received < total) {
        out.body = "Connection closed before the whole body arrived";
        return false;
    }

    return true;
}

bool Watt32HttpClient::perform(const char* verb, const std::string& path,
                               const std::string& body,
                               const std::string& content_type,
                               const std::string& extra, bool head_only,
                               Sink& sink, HttpResponse& out,
                               bool follow_redirects)
{
    std::string url = join_url(m_base_url, path);

    for (int hop = 0; hop <= MAX_REDIRECTS; ++hop) {
        std::string redirect_to;

        // Each hop starts from a clean slate: a partial body from a 302 must
        // not be prepended to the real one.
        if (sink.memory)
            sink.memory->clear();
        if (sink.file) {
            std::rewind(sink.file);
            // Truncation is not portable through <cstdio>; rewinding is
            // enough because a redirect body is discarded and the real one
            // is written from the start and is longer.
        }

        out = HttpResponse();

        if (!exchange(verb, url, body, content_type, extra, head_only, sink,
                      out, redirect_to))
            return false;

        if (redirect_to.empty() || !follow_redirects)
            return true;

        url = redirect_to;
    }

    out.body = "Too many redirects";
    return false;
}

HttpResponse Watt32HttpClient::head(
    const std::string& path,
    const std::map<std::string, std::string>& extra_headers)
{
    std::string extra;
    for (std::map<std::string, std::string>::const_iterator it = extra_headers.begin();
         it != extra_headers.end(); ++it) {
        extra += it->first + ": " + it->second + "\r\n";
    }

    HttpResponse response;
    Sink sink;
    perform("HEAD", path, std::string(), std::string(), extra, true, sink,
            response, false);
    return response;
}

HttpResponse Watt32HttpClient::get(const std::string& path)
{
    HttpResponse response;
    Sink sink;
    sink.memory = &response.body;

    // Redirects are deliberately not followed for API calls: the server
    // answers an unauthenticated request with a 302 to the login page, and
    // following it would turn a 401 into a 200 with an HTML body.
    perform("GET", path, std::string(), std::string(), std::string(), false,
            sink, response, false);
    return response;
}

HttpResponse Watt32HttpClient::post(const std::string& path,
                                    const std::string& body,
                                    const std::string& content_type)
{
    HttpResponse response;
    Sink sink;
    sink.memory = &response.body;
    perform("POST", path, body, content_type, std::string(), false, sink,
            response, false);
    return response;
}

HttpResponse Watt32HttpClient::put(const std::string& path,
                                   const std::string& body,
                                   const std::string& content_type)
{
    HttpResponse response;
    Sink sink;
    sink.memory = &response.body;
    perform("PUT", path, body, content_type, std::string(), false, sink,
            response, false);
    return response;
}

HttpResponse Watt32HttpClient::del(const std::string& path)
{
    HttpResponse response;
    Sink sink;
    sink.memory = &response.body;
    perform("DELETE", path, std::string(), std::string(), std::string(), false,
            sink, response, false);
    return response;
}

bool Watt32HttpClient::download(const std::string& path,
                                const std::string& dest_path,
                                DownloadProgressFn progress)
{
    std::FILE* f = std::fopen(dest_path.c_str(), "wb");
    if (!f)
        return false;

    HttpResponse response;
    Sink sink;
    sink.file = f;
    sink.progress = progress;

    // Unlike the API verbs, a download must follow redirects: this is the
    // path a server takes when the archive lives in object storage.
    const bool ok = perform("GET", path, std::string(), std::string(),
                            std::string(), false, sink, response, true);

    // Redirect hops rewind the file rather than truncating it, because
    // <cstdio> has no portable truncate. Cut it to what the final response
    // actually wrote before anyone can read a byte past that.
    if (ok && std::fflush(f) == 0)
        ftruncate(fileno(f), (off_t)sink.written);

    std::fclose(f);

    if (!ok || !response.ok()) {
        std::remove(dest_path.c_str());
        return false;
    }

    return true;
}

HttpResponse Watt32HttpClient::post_multipart_file(const std::string& path,
                                                   const std::string& field_name,
                                                   const std::string& file_path)
{
    HttpResponse response;

    std::FILE* f = std::fopen(file_path.c_str(), "rb");
    if (!f) {
        response.body = "Could not open " + file_path;
        return response;
    }

    // Read into memory rather than streaming, which exchange() has no shape
    // for. The one caller uploads a save archive; if that ever grows past
    // what a DOS heap can hold, this needs a streaming body instead.
    std::string content;
    char buf[READ_CHUNK];
    for (;;) {
        const size_t got = std::fread(buf, 1, sizeof(buf), f);
        if (got == 0)
            break;
        content.append(buf, got);
    }
    std::fclose(f);

    const std::string boundary = "----LANCommanderDOSBoundary";

    // Name the part after the file, not the full path: a server has no use
    // for a local DOS path and some reject one outright.
    std::string leaf = file_path;
    const std::string::size_type slash = leaf.find_last_of("\\/");
    if (slash != std::string::npos)
        leaf = leaf.substr(slash + 1);

    std::string body;
    body += "--" + boundary + "\r\n";
    body += "Content-Disposition: form-data; name=\"" + field_name +
            "\"; filename=\"" + leaf + "\"\r\n";
    body += "Content-Type: application/octet-stream\r\n\r\n";
    body += content;
    body += "\r\n--" + boundary + "--\r\n";

    Sink sink;
    sink.memory = &response.body;
    perform("POST", path, body, "multipart/form-data; boundary=" + boundary,
            std::string(), false, sink, response, false);

    return response;
}

} // namespace lancommander
