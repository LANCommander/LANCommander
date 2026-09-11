#include "lancommander/util/uri.h"

#include <cctype>
#include <cstdio>
#include <cstdlib>

namespace lancommander {

namespace {

const int KNOWN_PORTS[] = { 1337, 80, 443, 31337 };
const int KNOWN_PORT_COUNT = 4;

std::string trim(const std::string& s)
{
    size_t b = 0;
    size_t e = s.size();
    while (b < e && std::isspace(static_cast<unsigned char>(s[b]))) ++b;
    while (e > b && std::isspace(static_cast<unsigned char>(s[e - 1]))) --e;
    return s.substr(b, e - b);
}

std::string to_lower(const std::string& s)
{
    std::string out = s;
    for (size_t i = 0; i < out.size(); ++i)
        out[i] = static_cast<char>(std::tolower(static_cast<unsigned char>(out[i])));
    return out;
}

struct Seed {
    std::string scheme;
    std::string host;   // authority without the port
    int port;
};

// Splits "host", "host:1337" or "host/path" into a host and a port. The path
// is discarded: every LANCommander endpoint is rooted at the server, so a
// path in the typed address is noise rather than information.
void split_authority(const std::string& authority, const std::string& scheme,
                     std::string* host_out, int* port_out, bool* explicit_port_out)
{
    std::string a = authority;

    const size_t slash = a.find('/');
    if (slash != std::string::npos)
        a.erase(slash);

    *explicit_port_out = false;
    *port_out = default_port_for_scheme(scheme);
    *host_out = a;

    const size_t colon = a.rfind(':');
    if (colon == std::string::npos)
        return;

    const std::string port_str = a.substr(colon + 1);
    if (port_str.empty())
        return;

    for (size_t i = 0; i < port_str.size(); ++i)
    {
        if (!std::isdigit(static_cast<unsigned char>(port_str[i])))
            return; // not a port; leave the host as typed
    }

    *host_out = a.substr(0, colon);
    *port_out = std::atoi(port_str.c_str());
    *explicit_port_out = true;
}

void add_unique(std::vector<std::string>* out, const std::string& uri)
{
    for (size_t i = 0; i < out->size(); ++i)
    {
        if ((*out)[i] == uri)
            return;
    }
    out->push_back(uri);
}

// True when the input carries an explicit port, matching the .NET check:
// strip the scheme delimiter, then look for a colon.
bool has_explicit_port(const std::string& input)
{
    std::string s = input;
    const size_t d = s.find("://");
    if (d != std::string::npos)
        s.erase(d, 3);
    return s.find(':') != std::string::npos;
}

} // namespace

int default_port_for_scheme(const std::string& scheme)
{
    return to_lower(scheme) == "https" ? 443 : 80;
}

std::string format_uri(const std::string& scheme, const std::string& host, int port)
{
    std::string out = scheme + "://" + host;

    if (port != default_port_for_scheme(scheme))
    {
        char buf[16];
        std::sprintf(buf, ":%d", port);
        out += buf;
    }

    out += "/";
    return out;
}

std::vector<std::string> suggest_valid_uris(const std::string& input_raw)
{
    std::vector<std::string> out;

    const std::string input = trim(input_raw);
    if (input.empty())
        return out;

    // --- Seeds ---------------------------------------------------------
    std::vector<Seed> seeds;

    const size_t delim = input.find("://");
    if (delim != std::string::npos)
    {
        const std::string scheme = to_lower(input.substr(0, delim));
        const std::string authority = input.substr(delim + 3);

        if (authority.empty())
            return out;

        std::string host;
        int port = 0;
        bool explicit_port = false;
        split_authority(authority, scheme, &host, &port, &explicit_port);

        if (host.empty())
            return out;

        Seed a;
        a.scheme = scheme;
        a.host = host;
        a.port = port;
        seeds.push_back(a);

        // The other scheme at the SAME port. Not the other scheme's default:
        // this mirrors UriBuilder{ Scheme = ..., Port = uri.Port }, which is
        // why "http://host:5000" also probes "https://host:5000".
        Seed b;
        b.scheme = (scheme == "http") ? "https" : "http";
        b.host = host;
        b.port = port;
        seeds.push_back(b);
    }
    else
    {
        // No scheme: both schemes at their own defaults.
        std::string host;
        int port = 0;
        bool explicit_port = false;
        split_authority(input, "http", &host, &port, &explicit_port);

        if (host.empty())
            return out;

        Seed a;
        a.scheme = "http";
        a.host = host;
        a.port = explicit_port ? port : 80;
        seeds.push_back(a);

        Seed b;
        b.scheme = "https";
        b.host = host;
        b.port = explicit_port ? port : 443;
        seeds.push_back(b);
    }

    for (size_t i = 0; i < seeds.size(); ++i)
        add_unique(&out, format_uri(seeds[i].scheme, seeds[i].host, seeds[i].port));

    // --- Known-port expansion -------------------------------------------
    //
    // Iterates the seeds only, not the growing list, which is what the .NET
    // version does by snapshotting with ToArray(). Expanding the expansions
    // would produce the same set with a different, wrong order.
    for (size_t i = 0; i < seeds.size(); ++i)
    {
        for (int p = 0; p < KNOWN_PORT_COUNT; ++p)
        {
            if (seeds[i].port == KNOWN_PORTS[p])
                continue;
            add_unique(&out, format_uri(seeds[i].scheme, seeds[i].host, KNOWN_PORTS[p]));
        }
    }

    return out;
}

std::vector<std::string> suggest_probe_uris(const std::string& input_raw)
{
    std::vector<std::string> all = suggest_valid_uris(input_raw);

    const std::string input = trim(input_raw);
    if (!has_explicit_port(input))
        return all;

    // An explicit port means the user knows where the server is. Keep only
    // the seeds: one when a scheme was given too, both schemes otherwise.
    const bool absolute = input.find("://") != std::string::npos;
    const size_t keep = absolute ? 1 : 2;

    if (all.size() > keep)
        all.resize(keep);
    return all;
}

} // namespace lancommander
