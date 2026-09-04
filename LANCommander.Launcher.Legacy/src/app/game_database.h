#ifndef LAUNCHER_GAME_DATABASE_H
#define LAUNCHER_GAME_DATABASE_H

#include <string>
#include <utility>
#include <vector>

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

    struct PlaySessionRow
    {
        std::string id;
        std::string game_id;
        std::string user_id;
        std::string start;
        std::string end;   // empty while the game is still running
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

        std::string begin_play_session(const std::string &game_id,
                                       const std::string &user_id);

        void end_play_session(const std::string &session_id);

        long total_play_seconds(const std::string &game_id) const;

        std::string last_played(const std::string &game_id) const;

        void recent_games(int limit, std::vector<std::string> *out) const;

        void last_played_all(
            std::vector<std::pair<std::string, std::string> > *out) const;

    private:
        sqlite3 *m_db;

        void ensure_schema();
        void close_dangling_sessions();
    };

} // namespace launcher

#endif // LAUNCHER_GAME_DATABASE_H
