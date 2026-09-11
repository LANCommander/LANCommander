#include "app/game_art_fetcher.h"
#include "app/logger.h"

#include <lancommander/clients/game_client.h>
#include <lancommander/http/http_client.h>

#include "app/worker.h"

namespace launcher
{

    namespace
    {
        GameArt art_from_media(const lancommander::Game &g)
        {
            GameArt art;

            for (size_t m = 0; m < g.media.size(); ++m)
            {
                const std::string &type = g.media[m].type;
                const std::string &id = g.media[m].id;

                if (type == "Icon" && art.icon.empty()) art.icon = id;
                else if (type == "Cover" && art.cover.empty()) art.cover = id;
                else if (type == "Background" && art.background.empty()) art.background = id;
                else if (type == "Logo" && art.logo.empty()) art.logo = id;
            }

            if (art.cover.empty())
                art.cover = g.cover_media_id;

            art.complete = true;
            return art;
        }
    } // namespace

    GameArtFetcher::GameArtFetcher(lancommander::IHttpClient &http,
                                   lancommander::GameClient &games)
        : m_http(http), m_games(games), m_lock(NULL), m_thread(NULL),
          m_thread_done(true)
    {
        m_lock = worker_mutex_create();
    }

    GameArtFetcher::~GameArtFetcher()
    {
        shutdown();

        if (m_lock)
        {
            worker_mutex_destroy(m_lock);
            m_lock = NULL;
        }
    }

    void GameArtFetcher::lock() const
    {
        worker_mutex_lock(m_lock);
    }

    void GameArtFetcher::unlock() const
    {
        worker_mutex_unlock(m_lock);
    }

    void GameArtFetcher::shutdown()
    {
        if (!m_thread)
            return;

        // Emptying the queue makes the worker exit at its next iteration
        // rather than draining everything that happens to be pending.
        lock();
        m_queue.clear();
        unlock();

        worker_join(m_thread);
        m_thread = NULL;
        m_thread_done = true;
    }

    void GameArtFetcher::clear()
    {
        shutdown();

        lock();
        m_queue.clear();
        m_resolved.clear();
        m_in_flight.clear();
        unlock();
    }

    void GameArtFetcher::request(const std::string &game_id)
    {
        if (game_id.empty())
            return;

        lock();

        bool known = (m_in_flight == game_id) ||
                     (m_resolved.find(game_id) != m_resolved.end());

        for (size_t i = 0; i < m_queue.size() && !known; ++i)
            known = (m_queue[i] == game_id);

        if (!known)
        {
            if ((int)m_queue.size() >= MAX_QUEUE)
                m_queue.erase(m_queue.begin());

            m_queue.push_back(game_id);
        }

        unlock();
    }

    bool GameArtFetcher::resolved(const std::string &game_id, GameArt *out) const
    {
        if (game_id.empty() || !out)
            return false;

        lock();

        std::map<std::string, GameArt>::const_iterator it = m_resolved.find(game_id);
        const bool found = (it != m_resolved.end());
        if (found)
            *out = it->second; // copied under the lock; never a pointer out

        unlock();
        return found;
    }

    void GameArtFetcher::worker(void *param)
    {
        GameArtFetcher *self = (GameArtFetcher *)param;
        self->run_worker();
        self->m_thread_done = true;
    }

    void GameArtFetcher::run_worker()
    {
        for (;;)
        {
            std::string id;

            lock();
            if (!m_queue.empty())
            {
                id = m_queue.front();
                m_queue.erase(m_queue.begin());
                m_in_flight = id;
            }
            unlock();

            if (id.empty())
                break;

            auto result = m_games.get(id);

            // A failure is recorded as empty art rather than retried. The id
            // stays on screen for as long as the user is on the page, so a
            // retry loop would be one request per frame forever.
            const GameArt art = result ? art_from_media(result.value) : GameArt();

            lock();
            m_resolved[id] = art;
            m_in_flight.clear();
            unlock();
        }
    }

    void GameArtFetcher::tick(const std::string &base_url,
                              const std::string &access_token)
    {
        // Reap a finished worker before considering a new one.
        if (m_thread && m_thread_done)
        {
            worker_join(m_thread);
            m_thread = NULL;
        }

        if (m_thread)
            return; // still running; it will drain the queue itself

        bool have_work = false;
        lock();
        have_work = !m_queue.empty();
        unlock();

        if (!have_work)
            return;

        if (base_url.empty())
            return; // not connected yet; the ids stay queued

        // Applied only here, with no worker running, so the client the worker
        // uses is never mutated underneath it.
        m_http.set_base_url(base_url);
        m_http.set_bearer_token(access_token);

        m_thread_done = false;

        m_thread = worker_start(&GameArtFetcher::worker, this);

        if (!m_thread)
        {
            log_error("Failed to start the game art fetch worker");
            m_thread_done = true;

            // Without a worker the queue would grow forever, so drop it and
            // let the UI re-request whatever is still on screen.
            lock();
            m_queue.clear();
            unlock();
        }
    }

} // namespace launcher
