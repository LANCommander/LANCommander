#ifndef LAUNCHER_APP_GAME_ART_FETCHER_H
#define LAUNCHER_APP_GAME_ART_FETCHER_H

#include <map>
#include <string>
#include <vector>

#include "app/data_store.h"

namespace lancommander
{
    class IHttpClient;
    class GameClient;
}

// Resolves icon / cover / background / logo media ids for games that are NOT
// in the user's library, off the UI thread.
//
// The library's own art arrives in bulk: /api/Library/Games returns whole Game
// records with Media included, so one call covers every game the user owns.
// The depot is the gap. Its payload carries a cover and nothing else, and the
// hero cards on the Popular row are drawn from a background and a logo, so
// those have to be fetched per game from /api/Games/{id}.
//
// That is the same N+1 the Avalonia DepotViewModel does in
// FetchGameWithMediaAsync. The difference is where it runs: Avalonia awaits it,
// and this launcher's HTTP is synchronous, so ten blocking requests on entering
// the depot would be ten stalled frames. They go to a worker instead, and the
// UI draws a cover until the answer arrives.
//
// Threading rules are MediaPrefetch's, for the same reasons:
//
//   * One worker at a time, with its OWN IHttpClient. Sharing the UI thread's
//     would mean two threads reading a base URL and bearer token that login
//     and server-switching rewrite.
//   * The queue and the resolved map hold COPIES under a lock. Nothing hands
//     out a pointer into a container the other thread can reallocate.
//   * Credentials are applied only while no worker is running.

namespace launcher
{

    class GameArtFetcher
    {
    public:
        // Bounded like MediaPrefetch's: beyond this the OLDEST pending request
        // is dropped, because what is on screen now matters more than what was
        // on screen when the user was somewhere else.
        static const int MAX_QUEUE = 32;

        // `games` must be backed by `http`, and both are owned by the caller.
        GameArtFetcher(lancommander::IHttpClient &http, lancommander::GameClient &games);
        ~GameArtFetcher();

        // Non-blocking. Ignored if the id is already queued, in flight, or
        // resolved. Safe to call every frame for every visible card, which is
        // how the depot page uses it.
        void request(const std::string &game_id);

        // True when `game_id` has been resolved, in which case `*out` is
        // filled. A resolved game with no background and no logo still counts
        // as resolved — that is an answer, and re-asking would not change it.
        bool resolved(const std::string &game_id, GameArt *out) const;

        // Once per frame from App::run(), on the UI thread.
        void tick(const std::string &base_url, const std::string &access_token);

        // Drop everything. Called when the library is invalidated, so a
        // refresh re-reads art that may have changed on the server.
        void clear();

        // Block until the worker stops. Called from the destructor; also safe
        // to call before tearing down the HTTP client.
        void shutdown();

    private:
        lancommander::IHttpClient &m_http;
        lancommander::GameClient &m_games;

        // All guarded by the critical section.
        std::vector<std::string> m_queue;
        std::string m_in_flight;
        std::map<std::string, GameArt> m_resolved;

        void *m_lock;   // worker mutex
        void *m_thread; // worker handle
        volatile bool m_thread_done;

        void lock() const;
        void unlock() const;

        // WorkerFn (app/worker.h): runs on a thread where the platform
        // has them, inline on DOS.
        static void worker(void *param);
        void run_worker();

        GameArtFetcher(const GameArtFetcher &);
        GameArtFetcher &operator=(const GameArtFetcher &);
    };

} // namespace launcher

#endif // LAUNCHER_APP_GAME_ART_FETCHER_H
