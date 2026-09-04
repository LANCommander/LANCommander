#include "app/game_database.h"
#include "app/time_util.h"

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

        const char *sessions_sql =
            "CREATE TABLE IF NOT EXISTS PlaySessions ("
            "  Id TEXT PRIMARY KEY NOT NULL,"
            "  GameId TEXT NOT NULL,"
            "  UserId TEXT,"
            "  Start TEXT NOT NULL,"
            "  End TEXT"
            ");"
            "CREATE INDEX IF NOT EXISTS IX_PlaySessions_GameId ON PlaySessions(GameId);";

        sqlite3_exec(m_db, sessions_sql, NULL, NULL, NULL);

        close_dangling_sessions();
    }

    void GameDatabase::close_dangling_sessions()
    {
        const char *sql =
            "UPDATE PlaySessions SET End = Start WHERE End IS NULL;";

        sqlite3_exec(m_db, sql, NULL, NULL, NULL);
    }

    // -----------------------------------------------------------------------
    // Play sessions
    // -----------------------------------------------------------------------

    std::string GameDatabase::begin_play_session(const std::string &game_id,
                                                 const std::string &user_id)
    {
        if (!m_db || game_id.empty())
            return std::string();

        const std::string now = iso8601_utc_now();

        static int counter = 0;
        char id_buf[96];
        std::sprintf(id_buf, "%.8s-%s-%d", game_id.c_str(), now.c_str(), ++counter);
        const std::string id = id_buf;

        const char *sql =
            "INSERT INTO PlaySessions (Id, GameId, UserId, Start, End) "
            "VALUES (?, ?, ?, ?, NULL);";

        sqlite3_stmt *stmt = NULL;
        if (sqlite3_prepare_v2(m_db, sql, -1, &stmt, NULL) != SQLITE_OK)
            return std::string();

        sqlite3_bind_text(stmt, 1, id.c_str(), -1, SQLITE_TRANSIENT);
        sqlite3_bind_text(stmt, 2, game_id.c_str(), -1, SQLITE_TRANSIENT);
        sqlite3_bind_text(stmt, 3, user_id.c_str(), -1, SQLITE_TRANSIENT);
        sqlite3_bind_text(stmt, 4, now.c_str(), -1, SQLITE_TRANSIENT);

        const int rc = sqlite3_step(stmt);
        sqlite3_finalize(stmt);

        return (rc == SQLITE_DONE) ? id : std::string();
    }

    void GameDatabase::end_play_session(const std::string &session_id)
    {
        if (!m_db || session_id.empty())
            return;

        const std::string now = iso8601_utc_now();

        const char *sql =
            "UPDATE PlaySessions SET End = ? WHERE Id = ? AND End IS NULL;";

        sqlite3_stmt *stmt = NULL;
        if (sqlite3_prepare_v2(m_db, sql, -1, &stmt, NULL) != SQLITE_OK)
            return;

        sqlite3_bind_text(stmt, 1, now.c_str(), -1, SQLITE_TRANSIENT);
        sqlite3_bind_text(stmt, 2, session_id.c_str(), -1, SQLITE_TRANSIENT);

        sqlite3_step(stmt);
        sqlite3_finalize(stmt);
    }

    long GameDatabase::total_play_seconds(const std::string &game_id) const
    {
        if (!m_db || game_id.empty())
            return 0;

        const char *sql =
            "SELECT Start, End FROM PlaySessions "
            "WHERE GameId = ? AND End IS NOT NULL;";

        sqlite3_stmt *stmt = NULL;
        if (sqlite3_prepare_v2(m_db, sql, -1, &stmt, NULL) != SQLITE_OK)
            return 0;

        sqlite3_bind_text(stmt, 1, game_id.c_str(), -1, SQLITE_TRANSIENT);

        long total = 0;
        while (sqlite3_step(stmt) == SQLITE_ROW)
        {
            const char *s = (const char *)sqlite3_column_text(stmt, 0);
            const char *e = (const char *)sqlite3_column_text(stmt, 1);
            if (!s || !e)
                continue;

            const std::time_t st = iso8601_to_time_t(s);
            const std::time_t en = iso8601_to_time_t(e);

            if (st == 0 || en == 0 || en < st)
                continue;

            total += (long)(en - st);
        }

        sqlite3_finalize(stmt);
        return total;
    }

    std::string GameDatabase::last_played(const std::string &game_id) const
    {
        if (!m_db || game_id.empty())
            return std::string();

        const char *sql =
            "SELECT End FROM PlaySessions "
            "WHERE GameId = ? AND End IS NOT NULL "
            "ORDER BY End DESC LIMIT 1;";

        sqlite3_stmt *stmt = NULL;
        if (sqlite3_prepare_v2(m_db, sql, -1, &stmt, NULL) != SQLITE_OK)
            return std::string();

        sqlite3_bind_text(stmt, 1, game_id.c_str(), -1, SQLITE_TRANSIENT);

        std::string out;
        if (sqlite3_step(stmt) == SQLITE_ROW)
        {
            const char *e = (const char *)sqlite3_column_text(stmt, 0);
            if (e)
                out = e;
        }

        sqlite3_finalize(stmt);
        return out;
    }

    void GameDatabase::last_played_all(
        std::vector<std::pair<std::string, std::string> > *out) const
    {
        if (!out)
            return;
        out->clear();

        if (!m_db)
            return;

        const char *sql =
            "SELECT GameId, MAX(End) AS LastEnd FROM PlaySessions "
            "WHERE End IS NOT NULL GROUP BY GameId;";

        sqlite3_stmt *stmt = NULL;
        if (sqlite3_prepare_v2(m_db, sql, -1, &stmt, NULL) != SQLITE_OK)
            return;

        while (sqlite3_step(stmt) == SQLITE_ROW)
        {
            const char *g = (const char *)sqlite3_column_text(stmt, 0);
            const char *e = (const char *)sqlite3_column_text(stmt, 1);
            if (g && e)
                out->push_back(std::make_pair(std::string(g), std::string(e)));
        }

        sqlite3_finalize(stmt);
    }

    void GameDatabase::recent_games(int limit, std::vector<std::string> *out) const
    {
        if (!out)
            return;
        out->clear();

        if (!m_db || limit < 1)
            return;

        const char *sql =
            "SELECT GameId, MAX(End) AS LastEnd FROM PlaySessions "
            "WHERE End IS NOT NULL "
            "GROUP BY GameId ORDER BY LastEnd DESC LIMIT ?;";

        sqlite3_stmt *stmt = NULL;
        if (sqlite3_prepare_v2(m_db, sql, -1, &stmt, NULL) != SQLITE_OK)
            return;

        sqlite3_bind_int(stmt, 1, limit);

        while (sqlite3_step(stmt) == SQLITE_ROW)
        {
            const char *g = (const char *)sqlite3_column_text(stmt, 0);
            if (g)
                out->push_back(g);
        }

        sqlite3_finalize(stmt);
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
