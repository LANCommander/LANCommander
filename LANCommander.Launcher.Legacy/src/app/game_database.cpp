#include "app/game_database.h"

#include <sqlite3.h>
#include <cstdio>
#include <ctime>

namespace launcher
{

    GameDatabase::GameDatabase()
        : m_db(NULL)
    {
    }

    GameDatabase::~GameDatabase()
    {
        close();
    }

    bool GameDatabase::open(const std::string &path)
    {
        if (m_db)
            close();

        int rc = sqlite3_open(path.c_str(), &m_db);
        if (rc != SQLITE_OK)
        {
            m_db = NULL;
            return false;
        }

        // Enable WAL mode for better concurrent access.
        sqlite3_exec(m_db, "PRAGMA journal_mode=WAL;", NULL, NULL, NULL);

        ensure_schema();
        return true;
    }

    void GameDatabase::close()
    {
        if (m_db)
        {
            sqlite3_close(m_db);
            m_db = NULL;
        }
    }

    void GameDatabase::ensure_schema()
    {
        const char *sql =
            "CREATE TABLE IF NOT EXISTS Games ("
            "  Id TEXT PRIMARY KEY NOT NULL,"
            "  InstallDirectory TEXT,"
            "  InstalledVersion TEXT,"
            "  InstalledOn TEXT,"
            "  Installed INTEGER NOT NULL DEFAULT 1"
            ");";

        sqlite3_exec(m_db, sql, NULL, NULL, NULL);
    }

    bool GameDatabase::find(const std::string &game_id, InstalledGame *out) const
    {
        if (!m_db || !out)
            return false;

        const char *sql =
            "SELECT InstallDirectory, InstalledVersion, InstalledOn "
            "FROM Games WHERE Id = ? AND Installed = 1;";

        sqlite3_stmt *stmt = NULL;
        int rc = sqlite3_prepare_v2(m_db, sql, -1, &stmt, NULL);
        if (rc != SQLITE_OK)
            return false;

        sqlite3_bind_text(stmt, 1, game_id.c_str(), -1, SQLITE_TRANSIENT);

        bool found = false;
        if (sqlite3_step(stmt) == SQLITE_ROW)
        {
            out->game_id = game_id;

            const char *dir = (const char *)sqlite3_column_text(stmt, 0);
            out->install_directory = dir ? dir : "";

            const char *ver = (const char *)sqlite3_column_text(stmt, 1);
            out->version = ver ? ver : "";

            const char *date = (const char *)sqlite3_column_text(stmt, 2);
            out->installed_on = date ? date : "";

            found = true;
        }

        sqlite3_finalize(stmt);
        return found;
    }

    void GameDatabase::set_installed(const std::string &game_id,
                                     const std::string &install_directory,
                                     const std::string &version)
    {
        if (!m_db)
            return;

        // Generate current timestamp.
        time_t now = time(NULL);
        struct tm *t = localtime(&now);
        char ts[32];
        sprintf(ts, "%04d-%02d-%02dT%02d:%02d:%02d",
                t->tm_year + 1900, t->tm_mon + 1, t->tm_mday,
                t->tm_hour, t->tm_min, t->tm_sec);

        const char *sql =
            "INSERT INTO Games (Id, InstallDirectory, InstalledVersion, InstalledOn, Installed) "
            "VALUES (?, ?, ?, ?, 1) "
            "ON CONFLICT(Id) DO UPDATE SET "
            "  InstallDirectory = excluded.InstallDirectory,"
            "  InstalledVersion = excluded.InstalledVersion,"
            "  InstalledOn = COALESCE(Games.InstalledOn, excluded.InstalledOn),"
            "  Installed = 1;";

        sqlite3_stmt *stmt = NULL;
        int rc = sqlite3_prepare_v2(m_db, sql, -1, &stmt, NULL);
        if (rc != SQLITE_OK)
            return;

        sqlite3_bind_text(stmt, 1, game_id.c_str(), -1, SQLITE_TRANSIENT);
        sqlite3_bind_text(stmt, 2, install_directory.c_str(), -1, SQLITE_TRANSIENT);
        sqlite3_bind_text(stmt, 3, version.c_str(), -1, SQLITE_TRANSIENT);
        sqlite3_bind_text(stmt, 4, ts, -1, SQLITE_TRANSIENT);

        sqlite3_step(stmt);
        sqlite3_finalize(stmt);
    }

    void GameDatabase::set_uninstalled(const std::string &game_id)
    {
        if (!m_db)
            return;

        const char *sql =
            "UPDATE Games SET Installed = 0, InstallDirectory = NULL "
            "WHERE Id = ?;";

        sqlite3_stmt *stmt = NULL;
        int rc = sqlite3_prepare_v2(m_db, sql, -1, &stmt, NULL);
        if (rc != SQLITE_OK)
            return;

        sqlite3_bind_text(stmt, 1, game_id.c_str(), -1, SQLITE_TRANSIENT);

        sqlite3_step(stmt);
        sqlite3_finalize(stmt);
    }

} // namespace launcher
