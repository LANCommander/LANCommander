#ifndef LAUNCHER_SETTINGS_H
#define LAUNCHER_SETTINGS_H

#include <string>

namespace launcher
{

    // Matches the YAML schema used by the Avalonia launcher (Settings.yml).
    //
    // Only a subset of the full schema is relevant to this legacy launcher.
    // PascalCase YAML keys map to snake_case C++ members:
    //
    //   Authentication:
    //     ServerAddress        -> authentication.server_address
    //     Token:
    //       AccessToken        -> authentication.access_token
    //       RefreshToken       -> authentication.refresh_token
    //     OfflineModeEnabled   -> authentication.offline_mode
    //   Games:
    //     InstallDirectories   -> games.install_directory  (first entry)
    //   Launcher:
    //     Username             -> launcher.username

    struct AuthenticationToken
    {
        std::string access_token;
        std::string refresh_token;
    };

    struct AuthenticationSettings
    {
        std::string server_address;
        AuthenticationToken token;
        bool offline_mode;

        AuthenticationSettings() : offline_mode(false) {}
    };

    struct GameSettings
    {
        std::string install_directory;
    };

    struct LauncherSettings
    {
        std::string username;
    };

    struct Settings
    {
        AuthenticationSettings authentication;
        GameSettings games;
        LauncherSettings launcher;

        // Load from YAML file. Returns false if file doesn't exist (defaults are kept).
        bool load(const std::string &path);

        // Save to YAML file. Returns false on I/O error.
        bool save(const std::string &path) const;
    };

} // namespace launcher

#endif // LAUNCHER_SETTINGS_H
