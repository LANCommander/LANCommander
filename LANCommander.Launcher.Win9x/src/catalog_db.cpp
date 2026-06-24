#include "catalog_db.h"

#include "sqlite3.h"

#include <windows.h>

#include <cstdio>
#include <cstring>
#include <set>

namespace
{
    const char* kSchema =
        "CREATE TABLE IF NOT EXISTS games ("
        "  id            TEXT PRIMARY KEY,"
        "  title         TEXT NOT NULL,"
        "  sort_title    TEXT,"
        "  description   TEXT,"
        "  released_year INTEGER,"
        "  in_library    INTEGER DEFAULT 0,"
        "  developers    TEXT,"  // denormalized; junction tables come in a later phase
        "  publishers    TEXT,"
        "  genres        TEXT,"
        "  last_synced   INTEGER"
        ");"
        "CREATE TABLE IF NOT EXISTS media ("
        "  game_id   TEXT NOT NULL,"
        "  media_id  TEXT NOT NULL,"
        "  type      TEXT,"
        "  crc32     TEXT,"
        "  PRIMARY KEY(game_id, media_id)"
        ");"
        "CREATE INDEX IF NOT EXISTS media_game_idx ON media(game_id);"
        "CREATE TABLE IF NOT EXISTS schema_meta ("
        "  key   TEXT PRIMARY KEY,"
        "  value TEXT"
        ");";

    void BindText(sqlite3_stmt* st, int idx, const std::string& s)
    {
        if (s.empty())
            sqlite3_bind_null(st, idx);
        else
            sqlite3_bind_text(st, idx, s.c_str(), (int)s.size(), SQLITE_TRANSIENT);
    }

    std::string ColText(sqlite3_stmt* st, int idx)
    {
        const unsigned char* t = sqlite3_column_text(st, idx);
        return t ? std::string((const char*)t) : std::string();
    }
}

CatalogDb::CatalogDb()
    : m_db(0)
{
}

CatalogDb::~CatalogDb()
{
    Close();
}

std::string CatalogDb::DbPath() const
{
    char buf[MAX_PATH];
    DWORD n = GetModuleFileNameA(NULL, buf, MAX_PATH);
    std::string path = (n == 0) ? std::string(".") : std::string(buf, n);
    size_t slash = path.find_last_of("\\/");
    if (slash != std::string::npos) path = path.substr(0, slash);
    return path + "\\lancommander.db";
}

bool CatalogDb::Open(std::string* errorOut)
{
    if (m_db) return true;
    sqlite3* db = NULL;
    int rc = sqlite3_open(DbPath().c_str(), &db);
    if (rc != SQLITE_OK)
    {
        if (errorOut)
            *errorOut = db ? sqlite3_errmsg(db) : "sqlite3_open failed";
        if (db) sqlite3_close(db);
        return false;
    }
    m_db = db;
    return EnsureSchema(errorOut);
}

void CatalogDb::Close()
{
    if (!m_db) return;
    sqlite3_close((sqlite3*)m_db);
    m_db = 0;
}

bool CatalogDb::Exec(const char* sql, std::string* errorOut)
{
    char* err = NULL;
    int rc = sqlite3_exec((sqlite3*)m_db, sql, NULL, NULL, &err);
    if (rc != SQLITE_OK)
    {
        if (errorOut) *errorOut = err ? err : "sqlite3_exec failed";
        if (err) sqlite3_free(err);
        return false;
    }
    return true;
}

bool CatalogDb::EnsureSchema(std::string* errorOut)
{
    return Exec(kSchema, errorOut);
}

