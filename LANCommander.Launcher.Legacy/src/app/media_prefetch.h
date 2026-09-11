#ifndef LAUNCHER_APP_MEDIA_PREFETCH_H
#define LAUNCHER_APP_MEDIA_PREFETCH_H

#include <string>
#include <vector>

namespace lancommander
{
    class IHttpClient;
    class MediaClient;
}

// Downloads game art off the UI thread.
//
// ImageCache used to fetch on demand, inline, from inside a draw call. Four
// blocking HTTP requests per frame was survivable for a single cover grid and
// is not survivable for a library sidebar plus two carousels plus a grid, let
// alone the depot page.
//
// The split is deliberate: only the DOWNLOAD moves to the worker. Decoding
// stays on the UI thread, where it is already budgeted per frame, and where
// it does not need a lock around the surface it produces.
//
// Threading rules, all of which exist because of specific hazards:
//
//   * One worker at a time. Deterministic ordering, and a Win9x TCP stack
//     does not enjoy eight concurrent WinINet sessions.
//   * The queue holds std::string COPIES under a lock, never pointers into a
//     container the UI thread might reallocate. DownloadQueue::start_next
//     hands its thread a `&m_items[i]`, which a concurrent enqueue() can
//     invalidate; that pattern is not repeated here.
//   * The worker uses its OWN IHttpClient, not the one the UI thread makes
//     requests on. Sharing would mean two threads reading a base URL and
//     bearer token that a third code path can rewrite mid-request.

namespace launcher
{

    class MediaPrefetch
    {
    public:
        // At most this many ids wait at once. Beyond it the oldest request is
        // dropped: the UI re-requests whatever is still on screen next frame,
        // so a dropped entry costs a frame, not an image.
        static const int MAX_QUEUE = 64;

        // `media` must be backed by `http`, and both are owned by the caller.
        MediaPrefetch(lancommander::IHttpClient &http,
                      lancommander::MediaClient &media,
                      const std::string &cache_dir);
        ~MediaPrefetch();

        // Non-blocking. Ignored if the id is already queued, in flight, or on
        // disk. Safe to call every frame for every visible image, which is
        // exactly how ImageCache uses it.
        void request(const std::string &media_id);

        // Once per frame from App::run(), on the UI thread.
        //
        // Credentials are passed in rather than read from a shared connection
        // so the worker never touches state the UI thread owns; they are
        // applied only while no worker is running.
        void tick(const std::string &base_url, const std::string &access_token);

        // True while `media_id` is queued or downloading. Lets a caller draw a
        // spinner rather than a "failed" placeholder.
        bool is_pending(const std::string &media_id) const;

        // Block until the worker stops. Called from the destructor; also safe
        // to call before tearing down the HTTP client.
        void shutdown();

    private:
        lancommander::IHttpClient &m_http;
        lancommander::MediaClient &m_media;
        std::string m_cache_dir;

        // Guarded by the critical section: the queue, and the id currently
        // being downloaded.
        std::vector<std::string> m_queue;
        std::string m_in_flight;

        void *m_lock;    // worker mutex
        void *m_thread;  // worker handle
        volatile bool m_thread_done;
        bool m_credentials_set;

        void lock() const;
        void unlock() const;

        // Worker entry point; drains the queue and exits when it is empty.
        // WorkerFn (app/worker.h): runs on a thread where the platform
        // has them, inline on DOS.
        static void worker(void *param);
        void run_worker();

        std::string file_path(const std::string &media_id) const;

        MediaPrefetch(const MediaPrefetch &);
        MediaPrefetch &operator=(const MediaPrefetch &);
    };

} // namespace launcher

#endif // LAUNCHER_APP_MEDIA_PREFETCH_H
