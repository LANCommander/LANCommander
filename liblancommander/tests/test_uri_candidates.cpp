#include "test_main.h"

#include "lancommander/util/uri.h"

#include <algorithm>

using namespace lancommander;

namespace {

bool contains(const std::vector<std::string>& v, const std::string& s)
{
    return std::find(v.begin(), v.end(), s) != v.end();
}

int index_of(const std::vector<std::string>& v, const std::string& s)
{
    for (size_t i = 0; i < v.size(); ++i) {
        if (v[i] == s)
            return static_cast<int>(i);
    }
    return -1;
}

// Ported verbatim from LANCommander.SDK.Tests/UriExtensions.cs. If these two
// ever disagree with the .NET suite, the launcher and the Avalonia client are
// probing different servers from the same typed address.
void test_dotnet_parity()
{
    {
        const std::vector<std::string> u = suggest_valid_uris("http://localhost:5000");

        CHECK_INT(u.size(), 10);
        CHECK(contains(u, "http://localhost:5000/"));
        CHECK(contains(u, "https://localhost:5000/"));
        CHECK(contains(u, "http://localhost/"));
        CHECK(contains(u, "http://localhost:443/"));
        CHECK(contains(u, "https://localhost/"));
        CHECK(contains(u, "https://localhost:80/"));
        CHECK(contains(u, "http://localhost:1337/"));
        CHECK(contains(u, "https://localhost:1337/"));
        CHECK(contains(u, "http://localhost:31337/"));
        CHECK(contains(u, "https://localhost:31337/"));
    }

    {
        const std::vector<std::string> u = suggest_valid_uris("10.0.1.10");

        CHECK_INT(u.size(), 8);
        CHECK(contains(u, "http://10.0.1.10:1337/"));
        CHECK(contains(u, "https://10.0.1.10:1337/"));
        CHECK(contains(u, "http://10.0.1.10/"));
        CHECK(contains(u, "http://10.0.1.10:443/"));
        CHECK(contains(u, "https://10.0.1.10/"));
        CHECK(contains(u, "https://10.0.1.10:80/"));
        CHECK(contains(u, "http://10.0.1.10:31337/"));
        CHECK(contains(u, "https://10.0.1.10:31337/"));
    }
}

// Order is not incidental: the first candidate that answers is the server the
// user ends up on. On a LAN where more than one host responds, a reordering
// silently changes which one that is.
void test_order()
{
    const std::vector<std::string> u = suggest_valid_uris("lancommander");

    CHECK_INT(u.size(), 8);
    CHECK_EQ(u[0], std::string("http://lancommander/"));
    CHECK_EQ(u[1], std::string("https://lancommander/"));
    CHECK_EQ(u[2], std::string("http://lancommander:1337/"));
    CHECK_EQ(u[3], std::string("http://lancommander:443/"));
    CHECK_EQ(u[4], std::string("http://lancommander:31337/"));
    CHECK_EQ(u[5], std::string("https://lancommander:1337/"));
    CHECK_EQ(u[6], std::string("https://lancommander:80/"));
    CHECK_EQ(u[7], std::string("https://lancommander:31337/"));

    // Both schemes are seeded before either is expanded across the known
    // ports, and http is tried before https throughout.
    CHECK(index_of(u, "http://lancommander/") < index_of(u, "http://lancommander:1337/"));
    CHECK(index_of(u, "https://lancommander/") < index_of(u, "https://lancommander:1337/"));
}

// An explicitly typed port is a statement of intent and suppresses the
// fan-out, so typing host:5000 cannot silently connect to something on :1337.
void test_explicit_port_suppression()
{
    {
        const std::vector<std::string> u = suggest_probe_uris("10.0.1.10:1337");
        CHECK_INT(u.size(), 2);
        CHECK_EQ(u[0], std::string("http://10.0.1.10:1337/"));
        CHECK_EQ(u[1], std::string("https://10.0.1.10:1337/"));
    }

    {
        // Scheme AND port given: exactly one candidate.
        const std::vector<std::string> u = suggest_probe_uris("http://localhost:5000");
        CHECK_INT(u.size(), 1);
        CHECK_EQ(u[0], std::string("http://localhost:5000/"));
    }

    {
        // No port: the full fan-out still applies.
        const std::vector<std::string> u = suggest_probe_uris("lancommander");
        CHECK_INT(u.size(), 8);
    }

    {
        // A scheme without a port is not an explicit port.
        const std::vector<std::string> u = suggest_probe_uris("http://lancommander");
        CHECK(u.size() > 1);
    }
}

void test_normalisation()
{
    // Default ports are omitted from the rendered form, exactly as a URI
    // normalises them. Getting this wrong would make dedupe miss and probe
    // the same server twice.
    CHECK_EQ(format_uri("http", "host", 80), std::string("http://host/"));
    CHECK_EQ(format_uri("https", "host", 443), std::string("https://host/"));
    CHECK_EQ(format_uri("http", "host", 443), std::string("http://host:443/"));
    CHECK_EQ(format_uri("https", "host", 80), std::string("https://host:80/"));
    CHECK_EQ(format_uri("http", "host", 1337), std::string("http://host:1337/"));

    CHECK_INT(default_port_for_scheme("http"), 80);
    CHECK_INT(default_port_for_scheme("https"), 443);
    CHECK_INT(default_port_for_scheme("HTTPS"), 443);

    // No candidate may appear twice.
    const std::vector<std::string> u = suggest_valid_uris("lancommander");
    for (size_t i = 0; i < u.size(); ++i) {
        for (size_t j = i + 1; j < u.size(); ++j)
            CHECK(u[i] != u[j]);
    }
}

void test_edge_cases()
{
    CHECK_INT(suggest_valid_uris("").size(), 0);
    CHECK_INT(suggest_valid_uris("   ").size(), 0);
    CHECK_INT(suggest_probe_uris("").size(), 0);

    // A scheme with nothing after it yields nothing rather than "http:///".
    CHECK_INT(suggest_valid_uris("http://").size(), 0);

    // Surrounding whitespace, which a paste into the address box will carry.
    {
        const std::vector<std::string> u = suggest_valid_uris("  lancommander  ");
        CHECK_INT(u.size(), 8);
        CHECK_EQ(u[0], std::string("http://lancommander/"));
    }

    // A path is discarded: every endpoint is rooted at the server.
    {
        const std::vector<std::string> u = suggest_valid_uris("lancommander/some/path");
        CHECK_EQ(u[0], std::string("http://lancommander/"));
    }

    // A trailing colon with no digits is not a port, and must not be eaten
    // out of the hostname.
    {
        const std::vector<std::string> u = suggest_valid_uris("lancommander:");
        CHECK(!u.empty());
        CHECK_EQ(u[0], std::string("http://lancommander:/"));
    }

    // Uppercase scheme is normalised rather than treated as a new scheme.
    {
        const std::vector<std::string> u = suggest_valid_uris("HTTP://lancommander");
        CHECK(!u.empty());
        CHECK_EQ(u[0], std::string("http://lancommander/"));
    }
}

} // namespace

void test_uri_candidates()
{
    test_dotnet_parity();
    test_order();
    test_explicit_port_suppression();
    test_normalisation();
    test_edge_cases();
}
