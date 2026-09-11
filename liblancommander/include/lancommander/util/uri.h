#ifndef LANCOMMANDER_UTIL_URI_H
#define LANCOMMANDER_UTIL_URI_H

#include <string>
#include <vector>

// Candidate server URLs for a partially specified address.
//
// A user at a LAN party types "lancommander", not "http://lancommander:1337".
// The .NET SDK turns the short form into a list of plausible URLs and probes
// each one; this is the same generator, ported so the C++ launcher behaves
// identically. See LANCommander.SDK/Extensions/UriExtensions.cs.
//
// Pure string manipulation — no sockets, no I/O — so the ordering can be
// pinned by tests against the same cases the .NET suite pins.

namespace lancommander {

// Default ports by scheme, as a URL string omits them.
int default_port_for_scheme(const std::string& scheme);

// Renders scheme/host/port the way a URI normalises: the default port for the
// scheme is omitted, and the path is a bare "/".
std::string format_uri(const std::string& scheme, const std::string& host, int port);

// Every candidate worth trying, in probe order.
//
// For a bare host this is 8 entries: the two schemes at their default ports,
// then each expanded across the known LANCommander ports {1337, 80, 443,
// 31337}, skipping any that would duplicate the seed's own port. For an
// absolute URL carrying an explicit port it is 10, because the seed port
// survives alongside the four known ones.
//
// Order is part of the contract: the first candidate that answers wins, so
// reordering changes which server a user connects to on a network where more
// than one responds.
std::vector<std::string> suggest_valid_uris(const std::string& input);

// The list actually probed, which is suggest_valid_uris() plus the rule that
// an explicitly specified port is a statement of intent:
//
//   "host:5000"        -> just the two schemes at :5000, nothing else
//   "http://host:5000" -> just that one URL
//   "host"             -> the full fan-out
//
// Without this, typing a port would still hammer :1337 and :31337 and could
// connect to a different server than the one that was asked for.
std::vector<std::string> suggest_probe_uris(const std::string& input);

} // namespace lancommander

#endif // LANCOMMANDER_UTIL_URI_H
