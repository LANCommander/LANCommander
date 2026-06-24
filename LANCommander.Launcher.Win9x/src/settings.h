#ifndef LANCOMMANDER_WIN9X_SETTINGS_H
#define LANCOMMANDER_WIN9X_SETTINGS_H

#include <string>

// Persisted as an INI-style key=value file next to the exe.
class Settings
{
public:
    void Load();
    void Save();
    void Clear();

    std::string serverUrl;
    std::string userName;
    std::string accessToken;
    std::string refreshToken;
    std::string expiration; // server-supplied ISO string; opaque to us

    // Display name passed to NameChange scripts. Synced with /api/Profile so
    // the same alias appears in-game across machines.
    std::string alias;

    // View preferences.
    bool        showLibraryOnly;
    bool        showInstalledOnly;
    std::string filterGenre; // "" = no genre filter
    int         viewMode;    // 0 = List, 1 = Grid, 2 = Shelf

    // User-configurable preferences (Settings dialog).
    std::string defaultInstallDir; // "" = prompt every time
    int         discoveryTimeoutMs; // server-discovery probe window

    Settings()
        : showLibraryOnly(true), showInstalledOnly(false), viewMode(0),
          discoveryTimeoutMs(2000) {}

private:
    std::string FilePath() const;
};

#endif
