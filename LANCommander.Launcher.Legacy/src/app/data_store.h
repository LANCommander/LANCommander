#ifndef LAUNCHER_APP_DATA_STORE_H
#define LAUNCHER_APP_DATA_STORE_H

#include <string>
#include <vector>

#include <lancommander/lancommander.h>

// The launcher's two server-backed collections, and enough state around them
// to know whether they are worth trusting.
//
// These used to be bare vectors on App guarded by a one-shot `static bool
// s_..._loaded` inside screen_library.cpp. That flag could never be cleared,
// so there was no refresh path anywhere in the program: install a game and the
// library list still said it was not installed until the next restart.
//
// `state` replaces the bool and can be reset. `revision` is what lets a screen
// cache something derived from this data — a search result, a section index
// list — and know when to rebuild it, without either storing a second copy of
// the games or rebuilding on every frame.

#include <map>

namespace launcher
{

    // The four media ids a view can actually draw, per game.
    //
    // Kept as a flat index instead of holding whole Game records: the sidebar
    // wants an icon, the grid a cover, a hero card a background and a logo,
    // and nothing on a list view wants the rest.
    //
    // Filled in bulk from /api/Library/Games, which returns whole Game records
    // with Media included. Games OUTSIDE the library are not in that response
    // and are resolved one at a time by GameArtFetcher.
    struct GameArt
    {
        std::string icon;
        std::string cover;
        std::string background;
        std::string logo;

        // True only when this came from a media-bearing record, so every
        // field is known-good and an empty one means "this game has none".
        //
        // The depot payload seeds cover-only entries for the whole catalogue,
        // and without this flag those seeds were indistinguishable from a full
        // answer — so a hero card whose game was not in the library saw an
        // entry, never asked for the real one, and drew its title forever.
        bool complete;

        GameArt() : complete(false) {}
    };

    enum class LoadState
    {
        Empty,    // never loaded, or explicitly invalidated
        Loading,  // a fetch is in flight
        Loaded,
        Failed    // see `error`
    };

    struct DepotData
    {
        LoadState state;
        std::string error;

        // Bumped on every successful load. Screens keep the last value they
        // saw and rebuild derived views when it changes.
        unsigned revision;

        // Top-level games only, sorted by sort title.
        std::vector<lancommander::DepotGame> games;

        DepotData() : state(LoadState::Empty), revision(0) {}
    };

    struct LibraryData
    {
        LoadState state;
        std::string error;
        unsigned revision;

        std::vector<lancommander::Game> games;

        LibraryData() : state(LoadState::Empty), revision(0) {}
    };

    // When each game was last finished, merged across every source, as a UTC
    // epoch. Used for the Recently Played carousel and the play-time stats.
    //
    // The launcher keeps its own play sessions in SQLite, but that database is
    // per-install: a fresh copy of the legacy launcher has none, so Recently
    // Played was empty even for someone with years of history. The server
    // holds the authoritative record for the whole account, so both are read
    // and the newer of the two wins per game.
    struct PlaySessionIndex
    {
        LoadState state;
        std::string error;

        // long long, not long: this build targets 32-bit, where long is
        // 32 bits but time_t is 64, so a plain long would silently truncate
        // every epoch on its way in.
        std::map<std::string, long long> last_played;   // game id -> UTC epoch
        std::map<std::string, long long> total_seconds; // game id -> runtime

        PlaySessionIndex() : state(LoadState::Empty) {}
    };

    // Art for EVERY game the server knows about, not just the library ones,
    // which is why it is not a field on LibraryData.
    struct GameArtIndex
    {
        LoadState state;
        std::map<std::string, GameArt> by_game_id;

        GameArtIndex() : state(LoadState::Empty) {}
    };

} // namespace launcher

#endif // LAUNCHER_APP_DATA_STORE_H