bool CatalogDb::ReplaceCatalog(const std::vector<GameSummary>& games,
                               std::string* errorOut)
{
    if (!m_db) { if (errorOut) *errorOut = "DB not open"; return false; }

    if (!Exec("BEGIN IMMEDIATE", errorOut)) return false;

    const char* kUpsertGame =
        "INSERT INTO games (id, title, sort_title, description, released_year, "
        "                   in_library, developers, publishers, genres, last_synced) "
        "VALUES (?,?,?,?,?,?,?,?,?,?) "
        "ON CONFLICT(id) DO UPDATE SET "
        "  title         = excluded.title,"
        "  sort_title    = excluded.sort_title,"
        "  description   = excluded.description,"
        "  released_year = excluded.released_year,"
        "  in_library    = excluded.in_library,"
        "  developers    = excluded.developers,"
        "  publishers    = excluded.publishers,"
        "  genres        = excluded.genres,"
        "  last_synced   = excluded.last_synced";

    const char* kUpsertMedia =
        "INSERT INTO media (game_id, media_id, type, crc32) VALUES (?,?,?,?) "
        "ON CONFLICT(game_id, media_id) DO UPDATE SET "
        "  type  = excluded.type,"
        "  crc32 = excluded.crc32";

    sqlite3_stmt* stG = NULL;
    sqlite3_stmt* stM = NULL;
    if (sqlite3_prepare_v2((sqlite3*)m_db, kUpsertGame, -1, &stG, NULL) != SQLITE_OK ||
        sqlite3_prepare_v2((sqlite3*)m_db, kUpsertMedia, -1, &stM, NULL) != SQLITE_OK)
    {
        if (errorOut) *errorOut = sqlite3_errmsg((sqlite3*)m_db);
        if (stG) sqlite3_finalize(stG);
        if (stM) sqlite3_finalize(stM);
        Exec("ROLLBACK", NULL);
        return false;
    }

    DWORD now = (DWORD)(GetTickCount() / 1000); // good-enough wall-clock for cache aging

    std::set<std::string> seen;
    for (size_t i = 0; i < games.size(); ++i)
    {
        const GameSummary& g = games[i];
        sqlite3_reset(stG);
        BindText(stG, 1, g.id);
        BindText(stG, 2, g.title);
        BindText(stG, 3, g.sortTitle);
        BindText(stG, 4, g.description);
        if (g.releasedYear > 0) sqlite3_bind_int(stG, 5, g.releasedYear);
        else                    sqlite3_bind_null(stG, 5);
        sqlite3_bind_int(stG, 6, g.inLibrary ? 1 : 0);
        BindText(stG, 7, g.developers);
        BindText(stG, 8, g.publishers);
        BindText(stG, 9, g.genres);
        sqlite3_bind_int(stG, 10, (int)now);
        if (sqlite3_step(stG) != SQLITE_DONE)
        {
            if (errorOut) *errorOut = sqlite3_errmsg((sqlite3*)m_db);
            sqlite3_finalize(stG); sqlite3_finalize(stM);
            Exec("ROLLBACK", NULL);
            return false;
        }
        seen.insert(g.id);

        if (!g.coverMediaId.empty())
        {
            sqlite3_reset(stM);
            BindText(stM, 1, g.id);
            BindText(stM, 2, g.coverMediaId);
            sqlite3_bind_text(stM, 3, "Cover", 5, SQLITE_STATIC);
            BindText(stM, 4, g.coverCrc32);
            if (sqlite3_step(stM) != SQLITE_DONE)
            {
                if (errorOut) *errorOut = sqlite3_errmsg((sqlite3*)m_db);
                sqlite3_finalize(stG); sqlite3_finalize(stM);
                Exec("ROLLBACK", NULL);
                return false;
            }
        }
    }
    sqlite3_finalize(stG);
    sqlite3_finalize(stM);

    // Drop games we didn't see this sync. Build a temp set of ids to keep,
    // then delete everything else. SQLite has no native "VALUES list NOT IN"
    // shorthand here, so iterate.
    sqlite3_stmt* stIds = NULL;
    if (sqlite3_prepare_v2((sqlite3*)m_db, "SELECT id FROM games", -1,
                           &stIds, NULL) == SQLITE_OK)
    {
        std::vector<std::string> stale;
        while (sqlite3_step(stIds) == SQLITE_ROW)
        {
            std::string id = ColText(stIds, 0);
            if (seen.find(id) == seen.end()) stale.push_back(id);
        }
        sqlite3_finalize(stIds);

        sqlite3_stmt* stDel = NULL;
        sqlite3_stmt* stDelM = NULL;
        sqlite3_prepare_v2((sqlite3*)m_db, "DELETE FROM games WHERE id = ?",
                           -1, &stDel, NULL);
        sqlite3_prepare_v2((sqlite3*)m_db, "DELETE FROM media WHERE game_id = ?",
                           -1, &stDelM, NULL);
        for (size_t i = 0; i < stale.size(); ++i)
        {
            sqlite3_reset(stDel);
            BindText(stDel, 1, stale[i]);
            sqlite3_step(stDel);
            sqlite3_reset(stDelM);
            BindText(stDelM, 1, stale[i]);
            sqlite3_step(stDelM);
        }
        if (stDel)  sqlite3_finalize(stDel);
        if (stDelM) sqlite3_finalize(stDelM);
    }

    return Exec("COMMIT", errorOut);
}

