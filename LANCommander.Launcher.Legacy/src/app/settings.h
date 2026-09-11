#ifndef LAUNCHER_SETTINGS_H
#define LAUNCHER_SETTINGS_H

#include <string>
#include <vector>

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
    //     InstallDirectories   -> games.install_directories
    //   Launcher:
    //     Username             -> launcher.username
    //   Debug:
    //     EnableScriptDebugging -> debug.enable_script_debugging

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
        std::vector<std::string> install_directories;

        GameSettings()
        {
            install_directories.push_back("C:\\Games");
        }
    };

    struct LauncherSettings
    {
        std::string username;
    };

    struct DebugSettings
    {
        // Named to match LANCommander.SDK's DebugSettings.EnableScriptDebugging,
        // because both launchers read the same Settings.yml -- turning script
        // debugging on in one is meant to turn it on in the other.
        //
        // In the .NET SDK this makes a script echo its type and working
        // directory before it runs, and break afterwards. Here it does the
        // same echo AND arms the interpreter's per-statement hook, which is
        // what lets a script stop at its entry and at breakpoints.
        bool enable_script_debugging;

        DebugSettings() : enable_script_debugging(false) {}
    };

    struct Settings
    {
        AuthenticationSettings authentication;
        GameSettings games;
        LauncherSettings launcher;
        DebugSettings debug;

        // Load from YAML file. Returns false if file doesn't exist (defaults are kept).
        bool load(const std::string &path);

        // Save to YAML file. Returns false on I/O error.
        bool save(const std::string &path) const;
    };

} // namespace launcher

#endif // LAUNCHER_SETTINGS_H
