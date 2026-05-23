#ifndef LAUNCHER_GAME_DATABASE_H
#define LAUNCHER_GAME_DATABASE_H

#include <string>

// Forward-declare to avoid pulling sqlite3.h into every translation unit.
struct sqlite3;

namespace launcher
{

    // Tracks local installation state for games using SQLite.
    // Mirrors the schema used by the Avalonia launcher.

    struct InstalledGame
    {
        std::string game_id;
        std::string install_directory;
        std::string version;
        std::string installed_on; // ISO 8601 date string
    };

    class GameDatabase
    {
    public:
        GameDatabase();
        ~GameDatabase();

        // Open (or create) the database at the given path.
        bool open(const std::string &path);

        // Close the database.
        void close();

        // Look up a game by ID.  Returns false if not found.
        bool find(const std::string &game_id, InstalledGame *out) const;

        // Mark a game as installed (inserts or updates the row).
        void set_installed(const std::string &game_id,
                           const std::string &install_directory,
                           const std::string &version = std::string());

        // Remove a game's row (uninstall).
        void set_uninstalled(const std::string &game_id);

    private:
        sqlite3 *m_db;

        void ensure_schema();
    };

} // namespace launcher

#endif // LAUNCHER_GAME_DATABASE_H
