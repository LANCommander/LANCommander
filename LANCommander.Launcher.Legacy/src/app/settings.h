#ifndef LAUNCHER_SETTINGS_H
#define LAUNCHER_SETTINGS_H

#include <string>

namespace launcher
{

    // Persistent launcher settings, saved to an INI file.
    struct Settings
    {
        std::string server_address;
        std::string access_token;
        std::string refresh_token;
        std::string install_directory;
        std::string username; // last used username (for convenience, not security)
        bool offline_mode;

        Settings();

        // Load from file. Returns false if file doesn't exist (defaults are kept).
        bool load(const std::string &path);

        // Save to file. Returns false on I/O error.
        bool save(const std::string &path) const;
    };

} // namespace launcher

#endif // LAUNCHER_SETTINGS_H
