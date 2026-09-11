#include "test_main.h"

#include "app/game_database.h"
#include "app/time_util.h"

#include <sqlite3.h>

#include <cstdio>
#include <string>
#include <vector>

// Exercises the PlaySessions SQL against a real (temporary) SQLite file.
//
// sqlite3 is portable C with no platform dependencies, and game_database.cpp
// touches nothing Windows-specific, so this runs on the Linux CI runner
// alongside the rest of the headless suite. Without it the session queries
// would only ever be checked by launching a game by hand.

using namespace launcher;

namespace
{
    const char *const DB_PATH = "test_play_sessions.db";

    // Rows with controlled timestamps, written through a second connection.
    //
    // begin_play_session only ever writes "now", so durations cannot be
    // arranged through the public API. Seeding here rather than adding a
    // test-only method keeps GameDatabase free of hooks that exist purely for
    // the suite.
    void seed(const char *sql)
    {
        sqlite3 *raw = NULL;
        if (sqlite3_open(DB_PATH, &raw) != SQLITE_OK)
        {
            CHECK(false);
            return;
        }
        char *err = NULL;
        const int rc = sqlite3_exec(raw, sql, NULL, NULL, &err);
        if (rc != SQLITE_OK)
        {
            std::printf("  seed failed: %s\n", err ? err : "(unknown)");
            CHECK(false);
        }
        if (err)
            sqlite3_free(err);
        sqlite3_close(raw);
    }

    void cleanup()
    {
        std::remove(DB_PATH);
        std::remove("test_play_sessions.db-wal");
        std::remove("test_play_sessions.db-shm");
    }

    void test_basic_session()
    {
        cleanup();
        GameDatabase db;
        CHECK(db.open(DB_PATH));

        // Nothing recorded yet.
        CHECK_INT(db.total_play_seconds("game-a"), 0);
        CHECK_EQ(db.last_played("game-a"), std::string());

        const std::string id = db.begin_play_session("game-a", "user-1");
        CHECK(!id.empty());

        // An open session contributes nothing: the Avalonia query requires
        // both Start and End, and so does this one.
        CHECK_INT(db.total_play_seconds("game-a"), 0);

        db.end_play_session(id);

        // The run took under a second, so the total is 0 but a last-played
        // timestamp now exists. That distinction is the point.
        CHECK(!db.last_played("game-a").empty());
        CHECK(iso8601_to_time_t(db.last_played("game-a")) != 0);

        db.close();
        cleanup();
    }

    void test_totals_and_ordering()
    {
        cleanup();
        GameDatabase db;
        CHECK(db.open(DB_PATH));

        // begin/end only ever writes "now", so durations are injected
        // directly to get deterministic totals.
        seed(
            "INSERT INTO PlaySessions (Id, GameId, UserId, Start, End) VALUES "
            "('s1','game-a','u','2026-01-01T10:00:00Z','2026-01-01T11:00:00Z'),"
            "('s2','game-a','u','2026-01-02T10:00:00Z','2026-01-02T10:30:00Z'),"
            "('s3','game-b','u','2026-01-03T10:00:00Z','2026-01-03T12:00:00Z');");

        CHECK_INT(db.total_play_seconds("game-a"), 5400);  // 1h + 30m
        CHECK_INT(db.total_play_seconds("game-b"), 7200);
        CHECK_INT(db.total_play_seconds("game-missing"), 0);

        // Most recent END, not most recent start.
        CHECK_EQ(db.last_played("game-a"), std::string("2026-01-02T10:30:00Z"));

        // Recently played: newest first, one entry per game.
        std::vector<std::string> recent;
        db.recent_games(10, &recent);
        CHECK_INT(recent.size(), 2);
        CHECK_EQ(recent[0], std::string("game-b"));   // ended 01-03
        CHECK_EQ(recent[1], std::string("game-a"));   // ended 01-02

        // The limit is honoured.
        db.recent_games(1, &recent);
        CHECK_INT(recent.size(), 1);
        CHECK_EQ(recent[0], std::string("game-b"));

        db.recent_games(0, &recent);
        CHECK_INT(recent.size(), 0);

        db.close();
        cleanup();
    }

    void test_bad_rows_ignored()
    {
        cleanup();
        GameDatabase db;
        CHECK(db.open(DB_PATH));

        seed(
            "INSERT INTO PlaySessions (Id, GameId, UserId, Start, End) VALUES "
            "('ok','game-a','u','2026-01-01T10:00:00Z','2026-01-01T10:10:00Z'),"
            "('rev','game-a','u','2026-01-02T10:00:00Z','2026-01-02T09:00:00Z'),"
            "('junk','game-a','u','not-a-date','also-not-a-date');");

        // A reversed row would subtract an hour and a junk row would add
        // decades; both must be skipped, leaving only the good 10 minutes.
        CHECK_INT(db.total_play_seconds("game-a"), 600);

        db.close();
        cleanup();
    }

    void test_dangling_session_closed_on_open()
    {
        cleanup();
        {
            GameDatabase db;
            CHECK(db.open(DB_PATH));
            const std::string id = db.begin_play_session("game-a", "u");
            CHECK(!id.empty());
            // Close the database with the session still open, standing in for
            // a crash or a power cut mid-game.
            db.close();
        }

        {
            GameDatabase db;
            CHECK(db.open(DB_PATH));

            // The dangling row is collapsed to zero length rather than left
            // to be closed later and counted as a session lasting until then.
            CHECK_INT(db.total_play_seconds("game-a"), 0);
            CHECK(!db.last_played("game-a").empty());

            db.close();
        }
        cleanup();
    }

    void test_idempotent_end()
    {
        cleanup();
        GameDatabase db;
        CHECK(db.open(DB_PATH));

        seed(
            "INSERT INTO PlaySessions (Id, GameId, UserId, Start, End) VALUES "
            "('s1','game-a','u','2026-01-01T10:00:00Z',NULL);");

        db.end_play_session("s1");
        const std::string first = db.last_played("game-a");
        CHECK(!first.empty());

        // Ending twice must not stretch the session. The exit path can be
        // reached more than once — Stop pressed as the game is already
        // quitting, for instance.
        db.end_play_session("s1");
        CHECK_EQ(db.last_played("game-a"), first);

        // Unknown and empty ids are no-ops rather than errors.
        db.end_play_session("does-not-exist");
        db.end_play_session("");

        db.close();
        cleanup();
    }

    void test_schema_is_additive()
    {
        cleanup();
        {
            // A database that predates PlaySessions: only the Games table.
            GameDatabase db;
            CHECK(db.open(DB_PATH));
            db.set_installed("game-a", "C:\\Games\\A", "1.0");
            db.close();
        }
        {
            // Reopening creates the new table without disturbing the old one.
            GameDatabase db;
            CHECK(db.open(DB_PATH));

            InstalledGame g;
            CHECK(db.find("game-a", &g));
            CHECK_EQ(g.install_directory, std::string("C:\\Games\\A"));

            const std::string id = db.begin_play_session("game-a", "u");
            CHECK(!id.empty());
            db.close();
        }
        cleanup();
    }
} // namespace

void test_play_sessions()
{
    test_basic_session();
    test_totals_and_ordering();
    test_bad_rows_ignored();
    test_dangling_session_closed_on_open();
    test_idempotent_end();
    test_schema_is_additive();
}
