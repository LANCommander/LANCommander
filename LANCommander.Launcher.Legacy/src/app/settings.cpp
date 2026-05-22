#include "app/settings.h"

#include <cstdio>
#include <cstring>
#include <string>

namespace launcher
{

    // -----------------------------------------------------------------------
    // Minimal YAML helpers
    //
    // The Settings.yml schema uses only scalar values and a fixed two-level
    // nesting depth, so a full YAML parser is unnecessary.  We track the
    // current section(s) by indentation and match "Key: Value" lines.
    // -----------------------------------------------------------------------

    // Trim leading whitespace; returns the number of spaces removed.
    static int ltrim(const char *s, const char **out)
    {
        int n = 0;
        while (*s == ' ')
        {
            ++s;
            ++n;
        }
        *out = s;
        return n;
    }

    // Trim trailing whitespace/newlines in-place.
    static void rtrim(char *s)
    {
        size_t len = strlen(s);
        while (len > 0 && (s[len - 1] == '\n' || s[len - 1] == '\r' || s[len - 1] == ' '))
            s[--len] = '\0';
    }

    // Split "Key: Value" at the first ": ".  Returns false if no separator.
    static bool split_kv(const char *line, std::string &key, std::string &value)
    {
        const char *sep = strstr(line, ": ");
        if (!sep)
        {
            // Could be a section header ("Key:" with no value)
            size_t len = strlen(line);
            if (len > 0 && line[len - 1] == ':')
            {
                key.assign(line, len - 1);
                value.clear();
                return true;
            }
            return false;
        }

        key.assign(line, sep - line);
        value.assign(sep + 2);
        return true;
    }

    bool Settings::load(const std::string &path)
    {
        FILE *f = fopen(path.c_str(), "r");
        if (!f)
            return false;

        char line[1024];
        std::string section;    // top-level section  (e.g. "Authentication")
        std::string subsection; // nested section     (e.g. "Token")

        while (fgets(line, sizeof(line), f))
        {
            rtrim(line);

            const char *trimmed;
            int indent = ltrim(line, &trimmed);

            // Skip empty lines and comments
            if (*trimmed == '\0' || *trimmed == '#')
                continue;

            std::string key, value;
            if (!split_kv(trimmed, key, value))
                continue;

            if (indent == 0)
            {
                // Top-level key or section header
                section = key;
                subsection.clear();

                if (value.empty())
                    continue; // section header only
            }
            else if (indent == 2)
            {
                if (value.empty())
                {
                    // Nested section header (e.g. "  Token:")
                    subsection = key;
                    continue;
                }
            }
            else if (indent == 4)
            {
                // Value inside a subsection — key is already set
            }

            // --- Map YAML keys to settings fields ---

            if (section == "Authentication")
            {
                if (subsection.empty())
                {
                    if (key == "ServerAddress")
                        authentication.server_address = value;
                    else if (key == "OfflineModeEnabled")
                        authentication.offline_mode = (value == "true");
                }
                else if (subsection == "Token")
                {
                    if (key == "AccessToken")
                        authentication.token.access_token = value;
                    else if (key == "RefreshToken")
                        authentication.token.refresh_token = value;
                }
            }
            else if (section == "Games")
            {
                if (key == "InstallDirectories")
                {
                    // Inline flow sequence: [path]
                    // Strip surrounding brackets if present.
                    if (!value.empty() && value[0] == '[')
                    {
                        value = value.substr(1);
                        if (!value.empty() && value[value.size() - 1] == ']')
                            value = value.substr(0, value.size() - 1);
                    }
                    // Take the first (or only) entry.
                    if (!value.empty())
                        games.install_directory = value;
                }
                else if (key == "-")
                {
                    // Block sequence item under InstallDirectories
                    if (games.install_directory.empty())
                        games.install_directory = value;
                }
            }
            else if (section == "Launcher")
            {
                if (key == "Username")
                    launcher.username = value;
            }
        }

        // Handle block-sequence InstallDirectories:
        // Re-scan for "- value" lines under Games/InstallDirectories.
        // Already handled above via the "-" key fallback.

        fclose(f);
        return true;
    }

    bool Settings::save(const std::string &path) const
    {
        FILE *f = fopen(path.c_str(), "w");
        if (!f)
            return false;

        fprintf(f, "Authentication:\n");
        fprintf(f, "  ServerAddress: %s\n", authentication.server_address.c_str());
        fprintf(f, "  Token:\n");
        fprintf(f, "    AccessToken: %s\n", authentication.token.access_token.c_str());
        fprintf(f, "    RefreshToken: %s\n", authentication.token.refresh_token.c_str());
        fprintf(f, "  OfflineModeEnabled: %s\n", authentication.offline_mode ? "true" : "false");

        fprintf(f, "Games:\n");
        if (!games.install_directory.empty())
            fprintf(f, "  InstallDirectories:\n  - %s\n", games.install_directory.c_str());
        else
            fprintf(f, "  InstallDirectories: []\n");

        fprintf(f, "Launcher:\n");
        fprintf(f, "  Username: %s\n", launcher.username.c_str());

        fclose(f);
        return true;
    }

} // namespace launcher
