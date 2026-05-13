#ifndef LANCOMMANDER_WIN9X_CATALOG_DB_H
#define LANCOMMANDER_WIN9X_CATALOG_DB_H

#include <string>
#include <vector>

#include "game_client.h"

// SQLite-backed local catalog cache. Mirrors a subset of Avalonia's
// DatabaseContext so the UI can read games offline and avoid re-querying
// /api/Games on every refresh. Storage lives next to the exe in
// `lancommander.db`. Schema is created on Open(); existing DBs are upgraded
// only via CREATE TABLE IF NOT EXISTS — destructive schema changes require
// deleting the file.
class CatalogDb
{
public:
    CatalogDb();
    ~CatalogDb();

    bool Open(std::string* errorOut);
    void Close();
    bool IsOpen() const { return m_db != 0; }

    // Replace the cached catalog with `games` in a single transaction. Games
    // not present are removed so the cache mirrors the server's view at sync
    // time. Returns false on any SQL error.
    bool ReplaceCatalog(const std::vector<GameSummary>& games,
                        std::string* errorOut);

    // Loads every game (and its cover, if any) into `out`. Returns false on
    // any SQL error.
    bool LoadAllGames(std::vector<GameSummary>* out, std::string* errorOut);

    bool SetInLibrary(const std::string& gameId, bool inLibrary,
                      std::string* errorOut);

    // Drops a single game (e.g. after server-side delete).
    bool DeleteGame(const std::string& gameId, std::string* errorOut);

private:
    bool EnsureSchema(std::string* errorOut);
    bool Exec(const char* sql, std::string* errorOut);
    std::string DbPath() const;

    void* m_db; // sqlite3*
};

#endif