bool CatalogDb::LoadAllGames(std::vector<GameSummary>* out, std::string* errorOut)
{
    if (!m_db) { if (errorOut) *errorOut = "DB not open"; return false; }

    const char* kSql =
        "SELECT g.id, g.title, g.sort_title, g.description, g.released_year, "
        "       g.in_library, g.developers, g.publishers, g.genres, "
        "       m.media_id, m.crc32 "
        "FROM games g "
        "LEFT JOIN media m ON m.game_id = g.id AND m.type = 'Cover'";

    sqlite3_stmt* st = NULL;
    if (sqlite3_prepare_v2((sqlite3*)m_db, kSql, -1, &st, NULL) != SQLITE_OK)
    {
        if (errorOut) *errorOut = sqlite3_errmsg((sqlite3*)m_db);
        return false;
    }

    while (sqlite3_step(st) == SQLITE_ROW)
    {
        GameSummary g;
        g.id            = ColText(st, 0);
        g.title         = ColText(st, 1);
        g.sortTitle     = ColText(st, 2);
        g.description   = ColText(st, 3);
        g.releasedYear  = sqlite3_column_int(st, 4);
        g.inLibrary     = sqlite3_column_int(st, 5) != 0;
        g.developers    = ColText(st, 6);
        g.publishers    = ColText(st, 7);
        g.genres        = ColText(st, 8);
        g.coverMediaId  = ColText(st, 9);
        g.coverCrc32    = ColText(st, 10);
        out->push_back(g);
    }
    sqlite3_finalize(st);
    return true;
}

bool CatalogDb::SetInLibrary(const std::string& gameId, bool inLibrary,
                             std::string* errorOut)
{
    if (!m_db) { if (errorOut) *errorOut = "DB not open"; return false; }
    sqlite3_stmt* st = NULL;
    if (sqlite3_prepare_v2((sqlite3*)m_db,
                           "UPDATE games SET in_library = ? WHERE id = ?",
                           -1, &st, NULL) != SQLITE_OK)
    {
        if (errorOut) *errorOut = sqlite3_errmsg((sqlite3*)m_db);
        return false;
    }
    sqlite3_bind_int(st, 1, inLibrary ? 1 : 0);
    BindText(st, 2, gameId);
    bool ok = sqlite3_step(st) == SQLITE_DONE;
    if (!ok && errorOut) *errorOut = sqlite3_errmsg((sqlite3*)m_db);
    sqlite3_finalize(st);
    return ok;
}

bool CatalogDb::DeleteGame(const std::string& gameId, std::string* errorOut)
{
    if (!m_db) { if (errorOut) *errorOut = "DB not open"; return false; }
    if (!Exec("BEGIN", errorOut)) return false;

    sqlite3_stmt* s1 = NULL;
    sqlite3_stmt* s2 = NULL;
    sqlite3_prepare_v2((sqlite3*)m_db, "DELETE FROM games WHERE id = ?",
                       -1, &s1, NULL);
    sqlite3_prepare_v2((sqlite3*)m_db, "DELETE FROM media WHERE game_id = ?",
                       -1, &s2, NULL);
    BindText(s1, 1, gameId);
    BindText(s2, 1, gameId);
    bool ok = sqlite3_step(s1) == SQLITE_DONE &&
              sqlite3_step(s2) == SQLITE_DONE;
    sqlite3_finalize(s1);
    sqlite3_finalize(s2);
    if (!ok && errorOut) *errorOut = sqlite3_errmsg((sqlite3*)m_db);
    return ok ? Exec("COMMIT", errorOut) : (Exec("ROLLBACK", NULL), false);
}
