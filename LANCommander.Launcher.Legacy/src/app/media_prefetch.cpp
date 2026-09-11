#include "app/media_prefetch.h"
#include "app/logger.h"

#include <lancommander/clients/media_client.h>
#include <lancommander/http/http_client.h>

#include "app/fs.h"
#include "app/worker.h"

namespace launcher
{

    MediaPrefetch::MediaPrefetch(lancommander::IHttpClient &http,
                                 lancommander::MediaClient &media,
                                 const std::string &cache_dir)
        : m_http(http), m_media(media), m_cache_dir(cache_dir),
          m_lock(NULL), m_thread(NULL), m_thread_done(true),
          m_credentials_set(false)
    {
        m_lock = worker_mutex_create();

        fs_mkdir(m_cache_dir);
    }

    MediaPrefetch::~MediaPrefetch()
    {
        shutdown();

        if (m_lock)
        {
            worker_mutex_destroy(m_lock);
            m_lock = NULL;
        }
    }

    void MediaPrefetch::lock() const
    {
        worker_mutex_lock(m_lock);
    }

    void MediaPrefetch::unlock() const
    {
        worker_mutex_unlock(m_lock);
    }

    std::string MediaPrefetch::file_path(const std::string &media_id) const
    {
        return m_cache_dir + "\\" + media_id;
    }

    void MediaPrefetch::shutdown()
    {
        if (!m_thread)
            return;

        // Emptying the queue makes the worker exit at its next iteration
        // rather than downloading everything that happens to be pending.
        lock();
        m_queue.clear();
        unlock();

        worker_join(m_thread);
        m_thread = NULL;
        m_thread_done = true;
    }

    void MediaPrefetch::request(const std::string &media_id)
    {
        if (media_id.empty())
            return;

        // Already on disk: nothing to do. Checked outside the lock because
        // the filesystem is not shared state we own, and the worker only ever
        // creates files, never removes them.
        if (fs_exists(file_path(media_id)))
            return;

        lock();

        bool known = (m_in_flight == media_id);
        if (!known)
        {
            for (size_t i = 0; i < m_queue.size() && !known; ++i)
                known = (m_queue[i] == media_id);
        }

        if (!known)
        {
            // Bounded: drop the OLDEST pending request rather than refusing
            // the newest. What is on screen now matters more than what was on
            // screen when the user was somewhere else.
            if ((int)m_queue.size() >= MAX_QUEUE)
                m_queue.erase(m_queue.begin());

            m_queue.push_back(media_id);
        }

        unlock();
    }

    bool MediaPrefetch::is_pending(const std::string &media_id) const
    {
        if (media_id.empty())
            return false;

        lock();

        bool pending = (m_in_flight == media_id);
        for (size_t i = 0; i < m_queue.size() && !pending; ++i)
            pending = (m_queue[i] == media_id);

        unlock();
        return pending;
    }

    void MediaPrefetch::worker(void *param)
    {
        MediaPrefetch *self = (MediaPrefetch *)param;
        self->run_worker();
        self->m_thread_done = true;
    }

    void MediaPrefetch::run_worker()
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

            // Downloaded to a scratch name and MOVED into place, so the
            // final path never exists in a half-written state.
            //
            // IHttpClient::download() opens the destination before it sends
            // the request, which means the file is on disk and empty for the
            // whole transfer. ImageCache treats "the file exists" as "the file
            // is ready", so it was decoding partial images, failing, and
            // caching the failure permanently — the image then never appeared,
            // however many times it was asked for. That is what left library
            // icons blank and most game-detail backgrounds and logos missing.
            //
            // A failure is left alone rather than retried: ImageCache caches
            // the miss, and a genuinely missing media id would otherwise be
            // re-requested for as long as it stayed on screen.
            const std::string final_path = file_path(id);
            const std::string temp_path = final_path + ".part";

            if (m_media.download(id, temp_path))
            {
                if (!fs_rename(temp_path, final_path))
                    fs_remove(temp_path);
            }
            else
            {
                fs_remove(temp_path);
            }

            lock();
            m_in_flight.clear();
            unlock();
        }
    }

    void MediaPrefetch::tick(const std::string &base_url,
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
            return; // not connected yet; the ids will still be queued later

        // Applied only here, with no worker running, so the client the worker
        // uses is never mutated underneath it.
        m_http.set_base_url(base_url);
        m_http.set_bearer_token(access_token);
        m_credentials_set = true;

        m_thread_done = false;

        m_thread = worker_start(&MediaPrefetch::worker, this);

        if (!m_thread)
        {
            log_error("Failed to start the media prefetch worker");
            m_thread_done = true;

            // Without a worker the queue would grow forever, so drop it and
            // let the UI re-request what it still needs.
            lock();
            m_queue.clear();
            unlock();
        }
    }

} // namespace launcher
