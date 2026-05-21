#include "app/settings.h"

#include <cstdio>
#include <cstring>

namespace launcher
{

    Settings::Settings()
        : offline_mode(false)
    {
    }

    // Minimal INI parser — reads "key=value" lines, ignores sections and comments.
    bool Settings::load(const std::string &path)
    {
        FILE *f = fopen(path.c_str(), "r");
        if (!f)
            return false;

        char line[1024];
        while (fgets(line, sizeof(line), f))
        {
            // Strip newline
            size_t len = strlen(line);
            while (len > 0 && (line[len - 1] == '\n' || line[len - 1] == '\r'))
                line[--len] = '\0';

            // Skip empty lines, comments, sections
            if (len == 0 || line[0] == '#' || line[0] == ';' || line[0] == '[')
                continue;

            char *eq = strchr(line, '=');
            if (!eq)
                continue;

            *eq = '\0';
            std::string key(line);
            std::string value(eq + 1);

            if (key == "server_address")
                server_address = value;
            else if (key == "access_token")
                access_token = value;
            else if (key == "refresh_token")
                refresh_token = value;
            else if (key == "install_directory")
                install_directory = value;
            else if (key == "username")
                username = value;
            else if (key == "offline_mode")
                offline_mode = (value == "1" || value == "true");
        }

        fclose(f);
        return true;
    }

    bool Settings::save(const std::string &path) const
    {
        FILE *f = fopen(path.c_str(), "w");
        
        if (!f)
            return false;

        fprintf(f, "[launcher]\n");
        fprintf(f, "server_address=%s\n", server_address.c_str());
        fprintf(f, "access_token=%s\n", access_token.c_str());
        fprintf(f, "refresh_token=%s\n", refresh_token.c_str());
        fprintf(f, "install_directory=%s\n", install_directory.c_str());
        fprintf(f, "username=%s\n", username.c_str());
        fprintf(f, "offline_mode=%s\n", offline_mode ? "1" : "0");

        fclose(f);
        return true;
    }

} // namespace launcher
